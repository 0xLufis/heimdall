namespace App.Agent.Daemon.Infrastructure.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using App.Agent.Daemon.Interfaces;
using App.Shared.Sanitization;

/// <summary>
/// High-performance file scanner with path sanitization and directory pruning.
/// </summary>
public sealed class FileSystemScanner : IFileSystemScanner
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Project files & archives
        ".tszip", ".pro", ".tpy", ".tspproj", ".tmc", ".xti", ".plcproj",
        ".ap14", ".ap15", ".ap16", ".ap17", ".ap18", ".ap19",
        ".zal14", ".zal15", ".zal16", ".zal17", ".zal18", ".zal19",
        ".zap14", ".zap15", ".zap16", ".zap17", ".zap18", ".zap19",
        // Configurations & data
        ".json", ".xml", ".ini", ".csv", ".yaml", ".yml", ".conf", ".cfg"
    };

    private static readonly string[] BlacklistedDirectorySegments =
    {
        Path.Combine("AppData", "Local", "Google", "Chrome"),
        Path.Combine("AppData", "Local", "Microsoft", "Edge"),
        Path.Combine("AppData", "Roaming", "Mozilla"),
        "node_modules",
        ".git",
        Path.Combine("Windows", "WinSxS"),
        Path.Combine("Windows", "System32", "config"),
        "Cookies",
        "History"
    };

    private static readonly HashSet<string> BlacklistedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NTUSER.DAT",
        "UsrClass.dat",
        ".bash_history",
        ".zsh_history",
        "id_rsa",
        "id_ed25519",
        "id_ecdsa",
        "known_hosts",
        "sam",
        "security",
        "system"
    };

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
                if (BlacklistedFileNames.Contains(fileName))
                    continue;

                var ext = Path.GetExtension(filePath);
                if (!AllowedExtensions.Contains(ext))
                    continue;

                FileInfo fi;
                try
                {
                    fi = new FileInfo(filePath);
                    if (!fi.Exists) continue;
                }
                catch { continue; }

                string hash = string.Empty;
                try
                {
                    hash = ComputeStreamingSha256(filePath, ct);
                }
                catch { continue; }

                yield return new DiscoveredAsset(
                    FilePath: filePath,
                    FileName: fileName,
                    Extension: ext,
                    SizeBytes: fi.Length,
                    LastModifiedUtc: fi.LastWriteTimeUtc,
                    Sha256Hash: hash
                );
            }
        }
    }

    private static bool IsDirectoryBlacklisted(string dirPath)
    {
        foreach (var segment in BlacklistedDirectorySegments)
        {
            if (dirPath.Contains(segment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string ComputeStreamingSha256(string filePath, CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
