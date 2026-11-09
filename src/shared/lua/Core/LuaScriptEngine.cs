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
using App.Shared.Lua.Apis;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoonSharp.Interpreter;

/// <summary>
/// Safe sandboxed Lua Script Engine using MoonSharp.
/// Enforces isolation, strips dangerous modules (os, io, debug),
/// captures output, bounds execution duration, and provides domain APIs.
/// </summary>
public class LuaScriptEngine : ILuaScriptEngine
{
    private readonly ILuaPlantDataProvider _dataProvider;
    private readonly LuaSandboxOptions _options;
    private readonly ILogger<LuaScriptEngine> _logger;

    public ILuaPlantDataProvider DataProvider => _dataProvider;
    public MachinesLuaApi MachinesApi { get; }
    public GroupingsLuaApi GroupingsApi { get; }
    public TelemetryLuaApi TelemetryApi { get; }
    public TicketsLuaApi TicketsApi { get; }
    public UiLuaApi UiApi { get; }
    public EventsLuaApi EventsApi { get; }

    public LuaScriptEngine(
        ILuaPlantDataProvider? dataProvider = null,
        LuaSandboxOptions? options = null,
        ILogger<LuaScriptEngine>? logger = null,
        UiLuaApi? uiApi = null,
        EventsLuaApi? eventsApi = null)
    {
        _dataProvider = dataProvider ?? new InMemoryPlantDataProvider();
        _options = options ?? new LuaSandboxOptions();
        _logger = logger ?? NullLogger<LuaScriptEngine>.Instance;

        MachinesApi = new MachinesLuaApi(_dataProvider);
        GroupingsApi = new GroupingsLuaApi(_dataProvider);
        TelemetryApi = new TelemetryLuaApi(_dataProvider);
        TicketsApi = new TicketsLuaApi(_dataProvider);
        UiApi = uiApi ?? new UiLuaApi();
        EventsApi = eventsApi ?? new EventsLuaApi();
    }

    static LuaScriptEngine()
    {
        Script.GlobalOptions.Platform = new MoonSharp.Interpreter.Platforms.LimitedPlatformAccessor();
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion(typeof(JsonElement), (s, obj) => JsonElementToDynValue(s, (JsonElement)obj));
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion(typeof(JsonDocument), (s, obj) => JsonElementToDynValue(s, ((JsonDocument)obj).RootElement));
    }

