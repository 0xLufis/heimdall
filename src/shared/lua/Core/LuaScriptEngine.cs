namespace App.Shared.Lua.Core;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;
using App.Shared.Lua.Apis;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoonSharp.Interpreter;

/// <summary>
/// Safe sandboxed Lua Script Engine using MoonSharp.
///
/// Isolation model:
/// - Only the <see cref="CoreModules.Preset_HardSandbox"/> modules are loaded (no io/os/debug/load*/
///   metatables/coroutines/pcall), and residual risky globals are removed explicitly.
/// - No CLR types are registered as userdata; scripts only see plain tables and the domain API callbacks.
/// - Every run gets a fresh <see cref="Script"/> and a <see cref="LuaSandboxGuard"/> that bounds wall-clock
///   time, allocations and CLR re-entrancy at instruction granularity.
/// - Output, string building and host-visible return values are size-bounded.
/// </summary>
public class LuaScriptEngine : ILuaScriptEngine
{
    /// <summary>Globals the host injects; caller-supplied context may not shadow them.</summary>
    private static readonly HashSet<string> ReservedGlobals = new(StringComparer.Ordinal)
    {
        "machines", "groupings", "telemetry", "tickets", "ui", "events", "print", "string", "table",
        "math", "bit32", "_G", "_VERSION", "assert", "error", "ipairs", "pairs", "next", "select",
        "tonumber", "tostring", "type", "unpack", "pack"
    };

    private static readonly JsonSerializerOptions DataProjectionOptions = new()
    {
        MaxDepth = 8,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private readonly ILuaPlantDataProvider _dataProvider;
    private readonly LuaSandboxOptions _options;
    private readonly ILogger<LuaScriptEngine> _logger;

    public ILuaPlantDataProvider DataProvider => _dataProvider;
    public MachinesLuaApi MachinesApi { get; }
    public GroupingsLuaApi GroupingsApi { get; }
    public TelemetryLuaApi TelemetryApi { get; }
    public TicketsLuaApi TicketsApi { get; }

    public LuaScriptEngine(
        ILuaPlantDataProvider dataProvider,
        LuaSandboxOptions? options = null,
        ILogger<LuaScriptEngine>? logger = null)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _options = options ?? new LuaSandboxOptions();
        _logger = logger ?? NullLogger<LuaScriptEngine>.Instance;

        MachinesApi = new MachinesLuaApi(_dataProvider);
        GroupingsApi = new GroupingsLuaApi(_dataProvider);
        TelemetryApi = new TelemetryLuaApi(_dataProvider);
        TicketsApi = new TicketsLuaApi(_dataProvider);
    }

    static LuaScriptEngine()
    {
        Script.GlobalOptions.Platform = new MoonSharp.Interpreter.Platforms.LimitedPlatformAccessor();
    }

    /// <summary>
    /// Converts CLR primitives, JsonElement and collections into MoonSharp values.
    /// Arbitrary CLR objects are never exposed as userdata.
    /// </summary>
    public static DynValue ToDynValue(Script script, object? value, int depth = 0)
    {
        if (value == null) return DynValue.Nil;
        if (depth > 32) return DynValue.Nil;
        if (value is DynValue dv) return dv;

        if (value is JsonElement je) return JsonElementToDynValue(script, je, depth);
        if (value is JsonDocument jd) return JsonElementToDynValue(script, jd.RootElement, depth);

        if (value is string s) return DynValue.NewString(s);
        if (value is bool b) return DynValue.NewBoolean(b);
        if (value is Guid g) return DynValue.NewString(g.ToString());
        if (value is DateTimeOffset dto) return DynValue.NewString(dto.ToString("o"));
        if (value is DateTime dt) return DynValue.NewString(dt.ToString("o"));

        if (value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return DynValue.NewNumber(Convert.ToDouble(value));
        }

        if (value is IDictionary dict)
        {
            var table = new Table(script);
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? string.Empty;
                table[keyStr] = ToDynValue(script, entry.Value, depth + 1);
            }
            return DynValue.NewTable(table);
        }

        if (value is IEnumerable enumerable)
        {
            var table = new Table(script);
            int idx = 1;
            foreach (var item in enumerable)
            {
                table[idx++] = ToDynValue(script, item, depth + 1);
            }
            return DynValue.NewTable(table);
        }

