namespace App.Shared.Lua.Examples;

using System;
using System.Collections.Generic;
using App.Shared.Lua.Models;

/// <summary>
/// Pre-seeded plant automation scripts illustrating the four core features
/// specified in spec.md:
/// 1. Machines
/// 2. Groupings (Machines, Users, Telemetry)
/// 3. Telemetry API (Custom logic and handling of changes)
/// 4. Tickets (Automatic tickets from analytics and SLA preventive maintenance)
/// </summary>
public static class DefaultPlantScripts
{
    public static List<LuaScriptRecord> GetInitialScripts()
    {
        return new List<LuaScriptRecord>
        {
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Spindle Vibration & Temperature Watchdog",
                Description = "Custom telemetry processing logic: monitors spindle vibration moving average and raises automated ticket if abnormal spike detected.",
                TriggerType = LuaScriptTriggerType.TelemetryChange,
                IsActive = true,
                Code = @"-- Telemetry custom logic and change handling
local machine_id = context_machine_id or '11111111-1111-1111-1111-111111111111'
local vib = telemetry.get_latest(machine_id, 'spindle_vibration')
local temp = telemetry.get_latest(machine_id, 'temperature')

print('Evaluating telemetry for machine: ' .. machine_id)
print('Current Vibration: ' .. tostring(vib) .. ' G, Temperature: ' .. tostring(temp) .. ' °C')

local history = telemetry.get_history(machine_id, 'spindle_vibration', 10)
local avg_vib = telemetry.moving_average(history, 5)
print('Moving Average Vibration (last 5): ' .. tostring(avg_vib))

if vib ~= nil and avg_vib ~= nil and vib > (avg_vib * 1.5) then
    print('WARNING: Spindle vibration spike detected above baseline!')
    local spike_res = telemetry.detect_spike(vib, history, 2.0)
    
    if spike_res.is_spike then
        print('CRITICAL: Spike confirmed. Generating automatic maintenance ticket...')
        local ticket = tickets.create({
            title = '[AUTO-TELEMETRY] Excessive Spindle Vibration Spike',
            description = 'Telemetry watchdog detected spindle vibration: ' .. tostring(vib) .. ' G (baseline ' .. tostring(avg_vib) .. ' G).',
            machine_id = machine_id,
            priority = 'High',
            issue_type = 'Maintenance',
            is_line_stop = false,
            alarm_code = 'ALM-VIB-092',
            assigned_to = 'Vibration Specialist',
            sla_hours = 4.0
        })
        print('Created Ticket ID: ' .. ticket.id)
        return ticket.id
    end
end

return 'Telemetry normal'
"
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "SLA Preventive Maintenance Scheduler",
                Description = "SLA preventive maintenance rule: inspects runtime hours across machines and schedules preventive overhaul before SLA breach.",
                TriggerType = LuaScriptTriggerType.SlaPreventiveCheck,
                IsActive = true,
                Code = @"-- SLA Preventive Maintenance rule
print('Running Plant SLA Preventive Maintenance Scan...')

local machines_list = machines.list({ status = 'active' })
local tickets_created = 0

for i = 1, #machines_list do
    local m = machines_list[i]
    local hours = telemetry.get_latest(m.id, 'runtime_hours') or 0.0
    print('Station ' .. m.custom_identifier .. ' runtime: ' .. tostring(hours) .. ' hours')

    if hours >= 1400.0 then
        print('Station ' .. m.custom_identifier .. ' has reached preventive maintenance interval threshold.')
        
        -- Check if open ticket already exists
        local existing = tickets.list({ machine_id = m.id, status = 'Open' })
        if #existing == 0 then
            local pm_ticket = tickets.trigger_preventive_maintenance(m.id, '1500-Hour Scheduled Bearing & Seal Inspection', {
                title = '[SLA-PM] 1500h Periodic Overhaul: ' .. m.custom_identifier,
                priority = 'Medium',
                sla_hours = 24.0,
                assigned_group = 'Mechanical Maintenance Tier 2'
            })
            print('Preventive ticket raised: ' .. pm_ticket.id)
            tickets_created = tickets_created + 1
        else
            print('Ticket already in progress for station ' .. m.custom_identifier)
        end
    end
end

return 'SLA evaluation complete. Tickets generated: ' .. tostring(tickets_created)
"
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Dynamic Hierarchy Grouping & Team Assignment",
                Description = "Groupings rule: dynamic membership evaluation grouping CNC and Welding cells into high-priority monitoring tiers.",
                TriggerType = LuaScriptTriggerType.Manual,
                IsActive = true,
                Code = @"-- Groupings API: Machines, Users, Telemetry groupings
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
    technician_count = #tech_members
}
"
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Name = "Plant UI Dynamic Action & Badge Registration",
                Description = "Extends the Heimdall UI dynamically without modifying Vue templates by registering custom station actions and status badges.",
                TriggerType = LuaScriptTriggerType.UiExtension,
                IsActive = true,
                Code = @"-- Dynamic UI Extension registration
print('Registering Dynamic UI Extensions...')

-- 1. Action button on Machine Details Workbench
ui.register_action({
    id = 'action_recalibrate_spindle',
    title = 'Recalibrate Spindle Drive',
    description = 'Executes closed-loop zero-offset calibration on the station axis drive.',
    target = 'machine',
    icon = 'i-lucide-wrench',
    color = 'indigo',
    handler = function(ctx)
        print('Recalibration triggered for machine ' .. tostring(ctx and ctx.machine_id))
        return { status = 'Calibration routine dispatched successfully' }
    end
})

-- 2. Contextual SLA risk badge on Ticket workbench
ui.register_badge({
    id = 'badge_sla_risk',
    title = 'SLA Critical',
    target = 'ticket',
    color = 'red'
})

-- 3. Plant overview alert banner
ui.register_banner({
    id = 'banner_fleet_health',
    message = 'All production cells operational. 2 preventive maintenance inspections scheduled today.',
    target = 'dashboard',
    level = 'emerald'
})

print('UI Extensions registered successfully.')
return true
"
            }
        };
    }
}
