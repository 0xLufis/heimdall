namespace App.Agent.Daemon.Infrastructure.Opc;

using System;
using System.Collections.Concurrent;

/// <summary>
/// Abstraction for lightweight OPC UA simulation server.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface IMinimalOpcServer : IDisposable
{
    int Port { get; }
    string EndpointUrl { get; }
    bool IsListening { get; }
    ConcurrentDictionary<string, object> ServerNodes { get; }
    void Start();
    void Stop();
}
