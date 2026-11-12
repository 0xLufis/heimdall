namespace App.Agent.Daemon.Infrastructure.Persistence;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>Result of reading one queued record.</summary>
/// <param name="Payload">The record content, or null when the record cannot be trusted.</param>
/// <param name="Defect">Why the record cannot be trusted (<see cref="FileDefect.None"/> when it can).</param>
/// <param name="Detail">Human-readable description of the defect.</param>
public readonly record struct QueuedRecord(string? Payload, FileDefect Defect, string? Detail)
{
    public bool IsValid => Payload != null && Defect == FileDefect.None;
}

/// <summary>
/// A directory-backed FIFO of text records (one file per record) that survives a hard power loss.
/// Shared by the telemetry spool and the lifecycle event outbox.
/// <list type="bullet">
/// <item>Each record is written with <see cref="AtomicFile.Write"/> (temp file, flush to disk, atomic rename),
/// so a crash can never leave a half-written record under its final name.</item>
/// <item>Each record carries a checksum envelope
/// (<c>{"format":"heimdall.spool","version":1,"enqueuedAtUtc":"...","sha256":"...","payload":"..."}</c>).
/// A record that is empty, zero-filled, truncated or fails its checksum - which is what a file system
/// can still hand back after a BSOD - is moved to <c>corrupt/</c> and skipped; it never blocks the
/// records behind it and never stops the agent.</item>
/// <item>Records written by earlier agent versions (the bare payload) are still accepted when they are
/// complete JSON documents.</item>
/// <item>File names sort in enqueue order even when the wall clock steps backwards.</item>
/// </list>
/// </summary>
public sealed class DurableRecordQueue
{
    public const string FormatName = "heimdall.spool";
    public const string CorruptDirectoryName = "corrupt";
    /// <summary>How many damaged records are kept for forensics before the oldest are deleted.</summary>
    public const int MaxCorruptFilesKept = 20;
    /// <summary>A record whose consumer threw this many times is treated as poison and set aside.</summary>
    public const int MaxConsumerFailures = 3;

    private readonly string _prefix;
    private readonly string _component;
    private readonly ILogger? _logger;
    private readonly IRecoveryJournal? _journal;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, int> _consumerFailures = new(StringComparer.Ordinal);
    private long _lastStamp;
    private long _sequence;
    private long _corruptRecords;
    private long _evictedRecords;

    /// <param name="directory">Directory holding the records.</param>
    /// <param name="filePrefix">File name prefix; records are named <c>&lt;prefix&gt;&lt;unix ms&gt;_&lt;sequence&gt;_&lt;guid&gt;.json</c>.</param>
    /// <param name="component">Name used in recovery notices ("spool", "outbox").</param>
    public DurableRecordQueue(string directory, string filePrefix = "spool_", string component = "spool",
        ILogger? logger = null, IRecoveryJournal? journal = null)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _prefix = filePrefix;
        _component = component;
        _logger = logger;
        _journal = journal;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            // Temp files are only ever data once renamed; whatever a crash left behind is garbage.
            int removed = AtomicFile.CleanupTempFiles(Directory);
            if (removed > 0)
            {
                _logger?.LogInformation("Removed {Count} unfinished temp file(s) from {Directory}", removed, Directory);
            }

