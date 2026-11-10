namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'machines' table.
/// Exposes Machine query, lifecycle mutation, operational state, and dynamic tagging.
/// </summary>
public class MachinesLuaApi
{
    private readonly ILuaPlantDataProvider _provider;

    public MachinesLuaApi(ILuaPlantDataProvider provider)
    {
        _provider = provider;
    }

    public void Register(Script script)
    {
        var table = new Table(script);

        table["get"] = (Func<string, DynValue>)((id) =>
        {
            if (string.IsNullOrWhiteSpace(id)) return DynValue.Nil;
            var machine = _provider.GetMachineAsync(id).GetAwaiter().GetResult();
            return machine == null ? DynValue.Nil : DynValue.NewTable(MachineToTable(script, machine));
        });

        table["list"] = (Func<Table, DynValue>)((filter) =>
        {
            string? status = filter?["status"] as string;
            string? machineType = filter?["machine_type"] as string ?? filter?["machineType"] as string;
            string? lineGroupId = filter?["line_group_id"] as string ?? filter?["groupId"] as string;

            var machines = _provider.ListMachinesAsync(status, machineType, lineGroupId).GetAwaiter().GetResult();
            var resultList = new Table(script);
            int idx = 1;
            foreach (var m in machines)
            {
                resultList[idx++] = MachineToTable(script, m);
            }
            return DynValue.NewTable(resultList);
        });

        table["update_status"] = (Func<string, string, string, bool>)((id, status, reason) =>
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status)) return false;
            return _provider.UpdateMachineStatusAsync(id, status, reason).GetAwaiter().GetResult();
        });

        table["set_property"] = (Func<string, string, DynValue, bool>)((id, key, value) =>
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key)) return false;
            object? clrVal = LuaScriptEngine.ConvertDynValue(value, maxNodes: 256);
            return _provider.SetMachineCustomFieldAsync(id, key, clrVal).GetAwaiter().GetResult();
        });

        table["get_property"] = (Func<string, string, DynValue>)((id, key) =>
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key)) return DynValue.Nil;
            var val = _provider.GetMachineCustomFieldAsync(id, key).GetAwaiter().GetResult();
            return LuaScriptEngine.ToDynValue(script, val);
        });

        table["is_operational"] = (Func<string, bool>)((id) =>
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            var machine = _provider.GetMachineAsync(id).GetAwaiter().GetResult();
            if (machine == null) return false;
            return string.Equals(machine.Status, "active", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(machine.OperationalState, "Running", StringComparison.OrdinalIgnoreCase);
        });

        script.Globals["machines"] = table;
    }

    private static Table MachineToTable(Script script, Machine m)
    {
        var t = new Table(script);
        t["id"] = m.Id.ToString();
        t["name"] = m.Name;
        t["display_name"] = m.DisplayName ?? m.Name;
        t["custom_identifier"] = m.CustomIdentifier;
        t["machine_type"] = m.MachineType ?? string.Empty;
        t["status"] = m.Status;
        t["operational_state"] = m.OperationalState;
        t["criticality"] = m.Criticality;
        t["spoc"] = m.Spoc ?? string.Empty;
        t["spoc_user_id"] = m.SpocUserId ?? string.Empty;
        t["owner"] = m.Owner ?? string.Empty;
        t["group_id"] = m.GroupId ?? string.Empty;
        t["cell_type"] = m.CellType ?? string.Empty;

        var techTable = new Table(script);
        if (m.Technologies != null)
        {
            int tIdx = 1;
            foreach (var tech in m.Technologies)
            {
                techTable[tIdx++] = tech;
            }
        }
        t["technologies"] = techTable;

        return t;
    }
}
