-- Demo telemetry replay helper (used by `scripts/seed_demo_data.py --push-runtime`).
-- Not registered as an automation script: it is executed once through
-- POST /api/v1/lua/execute with the data passed in the request *context*
-- (global `demo_series` = { { machine_id, metric, values = { ... } }, ... }),
-- so no data is ever spliced into Lua source code.
-- Effects: telemetry.emit only (fills the backend's in-memory telemetry buffer
-- that telemetry.get_latest / telemetry.get_history read).

if demo_series == nil then
  return 'no demo_series in context'
end

local emitted = 0
for _, series in ipairs(demo_series) do
  for _, value in ipairs(series.values) do
    telemetry.emit(series.machine_id, series.metric, value, {})
    emitted = emitted + 1
  end
end
return emitted
