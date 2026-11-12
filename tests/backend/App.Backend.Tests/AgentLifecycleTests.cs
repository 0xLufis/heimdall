namespace App.Backend.Tests;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.Infrastructure;
using App.Agent.Daemon.Infrastructure.Identity;
using App.Agent.Daemon.Infrastructure.Persistence;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Lifecycle;
using App.Backend.Api.Services;
using App.Contracts.Mqtt;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Protos;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Xunit;
using C = App.Agent.Daemon.Lifecycle.LifecycleContract.Classification;

/// <summary>Lifecycle / power report: previous-run classification, stop reasons, crash records, last will, ingestion.</summary>
public class AgentLifecycleTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall_lifecycle_" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 1, 0, 0, TimeSpan.Zero);

    public AgentLifecycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    // ------------------------------------------------------------------ classification matrix

    private static BootInfo Boot(string id = "boot-A", long uptimeMs = 3_600_000) =>
        new(id, BootInfoProvider.KernelBootId, Now - TimeSpan.FromMilliseconds(uptimeMs), uptimeMs);

    private static SessionMarker Marker(bool clean = false, StopInfo? stop = null, string bootId = "boot-A", long uptimeMs = 3_000_000, string version = "1.0.0") => new()
    {
        SessionId = "prev-session",
        BootId = bootId,
        BootIdSource = BootInfoProvider.KernelBootId,
        OsBootedAtUtc = Now - TimeSpan.FromHours(1),
        ProcessStartedAtUtc = Now - TimeSpan.FromMinutes(50),
        AgentVersion = version,
        LastAliveAtUtc = Now - TimeSpan.FromMinutes(5),
        LastAliveUptimeMs = uptimeMs,
        CleanShutdown = clean,
        Stop = stop
    };

    private static PreviousRunAssessment Classify(SessionMarker? marker, BootInfo? boot = null, CrashRecord? crash = null,
        IReadOnlyList<OsEvidence>? evidence = null, bool damaged = false, string version = "1.0.0") =>
        PreviousRunClassifier.Classify(marker, damaged, boot ?? Boot(), crash, evidence, version, Now);

    [Fact]
    public void Classify_NoMarker_IsFirstStart_OrUnknownWhenTheMarkerWasDamaged()
    {
        Assert.Equal(C.FirstStart, Classify(null).Classification);
        Assert.Equal(C.Unknown, Classify(null, damaged: true).Classification);
    }

    [Fact]
    public void Classify_CleanServiceStop_SameBoot()
    {
        var result = Classify(Marker(clean: true, stop: new StopInfo { Reason = "service_stop", Initiator = "operator", Signal = "SERVICE_CONTROL_STOP" }));
        Assert.Equal(C.ServiceStop, result.Classification);
        Assert.Equal("operator", result.Initiator);
        Assert.True(result.SameBoot);
        Assert.Equal("Info", result.Level);
    }

    [Fact]
    public void Classify_CleanServiceStop_WithNewAgentVersion_IsAttributedToAnUpdate()
    {
        var result = Classify(Marker(clean: true, stop: new StopInfo { Reason = "service_stop", Initiator = "operator" }, version: "0.9.0"), version: "1.0.0");
        Assert.Equal(C.ServiceStop, result.Classification);
        Assert.Equal("update", result.Initiator);
        Assert.Contains("0.9.0", result.Reason);
    }

    [Fact]
    public void Classify_CleanOsShutdown_TakesInitiatingUserAndPlannedFlagFromEvent1074()
    {
        var evidence = new[]
        {
            new OsEvidence { Source = "windows_eventlog", Kind = OsEvidence.Kinds.ShutdownInitiated, EventId = 1074, AtUtc = Now - TimeSpan.FromMinutes(4),
                User = @"PLANT\jdoe", Process = @"C:\Windows\system32\shutdown.exe (IPC-07)", Reason = "Application: Installation (Planned)",
                ReasonCode = "0x80040002", ShutdownType = "restart", Planned = true, Comment = "KB5031356" }
        };
        var result = Classify(Marker(clean: true, stop: new StopInfo { Reason = "os_shutdown", Initiator = "unknown", Signal = "SERVICE_CONTROL_SHUTDOWN" }, uptimeMs: 3_000_000),
            boot: Boot("boot-B", 60_000), evidence: evidence);

        Assert.Equal(C.OsShutdown, result.Classification);
        Assert.Equal("user", result.Initiator);
        Assert.Equal(@"PLANT\jdoe", result.InitiatedBy);
        Assert.True(result.Planned);
        Assert.Equal("restart", result.OsAction);
        Assert.False(result.SameBoot);
        Assert.Contains("KB5031356", result.Reason);
    }

    [Fact]
    public void Classify_NoCleanFlag_SameBoot_WithCrashRecord_IsAgentCrash()
    {
        var crash = new CrashRecord { SessionId = "prev-session", ExceptionType = "System.InvalidOperationException", Message = "ADS buffer overrun", AtUtc = Now - TimeSpan.FromMinutes(5) };
        var result = Classify(Marker(), crash: crash);
        Assert.Equal(C.AgentCrash, result.Classification);
        Assert.Equal("none", result.Initiator);
        Assert.Same(crash, result.Crash);
        Assert.Equal("Error", result.Level);
    }

    [Fact]
    public void Classify_NoCleanFlag_SameBoot_WithoutCrashRecord_IsAgentKilled_OrTheRecordedIntent()
    {
        Assert.Equal(C.AgentKilled, Classify(Marker()).Classification);

        var marker = Marker();
        marker.PendingIntent = new StopInfo { Reason = "service_restart", Initiator = "supervisor", Detail = "memory limit exceeded" };
        var withIntent = Classify(marker);
        Assert.Equal(C.ServiceStop, withIntent.Classification);
        Assert.Equal("supervisor", withIntent.Initiator);
    }

    [Fact]
    public void Classify_NoCleanFlag_BootChanged_IsUnexpectedShutdown_RefinedByOsEvidence()
    {
        var nextBoot = Boot("boot-B", 90_000);

        var plain = Classify(Marker(), boot: nextBoot);
        Assert.Equal(C.UnexpectedShutdown, plain.Classification);
        Assert.Null(plain.Cause);
        Assert.Equal("Critical", plain.Level);

        var powerLoss = Classify(Marker(), boot: nextBoot, evidence: new[]
        {
            new OsEvidence { Kind = OsEvidence.Kinds.UnexpectedShutdown, EventId = 6008, AtUtc = Now - TimeSpan.FromMinutes(1) },
            new OsEvidence { Kind = OsEvidence.Kinds.KernelPower, EventId = 41, AtUtc = Now - TimeSpan.FromMinutes(1) }
        });
        Assert.Equal(C.UnexpectedShutdown, powerLoss.Classification);
        Assert.Equal("power_loss_or_reset", powerLoss.Cause);

        var bsod = Classify(Marker(), boot: nextBoot, evidence: new[]
        {
            new OsEvidence { Kind = OsEvidence.Kinds.KernelPower, EventId = 41, AtUtc = Now - TimeSpan.FromMinutes(1), BugcheckCode = "0x0000009f" }
        });
        Assert.Equal("os_crash", bsod.Cause);

        // The OS logged an orderly, user-initiated restart, but the agent was killed before it could say goodbye.
        var unreported = Classify(Marker(), boot: nextBoot, evidence: new[]
        {
            new OsEvidence { Kind = OsEvidence.Kinds.ShutdownInitiated, EventId = 1074, AtUtc = Now - TimeSpan.FromMinutes(2), User = @"PLANT\jdoe", ShutdownType = "restart", Planned = false }
        });
        Assert.Equal(C.OsShutdownUnreported, unreported.Classification);
        Assert.Equal("user", unreported.Initiator);
        Assert.Equal(@"PLANT\jdoe", unreported.InitiatedBy);
        Assert.Equal("restart", unreported.OsAction);
        Assert.False(unreported.Planned);

        // Evidence from before the previous run is not about it.
        var stale = Classify(Marker(), boot: nextBoot, evidence: new[]
        {
            new OsEvidence { Kind = OsEvidence.Kinds.ShutdownInitiated, AtUtc = Now - TimeSpan.FromDays(3), User = "old" }
        });
        Assert.Equal(C.UnexpectedShutdown, stale.Classification);
    }

    [Fact]
    public void IsSameBoot_UsesUptimeRegression_StrongIds_AndBootTimeToleranceForClockDerivedIds()
    {
        Assert.False(PreviousRunClassifier.IsSameBoot(Marker(uptimeMs: 5_000_000), Boot("boot-A", 1_000))); // uptime went down: rebooted
        Assert.False(PreviousRunClassifier.IsSameBoot(Marker(), Boot("boot-B")));
        Assert.True(PreviousRunClassifier.IsSameBoot(Marker(), Boot("boot-A")));

        var weak = Marker();
        weak.BootIdSource = BootInfoProvider.BootTime;
        weak.OsBootedAtUtc = Now - TimeSpan.FromHours(1) + TimeSpan.FromSeconds(40); // clock adjusted a little
        Assert.True(PreviousRunClassifier.IsSameBoot(weak, new BootInfo("bt-x", BootInfoProvider.BootTime, Now - TimeSpan.FromHours(1), 3_600_000)));
    }

    // ------------------------------------------------------------------ stop reasons

    [Theory]
    [InlineData("SERVICE_CONTROL_SHUTDOWN", true, null, null, false, "os_shutdown")]
    [InlineData("SERVICE_CONTROL_STOP", true, null, null, false, "service_stop")]
    [InlineData("SIGTERM", true, null, null, false, "os_shutdown")]
    [InlineData("SIGTERM", false, "stopping", "1 reboot.target start running", false, "os_restart")]
    [InlineData("SIGTERM", false, "stopping", "2 poweroff.target start running", false, "os_shutdown")]
    [InlineData("SIGTERM", false, "running", null, false, "service_stop")]
    [InlineData("SIGTERM", false, null, null, false, "service_stop")]
    [InlineData("SIGINT", false, null, null, false, "service_stop")]
    [InlineData(null, false, null, null, true, "agent_fault")]
    [InlineData(null, false, null, null, false, "unknown")]
    public void StopReasonResolver_SeparatesOsShutdownFromServiceStop(string? signal, bool windows, string? state, string? jobs, bool faulted, string expected)
    {
        Assert.Equal(expected, StopReasonResolver.Resolve(signal, windows, state, jobs, faulted).Reason);
    }

    // ------------------------------------------------------------------ OS evidence parsing

    [Fact]
    public void WindowsEventMapping_Reads1074_41_6008_AndBugcheck()
    {
        var e1074 = OsShutdownEvidenceProvider.MapWindowsEvent(1074, "User32",
            new[] { @"C:\Windows\system32\shutdown.exe (IPC-07)", "IPC-07", "Other (Planned)", "0x80000000", "restart", "patch window", @"PLANT\jdoe" }, Now);
        Assert.Equal(OsEvidence.Kinds.ShutdownInitiated, e1074!.Kind);
        Assert.Equal(@"PLANT\jdoe", e1074.User);
        Assert.Equal("restart", e1074.ShutdownType);
        Assert.True(e1074.Planned);

        var unplanned = OsShutdownEvidenceProvider.MapWindowsEvent(1074, "User32", new[] { "p", "c", "Other (Unplanned)", "0x00000000", "power off", "", "NT AUTHORITY\\SYSTEM" }, Now);
        Assert.False(unplanned!.Planned);
        Assert.Equal("system", PreviousRunClassifier.InitiatorOf(unplanned.User));

        Assert.Equal("0x0000009f", OsShutdownEvidenceProvider.MapWindowsEvent(41, "Microsoft-Windows-Kernel-Power", new[] { "159", "0", "0", "0", "0", "false", "0" }, Now)!.BugcheckCode);
        Assert.Null(OsShutdownEvidenceProvider.MapWindowsEvent(41, "Microsoft-Windows-Kernel-Power", new[] { "0" }, Now)!.BugcheckCode);
        Assert.Equal(OsEvidence.Kinds.UnexpectedShutdown, OsShutdownEvidenceProvider.MapWindowsEvent(6008, "EventLog", Array.Empty<string>(), Now)!.Kind);
        Assert.Equal(OsEvidence.Kinds.Bugcheck, OsShutdownEvidenceProvider.MapWindowsEvent(1001, "Microsoft-Windows-WER-SystemErrorReporting", new[] { "0x0000009f (0x3, 0x0, 0x0, 0x0)" }, Now)!.Kind);
        Assert.Null(OsShutdownEvidenceProvider.MapWindowsEvent(7034, "Service Control Manager", new[] { "Print Spooler" }, Now));
    }

    [Fact]
    public void LinuxJournalParsing_FindsWhoRebooted_AndCleanShutdownMarkers()
    {
        const string journal =
            "2026-11-12T00:55:01+0100 ipc07 sudo[4411]:     jdoe : TTY=pts/0 ; PWD=/home/jdoe ; USER=root ; COMMAND=/usr/bin/systemctl reboot\n" +
            "2026-11-12T00:55:02+0100 ipc07 systemd-logind[612]: System is rebooting.\n" +
            "2026-11-12T00:55:09+0100 ipc07 kernel: reboot: Restarting system\n" +
            "garbage line\n";

        var evidence = OsShutdownEvidenceProvider.ParseLinuxJournal(journal, Now - TimeSpan.FromHours(3));
        Assert.Empty(OsShutdownEvidenceProvider.ParseLinuxJournal(journal, Now)); // older than the window

        Assert.Equal(3, evidence.Count);
        Assert.Equal("jdoe", evidence[0].User);
        Assert.Equal("restart", evidence[1].ShutdownType);
        Assert.Equal(OsEvidence.Kinds.CleanShutdown, evidence[2].Kind);
        Assert.Empty(new OsShutdownEvidenceProvider().Collect(Now.AddYears(5), TimeSpan.FromMilliseconds(500))); // never throws
    }

    // ------------------------------------------------------------------ crash record

    [Fact]
    public void CrashRecord_RoundTrips_HasAStableStackHash_AndADamagedRecordIsIgnored()
    {
        var session = new AgentSession("crash-session", "1.2.3");
        var recorder = new CrashRecorder(Path.Combine(_root, "crash.json"), session);
        AgentActivity.Set("reporting PLC memory");

        Exception Thrown()
        {
            try { throw new InvalidOperationException("ADS buffer overrun"); }
            catch (Exception ex) { return ex; }
        }

        Assert.True(recorder.Record(Thrown(), "unhandled_exception"));
        var record = recorder.ReadPrevious(out _)!;
        Assert.Equal("crash-session", record.SessionId);
        Assert.Equal("System.InvalidOperationException", record.ExceptionType);
        Assert.Equal("ADS buffer overrun", record.Message);
        Assert.Equal("1.2.3", record.AgentVersion);
        Assert.Equal("reporting PLC memory", record.Activity);
        Assert.NotEmpty(record.TopFrames);
        Assert.Equal(16, record.StackHash.Length);
        Assert.Equal(record.StackHash, CrashRecorder.Build(Thrown(), "x", true, session, "y").StackHash);

        File.WriteAllBytes(recorder.FilePath, new byte[400]);
        File.Delete(recorder.FilePath + ".bak");
        Assert.Null(recorder.ReadPrevious(out var load));
        Assert.True(load.PrimaryWasDamaged);

        recorder.Clear();
        Assert.False(File.Exists(recorder.FilePath));
    }

    // ------------------------------------------------------------------ the agent side end to end

    private sealed class FakeMqtt : IMqttAgentClient
    {
        public bool Connected = true;
        public bool FailPublishes;
        public bool Disconnected;
        public readonly List<(string Topic, byte[] Payload)> Published = new();
        public string? WillTopic;
        public Func<byte[]>? WillFactory;

        public bool IsConnected => Connected;
        public string BrokerHost => "test";
        public int BrokerPort => 1883;
        public Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Connected);
        public Task<bool> PublishProtobufAsync<T>(string topic, T message, int qos = 1, CancellationToken cancellationToken = default) where T : IMessage
            => PublishRawAsync(topic, message.ToByteArray(), qos, cancellationToken);
        public Task<bool> PublishRawAsync(string topic, byte[] payload, int qos = 1, CancellationToken cancellationToken = default)
        {
            if (FailPublishes || !Connected) return Task.FromResult(false);
            lock (Published) Published.Add((topic, payload));
            return Task.FromResult(true);
        }
        public Task SubscribeToCommandsAsync(string machineIdentifier, Func<ServerCommand, Task> onCommandReceived, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ConfigureLastWill(string topic, Func<byte[]> payloadFactory) { WillTopic = topic; WillFactory = payloadFactory; }
        public Task DisconnectGracefullyAsync(CancellationToken cancellationToken = default) { Disconnected = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public List<(AgentEventMessage Event, JsonObject Payload)> Events()
        {
            lock (Published)
            {
                return Published.Select(p => AgentEventMessage.Parser.ParseFrom(p.Payload))
                    .Select(e => (e, JsonNode.Parse(e.PayloadJson)!.AsObject())).ToList();
            }
        }
    }

    private sealed class FixedIdentity : IMachineIdentity
    {
        public string MachineIdentifier => "HW-LIFECYCLE-TEST-0001";
        public string Source => "test";
        public string ResolveMacAddress(IEnumerable<NetworkAdapterInfo>? adapters = null) => "02:00:00:00:00:01";
    }

    private sealed class FixedEvidence : IOsShutdownEvidenceProvider
    {
        public IReadOnlyList<OsEvidence> Evidence = Array.Empty<OsEvidence>();
        public IReadOnlyList<OsEvidence> Collect(DateTimeOffset sinceUtc, TimeSpan timeout) => Evidence;
    }

    private AgentLifecycleService NewService(AgentPaths paths, AgentSession session, FakeMqtt mqtt, ShutdownSignalMonitor signals,
        IRecoveryJournal? journal = null, FixedEvidence? evidence = null) =>
        new(NullLogger<AgentLifecycleService>.Instance, paths, session, mqtt, new FixedIdentity(), signals,
            new CrashRecorder(paths.CrashRecordFile, session), journal ?? new RecoveryJournal(), evidence ?? new FixedEvidence(),
            new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig()));

    private static async Task<PreviousRunAssessment> WaitForClassification(AgentLifecycleService service)
    {
        for (int i = 0; i < 100 && service.PreviousRun == null; i++) await Task.Delay(50);
        return service.PreviousRun ?? throw new TimeoutException("previous run was not classified");
    }

    [Fact]
    public async Task Lifecycle_StartStopRestart_ClassifiesEachEnd_AndSendsStartedStoppingAndLastWill()
    {
        var paths = AgentPaths.ForRoot(_root);
        var mqtt = new FakeMqtt();

        // Run 1: first start on this machine. Its "started" report waits in the durable outbox.
        var signals1 = new ShutdownSignalMonitor(registerPosixSignals: false);
        var run1 = NewService(paths, new AgentSession("session-1", "1.0.0"), mqtt, signals1);
        await run1.StartAsync(CancellationToken.None);
        Assert.Equal(C.FirstStart, (await WaitForClassification(run1)).Classification);
        Assert.False(run1.CurrentMarker!.CleanShutdown);
        Assert.Equal(MqttTopics.Events("HW-LIFECYCLE-TEST-0001"), mqtt.WillTopic);

        var will = AgentEventMessage.Parser.ParseFrom(mqtt.WillFactory!());
        Assert.Equal(LifecycleContract.Source, will.Source);
        Assert.Equal("connection_lost", JsonNode.Parse(will.PayloadJson)!["phase"]!.GetValue<string>());
        Assert.NotEqual(will.EventId, AgentEventMessage.Parser.ParseFrom(mqtt.WillFactory!()).EventId); // one will per connection

        mqtt.FailPublishes = true;
        Assert.Equal(0, await run1.FlushOutboxAsync(CancellationToken.None));
        Assert.Equal(1, run1.PendingEvents);
        mqtt.FailPublishes = false;
        Assert.Equal(1, await run1.FlushOutboxAsync(CancellationToken.None));

        // Operator stops the agent from the console.
        signals1.Record(LifecycleContract.Signal.SigInt);
        await run1.StopAsync(CancellationToken.None);
        Assert.True(mqtt.Disconnected);

        var events = mqtt.Events();
        Assert.Equal(new[] { "started", "stopping" }, events.Select(e => e.Payload["phase"]!.GetValue<string>()).ToArray());
        Assert.Equal("service_stop", events[1].Payload["stop"]!["reason"]!.GetValue<string>());
        Assert.Equal("SIGINT", events[1].Payload["stop"]!["signal"]!.GetValue<string>());
        Assert.Equal("Info", events[1].Event.Level);

        // Run 2 on the same boot: the previous run is a clean service stop.
        var run2 = NewService(paths, new AgentSession("session-2", "1.0.0"), mqtt, new ShutdownSignalMonitor(false));
        await run2.StartAsync(CancellationToken.None);
        var previous = await WaitForClassification(run2);
        Assert.Equal(C.ServiceStop, previous.Classification);
        Assert.Equal("session-1", previous.SessionId);
        await run2.StopAsync(CancellationToken.None);

        // Run 3 crashes: the session marker stays unclean and a crash record is written.
        var session3 = new AgentSession("session-3", "1.0.0");
        new SessionTracker(paths.SessionMarkerFile).Begin(session3, BootInfoProvider.Read(), null);
        new CrashRecorder(paths.CrashRecordFile, session3).Record(new InvalidOperationException("collector exploded"), "unhandled_exception");

        var run4 = NewService(paths, new AgentSession("session-4", "1.0.0"), mqtt, new ShutdownSignalMonitor(false));
        await run4.StartAsync(CancellationToken.None);
        var crashed = await WaitForClassification(run4);
        Assert.Equal(C.AgentCrash, crashed.Classification);
        Assert.Equal("collector exploded", crashed.Crash!.Message);
        Assert.False(File.Exists(paths.CrashRecordFile)); // consumed into the report
        await run4.StopAsync(CancellationToken.None);

        // Run 5 died with the machine (power loss / BSOD): the marker says another boot and no clean flag.
        var session5 = new AgentSession("session-5", "1.0.0");
        new SessionTracker(paths.SessionMarkerFile).Begin(session5, new BootInfo("previous-boot", BootInfoProvider.KernelBootId, DateTimeOffset.UtcNow.AddDays(-2), long.MaxValue / 4), null);

        var recovered = new RecoveryJournal();
        recovered.Record(new RecoveryNotice { Component = "config", Defect = "zero_filled", RecoveredFrom = "backup", Severity = "Warning", File = "agent.json" });
        var run6 = NewService(paths, new AgentSession("session-6", "1.0.0"), mqtt, new ShutdownSignalMonitor(false), recovered,
            new FixedEvidence { Evidence = new[] { new OsEvidence { Kind = OsEvidence.Kinds.KernelPower, EventId = 41, AtUtc = DateTimeOffset.UtcNow, BugcheckCode = "0x0000009f" } } });
        await run6.StartAsync(CancellationToken.None);
        var bsod = await WaitForClassification(run6);
        Assert.Equal(C.UnexpectedShutdown, bsod.Classification);
        Assert.Equal("os_crash", bsod.Cause);
        // The start report is assembled just after the classification becomes visible: wait for the
        // journal to be drained instead of asserting immediately (raced under the full parallel suite).
        for (int i = 0; i < 100 && recovered.Pending.Count > 0; i++) await Task.Delay(50);
        Assert.Empty(recovered.Pending); // the recovery travels with the start report

        mqtt.Published.Clear();
        await run6.FlushOutboxAsync(CancellationToken.None);
        // The outbox is durable: the start reports of runs 2 and 4 (never flushed in those runs) go out first, in order.
        var startedReports = mqtt.Events().Where(e => e.Payload["phase"]!.GetValue<string>() == "started").ToList();
        Assert.Equal(new[] { "session-2", "session-4", "session-6" }, startedReports.Select(e => e.Payload["sessionId"]!.GetValue<string>()).ToArray());
        var started = startedReports.Last();
        Assert.Equal("Critical", started.Event.Level);
        Assert.Equal("zero_filled", started.Payload["recoveries"]![0]!["defect"]!.GetValue<string>());
        Assert.Equal("session-5", started.Payload["previous"]!["sessionId"]!.GetValue<string>());
        await run6.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void SessionTracker_BacksOffAfterRepeatedShortAbnormalRuns()
    {
        var file = Path.Combine(_root, "session.json");
        var boot = BootInfoProvider.Read();

        for (int run = 1; run <= 4; run++)
        {
            var tracker = new SessionTracker(file);
            tracker.Begin(new AgentSession($"crashloop-{run}"), boot, null); // never completes gracefully
            Assert.Equal(SessionTracker.BackoffFor(run - 1), tracker.StartupBackoff);
        }
        Assert.Equal(TimeSpan.FromSeconds(10), SessionTracker.BackoffFor(3));

        var graceful = new SessionTracker(file);
        graceful.Begin(new AgentSession("calm"), boot, null);
        graceful.CompleteGracefully(new StopInfo { Reason = "service_stop" });
        var next = new SessionTracker(file);
        next.Begin(new AgentSession("after-calm"), boot, null);
        Assert.Equal(TimeSpan.Zero, next.StartupBackoff);
    }

    // ------------------------------------------------------------------ MQTT last will through a real broker

    [Fact]
    public async Task LastWill_IsPublishedByTheBroker_OnAbruptLoss_AndDiscardedOnGracefulDisconnect()
    {
        int port = 19_000 + Random.Shared.Next(0, 900);
        var serverFactory = new MqttServerFactory();
        using var server = serverFactory.CreateMqttServer(serverFactory.CreateServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(port).Build());
        await server.StartAsync();

        try
        {
            var factory = new MqttClientFactory();
            using var subscriber = factory.CreateMqttClient();
            var received = new List<MqttApplicationMessage>();
            subscriber.ApplicationMessageReceivedAsync += e => { lock (received) received.Add(e.ApplicationMessage); return Task.CompletedTask; };
            await subscriber.ConnectAsync(factory.CreateClientOptionsBuilder().WithTcpServer("127.0.0.1", port).WithClientId("sub").Build());
            await subscriber.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(MqttTopics.EventsWildcard).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build());

            var agentClient = new MqttAgentClient(NullLogger<MqttAgentClient>.Instance, new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig()));
            var session = new AgentSession("will-session");
            int connection = 0;
            agentClient.ConfigureLastWill(MqttTopics.Events("HW-WILL-0001"), () =>
                LifecycleReportBuilder.ConnectionLost("HW-WILL-0001", "ipc-will", session, BootInfoProvider.Read(), ++connection).ToByteArray());

            async Task<IMqttClient> ConnectAgentLike(string clientId)
            {
                var builder = factory.CreateClientOptionsBuilder().WithTcpServer("127.0.0.1", port).WithClientId(clientId).WithCleanSession();
                agentClient.ApplyLastWill(builder, "test-agent-key");
                var client = factory.CreateMqttClient();
                await client.ConnectAsync(builder.Build());
                return client;
            }

            // Graceful: normal DISCONNECT, the broker must drop the will.
            var graceful = await ConnectAgentLike("agent-graceful");
            await graceful.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
            graceful.Dispose();
            await Task.Delay(500);
            lock (received) Assert.Empty(received);

            // Abrupt: the socket goes away without DISCONNECT (process killed, power cut).
            var abrupt = await ConnectAgentLike("agent-abrupt");
            abrupt.Dispose();
            for (int i = 0; i < 50; i++) { lock (received) if (received.Count > 0) break; await Task.Delay(100); }

            MqttApplicationMessage will;
            lock (received) will = Assert.Single(received);
            Assert.Equal(MqttTopics.Events("HW-WILL-0001"), will.Topic);
            Assert.Contains(will.UserProperties, p => p.Name == "x-agent-key" && p.ReadValueAsString() == "test-agent-key");
            var message = AgentEventMessage.Parser.ParseFrom(will.Payload.ToArray());
            Assert.Equal("connection_lost", JsonNode.Parse(message.PayloadJson)!["phase"]!.GetValue<string>());
            Assert.Equal("will-session", JsonNode.Parse(message.PayloadJson)!["sessionId"]!.GetValue<string>());
        }
        finally
        {
            await server.StopAsync();
        }
    }

    // ------------------------------------------------------------------ backend ingestion

    private async Task<ClientPc> SeedPcAsync(string machineId, string mac)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pc = new ClientPc { Id = Guid.NewGuid(), Name = machineId, Hostname = machineId, MachineIdentifier = machineId, MacAddress = mac, LastOnline = DateTimeOffset.UtcNow };
        db.ClientPcs.Add(pc);
        await db.SaveChangesAsync();
        return pc;
    }

    private (List<AgentEvent> Events, JsonObject? Lifecycle, List<MalformedTelemetryRecord> Quarantine) Read(string machineId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pc = db.ClientPcs.AsNoTracking().FirstOrDefault(p => p.MachineIdentifier == machineId);
        var events = pc == null ? new List<AgentEvent>() : db.AgentEvents.AsNoTracking().Where(e => e.ClientPcId == pc.Id).OrderBy(e => e.Timestamp).ToList();
        var lifecycle = pc?.SystemMetadata != null ? JsonNode.Parse(pc.SystemMetadata.RootElement.GetRawText())?[AgentLifecycleIngestor.MetadataKey] as JsonObject : null;
        var quarantine = db.MalformedTelemetryRecords.AsNoTracking().Where(q => q.SourceIdentifier == machineId).ToList();
        return (events, lifecycle, quarantine);
    }

    private static PreviousRunAssessment PowerLoss() => new()
    {
        Classification = C.UnexpectedShutdown, Cause = "power_loss_or_reset", Initiator = "none",
        Reason = "the OS logged an unexpected shutdown", SessionId = "old-session", DowntimeSeconds = 412
    };

    [Fact]
    public async Task Backend_IngestsLifecycleReports_PersistsEventAndState_DeduplicatesAndKeepsStateAcrossSystemInfo()
    {
        const string machineId = "HW-LIFECYCLE-INGEST-01";
        const string mac = "AA:BB:CC:11:22:01";
        await SeedPcAsync(machineId, mac);
        using var scope = _factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();

        var session = new AgentSession("new-session", "1.0.0");
        var boot = BootInfoProvider.Read();
        var started = LifecycleReportBuilder.Started(machineId, "ipc-01", session, boot, PowerLoss(), Array.Empty<RecoveryNotice>());

        // Over the wire and back, exactly as the broker delivers it.
        Assert.True(await ingestion.ProcessAgentEventAsync(AgentEventMessage.Parser.ParseFrom(started.ToByteArray())));
        Assert.True(await ingestion.ProcessAgentEventAsync(started)); // QoS 1 redelivery

        var (events, lifecycle, _) = Read(machineId);
        var evt = Assert.Single(events);
        Assert.Equal(AgentLifecycleIngestor.Source, evt.Source);
        Assert.Equal("Critical", evt.Level);
        Assert.Contains("unexpected_shutdown", evt.Message);
        Assert.Equal("online", lifecycle!["State"]!.GetValue<string>());
        Assert.Equal("unexpected_shutdown", lifecycle["PreviousRunClassification"]!.GetValue<string>());
        Assert.Equal("new-session", lifecycle["SessionId"]!.GetValue<string>());

        // A late last will of the OLD session must not mark the running agent offline.
        var oldWill = LifecycleReportBuilder.ConnectionLost(machineId, "ipc-01", new AgentSession("old-session"), boot, 1);
        oldWill.Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(-10)); // a will is built when its connection was opened
        Assert.True(await ingestion.ProcessAgentEventAsync(oldWill));
        Assert.Equal("online", Read(machineId).Lifecycle!["State"]!.GetValue<string>());

        // The next system_info report replaces system_metadata but keeps the server-written lifecycle state.
        var report = new SystemInfoRequest { Hostname = "ipc-01", MachineIdentifier = machineId, MacAddress = mac, LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow) };
        report.Components.Add(new InventoryComponent { Name = "Software", Technology = "Agent", Type = "software", DataJson = "{\"OsVersion\":\"Windows 10 IoT\",\"IPAddress\":\"10.0.0.7\"}" });
        Assert.True((await ingestion.ProcessSystemInfoAsync(report)).Success);
        Assert.Equal("new-session", Read(machineId).Lifecycle!["SessionId"]!.GetValue<string>());

        var stopping = LifecycleReportBuilder.Stopping(machineId, "ipc-01", session, boot,
            new StopInfo { Reason = "os_restart", Initiator = "user", InitiatedBy = @"PLANT\jdoe", Signal = "SERVICE_CONTROL_SHUTDOWN" });
        Assert.True(await ingestion.ProcessAgentEventAsync(stopping));
        var after = Read(machineId);
        Assert.Equal(3, after.Events.Count);
        Assert.Equal("stopped", after.Lifecycle!["State"]!.GetValue<string>());
        Assert.Equal("os_restart", after.Lifecycle["LastStopReason"]!.GetValue<string>());
        Assert.Equal("user", after.Lifecycle["LastStopInitiator"]!.GetValue<string>());

        var will = LifecycleReportBuilder.ConnectionLost(machineId, "ipc-01", new AgentSession("next-session"), boot, 1);
        Assert.True(await ingestion.ProcessAgentEventAsync(will));
        Assert.Equal("offline", Read(machineId).Lifecycle!["State"]!.GetValue<string>());
    }

    [Fact]
    public async Task Backend_QuarantinesInvalidLifecycleReports_AndReportsForUnknownDevices()
    {
        const string machineId = "HW-LIFECYCLE-INGEST-02";
        await SeedPcAsync(machineId, "AA:BB:CC:11:22:02");
        using var scope = _factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>();

        AgentEventMessage Bad(string payload, string eventId) => new()
        {
            MachineIdentifier = machineId, EventId = eventId, Source = "AgentLifecycle", Level = "Info",
            Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow), PayloadJson = payload
        };

        Assert.False(await ingestion.ProcessAgentEventAsync(Bad("{not json", "bad-1")));
        Assert.False(await ingestion.ProcessAgentEventAsync(Bad("{\"schema\":\"heimdall.lifecycle/1\",\"phase\":\"exploded\",\"sessionId\":\"s\"}", "bad-2")));
        Assert.False(await ingestion.ProcessAgentEventAsync(Bad("{\"schema\":\"heimdall.lifecycle/1\",\"phase\":\"started\",\"sessionId\":\"s\",\"previous\":{\"classification\":\"gremlins\"}}", "bad-3")));
        Assert.False(await ingestion.ProcessAgentEventAsync(Bad("{\"schema\":\"other/9\",\"phase\":\"online\",\"sessionId\":\"s\"}", "bad-4")));

        var (events, _, quarantine) = Read(machineId);
        Assert.Empty(events);
        Assert.Equal(4, quarantine.Count);
        Assert.All(quarantine, q => Assert.Equal(AgentLifecycleIngestor.QuarantineChannel, q.IngestionChannel));

        var unknown = LifecycleReportBuilder.Started("HW-NEVER-SEEN-0001", "ghost", new AgentSession(), BootInfoProvider.Read(), PowerLoss(), Array.Empty<RecoveryNotice>());
        Assert.False(await ingestion.ProcessAgentEventAsync(unknown));
        Assert.Single(Read("HW-NEVER-SEEN-0001").Quarantine);

        // Timestamps from a broken agent clock are not trusted.
        var future = LifecycleReportBuilder.Started(machineId, "ipc-02", new AgentSession("clock"), BootInfoProvider.Read(), PowerLoss(), Array.Empty<RecoveryNotice>());
        future.Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddYears(3));
        Assert.True(await ingestion.ProcessAgentEventAsync(future));
        Assert.True(Read(machineId).Events.Single().Timestamp < DateTime.UtcNow.AddMinutes(1));
    }
}
