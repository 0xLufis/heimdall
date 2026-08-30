namespace App.Agent.Daemon.Infrastructure.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

public record DiscoveredIndustrialAsset(
    string FilePath,
    string FileName,
    string Extension,
    long SizeBytes,
    DateTime LastModifiedUtc,
    string Sha256Hash);

/// <summary>
/// High-performance industrial file scanner with strict directory pruning of all personal/PII data.
/// </summary>
public sealed class SecureIndustrialFileScanner
{
    // Allowed industrial extensions: TwinCAT, Siemens TIA, Rockwell, CoDeSys, JSON/XML/YAML configs
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Beckhoff TwinCAT 2 & 3
        ".tszip", ".pro", ".tpy", ".tspproj", ".tmc", ".xti", ".plcproj",
        // Siemens TIA Portal & Step 7
        ".ap14", ".ap15", ".ap16", ".ap17", ".ap18", ".ap19",
        ".zal14", ".zal15", ".zal16", ".zal17", ".zal18", ".zal19",
        ".zap14", ".zap15", ".zap16", ".zap17", ".zap18", ".zap19",
        // Industrial Configurations & Recipes
        ".json", ".xml", ".ini", ".csv", ".yaml", ".yml", ".conf", ".cfg"
    };

    // STRICT BLACKLIST of directories to NEVER enter
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

    // STRICT BLACKLIST of sensitive files (Never read, never hash)
    private static readonly HashSet<string> BlacklistedFileNames = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    /// Recursively scans root paths, pruning blacklisted directories before descent.
    /// Computes streaming SHA256 hashes without allocating large files in memory.
    /// </summary>
    public IEnumerable<DiscoveredIndustrialAsset> ScanDirectory(string rootPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath)) yield break;

        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0 && !ct.IsCancellationRequested)
        {
            var currentDir = stack.Pop();

            // 1. Prune Blacklisted Directories
            if (IsDirectoryBlacklisted(currentDir))
            {
                continue;
            }

            // 2. Discover Subdirectories (Safe from unauthorized access & symlink loops)
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var dir in subDirs)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    // Skip ReparsePoints (Symlinks / Junctions) to avoid circular traversal loops
                    if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    stack.Push(dir);
                }
                catch { }
            }

            // 3. Discover Files in Current Directory
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch
            {
                continue;
            }

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                var extension = Path.GetExtension(filePath);

                // Check Blacklisted File Names
                if (BlacklistedFileNames.Contains(fileName) || fileName.StartsWith("NTUSER.DAT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check Allowed Industrial Extensions
                if (!AllowedExtensions.Contains(extension))
                {
                    continue;
                }

                DiscoveredIndustrialAsset? asset = null;
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 2L * 1024 * 1024 * 1024) // Skip files > 2GB for safety
                    {
                        continue;
                    }

                    string sha256 = ComputeStreamingSha256(filePath);

                    asset = new DiscoveredIndustrialAsset(
                        FilePath: filePath,
                        FileName: fileName,
                        Extension: extension.ToLowerInvariant(),
                        SizeBytes: fileInfo.Length,
                        LastModifiedUtc: fileInfo.LastWriteTimeUtc,
                        Sha256Hash: sha256
                    );
                }
                catch { /* Handle file locks or access restrictions */ }

                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }

    public static bool IsDirectoryBlacklisted(string dirPath)
    {
        var normalized = dirPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        foreach (var segment in BlacklistedDirectorySegments)
        {
            if (normalized.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Computes SHA256 using streaming buffers (zero full-file allocation).
    /// </summary>
    public static string ComputeStreamingSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: false);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
