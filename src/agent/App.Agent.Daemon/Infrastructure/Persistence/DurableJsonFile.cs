namespace App.Agent.Daemon.Infrastructure.Persistence;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Where a successfully loaded document came from.</summary>
public enum DurableOrigin
{
    /// <summary>Nothing usable was found (neither the primary file nor its backup).</summary>
    None,
    /// <summary>The primary file was valid.</summary>
    Primary,
    /// <summary>The primary file was missing or damaged and the previous good copy was used.</summary>
    Backup
}

/// <summary>Outcome of <see cref="DurableJsonFile{T}.Load"/>. Loading never throws.</summary>
/// <param name="Value">The document, or null when <paramref name="Origin"/> is <see cref="DurableOrigin.None"/>.</param>
/// <param name="Origin">Primary, backup or nothing.</param>
/// <param name="PrimaryDefect">What was wrong with the primary file (<see cref="FileDefect.None"/> when it was used).</param>
/// <param name="Detail">Human-readable description of the primary file's defect.</param>
public sealed record DurableLoad<T>(T? Value, DurableOrigin Origin, FileDefect PrimaryDefect, string? Detail) where T : class
{
    /// <summary>True when a damaged (not merely absent) primary file was encountered.</summary>
    public bool PrimaryWasDamaged => PrimaryDefect is not (FileDefect.None or FileDefect.Missing);

    /// <summary>True when the caller got something other than a healthy primary file although one was expected.</summary>
    public bool Recovered => Origin == DurableOrigin.Backup || (Origin == DurableOrigin.None && PrimaryWasDamaged);
}

/// <summary>
/// A small JSON state file owned by the agent (session marker, crash record, identity, pending
/// notices) that must survive a hard power loss.
///
/// On disk the document is wrapped in an integrity envelope:
/// <code>{"format":"heimdall.agent.state","version":1,"sha256":"&lt;hex of the raw data text&gt;","data":{...}}</code>
/// so a torn, zero-filled or bit-flipped file is detected instead of being trusted. Writes go
/// through <see cref="AtomicFile.Write"/>; the previous good content is kept as <c>&lt;file&gt;.bak</c> and is
/// what <see cref="Load"/> falls back to.
/// </summary>
public sealed class DurableJsonFile<T> where T : class
{
    public const string FormatName = "heimdall.agent.state";

    private static readonly JsonSerializerOptions DefaultOptions = new() { WriteIndented = false };

    private readonly Func<T, string?>? _validate;
    private readonly JsonSerializerOptions _options;
    private readonly object _writeLock = new();

    /// <param name="path">Primary file path. The backup is <c>path + ".bak"</c>.</param>
    /// <param name="validate">Optional schema / sanity check; returns an error description, or null when the document is acceptable.</param>
    /// <param name="options">Serializer options for the document itself.</param>
    public DurableJsonFile(string path, Func<T, string?>? validate = null, JsonSerializerOptions? options = null)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        _validate = validate;
        _options = options ?? DefaultOptions;
    }

    public string Path { get; }

    public string BackupPath => AtomicFile.BackupPathFor(Path);

    /// <summary>
    /// Loads the primary file, falling back to the backup when the primary is missing, empty,
    /// zero-filled, truncated, fails its checksum or fails validation.
    /// </summary>
    public DurableLoad<T> Load()
    {
        var primary = TryLoadFile(Path, out var primaryDefect, out var primaryDetail);
        if (primary != null)
        {
            return new DurableLoad<T>(primary, DurableOrigin.Primary, FileDefect.None, null);
        }

        var backup = TryLoadFile(BackupPath, out _, out _);
        if (backup != null)
        {
            return new DurableLoad<T>(backup, DurableOrigin.Backup, primaryDefect, primaryDetail);
        }

        return new DurableLoad<T>(null, DurableOrigin.None, primaryDefect, primaryDetail);
    }

    /// <summary>
    /// Durably replaces the file. A healthy current file becomes the backup; a damaged one is not
    /// rotated into the backup slot (the older good backup is worth more). Never throws.
    /// </summary>
    public bool TrySave(T value, out string? error)
    {
        error = null;
        try
        {
            byte[] bytes = Serialize(value);
            lock (_writeLock)
            {
                bool currentIsHealthy = TryLoadFile(Path, out _, out _) != null;
                AtomicFile.Write(Path, bytes, keepPreviousAsBackup: currentIsHealthy);
            }
            return true;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Removes the file and its backup (used when a record has been consumed). Never throws.</summary>
    public void Delete()
    {
        lock (_writeLock)
        {
            AtomicFile.TryDelete(Path);
            AtomicFile.TryDelete(BackupPath);
        }
    }

    /// <summary>Serializes a document into its on-disk envelope.</summary>
    public byte[] Serialize(T value)
    {
        string data = JsonSerializer.Serialize(value, _options);
        string envelope = "{\"format\":\"" + FormatName + "\",\"version\":1,\"sha256\":\"" + Sha256Hex(data) + "\",\"data\":" + data + "}";
        return Encoding.UTF8.GetBytes(envelope);
    }

    private T? TryLoadFile(string path, out FileDefect defect, out string? detail)
    {
        defect = AtomicFile.TryRead(path, out var bytes, out detail);
        if (defect != FileDefect.None)
        {
            return null;
        }

        return TryParse(bytes, out defect, out detail);
    }

    /// <summary>Parses and verifies an envelope. Returns null and the defect when it cannot be trusted.</summary>
    public T? TryParse(ReadOnlySpan<byte> bytes, out FileDefect defect, out string? detail)
    {
        defect = FileDefect.None;
        detail = null;
        byte[] buffer = bytes.ToArray();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(buffer);
        }
        catch (JsonException ex)
        {
            defect = LooksTruncated(buffer) ? FileDefect.Truncated : FileDefect.Malformed;
            detail = ex.Message;
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("sha256", out var checksum) || checksum.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("data", out var data))
            {
                defect = FileDefect.Malformed;
                detail = "not a state envelope (expected an object with 'sha256' and 'data')";
                return null;
            }

            string rawData = data.GetRawText();
            if (!string.Equals(checksum.GetString(), Sha256Hex(rawData), StringComparison.OrdinalIgnoreCase))
            {
                defect = FileDefect.Malformed;
                detail = "checksum mismatch";
                return null;
            }

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(rawData, _options);
            }
            catch (JsonException ex)
            {
                defect = FileDefect.Invalid;
                detail = ex.Message;
                return null;
            }

            if (value == null)
            {
                defect = FileDefect.Invalid;
                detail = "document is null";
                return null;
            }

            string? validationError = null;
            try
            {
                validationError = _validate?.Invoke(value);
            }
            catch (Exception ex)
            {
                validationError = $"validator failed: {ex.Message}";
            }

            if (validationError != null)
            {
                defect = FileDefect.Invalid;
                detail = validationError;
                return null;
            }

            return value;
        }
    }

    /// <summary>A JSON text that does not end with a closing bracket was cut off mid-write.</summary>
    internal static bool LooksTruncated(ReadOnlySpan<byte> bytes)
    {
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            byte b = bytes[i];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') continue;
            return b is not ((byte)'}' or (byte)']');
        }
        return true;
    }

    private static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
