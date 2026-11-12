namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon.Infrastructure.Identity;
using App.Agent.Daemon.Infrastructure.Persistence;
using App.Agent.Daemon.Interfaces;
using App.Contracts.Mqtt;
using App.Shared.Protos;
using App.Shared.Sanitization;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>What the rest of the agent needs from the lifecycle report.</summary>
public interface IAgentLifecycle
{
    string SessionId { get; }

    /// <summary>Delay before the collectors start, because recent runs kept ending abnormally right after start.</summary>
    TimeSpan StartupBackoff { get; }

    /// <summary>How the previous run ended (null until it has been classified).</summary>
    PreviousRunAssessment? PreviousRun { get; }

    /// <summary>Called after a report reached the broker: the device is known to the server, lifecycle events may follow.</summary>
    void MarkReportDelivered();

    /// <summary>Records a stop the agent decided on itself (supervised restart, update) before carrying it out.</summary>
    void RecordStopIntent(StopInfo intent);
}

/// <summary>
/// Produces the lifecycle / power report (contract "heimdall.lifecycle/1", see system_info.proto):
/// <list type="bullet">
/// <item>start: classifies how the previous run ended from the durable session marker, the crash record,
/// the OS boot identity and OS shutdown evidence, and queues a "started" report in a durable outbox
/// (so it survives a crash before it could be sent);</item>
/// <item>connect: registers the MQTT Last Will ("connection_lost") for the case no goodbye is possible;</item>
/// <item>run: refreshes the session marker, delivers the outbox, reports reconnects ("online");</item>
/// <item>graceful stop: records why and by whom (service control STOP vs SHUTDOWN, SIGTERM while systemd
/// is "stopping"), sets the clean-shutdown flag durably first, then sends the "stopping" goodbye with a
/// short bound and disconnects normally so the broker discards the Last Will.</item>
/// </list>
/// Registered as the first hosted service, so it starts first and stops last.
/// </summary>
public sealed class AgentLifecycleService : BackgroundService, IAgentLifecycle
{
    public static readonly TimeSpan GoodbyeTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan EvidenceTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DeliveryGateFallback = TimeSpan.FromSeconds(30);

    private readonly ILogger<AgentLifecycleService> _logger;
    private readonly AgentSession _session;
    private readonly IMqttAgentClient _mqtt;
    private readonly IMachineIdentity _identity;
    private readonly IHostApplicationLifetime? _hostLifetime;
    private readonly ShutdownSignalMonitor _signals;
    private readonly CrashRecorder _crashRecorder;
    private readonly IRecoveryJournal _journal;
    private readonly IOsShutdownEvidenceProvider _evidence;
    private readonly IConfigurationService _config;
    private readonly IServiceProvider? _services;
    private readonly SessionTracker _tracker;
    private readonly DurableRecordQueue _outbox;
    private readonly string _hostname;

    private PreviousRunContext? _previousContext;
    private BootInfo _boot;
    private int _stopping;
    private Task _goodbye = Task.CompletedTask;
    private int _connectionCount;
    private int _deliveryGateOpen;
    private DateTimeOffset _firstConnectedAt = DateTimeOffset.MaxValue;
    private bool _wasConnected;
    private long _eventsDelivered;

    public AgentLifecycleService(
        ILogger<AgentLifecycleService> logger,
        AgentPaths paths,
        AgentSession session,
        IMqttAgentClient mqtt,
        IMachineIdentity identity,
        ShutdownSignalMonitor signals,
        CrashRecorder crashRecorder,
        IRecoveryJournal journal,
        IOsShutdownEvidenceProvider evidence,
        IConfigurationService config,
        IHostApplicationLifetime? hostLifetime = null,
        IServiceProvider? services = null)
    {
        _logger = logger;
        _session = session;
        _mqtt = mqtt;
        _identity = identity;
        _signals = signals;
        _crashRecorder = crashRecorder;
        _journal = journal;
        _evidence = evidence;
        _config = config;
        _hostLifetime = hostLifetime;
        _services = services;
        _tracker = new SessionTracker(paths.SessionMarkerFile);
        _outbox = new DurableRecordQueue(paths.EventOutboxDirectory, "event_", "outbox", logger, journal);
        _hostname = StringSanitizer.ToSafeToken(Environment.MachineName, "endpoint");
        _boot = BootInfoProvider.Read();
    }

    public string SessionId => _session.SessionId;
    public TimeSpan StartupBackoff => _tracker.StartupBackoff;
    public PreviousRunAssessment? PreviousRun { get; private set; }
    public SessionMarker? CurrentMarker => _tracker.Current;
    public int PendingEvents => _outbox.Count;
    public long EventsDelivered => Interlocked.Read(ref _eventsDelivered);

    private string MachineId => _identity.MachineIdentifier;

    public void MarkReportDelivered() => Interlocked.Exchange(ref _deliveryGateOpen, 1);

