namespace App.Agent.Daemon.Interfaces;

using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Record representing an identified software or configuration asset file.
/// </summary>
public record DiscoveredAsset(
    string FilePath,
    string FileName,
    string Extension,
    long SizeBytes,
    DateTime LastModifiedUtc,
    string Sha256Hash);

/// <summary>
/// Service contract for scanning filesystem paths for configuration and code assets while enforcing security constraints.
/// </summary>
public interface IFileSystemScanner
{
    /// <summary>
    /// Recursively scans a root path for authorized assets while pruning blacklisted directories.
    /// </summary>
    IEnumerable<DiscoveredAsset> ScanDirectory(string rootPath, CancellationToken ct = default);
}
