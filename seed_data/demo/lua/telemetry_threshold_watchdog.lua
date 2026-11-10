-- Telemetry Threshold & Drift Watchdog  (Nordvik NV1 demo - FICTIONAL plant)
--
-- Trigger : TelemetryChange (POST /api/v1/lua/telemetry/evaluate) or manual run
--           with context { context_machine_id = '<station uuid>', context_metric = '<metric>' }.
-- Effects : read-only, except that it creates at most ONE ticket per machine +
--           alarm code while no unresolved ticket with the same alarm code exists.
--
-- For every watched metric it computes a 5-sample moving average, compares it
-- with the alarm/warning limits and - when the signal is still inside limits -
-- projects how many minutes remain until the alarm limit is reached
-- (least-squares slope over the recent history).

local SAMPLE_MINUTES = 5          -- spacing of the demo telemetry samples
local HISTORY_SAMPLES = 24

local WATCH = {
  laser_power_kw         = { low = 5.70, warn_low = 5.85, alarm = 'ALM-LSR-217', priority = 'Critical', sla = 2.0, label = 'Laser power' },
  protective_glass_temp_c = { high = 55.0, warn_high = 50.0, alarm = 'ALM-LSR-218', priority = 'High', sla = 4.0, label = 'Protective glass temperature' },
  resin_flow_ml_min      = { low = 12.0, warn_low = 12.5, alarm = 'ALM-DSP-112', priority = 'High', sla = 4.0, label = 'Resin flow' },
  spindle_vibration_mm_s = { high = 4.5, warn_high = 4.0, alarm = 'ALM-SPN-045', priority = 'High', sla = 4.0, label = 'Spindle vibration' }
}

local machine_id = context_machine_id
if machine_id == nil or machine_id == '' then
  return 'skipped: provide context_machine_id'
end

local function round(x, digits)
  local m = 10 ^ (digits or 2)
  return math.floor(x * m + 0.5) / m
end

-- least-squares slope (units per sample) over a telemetry history table
local function slope(history)
  local n = #history
  if n < 3 then return 0 end
  local sx, sy, sxx, sxy = 0, 0, 0, 0
  for i = 1, n do
    local y = history[i].value
    sx = sx + i; sy = sy + y; sxx = sxx + i * i; sxy = sxy + i * y
  end
  local denom = n * sxx - sx * sx
  if denom == 0 then return 0 end
  return (n * sxy - sx * sy) / denom
end

local function has_unresolved_ticket(alarm_code)
  local existing = tickets.list({ machine_id = machine_id })
  for i = 1, #existing do
    local t = existing[i]
    if t.alarm_code == alarm_code and t.status ~= 'Resolved' and t.status ~= 'Closed' then
      return t
    end
  end
  return nil
end

local machine = machines.get(machine_id)
local station = machine and machine.custom_identifier or machine_id

local metrics = {}
if context_metric ~= nil and WATCH[context_metric] ~= nil then
  metrics[1] = context_metric
else
  for name, _ in pairs(WATCH) do metrics[#metrics + 1] = name end
end

local report = {}
for _, metric in ipairs(metrics) do
  local rule = WATCH[metric]
  local history = telemetry.get_history(machine_id, metric, HISTORY_SAMPLES)
  if #history >= 5 then
    local avg = telemetry.moving_average(history, 5)
    local latest = history[#history].value
    local per_sample = slope(history)
    local state, limit = 'ok', nil

    if rule.low ~= nil then
      limit = rule.low
      if avg < rule.low then state = 'alarm' elseif avg < rule.warn_low then state = 'warning' end
    else
      limit = rule.high
      if avg > rule.high then state = 'alarm' elseif avg > rule.warn_high then state = 'warning' end
    end

    local eta = nil
    if state ~= 'alarm' and per_sample ~= 0 then
      local samples_left = (limit - avg) / per_sample
      if samples_left > 0 then eta = round(samples_left * SAMPLE_MINUTES, 0) end
    end

    print(string.format('%s %s: latest=%.2f avg5=%.2f limit=%.2f trend=%+.3f/sample state=%s%s',
      station, metric, latest, avg, limit, per_sample, state,
      eta and (' (alarm in ~' .. eta .. ' min)') or ''))

    local entry = { metric = metric, state = state, average = round(avg, 3), eta_minutes = eta }

    if state == 'alarm' then
      local open = has_unresolved_ticket(rule.alarm)
      if open ~= nil then
        print('  -> unresolved ticket already exists (' .. open.status .. '): ' .. open.id .. ' - no duplicate created')
        entry.ticket_id = open.id
      else
        local ticket = tickets.create({
          title = '[AUTO] ' .. rule.label .. ' out of limit on ' .. station,
          description = string.format('%s moving average %.2f breached limit %.2f (alarm %s). Created by the Telemetry Threshold & Drift Watchdog Lua script.',
            rule.label, avg, limit, rule.alarm),
          machine_id = machine_id,
          priority = rule.priority,
          issue_type = 'Maintenance',
          is_line_stop = false,
          alarm_code = rule.alarm,
          sla_hours = rule.sla
        })
        print('  -> created ticket ' .. ticket.id)
        entry.ticket_id = ticket.id
      end
    end
    report[#report + 1] = entry
  end
end

if #report == 0 then
  return 'no watched telemetry for ' .. station .. ' (run the demo telemetry replay first)'
end
return report
