-- Laser Fleet UI Extensions  (Nordvik NV1 demo - FICTIONAL plant)
--
-- Trigger : UiExtension (executed when the script is registered / updated).
-- Effects : READ-ONLY. Registers a machine badge, a dashboard banner and a
--           "Laser health snapshot" action whose handler only reads telemetry.
--           The UI is extended, never replaced (see src/shared/lua/spec.md).

local LASER_METRICS = { 'laser_power_kw', 'protective_glass_temp_c', 'weld_penetration_mm' }

-- Count unresolved laser tickets (alarm codes ALM-LSR-*) for the banner text.
local open_laser = 0
for _, status in ipairs({ 'Open', 'InProgress', 'Pending' }) do
  local list = tickets.list({ status = status })
  for i = 1, #list do
    local code = list[i].alarm_code or ''
    if string.sub(code, 1, 8) == 'ALM-LSR-' then open_laser = open_laser + 1 end
  end
end

ui.register_badge({
  id = 'nv_badge_laser_fleet',
  title = 'Laser Fleet',
  target = 'machine',
  color = 'red'
})

if open_laser > 0 then
  ui.register_banner({
    id = 'nv_banner_laser_fleet',
    message = 'Laser fleet: ' .. open_laser .. ' unresolved laser ticket(s). Protective glass stock below minimum - see NV-L2-OP060.',
    target = 'dashboard',
    level = 'amber'
  })
else
  ui.register_banner({
    id = 'nv_banner_laser_fleet',
    message = 'Laser fleet healthy: no unresolved laser tickets.',
    target = 'dashboard',
    level = 'emerald'
  })
end

ui.register_action({
  id = 'nv_action_laser_snapshot',
  title = 'Laser health snapshot',
  description = 'Shows the latest laser power, protective glass temperature and weld penetration for this station (read-only).',
  target = 'machine',
  icon = 'i-lucide-zap',
  color = 'amber',
  handler = function(ctx)
    local machine_id = ctx and ctx.machine_id
    if machine_id == nil then return { status = 'no machine selected' } end
    local snapshot = { machine_id = machine_id }
    for _, metric in ipairs(LASER_METRICS) do
      snapshot[metric] = telemetry.get_latest(machine_id, metric)
    end
    return snapshot
  end
})

return { registered = 3, open_laser_tickets = open_laser }