            // New records must sort after everything already queued, even if the clock went backwards.
            foreach (var file in ListFiles())
            {
                _lastStamp = Math.Max(_lastStamp, ParseStamp(file));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize record queue directory at {Path}", Directory);
        }
    }

    public string Directory { get; }

    public string CorruptDirectory => Path.Combine(Directory, CorruptDirectoryName);

    /// <summary>Records set aside as damaged or poison since this process started.</summary>
    public long CorruptRecords => Interlocked.Read(ref _corruptRecords);

    /// <summary>Records dropped to stay within the disk quota since this process started.</summary>
    public long EvictedRecords => Interlocked.Read(ref _evictedRecords);

    public int Count
    {
        get
        {
            try
            {
                return System.IO.Directory.Exists(Directory) ? System.IO.Directory.GetFiles(Directory, _prefix + "*.json").Length : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public long TotalBytes
    {
        get
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory)) return 0L;
                return new DirectoryInfo(Directory).GetFiles(_prefix + "*.json").Sum(f => f.Length);
            }
            catch
            {
                return 0L;
            }
        }
    }

    /// <summary>Queued record files, oldest first.</summary>
    public IReadOnlyList<string> ListFiles()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return Array.Empty<string>();
            var files = System.IO.Directory.GetFiles(Directory, _prefix + "*.json");
            Array.Sort(files, CompareRecordFiles);
            return files;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Durably appends a record. When <paramref name="maxTotalBytes"/> is given, the oldest records are
    /// evicted first so the queue stays within that size. Never throws.
    /// </summary>
    /// <returns>The file name of the record, or null when it could not be written.</returns>
    public string? TryEnqueue(string payload, long? maxTotalBytes, out string? error)
    {
        error = null;
        if (payload == null)
        {
            error = "payload is null";
            return null;
        }

        try
        {
            byte[] bytes = Wrap(payload, DateTimeOffset.UtcNow);

            lock (_lock)
            {
                if (maxTotalBytes.HasValue)
                {
                    if (bytes.Length > maxTotalBytes.Value)
                    {
                        error = $"record of {bytes.Length} bytes exceeds the quota of {maxTotalBytes.Value} bytes";
                        return null;
                    }
                    EnforceQuota(maxTotalBytes.Value - bytes.Length);
                }

                long stamp = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _lastStamp);
                _lastStamp = stamp;
                long sequence = ++_sequence;

                string fileName = string.Create(CultureInfo.InvariantCulture, $"{_prefix}{stamp:D13}_{sequence:D6}_{Guid.NewGuid():N}.json");
                AtomicFile.Write(Path.Combine(Directory, fileName), bytes, keepPreviousAsBackup: false);
                return fileName;
            }
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    /// <summary>Reads and verifies one record. Never throws.</summary>
    public QueuedRecord Read(string file)
    {
        var defect = AtomicFile.TryRead(file, out var bytes, out var detail);
        if (defect != FileDefect.None)
        {
            return new QueuedRecord(null, defect, detail);
        }

        return Unwrap(bytes);
    }

    /// <summary>Removes a delivered record. Never throws.</summary>
    public void Remove(string file)
    {
        AtomicFile.TryDelete(file);
        _consumerFailures.TryRemove(file, out _);
    }

    /// <summary>
    /// Moves a damaged or poison record to <c>corrupt/</c> so the queue can continue behind it.
    /// </summary>
    public void SetAside(string file, FileDefect defect, string? detail)
    {
        Interlocked.Increment(ref _corruptRecords);
        _consumerFailures.TryRemove(file, out _);

        try
        {
            if (!File.Exists(file)) return;

            System.IO.Directory.CreateDirectory(CorruptDirectory);
            File.Move(file, Path.Combine(CorruptDirectory, Path.GetFileName(file)), overwrite: true);

            var kept = System.IO.Directory.GetFiles(CorruptDirectory);
            Array.Sort(kept, StringComparer.Ordinal);
            for (int i = 0; i < kept.Length - MaxCorruptFilesKept; i++)
            {
                AtomicFile.TryDelete(kept[i]);
            }
        }
        catch (Exception ex)
        {
            // Cannot move it: delete it, a record that cannot be read must not block the queue.
            _logger?.LogWarning(ex, "Could not set aside damaged record {File}; deleting it", Path.GetFileName(file));
            AtomicFile.TryDelete(file);
        }

        _logger?.LogWarning("Skipped damaged {Component} record {File}: {Defect} ({Detail})",
            _component, Path.GetFileName(file), RecoveryNotice.DefectName(defect), detail ?? "no detail");
    }

    /// <summary>
    /// Counts a consumer exception for a record. Returns true when the record has now failed
    /// <see cref="MaxConsumerFailures"/> times and should be set aside as poison.
    /// </summary>
    public bool RegisterConsumerFailure(string file)
    {
        return _consumerFailures.AddOrUpdate(file, 1, (_, failures) => failures + 1) >= MaxConsumerFailures;
    }

    /// <summary>Deletes the oldest records until the queue is no larger than <paramref name="maxBytes"/>.</summary>
    public int EnforceQuota(long maxBytes)
    {
        int evicted = 0;
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return 0;

            var files = ListFiles().Select(f => new FileInfo(f)).ToList();
            long total = files.Sum(f => f.Length);
            int index = 0;
            while (total > Math.Max(0, maxBytes) && index < files.Count)
            {
                var oldest = files[index++];
                _logger?.LogWarning("{Component} quota exceeded ({Used} / {Max} bytes). Evicting oldest record {File}",
                    _component, total, maxBytes, oldest.Name);
                total -= oldest.Length;
                oldest.Delete();
                evicted++;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to enforce {Component} disk quota", _component);
        }

        if (evicted > 0) Interlocked.Add(ref _evictedRecords, evicted);
        return evicted;
    }

    /// <summary>Reports the records set aside during one pass as a single recovery notice.</summary>
    public void ReportSetAside(int count, string firstFile, FileDefect firstDefect, string? firstDetail)
    {
        if (count <= 0) return;
        _journal?.Record(new RecoveryNotice
        {
            Component = _component,
            File = Path.Combine(Directory, firstFile),
            Defect = RecoveryNotice.DefectName(firstDefect),
            RecoveredFrom = "skipped",
            Severity = "Warning",
            Detail = count == 1
                ? $"1 damaged record skipped and moved to {CorruptDirectoryName}/ ({firstDetail ?? "no detail"})"
                : $"{count} damaged records skipped and moved to {CorruptDirectoryName}/ (first: {firstDetail ?? "no detail"})"
        });
    }

    /// <summary>Builds the on-disk envelope of a record.</summary>
    public static byte[] Wrap(string payload, DateTimeOffset enqueuedAtUtc)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("format", FormatName);
            writer.WriteNumber("version", 1);
            writer.WriteString("enqueuedAtUtc", enqueuedAtUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            writer.WriteString("sha256", Sha256Hex(payload));
            writer.WriteString("payload", payload);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    /// <summary>Verifies an envelope (or a complete legacy JSON payload) and returns the record content.</summary>
    public static QueuedRecord Unwrap(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String &&
                format.GetString() == FormatName)
            {
                if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.String ||
                    !root.TryGetProperty("sha256", out var checksum) || checksum.ValueKind != JsonValueKind.String)
                {
                    return new QueuedRecord(null, FileDefect.Malformed, "record envelope without payload or checksum");
                }

                string content = payload.GetString() ?? string.Empty;
                if (!string.Equals(checksum.GetString(), Sha256Hex(content), StringComparison.OrdinalIgnoreCase))
                {
                    return new QueuedRecord(null, FileDefect.Malformed, "checksum mismatch");
                }

                return new QueuedRecord(content, FileDefect.None, null);
            }

            // Written by an agent version without envelopes: the whole file is the payload. It parsed
            // as a complete JSON document, which is all that can be verified about it.
            return new QueuedRecord(Encoding.UTF8.GetString(StripBom(bytes)), FileDefect.None, null);
        }
        catch (JsonException ex)
        {
            return new QueuedRecord(null,
                DurableJsonFile<object>.LooksTruncated(bytes) ? FileDefect.Truncated : FileDefect.Malformed,
                ex.Message);
        }
    }

    private static ReadOnlySpan<byte> StripBom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? bytes.AsSpan(3) : bytes;

    private static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private long ParseStamp(string file)
    {
        string name = Path.GetFileName(file);
        if (!name.StartsWith(_prefix, StringComparison.Ordinal)) return 0;
        int end = name.IndexOf('_', _prefix.Length);
        if (end < 0) return 0;
        return long.TryParse(name.AsSpan(_prefix.Length, end - _prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var stamp) ? stamp : 0;
    }

    /// <summary>Orders by the numeric timestamp in the name, then by the rest of the name (sequence).</summary>
    private int CompareRecordFiles(string left, string right)
    {
        int byStamp = ParseStamp(left).CompareTo(ParseStamp(right));
        return byStamp != 0 ? byStamp : string.CompareOrdinal(Path.GetFileName(left), Path.GetFileName(right));
    }
}
