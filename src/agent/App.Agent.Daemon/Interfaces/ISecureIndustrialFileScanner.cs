namespace App.Agent.Daemon.Interfaces;

using System.Collections.Generic;
using System.Threading;
using App.Agent.Daemon.Infrastructure.FileSystem;

/// <summary>
/// Service contract for scanning industrial edge paths with strict PII pruning.
/// </summary>
public interface ISecureIndustrialFileScanner
{
    IEnumerable<DiscoveredIndustrialAsset> ScanDirectory(string rootPath, CancellationToken ct = default);
}