    public void RecordStopIntent(StopInfo intent) => _tracker.RecordIntent(intent);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var crash = _crashRecorder.ReadPrevious(out var crashLoad);
            if (crashLoad.PrimaryWasDamaged)
            {
                _logger.LogWarning("Crash record {File} of the previous run is {Defect}; ignoring it.", _crashRecorder.FilePath,
                    RecoveryNotice.DefectName(crashLoad.PrimaryDefect));
            }

            _previousContext = _tracker.Begin(_session, _boot, crash);
            if (_tracker.StartupBackoff > TimeSpan.Zero)
            {
                _logger.LogWarning("The last {Count} runs ended abnormally shortly after start; delaying the collectors by {Delay}.",
                    _tracker.Current?.ConsecutiveAbnormalStarts, _tracker.StartupBackoff);
            }
        }
        catch (Exception ex)
        {
            // The lifecycle report must never be the reason the agent does not start.
            _logger.LogError(ex, "Could not read or write the session marker; previous-run classification is unavailable.");
            _previousContext = new PreviousRunContext(null, true, null, ex.Message);
        }

        try
        {
            _mqtt.ConfigureLastWill(MqttTopics.Events(MachineId), () =>
                LifecycleReportBuilder.ConnectionLost(MachineId, _hostname, _session, _boot, Interlocked.Increment(ref _connectionCount)).ToByteArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register the MQTT last will.");
        }

        _hostLifetime?.ApplicationStopping.Register(OnStopping);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            ClassifyAndQueueStartReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build the start report.");
        }

        var nextHeartbeat = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= nextHeartbeat)
                {
                    _boot = _boot with { UptimeMs = Environment.TickCount64 };
                    _tracker.Heartbeat(_boot);
                    nextHeartbeat = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(_config.Config.SessionHeartbeatSeconds, 5, 3600));
                }

                bool connected = _mqtt.IsConnected;
                if (connected && _firstConnectedAt == DateTimeOffset.MaxValue) _firstConnectedAt = DateTimeOffset.UtcNow;
                if (connected && !_wasConnected && _eventsDelivered > 0)
                {
                    Enqueue(LifecycleReportBuilder.Online(MachineId, _hostname, _session, _boot, _connectionCount));
                }
                _wasConnected = connected;

                bool gateOpen = Volatile.Read(ref _deliveryGateOpen) == 1 || DateTimeOffset.UtcNow - _firstConnectedAt >= DeliveryGateFallback;
                if (connected && gateOpen && _outbox.Count > 0)
                {
                    await FlushOutboxAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lifecycle loop iteration failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Classifies the previous run and queues the "started" report durably.</summary>
    public PreviousRunAssessment ClassifyAndQueueStartReport()
    {
        var context = _previousContext ?? new PreviousRunContext(null, false, null, null);
        var since = context.Marker != null ? context.Marker.LastAliveAtUtc - TimeSpan.FromMinutes(30) : DateTimeOffset.UtcNow - TimeSpan.FromDays(7);

        IReadOnlyList<OsEvidence> evidence = Array.Empty<OsEvidence>();
        if (context.Marker != null && !(context.Marker.CleanShutdown && PreviousRunClassifier.IsSameBoot(context.Marker, _boot)))
        {
            // Only consult the OS when the previous run did not end in a plain service stop.
            evidence = _evidence.Collect(since, EvidenceTimeout);
        }

        var assessment = PreviousRunClassifier.Classify(context.Marker, context.MarkerDamaged, _boot, context.Crash, evidence,
            _session.AgentVersion, DateTimeOffset.UtcNow);
        PreviousRun = assessment;

        var recoveries = _journal.Pending;
        Enqueue(LifecycleReportBuilder.Started(MachineId, _hostname, _session, _boot, assessment, recoveries));
        _journal.Acknowledge(recoveries.Select(r => r.Id)); // they now travel in the durable outbox
        if (context.Crash != null) _crashRecorder.Clear();

        var level = assessment.Level == "Info" ? LogLevel.Information : LogLevel.Warning;
        _logger.Log(level, "Previous agent run: {Classification} {Cause} (initiator {Initiator}{By}) - {Reason}",
            assessment.Classification, assessment.Cause ?? string.Empty, assessment.Initiator,
            assessment.InitiatedBy != null ? ", by " + assessment.InitiatedBy : string.Empty, assessment.Reason);
        return assessment;
    }

    /// <summary>Publishes queued lifecycle events oldest-first; stops at the first failure. Returns the number delivered.</summary>
    public async Task<int> FlushOutboxAsync(CancellationToken cancellationToken)
    {
        int delivered = 0;
        foreach (var file in _outbox.ListFiles())
        {
            var record = _outbox.Read(file);
            if (!record.IsValid)
            {
                if (record.Defect is FileDefect.Missing or FileDefect.Unreadable) continue;
                _outbox.SetAside(file, record.Defect, record.Detail);
                continue;
            }

            byte[] bytes;
            AgentEventMessage message;
            try
            {
                bytes = Convert.FromBase64String(record.Payload!);
                message = AgentEventMessage.Parser.ParseFrom(bytes);
            }
            catch (Exception ex) when (ex is FormatException or InvalidProtocolBufferException)
            {
                _outbox.SetAside(file, FileDefect.Malformed, ex.Message);
                continue;
            }

            if (!await _mqtt.PublishRawAsync(MqttTopics.Events(message.MachineIdentifier), bytes, 1, cancellationToken))
            {
                break;
            }

            _outbox.Remove(file);
            delivered++;
            Interlocked.Increment(ref _eventsDelivered);
        }
        return delivered;
    }

    private void Enqueue(AgentEventMessage message)
    {
        if (_outbox.TryEnqueue(Convert.ToBase64String(message.ToByteArray()), 8L * 1024 * 1024, out var error) == null)
        {
            _logger.LogWarning("Could not queue lifecycle event {EventId}: {Error}", message.EventId, error);
        }
    }

    /// <summary>
    /// First thing in a graceful stop (ApplicationStopping): decide why we stop, persist the clean flag
    /// with that reason, then start the goodbye so it races ahead of the other services stopping.
    /// </summary>
    private void OnStopping()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1) return;

        // A POSIX signal handler of the host lifetime calls StopApplication (and so this callback) on
        // the signal thread, possibly before our own handler recorded which signal it was. A short
        // hop off that thread lets every handler of the signal run first.
        _goodbye = Task.Run(async () =>
        {
            await Task.Delay(SignalSettleDelay);
            StopInfo stop;
            try
            {
                stop = ResolveStop();
                _tracker.CompleteGracefully(stop);
                _logger.LogInformation("Agent stopping: {Reason} (initiator {Initiator}, via {Signal})", stop.Reason, stop.Initiator, stop.Signal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not record the stop reason.");
                return;
            }
            await SendGoodbyeAsync(stop);
        });
    }

    /// <summary>Time given to the remaining handlers of a stop signal before the stop reason is resolved.</summary>
    public static readonly TimeSpan SignalSettleDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>Upper bound for the whole stop path: reason, clean flag, goodbye.</summary>
    public static readonly TimeSpan StopPathTimeout = TimeSpan.FromSeconds(5);

    private StopInfo ResolveStop()
    {
        string? signal = _signals.LastSignal;

        string? faultDetail = null;
        try
        {
            var faulted = _services?.GetServices<IHostedService>().OfType<BackgroundService>()
                .FirstOrDefault(s => !ReferenceEquals(s, this) && s.ExecuteTask?.IsFaulted == true);
            if (faulted?.ExecuteTask?.Exception is { } failure)
            {
                var inner = failure.InnerExceptions.Count == 1 ? failure.InnerExceptions[0] : failure;
                _crashRecorder.Record(inner, "background_service", terminating: false);
                faultDetail = $"{faulted.GetType().Name} failed: {inner.GetType().Name}: {inner.Message}";
            }
        }
        catch
        {
            // Diagnostics only.
        }

        var intent = _tracker.Current?.PendingIntent;
        if (signal == null && faultDetail == null && intent != null)
        {
            intent.Signal = LifecycleContract.Signal.Intent;
            intent.AtUtc = DateTimeOffset.UtcNow;
            return intent;
        }

        string? systemState = null;
        string? systemJobs = null;
        if (signal == LifecycleContract.Signal.SigTerm && OperatingSystem.IsLinux())
        {
            systemState = OsShutdownEvidenceProvider.RunCommand("systemctl", "is-system-running", TimeSpan.FromSeconds(1));
            if (string.Equals(systemState?.Trim(), "stopping", StringComparison.OrdinalIgnoreCase))
            {
                systemJobs = OsShutdownEvidenceProvider.RunCommand("systemctl", "list-jobs --no-legend --no-pager", TimeSpan.FromSeconds(1));
            }
        }

        var stop = StopReasonResolver.Resolve(signal, OperatingSystem.IsWindows(), systemState, systemJobs, faultDetail != null);
        if (faultDetail != null) stop.Detail = faultDetail;
        return stop;
    }

    private async Task SendGoodbyeAsync(StopInfo stop)
    {
        try
        {
            using var timeout = new CancellationTokenSource(GoodbyeTimeout);
            var message = LifecycleReportBuilder.Stopping(MachineId, _hostname, _session, _boot, stop);
            bool sent = await _mqtt.PublishRawAsync(MqttTopics.Events(MachineId), message.ToByteArray(), 1, timeout.Token);
            if (sent) Interlocked.Increment(ref _eventsDelivered);
            else _logger.LogInformation("Stop report not delivered (broker unreachable); the next start reports this stop from the session marker.");
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Stop report not delivered ({Error}); the next start reports this stop from the session marker.", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        OnStopping(); // no-op when ApplicationStopping already ran

        try
        {
            await base.StopAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            await _goodbye.WaitAsync(StopPathTimeout, cancellationToken);
        }
        catch
        {
            // Bounded: shutdown is never delayed by the goodbye.
        }

        try
        {
            // A normal DISCONNECT makes the broker discard the last will ("connection_lost").
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _mqtt.DisconnectGracefullyAsync(timeout.Token);
        }
        catch
        {
            // Disposal of the client disconnects as well.
        }
    }
}
