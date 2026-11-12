using Microsoft.Extensions.Logging;
using App.Agent.Daemon.Infrastructure.Persistence;
using App.Agent.Daemon.Interfaces;

namespace App.Agent.Daemon.Infrastructure.Spooling;

/// <summary>
/// Offline local telemetry spooler buffering telemetry payloads when disconnected from Heimdall backend.
/// <para>
/// Records are written durably (temp file, flush to disk, atomic rename) with a checksum envelope
/// (<see cref="DurableRecordQueue"/>), the disk quota is enforced with FIFO eviction, and draining is
/// single-flight and oldest-first. A record that a power loss or BSOD left empty, zero-filled or
/// truncated is moved to <c>corrupt/</c> and skipped: it neither blocks the records behind it nor
/// stops the agent.
/// </para>
/// </summary>
public class LocalTelemetrySpooler : ITelemetrySpooler
{
    private readonly ILogger<LocalTelemetrySpooler> _logger;
    private readonly IConfigurationService _configService;
    private readonly DurableRecordQueue _queue;
    private readonly SemaphoreSlim _drainGate = new(1, 1);

    public LocalTelemetrySpooler(
        ILogger<LocalTelemetrySpooler> logger,
        IConfigurationService configService,
        string? spoolDirectory = null,
        IRecoveryJournal? recoveryJournal = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        string directory = spoolDirectory ?? Path.Combine(AppContext.BaseDirectory, "telemetry_spool");
        _queue = new DurableRecordQueue(directory, "spool_", "spool", logger, recoveryJournal);
    }

    /// <summary>
    /// Spools a raw telemetry or system info JSON payload to local disk with a durable, crash-resistant write.
    /// </summary>
    public Task SpoolPayloadAsync(string payloadJson)
    {
        long maxBytes = (long)Math.Max(1, _configService.Config.MaxSpoolDiskMb) * 1024 * 1024;

        string? fileName = _queue.TryEnqueue(payloadJson, maxBytes, out var error);
        if (fileName != null)
        {
            _logger.LogInformation("Spooled telemetry payload to {Path} ({Bytes} bytes)", fileName, payloadJson.Length);
        }
        else
        {
            Interlocked.Increment(ref _rejectedRecords);
            _logger.LogError("Failed to write spooled telemetry record to {Path}: {Error}", _queue.Directory, error);
        }

        return Task.CompletedTask;
    }

    private long _rejectedRecords;

    public int PendingCount => _queue.Count;

    /// <summary>
    /// Returns total size in bytes of all spooled payloads buffered on disk.
    /// </summary>
    public long TotalSizeBytes => _queue.TotalBytes;

    /// <summary>Records skipped because they were damaged (torn, zero-filled, bad checksum) or poison since start.</summary>
    public long CorruptRecordsSkipped => _queue.CorruptRecords;

    /// <summary>Records dropped to stay within <c>MaxSpoolDiskMb</c> since start.</summary>
    public long EvictedRecords => _queue.EvictedRecords;

    /// <summary>Records that could not be written to the spool at all since start.</summary>
    public long RejectedRecords => Interlocked.Read(ref _rejectedRecords);

    /// <summary>
    /// Drains spooled payloads in chronological order and sends them via the provided send function.
    /// Only one drain runs at a time; a call that arrives while another is in progress returns 0.
    /// </summary>
    public async Task<int> DrainSpoolAsync(Func<string, Task<bool>> sendFunction)
    {
        ArgumentNullException.ThrowIfNull(sendFunction);

        // Two concurrent drains would read the same directory listing and send every record twice.
        if (!await _drainGate.WaitAsync(0))
        {
            return 0;
        }

        try
        {
            var files = _queue.ListFiles();
            if (files.Count == 0) return 0;

            _logger.LogInformation("Attempting to drain {Count} spooled telemetry entries", files.Count);
            int successCount = 0;
            int setAside = 0;
            string? firstBadFile = null;
            FileDefect firstDefect = FileDefect.None;
            string? firstDetail = null;

            foreach (var file in files)
            {
                var record = _queue.Read(file);
                if (!record.IsValid)
                {
                    if (record.Defect == FileDefect.Missing)
                    {
                        continue; // evicted by the quota or removed meanwhile
                    }

                    if (record.Defect == FileDefect.Unreadable)
                    {
                        // Locked or temporarily inaccessible is not proof of damage: keep order, retry later.
                        _logger.LogWarning("Spooled record {File} is not readable right now ({Detail}). Halting drain.",
                            Path.GetFileName(file), record.Detail);
                        break;
                    }

                    // Torn tail record after a power loss / BSOD, or a corrupted file: skip it, keep going.
                    _queue.SetAside(file, record.Defect, record.Detail);
                    setAside++;
                    firstBadFile ??= Path.GetFileName(file);
                    if (setAside == 1) { firstDefect = record.Defect; firstDetail = record.Detail; }
                    continue;
                }

                bool sent;
                try
                {
                    sent = await sendFunction(record.Payload!);
                }
                catch (Exception ex)
                {
                    if (_queue.RegisterConsumerFailure(file))
                    {
                        // The consumer cannot process this record at all: it must not block the spool forever.
                        _logger.LogError(ex, "Spooled record {File} failed {Attempts} times in the consumer; setting it aside.",
                            Path.GetFileName(file), DurableRecordQueue.MaxConsumerFailures);
                        _queue.SetAside(file, FileDefect.Invalid, $"consumer failed: {ex.GetType().Name}: {ex.Message}");
                        setAside++;
                        firstBadFile ??= Path.GetFileName(file);
                        if (setAside == 1) { firstDefect = FileDefect.Invalid; firstDetail = ex.Message; }
                        continue;
                    }

                    _logger.LogError(ex, "Error while draining spooled file {File}", Path.GetFileName(file));
                    break;
                }

                if (sent)
                {
                    _queue.Remove(file);
                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Downstream backend rejected spooled payload {File}. Halting drain.", Path.GetFileName(file));
                    break;
                }
            }

            if (setAside > 0)
            {
                _queue.ReportSetAside(setAside, firstBadFile!, firstDefect, firstDetail);
            }

            if (successCount > 0)
            {
                _logger.LogInformation("Successfully drained and removed {Count} spooled entries", successCount);
            }

            return successCount;
        }
        finally
        {
            _drainGate.Release();
        }
    }
}
