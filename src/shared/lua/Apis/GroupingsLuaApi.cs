namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'groupings' table.
/// Exposes Universal Grouping abstraction for Machines, Users/Technicians, Telemetry, and Generic sets,
/// plus programmatic dynamic rule-based grouping evaluation.
/// </summary>
public class GroupingsLuaApi
{
    private readonly ILuaPlantDataProvider _provider;
    private readonly ConcurrentDictionary<string, (string GroupType, Closure Predicate)> _dynamicRules = new(StringComparer.OrdinalIgnoreCase);

    public GroupingsLuaApi(ILuaPlantDataProvider provider)
    {
        _provider = provider;
    }

    public void Register(Script script)
    {
        var table = new Table(script);

        table["get"] = (Func<string, string, DynValue>)((id, groupType) =>
        {
            if (string.IsNullOrWhiteSpace(id)) return DynValue.Nil;
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            var group = _provider.GetGroupAsync(id, gType).GetAwaiter().GetResult();
            if (group == null) return DynValue.Nil;

            if (group is MachineGroup mg)
            {
                var t = new Table(script);
                t["id"] = mg.Id.ToString();
                t["name"] = mg.Name;
                t["description"] = mg.Description ?? string.Empty;
                t["color"] = mg.Color ?? string.Empty;
                t["parent_id"] = mg.ParentId?.ToString() ?? string.Empty;
                t["group_type"] = "machine";
                return DynValue.NewTable(t);
            }

            return LuaScriptEngine.ToDynValue(script, group);
        });

        table["list"] = (Func<string, DynValue>)((groupType) =>
        {
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            var groups = _provider.ListGroupsAsync(gType).GetAwaiter().GetResult();
            var resultList = new Table(script);
            int idx = 1;
            foreach (var g in groups)
            {
                if (g is MachineGroup mg)
                {
                    var t = new Table(script);
                    t["id"] = mg.Id.ToString();
                    t["name"] = mg.Name;
                    t["group_type"] = "machine";
                    resultList[idx++] = t;
                }
                else
                {
                    resultList[idx++] = LuaScriptEngine.ToDynValue(script, g);
                }
            }
            return DynValue.NewTable(resultList);
        });

        table["get_members"] = (Func<string, string, DynValue>)((groupId, groupType) =>
        {
            if (string.IsNullOrWhiteSpace(groupId)) return DynValue.NewTable(new Table(script));
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            var members = _provider.GetGroupMembersAsync(groupId, gType).GetAwaiter().GetResult();
            var resultList = new Table(script);
            int idx = 1;
            foreach (var m in members)
            {
                resultList[idx++] = LuaScriptEngine.ToDynValue(script, m);
            }
            return DynValue.NewTable(resultList);
        });

        table["add_member"] = (Func<string, string, string, bool>)((groupId, memberId, groupType) =>
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(memberId)) return false;
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            return _provider.AddGroupMemberAsync(groupId, memberId, gType).GetAwaiter().GetResult();
        });

        table["remove_member"] = (Func<string, string, string, bool>)((groupId, memberId, groupType) =>
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(memberId)) return false;
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            return _provider.RemoveGroupMemberAsync(groupId, memberId, gType).GetAwaiter().GetResult();
        });

        table["is_member"] = (Func<string, string, string, bool>)((groupId, memberId, groupType) =>
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(memberId)) return false;
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            return _provider.IsGroupMemberAsync(groupId, memberId, gType).GetAwaiter().GetResult();
        });

        // Dynamic rule definition: Allows user script to declare programmatically evaluated groups
        table["define_rule"] = (Func<string, string, Closure, bool>)((ruleName, groupType, predicate) =>
        {
            if (string.IsNullOrWhiteSpace(ruleName) || predicate == null) return false;
            string gType = string.IsNullOrWhiteSpace(groupType) ? "machine" : groupType;
            _dynamicRules[ruleName] = (gType, predicate);
            return true;
        });

        // Evaluates dynamic rule against all machines (or items) and returns matching IDs
        table["evaluate_rule"] = (Func<string, DynValue>)((ruleName) =>
        {
            if (string.IsNullOrWhiteSpace(ruleName) || !_dynamicRules.TryGetValue(ruleName, out var rule))
            {
                return DynValue.NewTable(new Table(script));
            }

            var resultTable = new Table(script);
            int idx = 1;

            if (string.Equals(rule.GroupType, "machine", StringComparison.OrdinalIgnoreCase))
            {
                var machines = _provider.ListMachinesAsync().GetAwaiter().GetResult();
                foreach (var m in machines)
                {
                    try
                    {
                        var mTable = new Table(script);
                        mTable["id"] = m.Id.ToString();
                        mTable["name"] = m.Name;
                        mTable["status"] = m.Status;
                        mTable["machine_type"] = m.MachineType ?? string.Empty;

                        var res = rule.Predicate.Call(mTable);
                        if (res.Boolean)
                        {
                            resultTable[idx++] = m.Id.ToString();
                        }
                    }
                    catch
                    {
                        // Ignore individual predicate failure
                    }
                }
            }

            return DynValue.NewTable(resultTable);
        });

        script.Globals["groupings"] = table;
    }
}
