using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Backend.Api.Services;

/// <summary>
/// Stub service for interacting with industrial OPC UA Gateways and PLCs.
/// Handles connection management, tag reading/writing, telemetry subscription, and EquipmentInterconnect registration.
/// </summary>
public class OpcUaGatewayService
{
    private readonly ILogger<OpcUaGatewayService> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public OpcUaGatewayService(ILogger<OpcUaGatewayService> logger, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Connects to an OPC UA Server at the specified endpoint URL.
    /// </summary>
    public async Task<bool> ConnectAsync(string endpointUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OPC UA Gateway: Connecting to endpoint {EndpointUrl}", endpointUrl);
        await Task.Delay(50, cancellationToken);
        return true;
    }

    /// <summary>
    /// Reads a tag value from a specific OPC UA Node ID.
    /// </summary>
    public async Task<object?> ReadNodeValueAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OPC UA Gateway: Reading node {NodeId}", nodeId);
        await Task.Delay(20, cancellationToken);
        return $"SimulatedValue_{nodeId}";
    }

    /// <summary>
    /// Writes a value to an OPC UA Node ID.
    /// </summary>
    public async Task<bool> WriteNodeValueAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OPC UA Gateway: Writing value '{Value}' to node {NodeId}", value, nodeId);
        await Task.Delay(20, cancellationToken);
        return true;
    }

    /// <summary>
    /// Subscribes to real-time telemetry updates for an OPC UA Node ID.
    /// </summary>
    public async Task<bool> SubscribeTelemetryAsync(string nodeId, Action<string, object> onDataReceived, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OPC UA Gateway: Subscribing to telemetry for node {NodeId}", nodeId);
        await Task.Delay(20, cancellationToken);
        onDataReceived(nodeId, "InitialTelemetryValue");
        return true;
    }

    /// <summary>
    /// Creates and persists an OPC UA EquipmentInterconnect record in the database.
    /// </summary>
    public async Task<EquipmentInterconnect> RegisterInterconnectAsync(Guid sourceId, Guid targetId, string endpointUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OPC UA Gateway: Registering interconnect between {SourceId} and {TargetId}", sourceId, targetId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var interconnect = new EquipmentInterconnect
        {
            Id = Guid.NewGuid(),
            SourceEquipmentId = sourceId,
            TargetEquipmentId = targetId,
            InterconnectType = "OPC UA",
            Protocol = "opc.tcp",
            PortOrAddress = endpointUrl,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.EquipmentInterconnects.Add(interconnect);
        await dbContext.SaveChangesAsync(cancellationToken);

        return interconnect;
    }
}
