namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;

/// <summary>
/// Interface for OPC UA Gateway and Node Manager service.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface IOpcUaGatewayService
{
    Task<bool> ConnectAsync(string endpointUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpcUaNodeDescriptor>> BrowseNodesAsync(string parentNodeId = "ns=2;s=Root", CancellationToken cancellationToken = default);
    Task<OpcUaNodeDescriptor?> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<object?> ReadNodeValueAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<bool> WriteNodeValueAsync(string nodeId, object value, CancellationToken cancellationToken = default);
    Task<bool> SubscribeTelemetryAsync(string nodeId, Action<string, object> onDataReceived, CancellationToken cancellationToken = default);
    Task<EquipmentInterconnect> RegisterInterconnectAsync(Guid sourceId, Guid targetId, string endpointUrl, CancellationToken cancellationToken = default);
}