        // Plain data objects: project through JSON so no CLR object (and none of its methods) leaks into Lua.
        try
        {
            var element = JsonSerializer.SerializeToElement(value, value.GetType(), DataProjectionOptions);
            return JsonElementToDynValue(script, element, depth);
        }
        catch (Exception)
        {
            return DynValue.NewString(value.ToString() ?? string.Empty);
        }
    }

    /// <summary>
    /// Converts a System.Text.Json JsonElement into a Lua primitive or nested Table.
    /// </summary>
    public static DynValue JsonElementToDynValue(Script script, JsonElement element, int depth = 0)
    {
        if (depth > 32) return DynValue.Nil;
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return DynValue.True;
            case JsonValueKind.False:
                return DynValue.False;
            case JsonValueKind.Number:
                return element.TryGetDouble(out double d) ? DynValue.NewNumber(d) : DynValue.NewNumber(0.0);
            case JsonValueKind.String:
                return DynValue.NewString(element.GetString() ?? string.Empty);
            case JsonValueKind.Array:
                var arrTable = new Table(script);
                int idx = 1;
                foreach (var item in element.EnumerateArray())
                {
                    arrTable[idx++] = JsonElementToDynValue(script, item, depth + 1);
                }
                return DynValue.NewTable(arrTable);
            case JsonValueKind.Object:
                var objTable = new Table(script);
                foreach (var prop in element.EnumerateObject())
                {
                    objTable[prop.Name] = JsonElementToDynValue(script, prop.Value, depth + 1);
                }
                return DynValue.NewTable(objTable);
            default:
                return DynValue.Nil;
        }
    }

    /// <summary>
    /// Converts a Lua value into plain CLR data (null, bool, double, string, List, Dictionary).
    /// Bounded by <paramref name="maxNodes"/> and depth; cyclic tables are cut. Functions and
    /// other non-data values become their type name so no interpreter object reaches the host.
    /// </summary>
    public static object? ConvertDynValue(DynValue dyn, int maxNodes = 10_000)
    {
        int budget = maxNodes;
        return ConvertDynValue(dyn, ref budget, 0, new HashSet<Table>(ReferenceEqualityComparer.Instance));
    }

    private static object? ConvertDynValue(DynValue dyn, ref int budget, int depth, HashSet<Table> visiting)
    {
        if (--budget < 0) return null;

        switch (dyn.Type)
        {
            case DataType.Nil:
            case DataType.Void:
                return null;
            case DataType.Boolean:
                return dyn.Boolean;
            case DataType.Number:
                return dyn.Number;
            case DataType.String:
                return dyn.String;
            case DataType.Tuple:
                return dyn.Tuple.Length > 0 ? ConvertDynValue(dyn.Tuple[0], ref budget, depth, visiting) : null;
            case DataType.Table:
                var table = dyn.Table;
                if (depth >= 32 || !visiting.Add(table)) return null;
                try
                {
                    int length = table.Length;
                    bool isArray = length > 0 && table.Keys.All(k => k.Type == DataType.Number);
                    if (isArray)
                    {
                        var list = new List<object?>();
                        for (int i = 1; i <= length && budget > 0; i++)
                        {
                            list.Add(ConvertDynValue(table.Get(i), ref budget, depth + 1, visiting));
                        }
                        return list;
                    }

                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in table.Pairs)
                    {
                        if (budget <= 0) break;
                        dict[pair.Key.ToPrintString()] = ConvertDynValue(pair.Value, ref budget, depth + 1, visiting);
                    }
                    return dict;
                }
                finally
                {
                    visiting.Remove(table);
                }
            default:
                return $"<{dyn.Type.ToString().ToLowerInvariant()}>";
        }
    }

    /// <summary>
    /// Builds a fresh sandboxed MoonSharp script with domain APIs and bounded output capture.
    /// </summary>
    private Script CreateSandboxedScript(LuaOutputBuffer output, UiLuaApi uiApi)
    {
        var script = new Script(CoreModules.Preset_HardSandbox);
        var globals = script.Globals;

        // Preset_HardSandbox already omits io/os/debug/load*/require/metatables/coroutines/pcall;
        // remove anything that may still be present or leaks host details.
        foreach (var name in new[]
                 {
                     "os", "io", "debug", "loadfile", "dofile", "load", "loadsafe", "loadstring", "loadfilesafe",
                     "package", "require", "collectgarbage", "_MOONSHARP", "getmetatable", "setmetatable",
                     "rawget", "rawset", "rawequal", "rawlen", "coroutine", "pcall", "xpcall", "dynamic", "json"
                 })
        {
            globals[name] = DynValue.Nil;
        }

        // string.rep is the one stdlib primitive that can allocate gigabytes in a single instruction.
        var stringLib = globals.Get("string").Table;
        stringLib["dump"] = DynValue.Nil;
        int maxStringLength = _options.MaxStringLength;
        stringLib["rep"] = DynValue.NewCallback((_, args) =>
        {
            var s = args.AsType(0, "rep", DataType.String).String;
            var n = (long)args.AsType(1, "rep", DataType.Number).Number;
            var sep = args.Count > 2 && args[2].Type == DataType.String ? args[2].String : string.Empty;
            if (n <= 0) return DynValue.NewString(string.Empty);
            long total = s.Length * n + sep.Length * (n - 1);
            if (total > maxStringLength)
            {
                throw new ScriptRuntimeException($"string.rep result exceeds the {maxStringLength} character limit.");
            }
            var sb = new StringBuilder((int)total);
            for (long i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(sep);
                sb.Append(s);
            }
            return DynValue.NewString(sb.ToString());
        }, "rep");

        // Built-ins that call back into Lua from CLR frames: bound their nesting depth.
        WrapReentrant(stringLib, "gsub");
        WrapReentrant(globals.Get("table").Table, "sort");

        globals["print"] = DynValue.NewCallback((_, args) =>
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0) sb.Append('\t');
                sb.Append(args[i].ToPrintString());
            }
            output.Append(sb.ToString());
            return DynValue.Nil;
        }, "print");
        script.Options.DebugPrint = output.Append;

        MachinesApi.Register(script);
        GroupingsApi.Register(script);
        TelemetryApi.Register(script);
        TicketsApi.Register(script);
        uiApi.Register(script);
        new EventsLuaApi().Register(script);

        return script;
    }

    private static void WrapReentrant(Table lib, string name)
    {
        var original = lib.Get(name);
        if (original.Type != DataType.ClrFunction) return;
        var callback = original.Callback;
        lib[name] = DynValue.NewCallback((ctx, args) =>
        {
            var guard = LuaSandboxGuard.For(ctx.GetScript())
                ?? throw new InvalidOperationException("Sandbox guard missing.");
            using (guard.EnterCallback())
            {
                return callback.Invoke(ctx, args.GetArray(), args.IsMethodCall);
            }
        }, name);
    }

    public Task<LuaExecutionResult> ExecuteAsync(
        string code,
        Dictionary<string, object?>? globalVariables = null,
        CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(code, globalVariables, afterRun: null, cancellationToken);

    public async Task<LuaExecutionResult> ExecuteScriptRecordAsync(
        LuaScriptRecord scriptRecord,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(scriptRecord.Code, context, cancellationToken);

        scriptRecord.LastExecutedAt = DateTimeOffset.UtcNow;
        scriptRecord.LastExecutionSuccess = result.Success;
        scriptRecord.LastExecutionError = result.Error is { Length: > 4000 } e ? e[..4000] : result.Error;

        return result;
    }

    public Task<LuaExecutionResult> InvokeUiActionAsync(
        LuaScriptRecord ownerScript,
        string extensionId,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        // The owning script is re-executed in a fresh sandbox so its declarations (and the handler
        // closure) are rebuilt, then the handler runs within the same budget.
        return ExecuteCoreAsync(
            ownerScript.Code,
            globalVariables: null,
            afterRun: (script, ui) => ui.InvokeAction(script, extensionId, context),
            cancellationToken);
    }

    private async Task<LuaExecutionResult> ExecuteCoreAsync(
        string code,
        Dictionary<string, object?>? globalVariables,
        Func<Script, UiLuaApi, DynValue>? afterRun,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new LuaExecutionResult { Success = true };
        }

        if (code.Length > _options.MaxScriptLength)
        {
            return LuaExecutionResult.FromError($"Script exceeds maximum allowed length of {_options.MaxScriptLength} characters.");
        }

        if (code.StartsWith('\x1b') || code.Contains('\0'))
        {
            return LuaExecutionResult.FromError("Precompiled binary Lua bytecode chunks are prohibited for security.");
        }

        var output = new LuaOutputBuffer(_options.MaxOutputLines, _options.MaxOutputLength);
        var uiApi = new UiLuaApi();
        var guard = new LuaSandboxGuard(_options, cancellationToken);
        var sw = Stopwatch.StartNew();

        LuaExecutionResult Fail(string error) => new()
        {
            Success = false,
            Error = error,
            Output = output.ToString(),
            Logs = output.Lines,
            Duration = sw.Elapsed
        };

        Script script;
        try
        {
            script = CreateSandboxedScript(output, uiApi);
            guard.Attach(script);

            if (globalVariables != null)
            {
                foreach (var (k, v) in globalVariables)
                {
                    if (string.IsNullOrEmpty(k) || ReservedGlobals.Contains(k)) continue;
                    script.Globals[k] = ToDynValue(script, v);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare Lua sandbox");
            return Fail("Failed to prepare the Lua sandbox.");
        }

        var executionTask = Task.Run(() =>
        {
            guard.Begin();
            var value = script.DoString(code);
            guard.ThrowIfViolated();
            if (afterRun != null)
            {
                value = afterRun(script, uiApi);
                guard.ThrowIfViolated();
            }
            return value;
        }, CancellationToken.None);

        using (var delayCts = new CancellationTokenSource())
        {
            var watchdog = Task.Delay(_options.MaxExecutionTime + _options.AbandonGracePeriod, delayCts.Token);
            var completed = await Task.WhenAny(executionTask, watchdog).ConfigureAwait(false);
            delayCts.Cancel();

            if (completed != executionTask)
            {
                // The script is stuck inside a single CLR call the instruction guard cannot preempt.
                // Mark it aborted so it stops at its next instruction and observe its eventual fault.
                guard.Abort("Script execution was abandoned after exceeding its time budget.");
                _ = executionTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _logger.LogWarning("Lua script exceeded its time budget inside a host call and was abandoned");
                return Fail($"Script execution timed out after exceeding {_options.MaxExecutionTime.TotalSeconds:F1}s limit.");
            }
        }

        try
        {
            var dynResult = await executionTask.ConfigureAwait(false);
            sw.Stop();
            return new LuaExecutionResult
            {
                Success = true,
                ReturnValue = ConvertDynValue(dynResult, _options.MaxReturnValueNodes),
                Output = output.ToString(),
                Logs = output.Lines,
                Duration = sw.Elapsed,
                UiExtensions = uiApi.GetAllExtensions()
            };
        }
        catch (Exception ex) when (guard.Violation != null)
        {
            // Covers violations surfacing directly and ones swallowed/wrapped by CLR frames (e.g. table.sort).
            _logger.LogInformation("Lua script terminated by sandbox guard: {Reason} ({Type})", guard.Violation, ex.GetType().Name);
            return Fail(guard.Violation);
        }
        catch (InterpreterException ex)
        {
            return Fail($"Lua error: {ex.DecoratedMessage ?? ex.Message}");
        }
        catch (IndexOutOfRangeException)
        {
            // MoonSharp's fixed-size VM stacks overflow this way on unbounded Lua recursion.
            return Fail("Lua error: stack overflow (recursion too deep).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing sandboxed Lua script");
            return Fail($"Execution error: {ex.Message}");
        }
    }

    /// <summary>Bounded, thread-safe capture of print output.</summary>
    private sealed class LuaOutputBuffer
    {
        private readonly int _maxLines;
        private readonly int _maxChars;
        private readonly List<string> _lines = new();
        private int _chars;
        private bool _truncated;

        public LuaOutputBuffer(int maxLines, int maxChars)
        {
            _maxLines = maxLines;
            _maxChars = maxChars;
        }

        public List<string> Lines
        {
            get { lock (_lines) return new List<string>(_lines); }
        }

        public void Append(string line)
        {
            lock (_lines)
            {
                if (_truncated) return;
                int remaining = _maxChars - _chars;
                if (_lines.Count >= _maxLines || remaining <= 0)
                {
                    _lines.Add("[output truncated]");
                    _truncated = true;
                    return;
                }
                if (line.Length > remaining) line = line[..remaining];
                _lines.Add(line);
                _chars += line.Length;
            }
        }

        public override string ToString()
        {
            lock (_lines) return string.Join("\n", _lines);
        }
    }
}
