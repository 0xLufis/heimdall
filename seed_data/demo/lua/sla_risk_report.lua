-- SLA Risk Report  (Nordvik NV1 demo - FICTIONAL plant)
--
-- Trigger : Manual (run from the Lua automation page or POST /api/v1/lua/scripts/{id}/run).
-- Effects : READ-ONLY. Lists unresolved tickets, evaluates their SLA with
--           tickets.check_sla (Critical 2 h, High 4 h, Medium 8 h, Low 24 h)
--           and returns the breached / at-risk tickets. Nothing is modified.

local AT_RISK_MINUTES = 120   -- same "warning" window the plant-map HUD uses
local MAX_LINES = 15

local breached, at_risk, healthy = {}, {}, 0
local statuses = { 'Open', 'InProgress', 'Pending' }

for _, status in ipairs(statuses) do
  local list = tickets.list({ status = status })
  for i = 1, #list do
    local t = list[i]
    local sla = tickets.check_sla(t.id, 0)
    local row = {
      id = t.id,
      title = t.title,
      priority = t.priority,
      status = t.status,
      escalated = t.is_escalated,
      remaining_minutes = sla.remaining_minutes
    }
    if sla.is_breached then
      breached[#breached + 1] = row
    elseif sla.remaining_minutes ~= nil and sla.remaining_minutes <= AT_RISK_MINUTES then
      at_risk[#at_risk + 1] = row
    else
      healthy = healthy + 1
    end
  end
end

-- freshest breaches first: those are the ones a shift can still act on
table.sort(breached, function(a, b) return a.remaining_minutes > b.remaining_minutes end)
table.sort(at_risk, function(a, b) return a.remaining_minutes < b.remaining_minutes end)

local function show(label, rows)
  print(label .. ' (' .. #rows .. ')')
  for i = 1, math.min(#rows, MAX_LINES) do
    local r = rows[i]
    print(string.format('  [%s/%s%s] %s  (%.0f min)', r.priority, r.status,
      r.escalated and ', escalated' or '', r.title, r.remaining_minutes))
  end
end

show('SLA BREACHED', breached)
show('SLA AT RISK (< ' .. AT_RISK_MINUTES .. ' min)', at_risk)
print('Healthy unresolved tickets: ' .. healthy)

return {
  breached = #breached,
  at_risk = #at_risk,
  healthy = healthy,
  most_recent_breach_id = breached[1] and breached[1].id or nil,
  most_urgent_at_risk_id = at_risk[1] and at_risk[1].id or nil
}
