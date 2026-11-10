namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Services;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using App.Shared.Lua.Examples;
using App.Shared.Lua.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class LuaScriptingAndSandboxTests
{
    private readonly InMemoryPlantDataProvider _dataProvider;
    private readonly LuaScriptEngine _engine;

    public LuaScriptingAndSandboxTests()
    {
        _dataProvider = new InMemoryPlantDataProvider();
        _engine = new LuaScriptEngine(_dataProvider, new LuaSandboxOptions
        {
            MaxExecutionTime = TimeSpan.FromSeconds(2)
        });
    }

    [Fact]
    public async Task Sandbox_BlocksDangerousGlobals_OsIoLoadfileAreNil()
    {
        var script = @"
            local has_os = os ~= nil
            local has_io = io ~= nil
            local has_debug = debug ~= nil
            local has_loadfile = loadfile ~= nil
            local has_dofile = dofile ~= nil
            local has_require = require ~= nil
            local has_string_dump = string.dump ~= nil

            return {
                os = has_os,
                io = has_io,
                debug = has_debug,
                loadfile = has_loadfile,
                dofile = has_dofile,
                require = has_require,
                string_dump = has_string_dump
            }
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.True(result.Success, result.Error);
        var table = result.ReturnValue as Dictionary<string, object?>;
        Assert.NotNull(table);
        Assert.False((bool)table["os"]!);
        Assert.False((bool)table["io"]!);
        Assert.False((bool)table["debug"]!);
        Assert.False((bool)table["loadfile"]!);
        Assert.False((bool)table["dofile"]!);
        Assert.False((bool)table["require"]!);
        Assert.False((bool)table["string_dump"]!);
    }

    [Fact]
    public async Task Sandbox_RejectsOversizedScript()
    {
        var smallEngine = new LuaScriptEngine(_dataProvider, new LuaSandboxOptions
        {
            MaxScriptLength = 100
        });

        var oversizedCode = new string('-', 150);
        var result = await smallEngine.ExecuteAsync(oversizedCode);

        Assert.False(result.Success);
        Assert.Contains("maximum allowed length", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sandbox_RejectsBinaryBytecode()
    {
        var bytecodeChunk = "\x1bLua\x51\x00\x01\x04\x08\x04\x08\x00";
        var result = await _engine.ExecuteAsync(bytecodeChunk);

        Assert.False(result.Success);
        Assert.Contains("bytecode", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sandbox_TerminatesInfiniteLoop_WithTimeoutError()
    {
        var script = @"
            while true do
                -- infinite loop
            end
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sandbox_CapturesPrintOutput()
    {
        var script = @"
            print('Line 1: Sandboxed OT system initializing')
            print('Line 2: Rule execution completed')
            return 42
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.True(result.Success);
        Assert.Contains("Line 1: Sandboxed OT system initializing", result.Output);
        Assert.Contains("Line 2: Rule execution completed", result.Output);
        Assert.Equal(42.0, Convert.ToDouble(result.ReturnValue));
    }

    [Fact]
    public async Task MachinesApi_CanQueryAndMutateMachineStatus()
    {
        var script = @"
            local m = machines.get('L01-CNC-01')
            assert(m ~= nil, 'Machine not found')
            print('Fetched: ' .. m.name .. ' Status: ' .. m.status)

            local ok = machines.update_status(m.id, 'maintenance', 'Automated bearing inspection')
            assert(ok == true, 'Status update failed')

            local updated = machines.get(m.id)
            return {
                id = m.id,
                name = m.name,
                old_status = m.status,
                new_status = updated.status
            }
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.True(result.Success, result.Error);
        var table = result.ReturnValue as Dictionary<string, object?>;
        Assert.NotNull(table);
        Assert.Equal("CNC Milling Station 01", table["name"]);
        Assert.Equal("maintenance", table["new_status"]);
    }

    [Fact]
    public async Task GroupingsApi_EvaluatesDynamicRulesAndMemberships()
    {
        var script = @"
            -- Query machine group
            local mg = groupings.get('Line 01', 'machine')
            assert(mg ~= nil, 'Line 01 group not found')

            -- Check membership
            local members = groupings.get_members(mg.id, 'machine')
            print('Line 01 members count: ' .. #members)

            -- Define a dynamic rule
            groupings.define_rule('welding_and_milling', 'machine', function(m)
                return m.machine_type == 'Milling' or m.machine_type == 'Welding'
            end)

            local matched = groupings.evaluate_rule('welding_and_milling')
            return #matched
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2.0, Convert.ToDouble(result.ReturnValue));
    }

    [Fact]
    public async Task TelemetryApi_ComputesMovingAverageAndDetectsSpikes()
    {
        var script = @"
            local machine_id = '11111111-1111-1111-1111-111111111111'
            local latest = telemetry.get_latest(machine_id, 'spindle_vibration')
            assert(latest ~= nil, 'Latest telemetry is nil')

            local history = telemetry.get_history(machine_id, 'spindle_vibration', 5)
            local avg = telemetry.moving_average(history, 3)

            -- Anomaly test with spike
            local spike_check = telemetry.detect_spike(0.25, history, 2.0)

            return {
                latest = latest,
                avg = avg,
                is_spike = spike_check.is_spike
            }
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.True(result.Success, result.Error);
        var table = result.ReturnValue as Dictionary<string, object?>;
        Assert.NotNull(table);
        Assert.True((bool)table["is_spike"]);
    }

    [Fact]
    public async Task TicketsApi_CreatesAutomatedTicket_AndEvaluatesSla()
    {
        var script = @"
            local t = tickets.create({
                title = '[LUA-TEST] Auto ticket test',
                description = 'Preventive maintenance generated by Lua rule',
                machine_id = '11111111-1111-1111-1111-111111111111',
                priority = 'High',
                sla_hours = 4.0
            })

            assert(t ~= nil, 'Ticket was not created')
            local sla = tickets.check_sla(t.id, 4.0)

            return {
                ticket_id = t.id,
                title = t.title,
                status = t.status,
                is_breached = sla.is_breached
            }
        ";

        var result = await _engine.ExecuteAsync(script);

        Assert.True(result.Success, result.Error);
        var table = result.ReturnValue as Dictionary<string, object?>;
        Assert.NotNull(table);
        Assert.Equal("[LUA-TEST] Auto ticket test", table["title"]);
        Assert.Equal("Open", table["status"]);
        Assert.False((bool)table["is_breached"]);
    }

    [Fact]
    public async Task UiApi_RegistersAndInvokesDynamicActionExtensions()
    {
        var script = @"
            ui.register_action({
                id = 'action_zero_calibrate',
                title = 'Zero Calibrate Axis',
                target = 'machine',
                icon = 'i-lucide-crosshair',
                handler = function(ctx)
                    print('Zero calibration invoked for ' .. tostring(ctx and ctx.machine_id))
                    return 'Calibration OK'
                end
            })

            local exts = ui.get_extensions('machine')
            return #exts
        ";

        var result = await _engine.ExecuteAsync(script);
        Assert.True(result.Success, result.Error);
        Assert.Equal(1.0, Convert.ToDouble(result.ReturnValue));
        var registered = Assert.Single(result.UiExtensions);
        Assert.Equal("action_zero_calibrate", registered.Id);
        Assert.Equal("action_zero_calibrate", registered.HandlerFunctionName);

        // Invoke registered action: the owner script is re-executed in a fresh sandbox.
        var owner = new LuaScriptRecord { Code = script, TriggerType = LuaScriptTriggerType.UiExtension };
        var actionResult = await _engine.InvokeUiActionAsync(owner, "action_zero_calibrate", new Dictionary<string, object?>
        {
            ["machine_id"] = "L01-CNC-01"
        });

        Assert.True(actionResult.Success, actionResult.Error);
        Assert.Equal("Calibration OK", actionResult.ReturnValue?.ToString());
        Assert.Contains("Zero calibration invoked for L01-CNC-01", actionResult.Output);
    }

    [Fact]
    public async Task DefaultPlantScripts_AllExecuteSuccessfully()
    {
        var scripts = DefaultPlantScripts.GetInitialScripts();
        Assert.NotEmpty(scripts);

        foreach (var s in scripts)
        {
            var res = await _engine.ExecuteScriptRecordAsync(s);
            Assert.True(res.Success, $"Failed executing script '{s.Name}': {res.Error}");
        }
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = $"LuaScripts_{Guid.NewGuid()}";
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<ILuaPlantDataProvider>(_dataProvider);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task LuaScriptController_EndpointsReturnSuccess()
    {
        using var sp = BuildServices();
        var service = new LuaAutomationService(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<LuaAutomationService>.Instance);
        var controller = new LuaScriptController(service);

        // 1. Create + list scripts (persisted through AppDbContext)
        var createResult = await controller.CreateScript(new LuaScriptRecord
        {
            Name = "SLA scan",
            Code = "return 'ok'",
            TriggerType = LuaScriptTriggerType.SlaPreventiveCheck
        }, default);
        Assert.IsType<CreatedAtActionResult>(createResult);

        var listResult = await controller.ListScripts(default);
        var okList = Assert.IsType<OkObjectResult>(listResult);
        var scripts = Assert.IsAssignableFrom<IEnumerable<LuaScriptRecord>>(okList.Value);
        Assert.Single(scripts);

        // 2. TestExecute snippet
        var execResult = await controller.TestExecute(new ExecuteLuaRequestDto("return 100 + 200"), default);
        var okExec = Assert.IsType<OkObjectResult>(execResult);
        var execData = Assert.IsType<LuaExecutionResult>(okExec.Value);
        Assert.True(execData.Success);
        Assert.Equal(300.0, Convert.ToDouble(execData.ReturnValue));

        // 3. UI Extensions
        var uiResult = await controller.GetUiExtensions(null, default);
        var okUi = Assert.IsType<OkObjectResult>(uiResult);
        Assert.NotNull(okUi.Value);

        // 4. SLA scan runs the stored script and records its execution status
        var slaResult = await controller.EvaluateSlaRules(default);
        var okSla = Assert.IsType<OkObjectResult>(slaResult);
        var slaRuns = Assert.IsAssignableFrom<IReadOnlyList<LuaExecutionResult>>(okSla.Value);
        Assert.Equal("ok", Assert.Single(slaRuns).ReturnValue);
        var stored = Assert.Single(await service.ListScriptsAsync());
        Assert.True(stored.LastExecutionSuccess);

        // 5. Unknown script → 404 rather than a fake 200
        Assert.IsType<NotFoundObjectResult>(await controller.RunScript(Guid.NewGuid(), null, default));
    }

    [Fact]
    public async Task LuaAutomationService_PersistsScripts_AndServesUiActionsFromStoredScript()
    {
        using var sp = BuildServices();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var service = new LuaAutomationService(scopeFactory, NullLogger<LuaAutomationService>.Instance);

        var created = await service.CreateScriptAsync(new LuaScriptRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-0000000000aa"), // client-supplied id must be ignored
            Name = "UI",
            TriggerType = LuaScriptTriggerType.UiExtension,
            Code = @"
                ui.register_action({ id = 'act_1', title = 'Act', handler = function(ctx) return 'done:' .. ctx.machine_id end })
                ui.register_badge({ id = 'badge_1', title = 'B', target = 'ticket' })
            "
        }, createdBy: "engineer@plant");
        Assert.NotEqual(Guid.Parse("00000000-0000-0000-0000-0000000000aa"), created.Id);
        Assert.Equal("engineer@plant", created.CreatedBy);

        // A brand new service instance (e.g. after restart) rebuilds the registry from the database.
        var restarted = new LuaAutomationService(scopeFactory, NullLogger<LuaAutomationService>.Instance);
        var exts = await restarted.GetUiExtensionsAsync();
        Assert.Equal(2, exts.Count);
        Assert.Single(await restarted.GetUiExtensionsAsync(LuaUiTargetZone.TicketDetails));

        var invoke = await restarted.InvokeUiActionAsync("act_1", new Dictionary<string, object?> { ["machine_id"] = "M1" });
        Assert.NotNull(invoke);
        Assert.True(invoke!.Success, invoke.Error);
        Assert.Equal("done:M1", invoke.ReturnValue);

        // Badges are not invocable; unknown ids are not found.
        Assert.Null(await restarted.InvokeUiActionAsync("badge_1"));
        Assert.Null(await restarted.InvokeUiActionAsync("nope"));

        // Deactivating removes the extensions.
        await restarted.UpdateScriptAsync(created.Id, new LuaScriptRecord
        {
            Name = created.Name, Code = created.Code, TriggerType = created.TriggerType, IsActive = false
        });
        Assert.Empty(await restarted.GetUiExtensionsAsync());
        Assert.Null(await restarted.InvokeUiActionAsync("act_1"));

        Assert.True(await restarted.DeleteScriptAsync(created.Id));
        Assert.Empty(await restarted.ListScriptsAsync());
    }

    [Fact]
    public void LuaEnums_SerializeAsStrings_MatchingFrontendContract()
    {
        var json = JsonSerializer.Serialize(new LuaScriptRecord { TriggerType = LuaScriptTriggerType.TelemetryChange },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"triggerType\":\"TelemetryChange\"", json);
        Assert.DoesNotContain("metadataJson", json);

        var ext = JsonSerializer.Serialize(new LuaUiExtension { TargetZone = LuaUiTargetZone.TicketDetails, ExtensionType = LuaUiExtensionType.Badge },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"targetZone\":\"TicketDetails\"", ext);
        Assert.Contains("\"extensionType\":\"Badge\"", ext);

        var roundTrip = JsonSerializer.Deserialize<LuaScriptRecord>("{\"name\":\"x\",\"code\":\"y\",\"triggerType\":\"UiExtension\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(LuaScriptTriggerType.UiExtension, roundTrip!.TriggerType);
    }

    // ---------------------------------------------------------------------------------------------
    // Sandbox escape / resource exhaustion regression tests
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("return os.execute('id')")]
    [InlineData("return io.open('/etc/passwd')")]
    [InlineData("return load('return 1')()")]
    [InlineData("return loadstring('return 1')()")]
    [InlineData("return dofile('/etc/passwd')")]
    [InlineData("return require('os')")]
    [InlineData("return setmetatable({}, {})")]
    [InlineData("return getmetatable('').__index")]
    [InlineData("return rawset(_G, 'x', 1)")]
    [InlineData("return collectgarbage('collect')")]
    [InlineData("return string.dump(print)")]
    [InlineData("return coroutine.create(print)")]
    [InlineData("return pcall(error)")]
    [InlineData("return _MOONSHARP.version")]
    public async Task Sandbox_DangerousFunctionsAreUnavailable(string code)
    {
        var result = await _engine.ExecuteAsync(code);

        Assert.False(result.Success);
        Assert.StartsWith("Lua error:", result.Error);
    }

    [Fact]
    public async Task Sandbox_InfiniteLoopInsideClrCallback_IsTerminated()
    {
        // table.sort swallows exceptions thrown from its comparator; the guard must still fail the run.
        var result = await _engine.ExecuteAsync("table.sort({3, 2, 1}, function(a, b) while true do end end) return 'escaped'");

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sandbox_ReentrantRecursionThroughClr_IsBoundedWithoutCrashingProcess()
    {
        // Without a re-entrancy bound this overflows the native stack and kills the backend process.
        var result = await _engine.ExecuteAsync(@"
            local function f() table.sort({2, 1}, function(a, b) f() return a < b end) end
            f()
            return 'escaped'");

        Assert.False(result.Success);
        Assert.Contains("nested callback depth", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sandbox_DeepLuaRecursion_FailsCleanly()
    {
        var result = await _engine.ExecuteAsync("local function f(n) return f(n + 1) + 1 end return f(1)");

        Assert.False(result.Success);
        Assert.Contains("stack overflow", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("return #string.rep('x', 2^30)")]
    [InlineData("return #(('x'):rep(2^30))")]
    public async Task Sandbox_StringRepMemoryBomb_IsRejected(string code)
    {
        var result = await _engine.ExecuteAsync(code);

        Assert.False(result.Success);
        Assert.Contains("string.rep", result.Error);
    }

    [Theory]
    [InlineData("local s = 'x' for i = 1, 40 do s = s .. s end return #s")]
    [InlineData("local t = {} for i = 1, 1e9 do t[i] = i end return #t")]
    public async Task Sandbox_AllocationBombs_AreTerminated(string code)
    {
        // Small budget + generous deadline so the memory guard (not the timeout) is what fires, even on a loaded CI box.
        var engine = new LuaScriptEngine(_dataProvider, new LuaSandboxOptions
        {
            MaxAllocatedBytes = 16L * 1024 * 1024,
            MaxExecutionTime = TimeSpan.FromSeconds(30)
        });
        var result = await engine.ExecuteAsync(code);

        Assert.False(result.Success);
        Assert.Contains("memory allocation budget", result.Error);
    }

    [Fact]
    public async Task Sandbox_CancellationStopsRunningScript()
    {
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var result = await _engine.ExecuteAsync("while true do end", cancellationToken: cts.Token);

        // The engine deadline is 2s; "cancelled" proves the caller's token stopped the script first.
        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Error);
    }

    [Fact]
    public async Task Sandbox_OutputIsBounded()
    {
        var small = new LuaScriptEngine(_dataProvider, new LuaSandboxOptions { MaxOutputLength = 1000, MaxOutputLines = 5 });
        var result = await small.ExecuteAsync("for i = 1, 100 do print(string.rep('y', 500)) end return 1");

        Assert.True(result.Success, result.Error);
        Assert.True(result.Output.Length < 1100);
        Assert.Contains("[output truncated]", result.Output);
    }

    [Fact]
    public async Task Sandbox_CyclicAndFunctionReturnValues_AreConvertedSafely()
    {
        var result = await _engine.ExecuteAsync("local t = { name = 'n', fn = print } t.self = t return t");

        Assert.True(result.Success, result.Error);
        var table = Assert.IsAssignableFrom<Dictionary<string, object?>>(result.ReturnValue);
        Assert.Equal("n", table["name"]);
        Assert.Null(table["self"]);
        Assert.Equal("<clrfunction>", table["fn"]);
        // Must be serializable by the API layer.
        JsonSerializer.Serialize(result);
    }

    [Fact]
    public async Task Sandbox_ContextCannotShadowHostApis()
    {
        var result = await _engine.ExecuteAsync("return type(machines.get)", new Dictionary<string, object?> { ["machines"] = "hijacked" });

        Assert.True(result.Success, result.Error);
        Assert.Equal("function", result.ReturnValue);
    }

    [Fact]
    public async Task Sandbox_ClosuresDoNotLeakAcrossRuns()
    {
        await _engine.ExecuteAsync("events.on('ping', function() print('stale listener') end)");
        var second = await _engine.ExecuteAsync("return events.trigger('ping', {})");

        Assert.True(second.Success, second.Error);
        Assert.Equal(0.0, Convert.ToDouble(second.ReturnValue));
        Assert.DoesNotContain("stale listener", second.Output);
    }

    [Theory]
    [InlineData("ui.register_action({ id = '../../scripts', title = 'x' })")]
    [InlineData("ui.register_action({ id = 'a/b', title = 'x' })")]
    public async Task UiApi_RejectsUnsafeExtensionIds(string code)
    {
        var result = await _engine.ExecuteAsync(code);

        Assert.False(result.Success);
        Assert.Contains("UI extension id", result.Error);
    }

    [Fact]
    public async Task UiApi_SanitizesIconAndColor()
    {
        var result = await _engine.ExecuteAsync(
            "ui.register_badge({ id = 'b', title = 'T', icon = 'javascript:alert(1)', color = 'red;background:url(x)' })");

        Assert.True(result.Success, result.Error);
        var ext = Assert.Single(result.UiExtensions);
        Assert.Equal("i-lucide-sparkles", ext.Icon);
        Assert.Equal("blue", ext.Color);
    }

    [Fact]
    public async Task DynamicHierarchyGrouping_ExecutesWithJsonElementContext()
    {
        var script = @"
            -- Groupings API: Machines, Users, Telemetry groupings
            print('Evaluating Dynamic Machine & User Groupings...')

            -- Define dynamic grouping rule for critical Tier-1 machines
            groupings.define_rule('tier1_critical_stations', 'machine', function(m)
                return m.machine_type == 'Milling' or m.machine_type == 'Welding'
            end)

            local critical_ids = groupings.evaluate_rule('tier1_critical_stations')
            print('Dynamic critical stations matched: ' .. tostring(#critical_ids))

            -- Query user responders group
            local tech_members = groupings.get_members('tier1-responders', 'user')
            print('Active Tier-1 responding technicians: ' .. tostring(#tech_members))

            return {
                critical_machine_count = #critical_ids,
                technician_count = #tech_members,
                is_test = context_test_mode,
                time = context_simulated_time,
                nested_foo = nested.foo
            }
        ";

        using var jsonDoc = JsonDocument.Parse(@"{ ""context_test_mode"": true, ""context_simulated_time"": ""2026-10-21T19:58:35Z"", ""nested"": { ""foo"": 123 } }");
        var context = new Dictionary<string, object?>();
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
        {
            context[prop.Name] = prop.Value; // This is a JsonElement!
        }

        var result = await _engine.ExecuteAsync(script, context);

        Assert.True(result.Success, result.Error);
        var table = Assert.IsAssignableFrom<Dictionary<string, object?>>(result.ReturnValue);
        Assert.True((bool)table["is_test"]!);
        Assert.Equal("2026-10-21T19:58:35Z", table["time"]);
        Assert.Equal(123.0, Convert.ToDouble(table["nested_foo"]));
    }
}
