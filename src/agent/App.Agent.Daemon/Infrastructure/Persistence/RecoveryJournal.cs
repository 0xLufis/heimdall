namespace App.Agent.Daemon.Infrastructure.Persistence;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One "something was damaged and the agent recovered" fact: a config file restored from its backup,
/// an identity re-derived, a torn spool record skipped. Reported to the server with the next
/// lifecycle report so a recovery is never silent.
/// </summary>
public sealed class RecoveryNotice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>"config", "identity", "spool", "outbox", "session", "crash_record", "secure_storage".</summary>
    public string Component { get; set; } = string.Empty;
    /// <summary>The affected file (full path on the agent).</summary>
    public string File { get; set; } = string.Empty;
    /// <summary>missing | empty | zero_filled | truncated | malformed | invalid | unreadable | changed.</summary>
    public string Defect { get; set; } = string.Empty;
    /// <summary>backup | defaults | hardware | persisted | regenerated | skipped.</summary>
    public string RecoveredFrom { get; set; } = string.Empty;
    /// <summary>"Warning" (nothing lost), "Error" (settings or data lost), "Critical" (identity or trust anchors lost).</summary>
    public string Severity { get; set; } = "Warning";
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;

    public static string DefectName(FileDefect defect) => defect switch
    {
        FileDefect.Missing => "missing",
        FileDefect.Empty => "empty",
        FileDefect.ZeroFilled => "zero_filled",
        FileDefect.Truncated => "truncated",
        FileDefect.Malformed => "malformed",
        FileDefect.Invalid => "invalid",
        FileDefect.Unreadable => "unreadable",
        _ => "none"
    };
}

/// <summary>Collects recovery notices until they have been reported.</summary>
public interface IRecoveryJournal
{
    /// <summary>Records a recovery. Never throws.</summary>
    void Record(RecoveryNotice notice);

    /// <summary>Notices recorded by this process plus unreported ones left by earlier runs (newest last).</summary>
    IReadOnlyList<RecoveryNotice> Recent { get; }

    /// <summary>Notices not yet acknowledged as delivered to the server.</summary>
    IReadOnlyList<RecoveryNotice> Pending { get; }

    /// <summary>Marks notices as delivered.</summary>
    void Acknowledge(IEnumerable<string> noticeIds);
}

/// <summary>
/// Thread-safe <see cref="IRecoveryJournal"/>. Unreported notices are kept in a durable state file so a
/// recovery that happened on a start which then crashed (or never reached the broker) is still reported.
/// </summary>
public sealed class RecoveryJournal : IRecoveryJournal
{
    public const int MaxPending = 50;
    public const int MaxRecent = 100;

    private readonly object _lock = new();
    private readonly List<RecoveryNotice> _recent = new();
    private readonly List<RecoveryNotice> _pending = new();
    private readonly DurableJsonFile<List<RecoveryNotice>>? _store;

    /// <param name="persistPath">State file for unreported notices; null keeps the journal in memory only.</param>
    public RecoveryJournal(string? persistPath = null)
    {
        if (string.IsNullOrWhiteSpace(persistPath)) return;

        _store = new DurableJsonFile<List<RecoveryNotice>>(persistPath);
        var loaded = _store.Load();
        if (loaded.Value != null)
        {
            _pending.AddRange(loaded.Value.Where(n => n != null).TakeLast(MaxPending));
            _recent.AddRange(_pending);
        }

        if (loaded.Recovered)
        {
            // The journal's own file was damaged: say so, in memory and (best effort) on disk.
            Record(new RecoveryNotice
            {
                Component = "recovery_journal",
                File = persistPath,
                Defect = RecoveryNotice.DefectName(loaded.PrimaryDefect),
                RecoveredFrom = loaded.Origin == DurableOrigin.Backup ? "backup" : "defaults",
                Severity = "Warning",
                Detail = loaded.Detail ?? "pending recovery notices could not be read"
            });
        }
    }

    public IReadOnlyList<RecoveryNotice> Recent
    {
        get { lock (_lock) return _recent.ToList(); }
    }

    public IReadOnlyList<RecoveryNotice> Pending
    {
        get { lock (_lock) return _pending.ToList(); }
    }

    public void Record(RecoveryNotice notice)
    {
        if (notice == null) return;
        lock (_lock)
        {
            _recent.Add(notice);
            if (_recent.Count > MaxRecent) _recent.RemoveRange(0, _recent.Count - MaxRecent);

            _pending.Add(notice);
            if (_pending.Count > MaxPending) _pending.RemoveRange(0, _pending.Count - MaxPending);

            Persist();
        }
    }

    public void Acknowledge(IEnumerable<string> noticeIds)
    {
        if (noticeIds == null) return;
        var ids = new HashSet<string>(noticeIds, StringComparer.Ordinal);
        if (ids.Count == 0) return;

        lock (_lock)
        {
            if (_pending.RemoveAll(n => ids.Contains(n.Id)) > 0)
            {
                Persist();
            }
        }
    }

    private void Persist()
    {
        if (_store == null) return;
        if (_pending.Count == 0)
        {
            _store.Delete();
            return;
        }
        _store.TrySave(_pending.ToList(), out _);
    }
}
