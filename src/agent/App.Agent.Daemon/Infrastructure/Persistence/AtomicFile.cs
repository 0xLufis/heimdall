namespace App.Agent.Daemon.Infrastructure.Persistence;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>What is wrong with a persisted file, as far as its raw bytes can tell.</summary>
public enum FileDefect
{
    /// <summary>The file is present and its bytes look usable (content validation is the caller's job).</summary>
    None,
    /// <summary>The file does not exist.</summary>
    Missing,
    /// <summary>The file exists but has no content (zero length or whitespace only).</summary>
    Empty,
    /// <summary>
    /// The file has a length but every byte is 0x00. This is what a journaled file system leaves behind when
    /// the machine loses power (or blue-screens) after the size was committed but before the data was written.
    /// </summary>
    ZeroFilled,
    /// <summary>The content stops mid-document or has a zeroed tail (a torn write).</summary>
    Truncated,
    /// <summary>The content is complete but is not what the format requires (bad JSON, wrong root type, bad checksum).</summary>
    Malformed,
    /// <summary>The content parsed but failed the schema / sanity validation of its owner.</summary>
    Invalid,
    /// <summary>The file could not be read at all (I/O error, access denied).</summary>
    Unreadable
}

/// <summary>
/// Crash-safe file replacement for everything the agent persists.
///
/// A plain <c>File.WriteAllText</c> (or a temp file renamed without a flush) can leave an empty,
/// zero-filled or half-written file behind after a power cut or a BSOD, because the rename may reach
/// the disk before the data does. <see cref="Write"/> therefore
/// <list type="number">
/// <item>writes the new content to a temp file in the <b>same directory</b> (same volume, so the rename is atomic),</item>
/// <item>flushes it to the device (<c>FileStream.Flush(true)</c> = fsync / FlushFileBuffers),</item>
/// <item>atomically swaps it in, optionally keeping the previous content as <c>&lt;name&gt;.bak</c>,</item>
/// <item>flushes the directory entry on Unix so the rename itself survives a power cut.</item>
/// </list>
/// A reader therefore always finds either the complete old file or the complete new file. The one
/// documented exception is Windows' <c>ReplaceFile</c>, which can be interrupted with the target
/// already renamed to the backup: the target is then missing while <c>.bak</c> holds the previous good
/// content, which is why loaders fall back to the backup when the primary is missing.
/// </summary>
public static class AtomicFile
{
    public const string BackupSuffix = ".bak";
    public const string TempInfix = ".tmp-";

    /// <summary>Path of the "previous good copy" kept next to <paramref name="path"/>.</summary>
    public static string BackupPathFor(string path) => path + BackupSuffix;

    /// <summary>
    /// Durably and atomically replaces <paramref name="path"/> with <paramref name="content"/>.
    /// </summary>
    /// <param name="path">Target file.</param>
    /// <param name="content">Complete new content.</param>
    /// <param name="keepPreviousAsBackup">
    /// When true and the target exists, the current target becomes <c>&lt;path&gt;.bak</c> in the same atomic
    /// step. Pass false when the current target is known to be damaged, so a good backup is not
    /// overwritten with garbage.
    /// </param>
    /// <param name="ownerOnly">
    /// On Unix, create the file readable and writable by its owner only (0600) - for key material.
    /// Ignored on Windows, where the directory ACL applies.
    /// </param>
    public static void Write(string path, ReadOnlySpan<byte> content, bool keepPreviousAsBackup = true, bool ownerOnly = false)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new IOException($"'{path}' has no parent directory.");
        Directory.CreateDirectory(directory);

