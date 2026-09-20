namespace App.Agent.Daemon.Infrastructure.Beckhoff;

using System;
using System.Collections.Concurrent;

/// <summary>
/// Abstraction for Beckhoff TwinCAT ADS simulation server.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface IAdsSimulationServer : IDisposable
{
    int Port { get; }
    string AmsNetId { get; }
    ushort AmsPort { get; }
    ushort CurrentAdsState { get; }
    bool IsListening { get; }
    ConcurrentDictionary<string, object> SimulatedVariables { get; }
    void Start();
    void Stop();
}
