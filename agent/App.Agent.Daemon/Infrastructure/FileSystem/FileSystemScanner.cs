namespace App.Agent.Daemon.Infrastructure.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using App.Agent.Daemon.Interfaces;
using App.Shared.Sanitization;

/// <summary>
/// High-performance file scanner with path sanitization, directory pruning,
/// configurable custom banned file types, and configurable allowed file naming templates.
/// Supports directory-based SCADA runtime trees, custom target paths, and versioned project archives.
/// </summary>
public sealed class FileSystemScanner : IFileSystemScanner
{
    private static readonly HashSet<string> DefaultAllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Project files & archives
        ".tszip", ".pro", ".tpy", ".tspproj", ".tmc", ".xti", ".plcproj",
        ".ap14", ".ap15", ".ap16", ".ap17", ".ap18", ".ap19",
        ".zal14", ".zal15", ".zal16", ".zal17", ".zal18", ".zal19",
        ".zap14", ".zap15", ".zap16", ".zap17", ".zap18", ".zap19",
        // Configurations & data
        ".json", ".xml", ".ini", ".csv", ".yaml", ".yml", ".conf", ".cfg"
    };

    // Standard regex templates for versioned industrial project files (e.g., Siemens TIA zal20, ap20, zap20)
    private static readonly Regex[] DefaultAllowedTemplates =
    {
        new(@"^.+\.(zal|zap|ap)\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250))
    };

    private static readonly string[] BlacklistedDirectorySegments =
    {
        Path.Combine("AppData", "Local", "Google", "Chrome"),
        Path.Combine("AppData", "Local", "Microsoft", "Edge"),
        Path.Combine("AppData", "Roaming", "Mozilla"),
        Path.Combine("AppData", "Local", "BraveSoftware"),
        Path.Combine("AppData", "Local", "Opera Software"),
        ".config" + Path.DirectorySeparatorChar + "google-chrome",
        ".config" + Path.DirectorySeparatorChar + "chromium",
        ".mozilla",
        "node_modules",
        ".git",
        Path.Combine("Windows", "WinSxS"),
        Path.Combine("Windows", "System32", "config"),
        Path.Combine("Users", "Default"),
        "Cookies",
        "History",
        "Login Data",
        "Web Data",
        "Sessions"
    };

    private static readonly HashSet<string> DefaultBlacklistedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NTUSER.DAT",
        "NTUSER.DAT.LOG1",
        "NTUSER.DAT.LOG2",
        "UsrClass.dat",
        "UsrClass.dat.LOG1",
        ".bash_history",
        ".zsh_history",
        ".history",
        "id_rsa",
        "id_ed25519",
        "id_ecdsa",
        "known_hosts",
        "sam",
        "security",
        "system",
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys"
    };

    private readonly FileSystemScannerOptions _options;
    private readonly List<Regex> _compiledAllowedPatterns = new();

    public FileSystemScanner() : this(new FileSystemScannerOptions())
    {
    }

    public FileSystemScanner(FileSystemScannerOptions? options)
    {
        _options = options ?? new FileSystemScannerOptions();

        _compiledAllowedPatterns.AddRange(DefaultAllowedTemplates);

        if (_options.AllowedFileNamePatterns != null)
        {
            foreach (var pattern in _options.AllowedFileNamePatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    _compiledAllowedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250)));
                }
            }
        }
    }

    public IEnumerable<DiscoveredAsset> ScanDirectory(string rootPath, CancellationToken ct = default)
    {
        if (!PathSanitizer.TrySanitizePath(rootPath, out var sanitizedRoot, out _) || !Directory.Exists(sanitizedRoot))
            yield break;

        var stack = new Stack<string>();
        stack.Push(sanitizedRoot);

        while (stack.Count > 0 && !ct.IsCancellationRequested)
        {
            var currentDir = stack.Pop();

            if (IsDirectoryBlacklisted(currentDir))
                continue;

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            catch (IOException) { continue; }

            foreach (var subDir in subDirs)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    stack.Push(subDir);
                }
                catch { }
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            catch (IOException) { continue; }

            foreach (var filePath in files)
            {
                if (ct.IsCancellationRequested) yield break;

                var fileName = Path.GetFileName(filePath);
                if (IsFileBlacklisted(fileName))
                    continue;

                var ext = Path.GetExtension(filePath);
                if (!IsFileAllowed(fileName, ext))
                    continue;

                FileInfo fi;
                try
                {
                    fi = new FileInfo(filePath);
                    if (!fi.Exists || fi.Length > _options.MaxFileSizeBytes) continue;
                }
                catch { continue; }

                string hash;
                try
                {
                    hash = ComputeStreamingSha256(filePath, ct);
                }
                catch { continue; }

                yield return new DiscoveredAsset(
                    FilePath: filePath,
                    FileName: fileName,
                    Extension: ext.ToLowerInvariant(),
                    SizeBytes: fi.Length,
                    LastModifiedUtc: fi.LastWriteTimeUtc,
                    Sha256Hash: hash
                );
            }
        }
    }

    private bool IsFileBlacklisted(string fileName)
    {
        if (DefaultBlacklistedFileNames.Contains(fileName) || fileName.StartsWith("NTUSER.DAT", StringComparison.OrdinalIgnoreCase))
            return true;

        if (_options.CustomBannedFileNames.Contains(fileName))
            return true;

        return false;
    }

    private bool IsFileAllowed(string fileName, string extension)
    {
        // 1. Strict precedence: Custom banned extensions take priority
        if (!string.IsNullOrEmpty(extension) && _options.CustomBannedExtensions.Contains(extension))
            return false;

        // 2. Check if extension matches default or additional allowed sets
        if (DefaultAllowedExtensions.Contains(extension) || _options.AdditionalAllowedExtensions.Contains(extension))
            return true;

        // 3. Check regex templates on fileName
        foreach (var pattern in _compiledAllowedPatterns)
        {
            try
            {
                if (pattern.IsMatch(fileName))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // Disallow pattern execution on regex catastrophic backtracking
            }
        }

        return false;
    }

    public static bool IsDirectoryBlacklisted(string dirPath)
    {
        var normalized = dirPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        foreach (var segment in BlacklistedDirectorySegments)
        {
            if (normalized.Contains(segment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string ComputeStreamingSha256(string filePath, CancellationToken ct = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
