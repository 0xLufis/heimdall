namespace App.Backend.Api.Logging;

/// <summary>
/// Settings of the rotating file logger, bound from <c>Logging:File</c> (environment variables:
/// <c>Logging__File__MaxFileSizeMB=50</c> and so on). Levels are not set here: like every logging
/// provider the file logger follows <c>Logging:LogLevel</c>, or <c>Logging:File:LogLevel</c> when that
/// section exists.
/// </summary>
public sealed class FileLoggerOptions
{
    public const string SectionName = "Logging:File";

    /// <summary>Off unless a configuration (appsettings.Development.json, environment) turns it on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Log directory. A relative path is resolved against the content root.</summary>
    public string Path { get; set; } = "logs";

    /// <summary>Name of the file being written. Rotated files get a timestamp: <c>backend-20261112-031522-123.log</c>.</summary>
    public string FileName { get; set; } = "backend.log";

    /// <summary>The active file is rotated before it grows past this size (and at least once a day).</summary>
    public double MaxFileSizeMB { get; set; } = 20;

    /// <summary>Upper bound for all files of this logger together (the active file included); the oldest rotated files are deleted first.</summary>
    public double MaxTotalSizeMB { get; set; } = 200;

    /// <summary>Rotated files older than this are deleted.</summary>
    public double MaxAgeDays { get; set; } = 7;

    /// <summary>
    /// Capacity of the in-memory spool between the logging call sites and the writer thread. When it is
    /// full the oldest Trace/Debug/Information entries are dropped first (and counted).
    /// </summary>
    public int QueueCapacity { get; set; } = 20_000;

    /// <summary>How often the writer flushes buffered lines to disk. Errors and criticals are flushed at once.</summary>
    public double FlushIntervalSeconds { get; set; } = 1;

    /// <summary>How long shutdown waits for the writer to drain and flush the spool.</summary>
    public double ShutdownTimeoutSeconds { get; set; } = 2;

    internal long MaxFileSizeBytes => ToBytes(MaxFileSizeMB, 20);
    internal long MaxTotalSizeBytes => ToBytes(MaxTotalSizeMB, 200);

    private static long ToBytes(double megabytes, double fallback)
    {
        var mb = double.IsFinite(megabytes) && megabytes > 0 ? megabytes : fallback;
        return Math.Max(1024, (long)(mb * 1024 * 1024));
    }

    internal static TimeSpan Seconds(double value, double fallback, double min) =>
        TimeSpan.FromSeconds(double.IsFinite(value) && value >= min ? value : fallback);
}
