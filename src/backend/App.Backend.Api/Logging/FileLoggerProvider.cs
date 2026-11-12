using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace App.Backend.Api.Logging;

/// <summary>
/// Rotating file logger without third-party packages. Logging calls only format the message and put it
/// into a bounded <see cref="LogSpool"/>; a dedicated background thread drains the spool in batches,
/// writes through a buffered stream and flushes every <see cref="FileLoggerOptions.FlushIntervalSeconds"/>
/// (errors at once). The active file is rotated by size and at the first entry of a new day; rotated
/// files are deleted by age and, oldest first, whenever all files together would exceed
/// <see cref="FileLoggerOptions.MaxTotalSizeMB"/>. Disposal drains and flushes within
/// <see cref="FileLoggerOptions.ShutdownTimeoutSeconds"/>.
/// The alias makes <c>Logging:File:LogLevel</c> apply to this provider.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int StreamBufferSize = 64 * 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan RetentionCheckInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReopenBackoff = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly LogSpool _spool;
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly Thread? _writer;
    private readonly int _wakeThreshold;
    private readonly string _baseName;
    private readonly string _extension;
    private readonly long _maxFileBytes;
    private readonly long _maxTotalBytes;
    private readonly TimeSpan _maxAge;
    private readonly TimeSpan _flushInterval;
    private readonly TimeSpan _shutdownTimeout;
    private volatile bool _stopping;
    private int _disposed;

    // Writer-thread state (only touched by the writer, or by the caller of ProcessQueue when no writer runs).
    private readonly List<LogEntry> _batch = new();
    private readonly StringBuilder _line = new(256);
    private FileStream? _stream;
    private long _currentSize;
    private DateTime _currentDay;
    private DateTime _lastFlushUtc = DateTime.UtcNow;
    private DateTime _lastRetentionUtc = DateTime.MinValue;
    private DateTime _nextOpenAttemptUtc = DateTime.MinValue;
    private DateTime _lastErrorReportUtc = DateTime.MinValue;
    private DateTime _lastArchiveStamp = DateTime.MinValue;
    private long _lostOnError;
    private string? _lastError;

    public FileLoggerProvider(FileLoggerOptions options, string? baseDirectory = null)
        : this(options, baseDirectory, startWriter: true)
    {
    }

    /// <param name="startWriter">
    /// False leaves the spool undrained until <see cref="ProcessQueue"/> or <see cref="Dispose"/> is
    /// called (tests use this to fill the spool deterministically).
    /// </param>
    public FileLoggerProvider(FileLoggerOptions options, string? baseDirectory, bool startWriter)
    {
        ArgumentNullException.ThrowIfNull(options);
        var path = string.IsNullOrWhiteSpace(options.Path) ? "logs" : options.Path.Trim();
        Directory = System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(path)
            ? path
            : System.IO.Path.Combine(baseDirectory ?? AppContext.BaseDirectory, path));
        var fileName = string.IsNullOrWhiteSpace(options.FileName) ? "backend.log" : System.IO.Path.GetFileName(options.FileName.Trim());
        ActiveFilePath = System.IO.Path.Combine(Directory, fileName);
        _baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        _extension = System.IO.Path.GetExtension(fileName) is { Length: > 0 } ext ? ext : ".log";

        _maxTotalBytes = options.MaxTotalSizeBytes;
        _maxFileBytes = Math.Min(options.MaxFileSizeBytes, _maxTotalBytes);
        _maxAge = TimeSpan.FromDays(double.IsFinite(options.MaxAgeDays) && options.MaxAgeDays > 0 ? options.MaxAgeDays : 7);
        _flushInterval = FileLoggerOptions.Seconds(options.FlushIntervalSeconds, 1, 0.05);
        _shutdownTimeout = FileLoggerOptions.Seconds(options.ShutdownTimeoutSeconds, 2, 0);
        _spool = new LogSpool(options.QueueCapacity);
        // Wake the writer early once a quarter of the spool is used, so bursts are written long before
        // the spool overflows; otherwise it wakes once per flush interval.
        _wakeThreshold = Math.Max(1, _spool.Capacity / 4);

        if (startWriter)
        {
            _writer = new Thread(WriterLoop) { IsBackground = true, Name = "Heimdall file logger" };
            _writer.Start();
        }
    }

    /// <summary>Absolute log directory.</summary>
    public string Directory { get; }

    /// <summary>Absolute path of the file currently written.</summary>
    public string ActiveFilePath { get; }

    public long TotalDropped => _spool.TotalDropped;
    public int QueueLength => _spool.Count;
    public int QueueCapacity => _spool.Capacity;
    public long MaxFileSizeBytes => _maxFileBytes;
    public long MaxTotalSizeBytes => _maxTotalBytes;
    public TimeSpan MaxAge => _maxAge;
    public string? LastError => _lastError;

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, static (name, provider) => new FileLogger(name, provider), this);

    internal void Enqueue(in LogEntry entry)
    {
        if (_stopping) return;
        var count = _spool.Enqueue(entry);
        if (count == _wakeThreshold || entry.Level >= LogLevel.Error) _wake.Set();
    }

    /// <summary>Rotated files of this logger (the active file excluded), oldest first.</summary>
    public IReadOnlyList<FileInfo> GetRotatedFiles()
    {
        if (!System.IO.Directory.Exists(Directory)) return Array.Empty<FileInfo>();
        return new DirectoryInfo(Directory)
            .GetFiles($"{_baseName}-*{_extension}")
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Drains the spool and writes it. Called by the writer thread; tests that construct the provider
    /// without a writer call it directly.
    /// </summary>
    public void ProcessQueue(bool flush = false)
    {
        if (_writer != null && Thread.CurrentThread != _writer)
            throw new InvalidOperationException("The writer thread owns the log file; ProcessQueue is only for providers created without one.");
        _batch.Clear();
        var dropped = _spool.DrainTo(_batch);
        var now = DateTime.UtcNow;
        var written = 0;
        try
        {
            if (_stream == null && now < _nextOpenAttemptUtc)
            {
                _lostOnError += _batch.Count + dropped; // still backing off after a write error
                return;
            }

            if (_lostOnError > 0)
            {
                WriteEntry(Internal(LogLevel.Warning, $"{_lostOnError} log entries were lost because the log file could not be written ({_lastError})."));
                _lostOnError = 0;
            }
            if (dropped > 0)
            {
                WriteEntry(Internal(LogLevel.Warning, $"{dropped} log entries dropped: the file log queue was full (capacity {_spool.Capacity}); lowest levels are dropped first."));
            }

            var urgent = false;
            foreach (var entry in _batch)
            {
                WriteEntry(entry);
                written++;
                urgent |= entry.Level >= LogLevel.Error;
            }

            if (_stream != null && (flush || urgent || now - _lastFlushUtc >= _flushInterval))
            {
                _stream.Flush();
                _lastFlushUtc = now;
            }
            if (now - _lastRetentionUtc >= RetentionCheckInterval)
            {
                ApplyRetention();
            }
        }
        catch (Exception ex)
        {
            _lostOnError += _batch.Count - written;
            ReportError(ex);
            CloseStream(flush: false);
            _nextOpenAttemptUtc = now + ReopenBackoff;
        }
        finally
        {
            _batch.Clear();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stopping = true;
        if (_writer != null)
        {
            _wake.Set();
            // A writer stuck on a hung disk is abandoned after the deadline (it is a background thread).
            _writer.Join(_shutdownTimeout);
        }
        else
        {
            ProcessQueue(flush: true);
            CloseStream(flush: true);
        }
    }

    private void WriterLoop()
    {
        while (true)
        {
            var stopping = _stopping;
            if (!stopping)
            {
                _wake.Wait(_flushInterval);
                _wake.Reset();
            }
            ProcessQueue(flush: stopping);
            if (stopping) break;
        }
        CloseStream(flush: true);
    }

    private static LogEntry Internal(LogLevel level, string message) =>
        new(DateTimeOffset.Now, level, typeof(FileLoggerProvider).FullName!, 0, message, null);

    private void WriteEntry(in LogEntry entry)
    {
        var text = Format(entry);
        var byteCount = Utf8.GetByteCount(text);
        EnsureStream(entry.Timestamp, byteCount);

        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var length = Utf8.GetBytes(text, 0, text.Length, buffer, 0);
            _stream!.Write(buffer, 0, length);
            _currentSize += length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private string Format(in LogEntry entry)
    {
        var sb = _line.Clear();
        sb.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
          .Append(" [").Append(LevelTag(entry.Level)).Append("] ")
          .Append(entry.Category);
        if (entry.EventId != 0) sb.Append('[').Append(entry.EventId).Append(']');
        sb.Append(": ").Append(entry.Message);
        if (entry.Exception != null) sb.Append(Environment.NewLine).Append(entry.Exception);
        sb.Append(Environment.NewLine);
        return sb.ToString();
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    /// <summary>Opens the active file, or rotates it when the next line would exceed the size limit or a new day began.</summary>
    private void EnsureStream(DateTimeOffset timestamp, int nextBytes)
    {
        if (_stream == null)
        {
            Open();
        }
        else if (_currentSize > 0 && (_currentSize + nextBytes > _maxFileBytes || timestamp.Date > _currentDay))
        {
            CloseStream(flush: true);
            ArchiveActiveFile();
            Open();
        }
    }

    private void Open()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var existing = new FileInfo(ActiveFilePath);
        if (existing.Exists && existing.Length > 0 &&
            (existing.Length >= _maxFileBytes || existing.LastWriteTime.Date != DateTime.Now.Date))
        {
            // Left over from an earlier run: keep each file within the size limit and to a single day.
            ArchiveActiveFile();
        }

        _stream = new FileStream(ActiveFilePath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete, StreamBufferSize);
        _currentSize = _stream.Length;
        _currentDay = DateTime.Now.Date;
        ApplyRetention();
    }

    private void ArchiveActiveFile()
    {
        if (!File.Exists(ActiveFilePath)) return;
        // Names must sort chronologically: file timestamps come from a coarse clock and often tie, and
        // retention orders by (mtime, name). On a collision the stamp moves forward by a millisecond
        // rather than getting a suffix (which would sort before the original name).
        var time = DateTime.Now;
        if (_lastArchiveStamp >= time) time = _lastArchiveStamp.AddMilliseconds(1);
        string target;
        while (File.Exists(target = System.IO.Path.Combine(Directory,
                   $"{_baseName}-{time.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}{_extension}")))
        {
            time = time.AddMilliseconds(1);
        }
        _lastArchiveStamp = new DateTime(time.Ticks - time.Ticks % TimeSpan.TicksPerMillisecond, time.Kind);
        File.Move(ActiveFilePath, target);
    }

    /// <summary>
    /// Deletes rotated files older than the age limit, then the oldest ones until the rotated files plus
    /// a full-size active file fit the total cap, so the directory never grows beyond it.
    /// </summary>
    private void ApplyRetention()
    {
        _lastRetentionUtc = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow - _maxAge;
        var budget = Math.Max(0, _maxTotalBytes - _maxFileBytes);
        var keep = new List<FileInfo>();
        foreach (var file in GetRotatedFiles())
        {
            if (file.LastWriteTimeUtc < cutoff) TryDelete(file);
            else keep.Add(file);
        }

        var total = keep.Sum(LengthOf);
        foreach (var file in keep)
        {
            if (total <= budget) break;
            var length = LengthOf(file); // read before Delete(), which invalidates the cached state
            if (TryDelete(file)) total -= length;
        }
    }

    /// <summary>File length, 0 when the file vanished meanwhile (the writer may delete rotated files at any time).</summary>
    public static long LengthOf(FileInfo file)
    {
        try
        {
            return file.Exists ? file.Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private bool TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportError(ex);
            return false;
        }
    }

    private void CloseStream(bool flush)
    {
        var stream = _stream;
        _stream = null;
        if (stream == null) return;
        try
        {
            if (flush) stream.Flush();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            try { stream.Dispose(); } catch (Exception) { /* the buffer could not be written; already reported */ }
        }
    }

    /// <summary>Logging must not log its own failures through itself; stderr, at most once a minute.</summary>
    private void ReportError(Exception ex)
    {
        _lastError = ex.Message;
        var now = DateTime.UtcNow;
        if (now - _lastErrorReportUtc < TimeSpan.FromMinutes(1)) return;
        _lastErrorReportUtc = now;
        try
        {
            Console.Error.WriteLine($"[file logger] cannot write {ActiveFilePath}: {ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception)
        {
            // nowhere left to report to
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Levels are filtered by the LoggerFactory rules (Logging:LogLevel, Logging:File:LogLevel).
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.None) return;
            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null) return;
            _provider.Enqueue(new LogEntry(DateTimeOffset.Now, logLevel, _category, eventId.Id, message ?? string.Empty, exception?.ToString()));
        }
    }
}
