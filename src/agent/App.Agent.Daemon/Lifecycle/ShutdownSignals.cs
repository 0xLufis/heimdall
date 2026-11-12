namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Remembers what told the process to stop: a POSIX signal (SIGTERM, SIGINT, SIGQUIT, SIGHUP - on
/// Windows consoles .NET maps CTRL_SHUTDOWN/LOGOFF to SIGTERM, CTRL_C to SIGINT, CTRL_BREAK to
/// SIGQUIT and CTRL_CLOSE to SIGHUP) or a Windows service control (STOP vs SHUTDOWN, which is what
/// separates "operator stopped the service" from "the OS is going down"). The handlers only record;
/// stopping the host stays with the host lifetime.
/// </summary>
public sealed class ShutdownSignalMonitor : IDisposable
{
    private readonly PosixSignalRegistration?[] _registrations;
    private string? _lastSignal;

    public ShutdownSignalMonitor(bool registerPosixSignals = true)
    {
        _registrations = registerPosixSignals
            ? new[]
            {
                TryRegister(PosixSignal.SIGTERM, LifecycleContract.Signal.SigTerm),
                TryRegister(PosixSignal.SIGINT, LifecycleContract.Signal.SigInt),
                TryRegister(PosixSignal.SIGQUIT, LifecycleContract.Signal.SigQuit),
                TryRegister(PosixSignal.SIGHUP, LifecycleContract.Signal.SigHup)
            }
            : Array.Empty<PosixSignalRegistration?>();
    }

    /// <summary>The most recent stop trigger, or null when the host stopped by itself.</summary>
    public string? LastSignal => Volatile.Read(ref _lastSignal);

    public void Record(string signal) => Volatile.Write(ref _lastSignal, signal);

    private PosixSignalRegistration? TryRegister(PosixSignal signal, string name)
    {
        try
        {
            // Cancel is left untouched: the host lifetime's own handler still performs the stop.
            return PosixSignalRegistration.Create(signal, _ => Record(name));
        }
        catch
        {
            return null; // signal not supported on this platform
        }
    }

    public void Dispose()
    {
        foreach (var registration in _registrations) registration?.Dispose();
    }
}

/// <summary>
/// The Windows service lifetime, extended to record whether the service control manager sent
/// SERVICE_CONTROL_STOP (operator / installer stopped the service) or SERVICE_CONTROL_SHUTDOWN
/// (the OS is shutting down or restarting). Registered in <c>Program.cs</c> when running as a service.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AgentWindowsServiceLifetime : WindowsServiceLifetime
{
    private readonly ShutdownSignalMonitor _signals;

    public AgentWindowsServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor,
        ShutdownSignalMonitor signals)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        _signals = signals;
        CanShutdown = true;
    }

    protected override void OnStop()
    {
        _signals.Record(LifecycleContract.Signal.ServiceControlStop);
        base.OnStop();
    }

    protected override void OnShutdown()
    {
        _signals.Record(LifecycleContract.Signal.ServiceControlShutdown);
        base.OnShutdown();
    }
}
