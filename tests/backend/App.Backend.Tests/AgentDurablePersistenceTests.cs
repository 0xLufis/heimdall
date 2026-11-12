namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.Configuration;
using App.Agent.Daemon.Infrastructure.Identity;
using App.Agent.Daemon.Infrastructure.Persistence;
using App.Agent.Daemon.Infrastructure.Security;
using App.Agent.Daemon.Infrastructure.Spooling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// What a hard power loss or BSOD can leave on disk (missing, empty, zero-filled, truncated or
/// malformed files) must never stop the agent or silently reset its configuration, identity,
/// key material or spool.
/// </summary>
public class AgentDurablePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall_persist_" + Guid.NewGuid().ToString("N"));

    public AgentDurablePersistenceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void Resolve_HonoursSpoolDir_AndFallsBackNextToStateWhenTheInstallDirIsReadOnly()
    {
        string install = Path.Combine(_root, "install");
        string state = Path.Combine(_root, "state");
        string spool = Path.Combine(_root, "spool");
        Directory.CreateDirectory(install);

        // HEIMDALL_SPOOL_DIR / HEIMDALL_STATE_DIR (the packaged Linux service sets both).
        var configured = AgentPaths.Resolve(name => name switch
        {
            AgentPaths.SpoolDirEnvVar => spool,
            AgentPaths.StateDirEnvVar => state,
            _ => null
        }, install);
        Assert.Equal(spool, configured.SpoolDirectory);
        Assert.Equal(state, configured.StateDirectory);
        Assert.Equal(Path.Combine(spool, "crash_dumps"), configured.CrashDumpDirectory);

        // Unset: the install directory, as before.
        var byDefault = AgentPaths.Resolve(name => name == AgentPaths.StateDirEnvVar ? state : null, install);
        Assert.Equal(Path.Combine(install, "telemetry_spool"), byDefault.SpoolDirectory);

        // A read-only install directory (systemd ProtectSystem=strict): next to the state instead.
        // Root ignores directory permissions, so this part needs an unprivileged account.
        if (OperatingSystem.IsWindows() || Environment.UserName == "root") return;
        string readOnlyInstall = Path.Combine(_root, "ro-install");
        Directory.CreateDirectory(readOnlyInstall);
        File.SetUnixFileMode(readOnlyInstall, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var readOnly = AgentPaths.Resolve(name => name == AgentPaths.StateDirEnvVar ? state : null, readOnlyInstall);
            Assert.Equal(Path.Combine(state, "telemetry_spool"), readOnly.SpoolDirectory);
        }
        finally
        {
            File.SetUnixFileMode(readOnlyInstall, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private const string GoodConfigJson =
        "{\"BackendUrl\":\"http://plant-a.example:5001\",\"ServerPublicKey\":null,\"ExtensionApiKey\":\"k-123\",\"HeartbeatIntervalSeconds\":15,\"AdsAmsNetId\":\"5.80.201.44.1.1\"}";

    public static IEnumerable<object[]> DamagedContents() => new[]
    {
        new object[] { "empty", Array.Empty<byte>(), FileDefect.Empty },
        new object[] { "whitespace", Encoding.UTF8.GetBytes("  \r\n "), FileDefect.Empty },
        new object[] { "zero_filled", new byte[512], FileDefect.ZeroFilled },
        new object[] { "truncated", Encoding.UTF8.GetBytes("{\"BackendUrl\":\"http://plant-a.example:5001\",\"Heartbeat"), FileDefect.Truncated },
        new object[] { "zeroed_tail", Encoding.UTF8.GetBytes("{\"BackendUrl\":\"http://plant-a.example:5001\"").Concat(new byte[64]).ToArray(), FileDefect.Truncated },
        new object[] { "not_json", Encoding.UTF8.GetBytes("this is not json }"), FileDefect.Malformed },
        new object[] { "wrong_root", Encoding.UTF8.GetBytes("[1,2,3]"), FileDefect.Malformed },
        new object[] { "null_literal", Encoding.UTF8.GetBytes("null"), FileDefect.Malformed },
        new object[] { "wrong_type", Encoding.UTF8.GetBytes("{\"HeartbeatIntervalSeconds\":\"ten\"}"), FileDefect.Malformed }
    };

    // ---------------------------------------------------------------- AtomicFile

    [Fact]
    public void AtomicFile_Write_ReplacesAtomically_KeepsPreviousAsBackup_AndLeavesNoTempFiles()
    {
        string path = Path.Combine(_root, "state.json");

        AtomicFile.Write(path, Encoding.UTF8.GetBytes("v1"));
        Assert.Equal("v1", File.ReadAllText(path));
        Assert.False(File.Exists(AtomicFile.BackupPathFor(path)));

        AtomicFile.Write(path, Encoding.UTF8.GetBytes("v2"));
        Assert.Equal("v2", File.ReadAllText(path));
        Assert.Equal("v1", File.ReadAllText(AtomicFile.BackupPathFor(path)));

        // A damaged current file must not be rotated over the good backup.
        AtomicFile.Write(path, Encoding.UTF8.GetBytes("v3"), keepPreviousAsBackup: false);
        Assert.Equal("v3", File.ReadAllText(path));
        Assert.Equal("v1", File.ReadAllText(AtomicFile.BackupPathFor(path)));

        Assert.DoesNotContain(Directory.GetFiles(_root), f => f.Contains(AtomicFile.TempInfix));
    }

    [Fact]
    public void AtomicFile_CleanupTempFiles_RemovesLeftoversOfAnInterruptedWrite()
    {
        File.WriteAllText(Path.Combine(_root, "agent.json" + AtomicFile.TempInfix + "deadbeef"), "{\"Back");
        File.WriteAllText(Path.Combine(_root, "spool_1.json.tmp"), "{");
        File.WriteAllText(Path.Combine(_root, "agent.json"), "{}");

        Assert.Equal(2, AtomicFile.CleanupTempFiles(_root));
        Assert.Equal(new[] { "agent.json" }, Directory.GetFiles(_root).Select(Path.GetFileName).ToArray());
    }

    // ---------------------------------------------------------------- agent.json

    [Fact]
    public void ConfigStore_FirstRun_CreatesDefaults_AndIsNotARecovery()
    {
        var store = new AgentConfigStore(Path.Combine(_root, "agent.json"));

        var result = store.Load("http://backend:5001");

        Assert.Equal(ConfigOrigin.FirstRun, result.Origin);
        Assert.False(result.Recovered);
        Assert.Equal("http://backend:5001", result.Config.BackendUrl);
        Assert.True(File.Exists(store.Path));
    }

    [Theory]
    [MemberData(nameof(DamagedContents))]
    public void ConfigStore_DamagedPrimary_RestoresLastKnownGoodBackup(string name, byte[] damaged, FileDefect expectedDefect)
    {
        string path = Path.Combine(_root, name + ".json");
        var store = new AgentConfigStore(path);
        File.WriteAllText(path, GoodConfigJson);
        Assert.Equal(ConfigOrigin.Primary, store.Load().Origin); // establishes agent.json.bak
        Assert.True(File.Exists(store.BackupPath));

        File.WriteAllBytes(path, damaged);
        var result = store.Load();

        Assert.Equal(ConfigOrigin.Backup, result.Origin);
        Assert.True(result.Recovered);
        Assert.Equal(expectedDefect, result.Defect);
        Assert.Equal("http://plant-a.example:5001", result.Config.BackendUrl);
        Assert.Equal("k-123", result.Config.ExtensionApiKey);
        Assert.Equal(15, result.Config.HeartbeatIntervalSeconds);

        // The primary is valid again, and the damaged file was kept for forensics.
        Assert.NotNull(AgentConfigStore.TryParse(File.ReadAllBytes(path), out _, out _));
        Assert.NotNull(result.QuarantinedCopy);
        Assert.True(File.Exists(result.QuarantinedCopy));
        Assert.Equal(ConfigOrigin.Primary, store.Load().Origin);
    }

    [Theory]
    [MemberData(nameof(DamagedContents))]
    public void ConfigStore_DamagedPrimaryWithoutBackup_FallsBackToSafeDefaults(string name, byte[] damaged, FileDefect expectedDefect)
    {
        string path = Path.Combine(_root, name + ".json");
        File.WriteAllBytes(path, damaged);

        var result = new AgentConfigStore(path).Load();

        Assert.Equal(ConfigOrigin.Defaults, result.Origin);
        Assert.True(result.Recovered);
        Assert.Equal(expectedDefect, result.Defect);
        Assert.False(result.Config.AllowUnsignedCommands);
        Assert.True(result.Config.RequireLoopbackForExtensions); // the extension API key is gone: fail closed
        Assert.NotNull(AgentConfigStore.TryParse(File.ReadAllBytes(path), out _, out _));
    }

    [Fact]
    public void ConfigStore_MissingPrimaryWithBackup_IsRecoveredFromBackup()
    {
        string path = Path.Combine(_root, "agent.json");
        var store = new AgentConfigStore(path);
        File.WriteAllText(path, GoodConfigJson);
        store.Load();
        File.Delete(path); // e.g. an interrupted ReplaceFile left only the backup

        var result = store.Load();

        Assert.Equal(ConfigOrigin.Backup, result.Origin);
        Assert.Equal(FileDefect.Missing, result.Defect);
        Assert.Equal("http://plant-a.example:5001", result.Config.BackendUrl);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ConfigStore_Save_RotatesBackup_AndPreservesUnknownProperties()
    {
        string path = Path.Combine(_root, "agent.json");
        var store = new AgentConfigStore(path);
        File.WriteAllText(path, GoodConfigJson);
        var config = store.Load().Config;

        config.HeartbeatIntervalSeconds = 45;
        Assert.True(store.TrySave(config, out var error), error);

        string saved = File.ReadAllText(path);
        Assert.Contains("\"HeartbeatIntervalSeconds\": 45", saved);
        Assert.Contains("AdsAmsNetId", saved); // not a property of this agent version, still kept
        Assert.Contains("\"HeartbeatIntervalSeconds\":15", File.ReadAllText(store.BackupPath));
    }

    [Fact]
    public void ConfigStore_Load_RepairsValuesOutsideSaneLimits_AndReportsThem()
    {
        string path = Path.Combine(_root, "agent.json");
        File.WriteAllText(path,
            "{\"BackendUrl\":\"ftp://nowhere\",\"HeartbeatIntervalSeconds\":0,\"MaxSpoolDiskMb\":-5,\"MaxMemoryMb\":99999999," +
            "\"PluginExecutionTimeoutSeconds\":-1,\"DeadbandTolerancePercentage\":250,\"Environment\":\"prod-ish\",\"RecipeWatchDirectories\":null}");

        var result = new AgentConfigStore(path).Load();

        Assert.Equal(ConfigOrigin.Primary, result.Origin);
        Assert.Equal(10, result.Config.HeartbeatIntervalSeconds);
        Assert.Equal(500, result.Config.MaxSpoolDiskMb);
        Assert.Equal(1_048_576, result.Config.MaxMemoryMb);
        Assert.Equal(30, result.Config.PluginExecutionTimeoutSeconds);
        Assert.Equal(100, result.Config.DeadbandTolerancePercentage);
        Assert.Equal("Production", result.Config.Environment); // an unreadable environment must not relax plugin policy
        Assert.Equal("http://localhost:5001", result.Config.BackendUrl);
        Assert.NotNull(result.Config.RecipeWatchDirectories);
        Assert.Contains(result.Issues, i => i.Field == nameof(AgentConfig.HeartbeatIntervalSeconds));
        Assert.Contains(result.Issues, i => i.Field == nameof(AgentConfig.BackendUrl));
        Assert.Contains(result.Issues, i => i.Field == nameof(AgentConfig.Environment));
    }

    [Fact]
    public void ConfigStore_ToleratesHandEditedFiles_CommentsTrailingCommasAndUtf16()
    {
        string path = Path.Combine(_root, "agent.json");
        File.WriteAllText(path, "{ // set by the commissioning engineer\n \"backendUrl\": \"https://edge.example\", }", Encoding.Unicode);

        var result = new AgentConfigStore(path).Load();

        Assert.Equal(ConfigOrigin.Primary, result.Origin);
        Assert.Equal("https://edge.example", result.Config.BackendUrl);
    }

    [Fact]
    public void ConfigurationService_ReportsRecovery_AndNeverThrowsOnCorruptFile()
    {
        var paths = AgentPaths.ForRoot(_root);
        File.WriteAllText(paths.ConfigFile, GoodConfigJson);
        new ConfigurationService(NullLogger<ConfigurationService>.Instance, paths: paths); // good start writes the backup
        File.WriteAllBytes(paths.ConfigFile, new byte[2048]); // BSOD: zero-filled file

        var journal = new RecoveryJournal(paths.RecoveryNoticesFile);
        var service = new ConfigurationService(NullLogger<ConfigurationService>.Instance, paths: paths, recoveryJournal: journal);

        Assert.Equal("http://plant-a.example:5001", service.Config.BackendUrl);
        Assert.Equal(ConfigOrigin.Backup, service.LastLoad!.Origin);
        var notice = Assert.Single(journal.Pending);
        Assert.Equal("config", notice.Component);
        Assert.Equal("zero_filled", notice.Defect);
        Assert.Equal("backup", notice.RecoveredFrom);

        // Unreported notices survive a restart of the agent.
        Assert.Single(new RecoveryJournal(paths.RecoveryNoticesFile).Pending);
        journal.Acknowledge(new[] { notice.Id });
        Assert.Empty(new RecoveryJournal(paths.RecoveryNoticesFile).Pending);
    }

    [Fact]
    public void ConfigurationService_UpdateConfigSigned_MergesPartialPayload_AndRejectsInvalidValues()
    {
        var paths = AgentPaths.ForRoot(_root);
        File.WriteAllText(paths.ConfigFile,
            "{\"BackendUrl\":\"http://plant-a.example:5001\",\"AllowUnsignedCommands\":true,\"ExtensionApiKey\":\"k-123\"}");
        var service = new ConfigurationService(NullLogger<ConfigurationService>.Instance, paths: paths);

        // A partial update changes what it names and nothing else (it used to reset the rest to defaults).
        Assert.True(service.UpdateConfigSigned("{\"heartbeatIntervalSeconds\":30}", null));
        Assert.Equal(30, service.Config.HeartbeatIntervalSeconds);
        Assert.Equal("http://plant-a.example:5001", service.Config.BackendUrl);
        Assert.Equal("k-123", service.Config.ExtensionApiKey);

        Assert.False(service.UpdateConfigSigned("{\"HeartbeatIntervalSeconds\":0}", null));
        Assert.False(service.UpdateConfigSigned("{\"BackendUrl\":\"not a url\"}", null));
        Assert.False(service.UpdateConfigSigned("{\"MaxSpoolDiskMb\":-1}", null));
        Assert.False(service.UpdateConfigSigned("[1,2]", null));
        Assert.False(service.UpdateConfigSigned("{\"HeartbeatIntervalSeconds\":", null));
        Assert.Equal(30, service.Config.HeartbeatIntervalSeconds);
        Assert.Equal("http://plant-a.example:5001", service.Config.BackendUrl);

        Assert.False(service.TryUpdate(c => c.BackendUrl = "   ", out var problems));
        Assert.Contains(problems, p => p.Field == nameof(AgentConfig.BackendUrl));
        Assert.Equal("http://plant-a.example:5001", new AgentConfigStore(paths.ConfigFile).Load().Config.BackendUrl);
    }

    // ---------------------------------------------------------------- state files

    private sealed class Marker
    {
        public string Session { get; set; } = string.Empty;
        public int Counter { get; set; }
    }

    [Fact]
    public void DurableJsonFile_FallsBackToBackup_WhenPrimaryIsTornOrTampered()
    {
        var file = new DurableJsonFile<Marker>(Path.Combine(_root, "session.json"));
        Assert.Equal(DurableOrigin.None, file.Load().Origin);
        Assert.False(file.Load().Recovered); // never written: not a recovery

        Assert.True(file.TrySave(new Marker { Session = "a", Counter = 1 }, out _));
        Assert.True(file.TrySave(new Marker { Session = "a", Counter = 2 }, out _));
        Assert.Equal(2, file.Load().Value!.Counter);

        foreach (var damaged in new[]
                 {
                     Array.Empty<byte>(),
                     new byte[300],
                     File.ReadAllBytes(file.Path)[..40],
                     Encoding.UTF8.GetBytes(File.ReadAllText(file.Path).Replace("\"Counter\":2", "\"Counter\":7"))
                 })
        {
            File.WriteAllBytes(file.Path, damaged);
            var loaded = file.Load();
            Assert.Equal(DurableOrigin.Backup, loaded.Origin);
            Assert.True(loaded.Recovered);
            Assert.Equal(1, loaded.Value!.Counter);
        }

        // Saving over a damaged primary keeps the good backup instead of rotating garbage into it.
        Assert.True(file.TrySave(new Marker { Session = "a", Counter = 3 }, out _));
        Assert.Equal(3, file.Load().Value!.Counter);
        File.WriteAllBytes(file.Path, new byte[10]);
        Assert.Equal(1, file.Load().Value!.Counter);

        File.Delete(file.BackupPath);
        var lost = file.Load();
        Assert.Equal(DurableOrigin.None, lost.Origin);
        Assert.True(lost.Recovered);
        Assert.Equal(FileDefect.ZeroFilled, lost.PrimaryDefect);
    }

    // ---------------------------------------------------------------- spool

    [Fact]
    public async Task Spool_SkipsTornAndZeroFilledRecords_AndDeliversTheRestInOrder()
    {
        string spoolDir = Path.Combine(_root, "spool");
        var config = new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig { MaxSpoolDiskMb = 10 });
        var journal = new RecoveryJournal();
        var spooler = new LocalTelemetrySpooler(NullLogger<LocalTelemetrySpooler>.Instance, config, spoolDir, journal);

        await spooler.SpoolPayloadAsync("{\"n\":1}");
        await spooler.SpoolPayloadAsync("{\"n\":2}");
        await spooler.SpoolPayloadAsync("{\"n\":3}");
        var files = Directory.GetFiles(spoolDir, "spool_*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.Equal(3, files.Length);

        // The power went out while the middle record was being written, and the newest one is a zero-filled tail.
        File.WriteAllBytes(files[1], File.ReadAllBytes(files[1])[..25]);
        File.WriteAllBytes(files[2], new byte[128]);
        // A record written by an older agent version (bare JSON) and one that is not even JSON.
        File.WriteAllText(Path.Combine(spoolDir, "spool_9999999999999_legacy.json"), "{\"n\":4}");
        File.WriteAllText(Path.Combine(spoolDir, "spool_9999999999999_zzz.json"), "{\"n\":5");
        File.WriteAllText(Path.Combine(spoolDir, "spool_1.json.tmp"), "{\"n\":"); // unfinished temp file of the old writer

        var received = new List<string>();
        int drained = await spooler.DrainSpoolAsync(p => { received.Add(p); return Task.FromResult(true); });

        Assert.Equal(2, drained);
        Assert.Equal(new[] { "{\"n\":1}", "{\"n\":4}" }, received);
        Assert.Equal(0, spooler.PendingCount);
        Assert.Equal(3, spooler.CorruptRecordsSkipped);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(spoolDir, DurableRecordQueue.CorruptDirectoryName)).Length);
        var notice = Assert.Single(journal.Pending);
        Assert.Equal("spool", notice.Component);
        Assert.Equal("skipped", notice.RecoveredFrom);

        // Temp files a crash left behind are removed when the spool is opened again.
        new LocalTelemetrySpooler(NullLogger<LocalTelemetrySpooler>.Instance, config, spoolDir);
        Assert.False(File.Exists(Path.Combine(spoolDir, "spool_1.json.tmp")));
    }

    [Fact]
    public async Task Spool_KeepsOrder_StopsOnTransientFailure_SetsPoisonAside_AndDrainsSingleFlight()
    {
        string spoolDir = Path.Combine(_root, "spool2");
        var config = new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig { MaxSpoolDiskMb = 10 });
        var spooler = new LocalTelemetrySpooler(NullLogger<LocalTelemetrySpooler>.Instance, config, spoolDir);
        for (int i = 1; i <= 4; i++) await spooler.SpoolPayloadAsync($"{{\"n\":{i}}}");

        // Backend unreachable: nothing is lost, order is kept.
        Assert.Equal(0, await spooler.DrainSpoolAsync(_ => Task.FromResult(false)));
        Assert.Equal(4, spooler.PendingCount);

        // A record the consumer cannot process blocks the spool only for a bounded number of attempts.
        var received = new List<string>();
        Func<string, Task<bool>> consumer = p =>
        {
            if (p.Contains("\"n\":2")) throw new InvalidOperationException("cannot process");
            received.Add(p);
            return Task.FromResult(true);
        };
        for (int attempt = 0; attempt < DurableRecordQueue.MaxConsumerFailures; attempt++) await spooler.DrainSpoolAsync(consumer);
        Assert.Equal(new[] { "{\"n\":1}", "{\"n\":3}", "{\"n\":4}" }, received);
        Assert.Equal(0, spooler.PendingCount);
        Assert.Equal(1, spooler.CorruptRecordsSkipped);

        // Only one drain at a time: an overlapping call must not send the same records again.
        await spooler.SpoolPayloadAsync("{\"n\":5}");
        var gate = new TaskCompletionSource<bool>();
        var first = spooler.DrainSpoolAsync(async _ => await gate.Task);
        Assert.Equal(0, await spooler.DrainSpoolAsync(_ => Task.FromResult(true)));
        gate.SetResult(true);
        Assert.Equal(1, await first);
    }

    // ---------------------------------------------------------------- identity

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("03000200-0400-0500-0006-000700080009")]
    [InlineData("Unknown UUID")]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("uninitialized")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void Identity_RejectsPlaceholderMachineIdentifiers(string candidate)
    {
        Assert.False(MachineIdentityResolver.IsPlausibleMachineId(candidate));
    }

    [Theory]
    [InlineData("02:91:48:86:5B:7B", true)]
    [InlineData("00:00:00:00:00:00", false)]
    [InlineData("FF:FF:FF:FF:FF:FF", false)]
    [InlineData("", false)]
    [InlineData("02:91:48:86:5B", false)]
    [InlineData("00:00:00:00:00:00:00:E0", false)]
    [InlineData("0291.4886.5B7B", false)]
    public void Identity_ValidatesMacAddresses(string mac, bool expected)
    {
        Assert.Equal(expected, MachineIdentityResolver.IsPlausibleMac(mac));
    }

    [Fact]
    public void Identity_SkipsPlaceholderUuid_PersistsResult_AndSurvivesUnreadableHardware()
    {
        string file = Path.Combine(_root, "state", "identity.json");
        var journal = new RecoveryJournal();
        var probes = new[]
        {
            new HardwareIdProbe("smbios_uuid", () => "00000000-0000-0000-0000-000000000000"),
            new HardwareIdProbe("machine_guid", () => "7c9e6679-7425-40de-944b-e07fc1f90ae7")
        };

        var first = new MachineIdentityResolver(file, probes, journal);
        Assert.Equal("7c9e6679-7425-40de-944b-e07fc1f90ae7", first.MachineIdentifier);
        Assert.Equal("machine_guid", first.Source);
        Assert.Contains(journal.Pending, n => n.Component == "identity" && n.Detail.Contains("smbios_uuid"));

        // After a reboot the hardware query fails (WMI not up yet): the agent must stay who it was
        // instead of inventing "{MachineName}-{OSVersion}".
        var failing = new[] { new HardwareIdProbe("smbios_uuid", () => throw new InvalidOperationException("WMI unavailable")) };
        Assert.Equal(first.MachineIdentifier, new MachineIdentityResolver(file, failing).MachineIdentifier);

        // A zero-filled identity file falls back to its backup / the hardware, and says so.
        var again = new MachineIdentityResolver(file, probes); // second save creates the .bak
        again.ResolveMacAddress(new[] { new NetworkAdapterInfo("02:91:48:86:5B:7B", true, 1000, false) });
        File.WriteAllBytes(file, new byte[200]);
        var recoveryJournal = new RecoveryJournal();
        Assert.Equal(first.MachineIdentifier, new MachineIdentityResolver(file, failing, recoveryJournal).MachineIdentifier);
        Assert.Contains(recoveryJournal.Pending, n => n.Component == "identity" && n.Defect == "zero_filled");
    }

    [Fact]
    public void Identity_GeneratesOnce_WhenNothingIsAvailable_AndReportsHardwareChanges()
    {
        string file = Path.Combine(_root, "identity.json");
        var none = Array.Empty<HardwareIdProbe>();

        string generated = new MachineIdentityResolver(file, none).MachineIdentifier;
        Assert.StartsWith("HMD-", generated);
        Assert.True(MachineIdentityResolver.IsPlausibleMachineId(generated));
        Assert.Equal(generated, new MachineIdentityResolver(file, none).MachineIdentifier);

        // Hardware identifier readable now (or the disk was cloned into another machine): the
        // hardware wins, and the change is reported instead of happening silently.
        var journal = new RecoveryJournal();
        var changed = new MachineIdentityResolver(file, new[] { new HardwareIdProbe("machine_id", () => "4c4c4544004210338052b4c04f4e4433") }, journal);
        Assert.Equal("4c4c4544004210338052b4c04f4e4433", changed.MachineIdentifier);
        var notice = Assert.Single(journal.Pending);
        Assert.Equal("changed", notice.Defect);
        Assert.Contains(generated, notice.Detail);
    }

    [Fact]
    public void Identity_MacAddress_IsStableAcrossAdapterChanges_AndNeverAPlaceholder()
    {
        var identity = new MachineIdentityResolver(Path.Combine(_root, "identity.json"),
            new[] { new HardwareIdProbe("machine_id", () => "4c4c4544004210338052b4c04f4e4433") });

        Assert.Equal(string.Empty, identity.ResolveMacAddress(new[] { new NetworkAdapterInfo("00:00:00:00:00:00", true, 10_000, false) }));

        var ethernet = new NetworkAdapterInfo("02:91:48:86:5B:7B", true, 1_000, false);
        var vpn = new NetworkAdapterInfo("0A:00:27:00:00:11", true, 10_000, false);
        Assert.Equal("02:91:48:86:5B:7B", identity.ResolveMacAddress(new[] { ethernet }));
        // A faster adapter appearing later does not change the reported identity...
        Assert.Equal("02:91:48:86:5B:7B", identity.ResolveMacAddress(new[] { vpn, ethernet with { IsUp = false } }));
        // ...and with no usable adapter at all the last good address is reused.
        Assert.Equal("02:91:48:86:5B:7B", identity.ResolveMacAddress(Array.Empty<NetworkAdapterInfo>()));
        // Only when that adapter is really gone does another one take over.
        Assert.Equal("0A:00:27:00:00:11", identity.ResolveMacAddress(new[] { vpn }));
    }

    // ---------------------------------------------------------------- master seed

    [Fact]
    public void SecureStorage_SeedIsValidated_RestoredFromBackup_AndRegenerationIsReported()
    {
        string seedPath = Path.Combine(_root, "master.seed");
        byte[] secret = Encoding.UTF8.GetBytes("{\"dbPassword\":\"SuperIndustrialSecret99!\"}");

        var created = new CrossPlatformSecureStorage(seedPath);
        Assert.Equal(CrossPlatformSecureStorage.SeedOrigin.Created, created.MasterSeedOrigin);
        Assert.True(File.Exists(seedPath + ".bak"));
        byte[] encrypted = created.Encrypt(secret);

        Assert.Equal(CrossPlatformSecureStorage.SeedOrigin.Loaded, new CrossPlatformSecureStorage(seedPath).MasterSeedOrigin);

        foreach (var damaged in new[] { Array.Empty<byte>(), new byte[32], new byte[] { 1, 2, 3 } })
        {
            File.WriteAllBytes(seedPath, damaged);
            var journal = new RecoveryJournal();
            var restored = new CrossPlatformSecureStorage(seedPath, journal);
            Assert.Equal(CrossPlatformSecureStorage.SeedOrigin.RestoredFromBackup, restored.MasterSeedOrigin);
            Assert.Equal(secret, restored.Decrypt(encrypted)); // same key: nothing was lost
            Assert.Equal("Warning", Assert.Single(journal.Pending).Severity);
        }

        File.WriteAllBytes(seedPath, new byte[32]);
        File.WriteAllBytes(seedPath + ".bak", Array.Empty<byte>());
        var lostJournal = new RecoveryJournal();
        var regenerated = new CrossPlatformSecureStorage(seedPath, lostJournal);
        Assert.Equal(CrossPlatformSecureStorage.SeedOrigin.RegeneratedAfterCorruption, regenerated.MasterSeedOrigin);
        Assert.Equal("Critical", Assert.Single(lostJournal.Pending).Severity);
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => regenerated.Decrypt(encrypted));
    }
}

