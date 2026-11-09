namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Services;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using App.Shared.Lua.Examples;
using App.Shared.Lua.Models;
using Microsoft.AspNetCore.Mvc;
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

        // Invoke registered action
        var actionResult = await _engine.InvokeUiActionAsync("action_zero_calibrate", new Dictionary<string, object?>
        {
            ["machine_id"] = "L01-CNC-01"
        });

        Assert.True(actionResult.Success, actionResult.Error);
        Assert.Equal("Calibration OK", actionResult.ReturnValue?.ToString());
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

    [Fact]
    public async Task LuaScriptController_EndpointsReturnSuccess()
    {
        var service = new LuaAutomationService(_dataProvider, NullLogger<LuaAutomationService>.Instance);
        var controller = new LuaScriptController(service);

        // 1. List scripts
        var listResult = await controller.ListScripts(default);
        var okList = Assert.IsType<OkObjectResult>(listResult);
        var scripts = Assert.IsAssignableFrom<IEnumerable<LuaScriptRecord>>(okList.Value);
        Assert.NotEmpty(scripts);

        // 2. TestExecute snippet
        var execResult = await controller.TestExecute(new ExecuteLuaRequestDto("return 100 + 200"), default);
        var okExec = Assert.IsType<OkObjectResult>(execResult);
        var execData = Assert.IsType<LuaExecutionResult>(okExec.Value);
        Assert.True(execData.Success);
        Assert.Equal(300.0, Convert.ToDouble(execData.ReturnValue));

        // 3. UI Extensions
        var uiResult = controller.GetUiExtensions(null);
        var okUi = Assert.IsType<OkObjectResult>(uiResult);
        Assert.NotNull(okUi.Value);

        // 4. SLA scan
        var slaResult = await controller.EvaluateSlaRules(default);
        var okSla = Assert.IsType<OkObjectResult>(slaResult);
        Assert.NotNull(okSla.Value);
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
