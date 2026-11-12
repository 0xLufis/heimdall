namespace App.Agent.Daemon.Infrastructure.Persistence;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// The single definition of where the agent keeps its files.
/// <list type="bullet">
/// <item><b>Configuration</b> (<c>agent.json</c>, <c>master.seed</c>): <c>%ProgramData%\Heimdall</c> on Windows,
/// <c>/etc/heimdall</c> on Linux, the install directory when neither can be created (unchanged from earlier versions).</item>
/// <item><b>State</b> (identity, session marker, crash record, pending notices, event outbox):
/// <c>HEIMDALL_STATE_DIR</c> when set, otherwise <c>%ProgramData%\Heimdall\state</c> / <c>/var/lib/heimdall</c>,
/// falling back to <c>&lt;config dir&gt;/state</c> and finally <c>&lt;install dir&gt;/state</c> - the first one that is writable.</item>
/// <item><b>Spool</b>: <c>&lt;install dir&gt;/telemetry_spool</c> (unchanged).</item>
/// </list>
/// State must live on a volume that survives a reboot. On machines with a write filter (UWF/EWF) the
/// state directory has to be excluded from the filter (or pointed at a persistent volume with
/// <c>HEIMDALL_STATE_DIR</c>), otherwise identity and session history are discarded with the overlay.
/// </summary>
public sealed class AgentPaths
{
    public const string StateDirEnvVar = "HEIMDALL_STATE_DIR";
    public const string ConfigFileName = "agent.json";

    private AgentPaths(string configDirectory, string stateDirectory, string spoolDirectory, string? configDirectoryWarning)
    {
        ConfigDirectory = configDirectory;
        StateDirectory = stateDirectory;
        SpoolDirectory = spoolDirectory;
        ConfigDirectoryWarning = configDirectoryWarning;
    }

    public string ConfigDirectory { get; }
    public string StateDirectory { get; }
    public string SpoolDirectory { get; }

    /// <summary>Set when the preferred configuration directory could not be created and the install directory is used instead.</summary>
    public string? ConfigDirectoryWarning { get; }

    public string ConfigFile => Path.Combine(ConfigDirectory, ConfigFileName);
    public string MasterSeedFile => Path.Combine(ConfigDirectory, "master.seed");
    public string IdentityFile => Path.Combine(StateDirectory, "identity.json");
    public string SessionMarkerFile => Path.Combine(StateDirectory, "session.json");
    public string CrashRecordFile => Path.Combine(StateDirectory, "crash.json");
    public string RecoveryNoticesFile => Path.Combine(StateDirectory, "recovery-notices.json");
    public string EventOutboxDirectory => Path.Combine(StateDirectory, "outbox");
    public string CrashDumpDirectory => Path.Combine(SpoolDirectory, "crash_dumps");

    /// <summary>Resolves the production layout for this machine. Never throws.</summary>
    public static AgentPaths Resolve()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string configDirectory = ResolveConfigDirectory(out var warning);

        var candidates = new System.Collections.Generic.List<string>();
        string? fromEnvironment = Environment.GetEnvironmentVariable(StateDirEnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) candidates.Add(fromEnvironment.Trim());
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) candidates.Add("/var/lib/heimdall");
        candidates.Add(Path.Combine(configDirectory, "state"));
        candidates.Add(Path.Combine(baseDirectory, "state"));

        string stateDirectory = candidates[^1];
        foreach (var candidate in candidates)
        {
            if (IsWritableDirectory(candidate))
            {
                stateDirectory = candidate;
                break;
            }
        }

        return new AgentPaths(configDirectory, stateDirectory, Path.Combine(baseDirectory, "telemetry_spool"), warning);
    }

    /// <summary>
    /// The directory holding <c>agent.json</c>: <c>%ProgramData%\Heimdall</c> / <c>/etc/heimdall</c>, or the
    /// install directory when that cannot be created. Has no side effect other than creating the directory.
    /// </summary>
    public static string ResolveConfigDirectory(out string? warning)
    {
        warning = null;
        string configDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Heimdall")
            : Path.Combine("/etc", "heimdall");

        if (!Directory.Exists(configDirectory))
        {
            try
            {
                Directory.CreateDirectory(configDirectory);
            }
            catch (Exception ex)
            {
                warning = $"Could not create config directory '{configDirectory}' ({ex.GetType().Name}: {ex.Message}); using '{AppContext.BaseDirectory}'.";
                configDirectory = AppContext.BaseDirectory;
            }
        }

        return configDirectory;
    }

    /// <summary>A self-contained layout under one root directory (tests, portable installs).</summary>
    public static AgentPaths ForRoot(string root)
    {
        return new AgentPaths(root, Path.Combine(root, "state"), Path.Combine(root, "telemetry_spool"), null);
    }

    /// <summary>True when the directory exists (or can be created) and a file can be created in it.</summary>
    public static bool IsWritableDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