/// <summary>Client-side sanity checks run before a report is published or spooled.</summary>
public class AgentTelemetrySanityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 1, 0, 0, TimeSpan.Zero);

    private static App.Agent.Daemon.SystemInfoData Valid() => new()
    {
        Hostname = "IPC-07",
        MachineIdentifier = "7c9e6679-7425-40de-944b-e07fc1f90ae7",
        MacAddress = "02:91:48:86:5B:7B",
        LastOnline = Now,
        LiveTelemetry = new App.Agent.Daemon.LiveTelemetryData { CpuUsagePercent = 12.5, RamUsagePercent = 40, Timestamp = Now },
        Disk = new App.Agent.Daemon.DiskData { TotalFreeGB = 120, OsDriveFreeGB = 80, Drives = new() { ["C:\\"] = 80, ["D:\\"] = 40 } }
    };

    [Fact]
    public void ValidReport_PassesUnchanged()
    {
        var filter = new App.Agent.Daemon.Reporting.TelemetrySanityFilter();
        var result = filter.Check(Valid(), Now);
        Assert.True(result.Accepted);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("", "7c9e6679-7425-40de-944b-e07fc1f90ae7", "02:91:48:86:5B:7B", "empty_hostname")]
    [InlineData("IPC-07", "", "02:91:48:86:5B:7B", "invalid_machine_identifier")]
    [InlineData("IPC-07", "00000000-0000-0000-0000-000000000000", "02:91:48:86:5B:7B", "invalid_machine_identifier")]
    [InlineData("IPC-07", "7c9e6679-7425-40de-944b-e07fc1f90ae7", "00:00:00:00:00:00", "invalid_mac_address")]
    [InlineData("IPC-07", "7c9e6679-7425-40de-944b-e07fc1f90ae7", "", "invalid_mac_address")]
    public void IdentityProblems_RejectTheReport_AndAreCounted(string host, string id, string mac, string rule)
    {
        var filter = new App.Agent.Daemon.Reporting.TelemetrySanityFilter();
        var data = Valid();
        data.Hostname = host;
        data.MachineIdentifier = id;
        data.MacAddress = mac;

        var result = filter.Check(data, Now);

        Assert.False(result.Accepted);
        Assert.Contains(rule, result.Issues);
        Assert.Equal(1, filter.ReportsRejected);
        Assert.Equal(1, filter.Counters[rule]);
    }

    [Fact]
    public void BadValues_AreClampedOrDropped_AndCounted()
    {
        var filter = new App.Agent.Daemon.Reporting.TelemetrySanityFilter();
        var data = Valid();
        data.LastOnline = DateTimeOffset.UnixEpoch;
        data.LiveTelemetry.CpuUsagePercent = 140;
        data.LiveTelemetry.RamUsagePercent = -3;
        data.LiveTelemetry.Timestamp = Now.AddDays(2);
        data.Disk.TotalFreeGB = double.NaN;
        data.Disk.OsDriveFreeGB = -1;
        data.Disk.Drives["E:\\"] = double.PositiveInfinity;
        data.Disk.Drives["F:\\"] = -5;
        data.Disk.Drives["G:\\"] = 5e9;
        data.Disk.PhysicalDrives.Add(new App.Agent.Daemon.PhysicalDriveInfo { Model = "ok", SizeBytes = 512_110_190_592 });
        data.Disk.PhysicalDrives.Add(new App.Agent.Daemon.PhysicalDriveInfo { Model = "bad", SizeBytes = -1 });

        var result = filter.Check(data, Now);

        Assert.True(result.Accepted);
        Assert.Equal(Now, data.LastOnline);
        Assert.Equal(Now, data.LiveTelemetry.Timestamp);
        Assert.Equal(100, data.LiveTelemetry.CpuUsagePercent);
        Assert.Equal("100.0%", data.LiveTelemetry.CpuLoad);
        Assert.Equal(0, data.LiveTelemetry.RamUsagePercent);
        Assert.Equal(0, data.Disk.TotalFreeGB);
        Assert.Equal(0, data.Disk.OsDriveFreeGB);
        Assert.Equal(new[] { "C:\\", "D:\\" }, data.Disk.Drives.Keys.OrderBy(k => k).ToArray());
        Assert.Single(data.Disk.PhysicalDrives);
        Assert.Equal(3, filter.Counters["invalid_drive_entry"]);
        Assert.Equal(2, filter.Counters["percentage_out_of_range"]);
        Assert.Equal(1, filter.Counters["timestamp_at_epoch"]);
        Assert.Equal(1, filter.Counters["timestamp_in_future"]);

        var nan = Valid();
        nan.LiveTelemetry.CpuUsagePercent = double.NaN;
        Assert.True(filter.Check(nan, Now).Accepted);
        Assert.Null(nan.LiveTelemetry); // the live slice is left out, not reported as NaN
    }

    [Fact]
    public void LiveReportTimestamps_StayMonotonic_WhenTheClockStepsBack()
    {
        var filter = new App.Agent.Daemon.Reporting.TelemetrySanityFilter();
        filter.Check(Valid(), Now);

        var earlier = Valid();
        earlier.LastOnline = Now.AddMinutes(-3);
        Assert.True(filter.Check(earlier, Now).Accepted);
        Assert.Equal(Now, earlier.LastOnline);
        Assert.Equal(1, filter.Counters["out_of_order_timestamp"]);

        var replayed = Valid(); // spooled reports are old by design and keep their time
        replayed.LastOnline = Now.AddHours(-1);
        filter.Check(replayed, Now, liveReport: false);
        Assert.Equal(Now.AddHours(-1), replayed.LastOnline);
    }

    [Fact]
    public void OversizedReports_DropTheLargestComponents_OrAreRejected()
    {
        var filter = new App.Agent.Daemon.Reporting.TelemetrySanityFilter();
        var request = new App.Shared.Protos.SystemInfoRequest { Hostname = "IPC-07", MachineIdentifier = "id", MacAddress = "02:91:48:86:5B:7B" };
        request.Components.Add(new App.Shared.Protos.InventoryComponent { Name = "Drivers", DataJson = new string('x', 50_000) });
        request.Components.Add(new App.Shared.Protos.InventoryComponent { Name = "Live Telemetry", DataJson = "{\"CpuLoad\":\"1%\"}" });

        Assert.True(filter.EnforceSize(request, 20_000, out var dropped));
        Assert.Equal(new[] { "Drivers" }, dropped);
        Assert.Single(request.Components);

        var huge = new App.Shared.Protos.SystemInfoRequest { Hostname = new string('h', 30_000) };
        Assert.False(filter.EnforceSize(huge, 20_000, out _));
        Assert.Equal(1, filter.Counters["oversized_report"]);
    }

    [Fact]
    public async Task Reporter_IsolatesAFailingContributor_AndDoesNotPublishRejectedReports()
    {
        var mqtt = new RecordingMqtt();
        var config = new App.Agent.Daemon.ConfigurationService(Microsoft.Extensions.Logging.Abstractions.NullLogger<App.Agent.Daemon.ConfigurationService>.Instance, new App.Agent.Daemon.AgentConfig());
        var reporter = new App.Agent.Daemon.SystemInfoReporter(Microsoft.Extensions.Logging.Abstractions.NullLogger<App.Agent.Daemon.SystemInfoReporter>.Instance,
            config, mqtt, new App.Agent.Daemon.Reporting.IComponentContributor[] { new ThrowingContributor() });

        var response = await reporter.ReportInfoAsync(Valid());
        Assert.True(response?.Success);
        var published = App.Shared.Protos.SystemInfoRequest.Parser.ParseFrom(Assert.Single(mqtt.Payloads));
        Assert.Contains(published.Components, c => c.Name == "Live Telemetry");
        Assert.Equal(1, reporter.ContributorFailures[nameof(ThrowingContributor)]);

        var invalid = Valid();
        invalid.MacAddress = "00:00:00:00:00:00";
        Assert.Null(await reporter.ReportInfoAsync(invalid));
        Assert.Single(mqtt.Payloads);
        Assert.Equal(1, reporter.SanityFilter.ReportsRejected);
    }

    private sealed class ThrowingContributor : App.Agent.Daemon.Reporting.IComponentContributor
    {
        public App.Shared.Protos.InventoryComponent? CreateComponent(App.Agent.Daemon.SystemInfoData data) => throw new InvalidOperationException("plugin bug");
    }

    private sealed class RecordingMqtt : App.Agent.Daemon.Interfaces.IMqttAgentClient
    {
        public readonly List<byte[]> Payloads = new();
        public bool IsConnected => true;
        public string BrokerHost => "test";
        public int BrokerPort => 1883;
        public Task<bool> EnsureConnectedAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> PublishProtobufAsync<T>(string topic, T message, int qos = 1, System.Threading.CancellationToken cancellationToken = default) where T : Google.Protobuf.IMessage
        {
            Payloads.Add(Google.Protobuf.MessageExtensions.ToByteArray(message));
            return Task.FromResult(true);
        }
        public Task<bool> PublishRawAsync(string topic, byte[] payload, int qos = 1, System.Threading.CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(true);
        }
        public Task SubscribeToCommandsAsync(string machineIdentifier, Func<App.Shared.Protos.ServerCommand, Task> onCommandReceived, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
