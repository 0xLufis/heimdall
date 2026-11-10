namespace App.Shared.Lua.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;

/// <summary>
/// Raised when a sandboxed script exceeds a resource budget. Deliberately NOT an
/// <see cref="InterpreterException"/> so it can never be caught by Lua code.
/// </summary>
public sealed class LuaSandboxViolationException : Exception
{
    public LuaSandboxViolationException(string message) : base(message) { }
}

/// <summary>
/// Per-execution resource governor. Attached to a MoonSharp <see cref="Script"/> as a debugger so
/// that <see cref="IsPauseRequested"/> runs before every VM instruction — including instructions
/// executed in nested CLR→Lua calls (table.sort comparators, gsub callbacks, API callbacks) that
/// MoonSharp's coroutine auto-yield cannot interrupt.
///
/// Enforces: wall-clock deadline, caller cancellation, per-thread allocation budget and
/// CLR re-entrancy depth (unbounded Lua→CLR→Lua recursion otherwise overflows the .NET stack and
/// kills the process). Once tripped, the violation is sticky: every subsequent instruction throws
/// again, so CLR code that swallows exceptions cannot keep the script alive.
/// </summary>
public sealed class LuaSandboxGuard : IDebugger
{
    private static readonly ConditionalWeakTable<Script, LuaSandboxGuard> Guards = new();

    private readonly LuaSandboxOptions _options;
    private readonly CancellationToken _cancellationToken;
    private readonly long _deadlineTicks;
    private long _allocationBaseline;
    private int _executingThreadId;
    private int _callbackDepth;
    private long _instructionCount;
    private volatile string? _violation;

    public LuaSandboxGuard(LuaSandboxOptions options, CancellationToken cancellationToken)
    {
        _options = options;
        _cancellationToken = cancellationToken;
        _deadlineTicks = Stopwatch.GetTimestamp() + (long)(options.MaxExecutionTime.TotalSeconds * Stopwatch.Frequency);
    }

    /// <summary>The first violation recorded, or null if the script stayed within budget.</summary>
    public string? Violation => _violation;

    public static LuaSandboxGuard? For(Script script) => Guards.TryGetValue(script, out var g) ? g : null;

    /// <summary>
    /// Calls a Lua closure from CLR code under the owning script's guard. All CLR→Lua re-entry in
    /// the domain APIs must go through here; calling a closure without a guard is refused.
    /// </summary>
    public static DynValue Call(Closure closure, params DynValue[] args)
    {
        var guard = For(closure.OwnerScript)
            ?? throw new InvalidOperationException("Refusing to invoke a Lua closure outside a guarded sandbox.");
        using (guard.EnterCallback())
        {
            return closure.Call(args);
        }
    }

    public void Attach(Script script)
    {
        Guards.AddOrUpdate(script, this);
        script.AttachDebugger(this);
    }

    /// <summary>Must be called on the thread that will run the script, right before execution.</summary>
    public void Begin()
    {
        _executingThreadId = Environment.CurrentManagedThreadId;
        _allocationBaseline = GC.GetAllocatedBytesForCurrentThread();
    }

    /// <summary>Requests termination (e.g. the host gave up waiting).</summary>
    public void Abort(string reason) => _violation ??= reason;

    public void ThrowIfViolated()
    {
        var v = _violation;
        if (v != null) throw new LuaSandboxViolationException(v);
    }

    /// <summary>
    /// Wraps a CLR→Lua re-entry (calling a Lua closure from C#). Bounds nesting depth so recursion
    /// through CLR frames cannot exhaust the native stack.
    /// </summary>
    public CallbackScope EnterCallback()
    {
        ThrowIfViolated();
        if (++_callbackDepth > _options.MaxCallbackDepth)
        {
            _callbackDepth--;
            Trip($"Script exceeded the maximum nested callback depth of {_options.MaxCallbackDepth}.");
        }
        return new CallbackScope(this);
    }

    public readonly struct CallbackScope : IDisposable
    {
        private readonly LuaSandboxGuard _guard;
        internal CallbackScope(LuaSandboxGuard guard) => _guard = guard;
        public void Dispose() => _guard._callbackDepth--;
    }

    private void Trip(string reason)
    {
        _violation ??= reason;
        throw new LuaSandboxViolationException(_violation);
    }

    public bool IsPauseRequested()
    {
        ThrowIfViolated();

        if (Environment.CurrentManagedThreadId == _executingThreadId &&
            GC.GetAllocatedBytesForCurrentThread() - _allocationBaseline > _options.MaxAllocatedBytes)
        {
            Trip($"Script exceeded the memory allocation budget of {_options.MaxAllocatedBytes / (1024 * 1024)} MB.");
        }

        if ((++_instructionCount & 0x3FF) == 0)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Trip("Script execution was cancelled.");
            }
            if (Stopwatch.GetTimestamp() > _deadlineTicks)
            {
                Trip($"Script execution timed out after exceeding {_options.MaxExecutionTime.TotalSeconds:F1}s limit.");
            }
        }

        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            Trip("Script exceeded the maximum call nesting depth.");
        }

        return false;
    }

    // --- Remaining IDebugger members: inert ---
    public DebuggerCaps GetDebuggerCaps() => 0;
    public void SetDebugService(DebugService debugService) { }
    public void SetSourceCode(SourceCode sourceCode) { }
    public void SetByteCode(string[] byteCode) { }
    public bool SignalRuntimeException(ScriptRuntimeException ex) => false;
    public DebuggerAction GetAction(int ip, SourceRef sourceref) => new() { Action = DebuggerAction.ActionType.Run };
    public void SignalExecutionEnded() { }
    public void Update(WatchType watchType, IEnumerable<WatchItem> items) { }
    public List<DynamicExpression> GetWatchItems() => new();
    public void RefreshBreakpoints(IEnumerable<SourceRef> refs) { }
}