    /// <summary>
    /// Robust recursive converter for CLR primitives, JsonElement, collections, and custom objects into MoonSharp DynValue.
    /// </summary>
    public static DynValue ToDynValue(Script script, object? value)
    {
        if (value == null) return DynValue.Nil;
        if (value is DynValue dv) return dv;

        if (value is JsonElement je)
        {
            return JsonElementToDynValue(script, je);
        }

        if (value is JsonDocument jd)
        {
            return JsonElementToDynValue(script, jd.RootElement);
        }

        if (value is string s) return DynValue.NewString(s);
        if (value is bool b) return DynValue.NewBoolean(b);

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
                table[keyStr] = ToDynValue(script, entry.Value);
            }
            return DynValue.NewTable(table);
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var table = new Table(script);
            int idx = 1;
            foreach (var item in enumerable)
            {
                table[idx++] = ToDynValue(script, item);
            }
            return DynValue.NewTable(table);
        }

        try
        {
            return DynValue.FromObject(script, value);
        }
        catch
        {
            return DynValue.NewString(value.ToString() ?? string.Empty);
        }
    }

    /// <summary>
    /// Converts a System.Text.Json JsonElement into a Lua primitive or nested Table.
    /// </summary>
    public static DynValue JsonElementToDynValue(Script script, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return DynValue.Nil;
            case JsonValueKind.True:
                return DynValue.NewBoolean(true);
            case JsonValueKind.False:
                return DynValue.NewBoolean(false);
            case JsonValueKind.Number:
                return element.TryGetDouble(out double d) ? DynValue.NewNumber(d) : DynValue.NewNumber(0.0);
            case JsonValueKind.String:
                return DynValue.NewString(element.GetString() ?? string.Empty);
            case JsonValueKind.Array:
                var arrTable = new Table(script);
                int idx = 1;
                foreach (var item in element.EnumerateArray())
                {
                    arrTable[idx++] = JsonElementToDynValue(script, item);
                }
                return DynValue.NewTable(arrTable);
            case JsonValueKind.Object:
                var objTable = new Table(script);
                foreach (var prop in element.EnumerateObject())
                {
                    objTable[prop.Name] = JsonElementToDynValue(script, prop.Value);
                }
                return DynValue.NewTable(objTable);
            default:
                return DynValue.Nil;
        }
    }

    /// <summary>
    /// Configures a clean sandboxed MoonSharp script environment.
    /// </summary>
    public Script CreateSandboxedScript(List<string> logs)
    {
        // Preset_HardSandbox isolates OS, File IO, Debug, Time and compilation modules
        var script = new Script(CoreModules.Preset_HardSandbox);

        // Explicitly sanitize and eliminate any risky globals
        script.Globals["os"] = DynValue.Nil;
        script.Globals["io"] = DynValue.Nil;
        script.Globals["debug"] = DynValue.Nil;
        script.Globals["loadfile"] = DynValue.Nil;
        script.Globals["dofile"] = DynValue.Nil;
        script.Globals["load"] = DynValue.Nil;
        script.Globals["loadstring"] = DynValue.Nil;
        script.Globals["package"] = DynValue.Nil;
        script.Globals["require"] = DynValue.Nil;

        // Strip bytecode dumping from string module
        if (script.Globals.Get("string").Type == DataType.Table)
        {
            script.Globals.Get("string").Table["dump"] = DynValue.Nil;
        }

        // Redirect print to capture standard output
        script.Options.DebugPrint = s =>
        {
            lock (logs)
            {
                if (logs.Count < 500)
                {
                    logs.Add(s);
                }
            }
        };

        // Redirect print to capture standard output using CallbackFunction
        script.Globals["print"] = new CallbackFunction((context, args) =>
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0) sb.Append('\t');
                sb.Append(args[i].ToPrintString());
            }
            string str = sb.ToString();
            lock (logs)
            {
                if (logs.Count < 500)
                {
                    logs.Add(str);
                }
            }
            return DynValue.Nil;
        });

        // Register domain APIs
        MachinesApi.Register(script);
        GroupingsApi.Register(script);
        TelemetryApi.Register(script);
        TicketsApi.Register(script);
        UiApi.Register(script);
        EventsApi.Register(script);

        return script;
    }

    public static object? ConvertDynValue(DynValue dyn)
    {
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
            case DataType.Table:
                var table = dyn.Table;
                bool isArray = table.Length > 0 && table.Keys.All(k => k.Type == DataType.Number);
                if (isArray)
                {
                    var list = new List<object?>();
                    for (int i = 1; i <= table.Length; i++)
                    {
                        list.Add(ConvertDynValue(table.Get(i)));
                    }
                    return list;
                }
                else
                {
                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in table.Pairs)
                    {
                        dict[pair.Key.ToPrintString()] = ConvertDynValue(pair.Value);
                    }
                    return dict;
                }
            default:
                return dyn.ToObject();
        }
    }

    public async Task<LuaExecutionResult> ExecuteAsync(
        string code,
        Dictionary<string, object?>? globalVariables = null,
        CancellationToken cancellationToken = default)
    {
        var logs = new List<string>();
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(code))
        {
            return new LuaExecutionResult
            {
                Success = true,
                Output = string.Empty,
                Duration = TimeSpan.Zero
            };
        }

        // Hard security checks: enforce size bounds and block binary bytecode chunks
        if (code.Length > _options.MaxScriptLength)
        {
            return new LuaExecutionResult
            {
                Success = false,
                Error = $"Script exceeds maximum allowed length of {_options.MaxScriptLength} characters.",
                Duration = TimeSpan.Zero
            };
        }

        if (code.StartsWith('\x1b') || code.Contains('\0'))
        {
            return new LuaExecutionResult
            {
                Success = false,
                Error = "Precompiled binary Lua bytecode chunks are prohibited for security.",
                Duration = TimeSpan.Zero
            };
        }

        try
        {
            var script = CreateSandboxedScript(logs);

            // Bind contextual variables
            if (globalVariables != null)
            {
                foreach (var (k, v) in globalVariables)
                {
                    script.Globals[k] = ToDynValue(script, v);
                }
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.MaxExecutionTime);

            // Run in Task with timeout safeguard
            var executionTask = Task.Run(() =>
            {
                return script.DoString(code);
            }, cts.Token);

            var completedTask = await Task.WhenAny(executionTask, Task.Delay(_options.MaxExecutionTime, cts.Token));

            if (completedTask != executionTask)
            {
                sw.Stop();
                return new LuaExecutionResult
                {
                    Success = false,
                    Error = $"Script execution timed out after exceeding {_options.MaxExecutionTime.TotalSeconds:F1}s limit.",
                    Output = string.Join("\n", logs),
                    Duration = sw.Elapsed,
                    Logs = logs
                };
            }

            var dynResult = await executionTask;
            sw.Stop();

            return new LuaExecutionResult
            {
                Success = true,
                ReturnValue = ConvertDynValue(dynResult),
                Output = string.Join("\n", logs),
                Duration = sw.Elapsed,
                Logs = logs
            };
        }
        catch (InterpreterException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Lua interpreter execution error");
            return new LuaExecutionResult
            {
                Success = false,
                Error = $"Lua error: {ex.DecoratedMessage ?? ex.Message}",
                Output = string.Join("\n", logs),
                Duration = sw.Elapsed,
                Logs = logs
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Unexpected error executing sandboxed Lua script");
            return new LuaExecutionResult
            {
                Success = false,
                Error = $"Execution error: {ex.Message}",
                Output = string.Join("\n", logs),
                Duration = sw.Elapsed,
                Logs = logs
            };
        }
    }

    public async Task<LuaExecutionResult> ExecuteScriptRecordAsync(
        LuaScriptRecord scriptRecord,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(scriptRecord.Code, context, cancellationToken);

        scriptRecord.LastExecutedAt = DateTimeOffset.UtcNow;
        scriptRecord.LastExecutionSuccess = result.Success;
        scriptRecord.LastExecutionError = result.Error;

        return result;
    }

    public IReadOnlyCollection<LuaUiExtension> GetUiExtensions(LuaUiTargetZone? zone = null)
    {
        return zone.HasValue ? UiApi.GetExtensionsForZone(zone.Value) : UiApi.GetAllExtensions();
    }

    public async Task<LuaExecutionResult> InvokeUiActionAsync(
        string extensionId,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var logs = new List<string>();
        var sw = Stopwatch.StartNew();

        try
        {
            var dynResult = UiApi.InvokeAction(extensionId, context);
            sw.Stop();

            return new LuaExecutionResult
            {
                Success = true,
                ReturnValue = ConvertDynValue(dynResult),
                Output = string.Join("\n", logs),
                Duration = sw.Elapsed,
                Logs = logs
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new LuaExecutionResult
            {
                Success = false,
                Error = ex.Message,
                Output = string.Join("\n", logs),
                Duration = sw.Elapsed,
                Logs = logs
            };
        }
    }
}
