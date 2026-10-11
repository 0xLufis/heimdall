namespace App.Agent.Daemon.Interfaces;

using System.Threading.Tasks;
using App.Shared.Protos;

/// <summary>
/// Service contract for dispatching gathered telemetry and inventory to the central backend.
/// </summary>
public interface ISystemInfoReporter
{
    /// <summary>
    /// Reports endpoint telemetry and inventory via gRPC to the central backend.
    /// </summary>
    Task<SystemInfoResponse?> ReportInfoAsync(SystemInfoData data);

    /// <summary>
    /// Returns the configuration service associated with this reporter.
    /// </summary>
    IConfigurationService GetConfigService();

    /// <summary>
    /// Triggers an immediate telemetry reporting cycle to the backend.
    /// </summary>
    Task<SystemInfoResponse?> TriggerSyncAsync();
}
