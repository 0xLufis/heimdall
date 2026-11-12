namespace App.Agent.Daemon.Configuration;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using App.Agent.Daemon.Infrastructure.Persistence;

/// <summary>Where the active configuration came from.</summary>
public enum ConfigOrigin
{
    /// <summary><c>agent.json</c> was valid.</summary>
    Primary,
    /// <summary><c>agent.json</c> was missing or damaged; the last known good copy (<c>agent.json.bak</c>) was restored.</summary>
    Backup,
    /// <summary><c>agent.json</c> was damaged and no usable backup existed; safe defaults are active.</summary>
    Defaults,
    /// <summary>No configuration has ever been written on this machine (not a recovery).</summary>
    FirstRun
}

/// <summary>Outcome of loading the agent configuration. Loading never throws.</summary>
public sealed record ConfigLoadResult(
    AgentConfig Config,
    ConfigOrigin Origin,
    FileDefect Defect,
    string? Detail,
    IReadOnlyList<ConfigIssue> Issues,
    string? QuarantinedCopy)
{
    /// <summary>True when the configuration on disk was unusable and something else had to be used.</summary>
    public bool Recovered => Origin is ConfigOrigin.Backup or ConfigOrigin.Defaults;
}

/// <summary>
/// Reads and writes <c>agent.json</c> so that a hard power loss or BSOD cannot leave the agent without
/// a configuration.
/// <para>
/// <b>Write</b>: temp file in the same directory, flushed to disk, atomically swapped in; the previous
/// good file is kept as <c>agent.json.bak</c> (<see cref="AtomicFile"/>).
/// </para>
/// <para>
/// <b>Load</b>: the file must decode, parse as a JSON object and deserialize. If it is missing, empty,
/// zero-filled, truncated or malformed, the damaged file is moved aside
/// (<c>agent.json.corrupt-&lt;timestamp&gt;</c>), the last known good backup is restored, and only when
/// there is no usable backup do safe defaults apply. Out-of-range values are repaired by
/// <see cref="AgentConfigValidator.Repair"/>. Every one of these outcomes is returned to the caller
/// so it can be reported.
/// </para>
/// The file stays plain, hand-editable JSON: comments and trailing commas are tolerated and
/// properties the agent does not know are preserved on save.
/// </summary>
public sealed class AgentConfigStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // One lock per file, shared by every store instance in the process.
    private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.Ordinal);

    private readonly object _fileLock;

    public AgentConfigStore(string path)
    {
        Path = System.IO.Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        _fileLock = FileLocks.GetOrAdd(Path, _ => new object());
    }

    public string Path { get; }

    public string BackupPath => AtomicFile.BackupPathFor(Path);

    /// <summary>
    /// Loads the configuration: primary file, else last known good backup, else safe defaults.
    /// </summary>
    /// <param name="environmentBackendUrl">Backend URL from the environment, applied when the file does not carry one.</param>
    public ConfigLoadResult Load(string? environmentBackendUrl = null)
    {
        lock (_fileLock)
        {
            var primary = TryReadConfig(Path, out var defect, out var detail, out var primaryBytes);
            if (primary != null)
            {
                var issues = AgentConfigValidator.Repair(primary);
                ApplyEnvironmentBackendUrl(primary, environmentBackendUrl);
                RefreshBackup(primaryBytes);
                return new ConfigLoadResult(primary, ConfigOrigin.Primary, FileDefect.None, null, issues, null);
            }

            var backup = TryReadConfig(BackupPath, out _, out _, out var backupBytes);

            if (defect == FileDefect.Missing && backup == null)
            {
                // Nothing was ever written here: first start on this machine.
                var fresh = new AgentConfig();
                ApplyEnvironmentBackendUrl(fresh, environmentBackendUrl);
                TryWrite(fresh, keepPreviousAsBackup: false, out _);
                return new ConfigLoadResult(fresh, ConfigOrigin.FirstRun, FileDefect.Missing, null, Array.Empty<ConfigIssue>(), null);
            }

            // The file is gone or unusable although the agent has run before.
            // A file that merely could not be opened (locked by an editor or a virus scanner, access
            // denied) is not proven damaged: it is neither moved aside nor overwritten.
            bool mayRewrite = defect != FileDefect.Unreadable;
            string? quarantined = mayRewrite && defect != FileDefect.Missing ? AtomicFile.Quarantine(Path) : null;

            if (backup != null)
            {
                var issues = AgentConfigValidator.Repair(backup);
                ApplyEnvironmentBackendUrl(backup, environmentBackendUrl);
                if (mayRewrite)
                {
                    try
                    {
                        // Restore the primary from the good copy; the backup itself stays untouched.
                        AtomicFile.Write(Path, backupBytes, keepPreviousAsBackup: false);
                    }
                    catch
                    {
                        // Read-only location: the restored configuration is still used in memory.
                    }
                }
                return new ConfigLoadResult(backup, ConfigOrigin.Backup, defect, detail, issues, quarantined);
            }

            var safe = CreateRecoveryDefaults();
            ApplyEnvironmentBackendUrl(safe, environmentBackendUrl);
            if (mayRewrite)
            {
                TryWrite(safe, keepPreviousAsBackup: false, out _);
            }
            return new ConfigLoadResult(safe, ConfigOrigin.Defaults, defect, detail, Array.Empty<ConfigIssue>(), quarantined);
        }
    }

    /// <summary>
    /// Durably persists a configuration. A healthy current file becomes the backup. Never throws.
    /// </summary>
    public bool TrySave(AgentConfig config, out string? error)
    {
        lock (_fileLock)
        {
            bool currentIsHealthy = TryReadConfig(Path, out _, out _, out _) != null;
            return TryWrite(config, keepPreviousAsBackup: currentIsHealthy, out error);
        }
    }

    /// <summary>
    /// The configuration used when both <c>agent.json</c> and its backup are unusable. Trust anchors
    /// and credentials cannot be recovered, so the defaults fail closed: no server key means every
    /// signed command is rejected, and the local extension API is restricted to loopback because its
    /// API key is gone too.
    /// </summary>
    public static AgentConfig CreateRecoveryDefaults() => new()
    {
        AllowUnsignedCommands = false,
        AllowUnsignedPlugins = false,
        RequireLoopbackForExtensions = true
    };

    /// <summary>Parses configuration text. Returns null and the defect when it is not a usable configuration.</summary>
    public static AgentConfig? TryParse(ReadOnlySpan<byte> bytes, out FileDefect defect, out string? detail)
    {
        defect = AtomicFile.InspectBytes(bytes, out detail);
        if (defect is FileDefect.Empty or FileDefect.ZeroFilled)
        {
            return null;
        }

        // Hand-edited files may be UTF-16 (PowerShell 5 redirection), so decode by byte-order mark
        // before judging the content; NUL bytes are only a torn write in the decoded text.
        string text;
        try
        {
            using var reader = new StreamReader(new MemoryStream(bytes.ToArray()), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            defect = FileDefect.Malformed;
            detail = ex.Message;
            return null;
        }

        int nul = text.IndexOf('\0');
        if (nul >= 0)
        {
            defect = FileDefect.Truncated;
            detail = $"NUL character at position {nul} (torn write)";
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            defect = FileDefect.Empty;
            detail = "file contains only whitespace";
            return null;
        }

        try
        {
            var config = JsonSerializer.Deserialize<AgentConfig>(text, ReadOptions);
            if (config == null)
            {
                defect = FileDefect.Malformed;
                detail = "document is the JSON literal null";
                return null;
            }

            defect = FileDefect.None;
            detail = null;
            return config;
        }
        catch (JsonException ex)
        {
            defect = LooksTruncated(text) ? FileDefect.Truncated : FileDefect.Malformed;
            detail = ex.Message;
            return null;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            defect = FileDefect.Malformed;
            detail = ex.Message;
            return null;
        }
    }

    public static byte[] Serialize(AgentConfig config) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config, WriteOptions));

    private static AgentConfig? TryReadConfig(string path, out FileDefect defect, out string? detail, out byte[] bytes)
    {
        defect = AtomicFile.TryRead(path, out bytes, out detail);
        if (defect is FileDefect.Missing or FileDefect.Unreadable)
        {
            return null;
        }

        return TryParse(bytes, out defect, out detail);
    }

    private bool TryWrite(AgentConfig config, bool keepPreviousAsBackup, out string? error)
    {
        error = null;
        try
        {
            AtomicFile.Write(Path, Serialize(config), keepPreviousAsBackup);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Keeps <c>agent.json.bak</c> equal to the last configuration that loaded successfully, so a file
    /// edited by hand (which never went through <see cref="TrySave"/>) is also protected.
    /// </summary>
    private void RefreshBackup(byte[] primaryBytes)
    {
        try
        {
            if (File.Exists(BackupPath))
            {
                var existing = File.ReadAllBytes(BackupPath);
                if (existing.AsSpan().SequenceEqual(primaryBytes)) return;
            }
            AtomicFile.Write(BackupPath, primaryBytes, keepPreviousAsBackup: false);
        }
        catch
        {
            // Read-only configuration directory: run without a backup rather than fail the start.
        }
    }

    private static void ApplyEnvironmentBackendUrl(AgentConfig config, string? environmentBackendUrl)
    {
        if (string.IsNullOrWhiteSpace(environmentBackendUrl)) return;
        if (!AgentConfigValidator.TryValidateBackendUrl(environmentBackendUrl, out _)) return;

        // The environment only fills in for a file that still carries the built-in default.
        if (string.Equals(config.BackendUrl, new AgentConfig().BackendUrl, StringComparison.Ordinal))
        {
            config.BackendUrl = environmentBackendUrl.Trim();
        }
    }

    private static bool LooksTruncated(string text)
    {
        string trimmed = text.TrimEnd();
        return trimmed.Length == 0 || trimmed[^1] is not ('}' or ']');
    }
}