        string tempPath = fullPath + TempInfix + Guid.NewGuid().ToString("N");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.WriteThrough
            };
            if (ownerOnly && !OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(tempPath, options))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            if (keepPreviousAsBackup && File.Exists(fullPath))
            {
                ReplaceKeepingBackup(tempPath, fullPath, BackupPathFor(fullPath));
            }
            else
            {
                File.Move(tempPath, fullPath, overwrite: true);
            }

            FlushDirectory(directory);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void ReplaceKeepingBackup(string tempPath, string targetPath, string backupPath)
    {
        try
        {
            // Windows: ReplaceFile. Unix: hard-link target -> backup, then rename(temp, target).
            File.Replace(tempPath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // File systems without hard links / ReplaceFile support (FAT, some network shares), or a
            // backup that cannot be replaced: keep a copy if we can, never lose the new content.
            if (File.Exists(tempPath))
            {
                TryCopyDurable(targetPath, backupPath);
                File.Move(tempPath, targetPath, overwrite: true);
            }
            else if (!File.Exists(targetPath))
            {
                throw;
            }
            // else: the replace actually happened before the error was raised (metadata-only failure).
        }
    }

    /// <summary>Copies <paramref name="source"/> to <paramref name="destination"/> through a flushed temp file. Never throws.</summary>
    public static bool TryCopyDurable(string source, string destination)
    {
        try
        {
            Write(destination, File.ReadAllBytes(source), keepPreviousAsBackup: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a file for a loader that must not throw: returns the bytes when they look usable, or the
    /// raw-byte level <see cref="FileDefect"/> (missing / empty / zero-filled / unreadable).
    /// </summary>
    public static FileDefect TryRead(string path, out byte[] content, out string? detail)
    {
        content = Array.Empty<byte>();
        detail = null;
        try
        {
            if (!File.Exists(path))
            {
                return FileDefect.Missing;
            }

            content = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            detail = $"{ex.GetType().Name}: {ex.Message}";
            return FileDefect.Unreadable;
        }

        return InspectBytes(content, out detail);
    }

    /// <summary>Classifies raw content: empty, zero-filled, zeroed tail (torn write) or apparently fine.</summary>
    public static FileDefect InspectBytes(ReadOnlySpan<byte> content, out string? detail)
    {
        detail = null;
        if (content.Length == 0)
        {
            detail = "file is zero bytes long";
            return FileDefect.Empty;
        }

        int zeros = 0;
        int whitespace = 0;
        foreach (byte b in content)
        {
            if (b == 0) zeros++;
            else if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') whitespace++;
        }

        if (zeros == content.Length || (zeros > 0 && zeros + whitespace == content.Length))
        {
            detail = $"all {content.Length} bytes are 0x00";
            return FileDefect.ZeroFilled;
        }

        if (whitespace == content.Length)
        {
            detail = "file contains only whitespace";
            return FileDefect.Empty;
        }

        if (zeros > 0)
        {
            // Text formats never contain NUL: a partially zeroed block is a torn write.
            detail = $"{zeros} of {content.Length} bytes are 0x00 (first at offset {content.IndexOf((byte)0)})";
            return FileDefect.Truncated;
        }

        return FileDefect.None;
    }

    /// <summary>
    /// Moves a damaged file aside as <c>&lt;path&gt;.corrupt-&lt;utc timestamp&gt;</c> so it stays available for
    /// forensics and cannot be picked up again. Keeps at most <paramref name="keep"/> such copies.
    /// Returns the new path, or null when nothing could be moved. Never throws.
    /// </summary>
    public static string? Quarantine(string path, int keep = 3)
    {
        try
        {
            if (!File.Exists(path)) return null;

            string target = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}";
            File.Move(path, target, overwrite: true);

            string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            string prefix = Path.GetFileName(path) + ".corrupt-";
            var copies = Directory.GetFiles(directory, prefix + "*");
            Array.Sort(copies, StringComparer.Ordinal);
            for (int i = 0; i < copies.Length - Math.Max(1, keep); i++)
            {
                TryDelete(copies[i]);
            }

            return target;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes temp files a crash left behind (<c>&lt;name&gt;.tmp-&lt;guid&gt;</c>, and the legacy <c>*.tmp</c>
    /// pattern of older agent versions). They are never valid on their own: a temp file only becomes
    /// data by being renamed. Returns the number of files removed. Never throws.
    /// </summary>
    public static int CleanupTempFiles(string directory, TimeSpan? olderThan = null)
    {
        int removed = 0;
        try
        {
            if (!Directory.Exists(directory)) return 0;
            var cutoff = DateTime.UtcNow - (olderThan ?? TimeSpan.Zero);
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                string name = Path.GetFileName(file);
                bool isTemp = name.Contains(TempInfix, StringComparison.Ordinal) || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
                if (!isTemp) continue;
                try
                {
                    if (File.GetLastWriteTimeUtc(file) > cutoff) continue;
                    File.Delete(file);
                    removed++;
                }
                catch
                {
                    // In use or protected: leave it for the next start.
                }
            }
        }
        catch
        {
            // Directory vanished or is not listable: nothing to clean.
        }
        return removed;
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort.
        }
    }

    /// <summary>
    /// Makes a rename durable on Unix by fsync-ing the containing directory. On Windows NTFS journals
    /// the rename and there is no portable directory flush, so this is a no-op there.
    /// </summary>
    private static void FlushDirectory(string directory)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD()) return;

        try
        {
            int fd = open(directory, 0 /* O_RDONLY */);
            if (fd < 0) return;
            try
            {
                _ = fsync(fd);
            }
            finally
            {
                _ = close(fd);
            }
        }
        catch
        {
            // libc not resolvable or fsync not permitted on this mount: the data file itself is flushed,
            // only the rename could be replayed, which leaves the previous good file in place.
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int fsync(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);
}
