namespace App.Agent.Daemon.Infrastructure.Opc;

using System;
using System.Collections.Generic;

/// <summary>
/// Abstraction for lightweight OPC UA client polling service.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface IMinimalOpcClient : IDisposable
{
    string EndpointUrl { get; }
    bool IsConnected { get; }
    IReadOnlyDictionary<string, object> MonitoredNodes { get; }
    void Start(int pollIntervalMs = 2000);
    void Stop();
}
