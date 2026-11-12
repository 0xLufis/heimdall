/**
 * "New KPI" templates for the Lua automation page. Each template asks for a few parameters and
 * generates a plain Lua script against the sandbox API (see luaApiReference.ts). The script
 * returns `{ kpi, station, value, unit, target, status, details }`, which the page renders as a
 * KPI result; the generated code stays editable.
 *
 * Station: the chosen station id is baked in as the default and `context_machine_id` overrides
 * it, so the same script can be test-run against another station or bound to telemetry changes.
 * Metric defaults are the names the backend stores for the fleet simulator (parts_produced,
 * scrap_parts, cycle_time_s, target_cycle_time_s, availability_pct).
 */

export type KpiParamType = 'station' | 'metric' | 'number'

export interface KpiParamDef {
  key: string
  label: string
  type: KpiParamType
  default?: string | number
  help?: string
  min?: number
  max?: number
  optional?: boolean
}

export interface KpiTemplate {
  id: 'oee' | 'availability' | 'scrap_rate' | 'mtbf' | 'cycle_time_deviation'
  name: string
  description: string
  unit: string
  params: KpiParamDef[]
}

export type KpiParams = Record<string, string | number | undefined>

const station: KpiParamDef = { key: 'stationId', label: 'Station', type: 'station', help: 'Default station; a test run or trigger can pass another one as context_machine_id.' }
const target = (def: number, label = 'Target', unit = '%'): KpiParamDef => ({ key: 'target', label: `${label} (${unit})`, type: 'number', default: def, min: 0 })

export const KPI_TEMPLATES: KpiTemplate[] = [
  {
    id: 'oee',
    name: 'OEE',
    description: 'Availability × performance × quality from the latest station telemetry.',
    unit: '%',
    params: [
      station,
      { key: 'availabilityMetric', label: 'Availability metric (0–1 or %)', type: 'metric', default: 'availability_pct' },
      { key: 'cycleTimeMetric', label: 'Actual cycle time metric (s)', type: 'metric', default: 'cycle_time_s' },
      { key: 'idealCycleTime', label: 'Ideal cycle time (s)', type: 'number', default: 30, min: 0.001 },
      { key: 'producedMetric', label: 'Produced parts metric', type: 'metric', default: 'parts_produced' },
      { key: 'scrapMetric', label: 'Scrap parts metric', type: 'metric', default: 'scrap_parts' },
      target(85)
    ]
  },
  {
    id: 'availability',
    name: 'Availability',
    description: 'Average availability over the last samples of the station.',
    unit: '%',
    params: [
      station,
      { key: 'availabilityMetric', label: 'Availability metric (0–1 or %)', type: 'metric', default: 'availability_pct' },
      { key: 'samples', label: 'Samples to average', type: 'number', default: 20, min: 1, max: 500 },
      target(90)
    ]
  },
  {
    id: 'scrap_rate',
    name: 'Scrap rate',
    description: 'Scrapped parts as a share of produced parts.',
    unit: '%',
    params: [
      station,
      { key: 'producedMetric', label: 'Produced parts metric', type: 'metric', default: 'parts_produced' },
      { key: 'scrapMetric', label: 'Scrap parts metric', type: 'metric', default: 'scrap_parts' },
      target(2, 'Upper limit')
    ]
  },
  {
    id: 'mtbf',
    name: 'MTBF from tickets',
    description: 'Mean time between failures: average gap between the station\'s latest breakdown tickets (preventive maintenance tickets are not failures).',
    unit: 'h',
    params: [
      station,
      { key: 'failures', label: 'Failures to look at', type: 'number', default: 10, min: 2, max: 200 },
      target(72, 'Target', 'h')
    ]
  },
  {
    id: 'cycle_time_deviation',
    name: 'Cycle-time deviation',
    description: 'Average cycle time against the target cycle time, in percent.',
    unit: '%',
    params: [
      station,
      { key: 'cycleTimeMetric', label: 'Cycle time metric (s)', type: 'metric', default: 'cycle_time_s' },
      { key: 'targetMetric', label: 'Target cycle time metric (s)', type: 'metric', default: 'target_cycle_time_s', optional: true, help: 'Used when the station publishes it; otherwise the fixed target below.' },
      { key: 'targetCycleTime', label: 'Fixed target cycle time (s)', type: 'number', default: 30, min: 0.001 },
      { key: 'samples', label: 'Samples to average', type: 'number', default: 20, min: 1, max: 500 },
      { key: 'tolerance', label: 'Tolerance (±%)', type: 'number', default: 5, min: 0 }
    ]
  }
]

export const findKpiTemplate = (id: string) => KPI_TEMPLATES.find(t => t.id === id)

export function defaultKpiParams(template: KpiTemplate): KpiParams {
  return Object.fromEntries(template.params.map(p => [p.key, p.default]))
}

/** Validation messages per parameter key; empty when the parameters can generate a script. */
export function validateKpiParams(template: KpiTemplate, params: KpiParams): Record<string, string> {
  const errors: Record<string, string> = {}
  for (const p of template.params) {
    const raw = params[p.key]
    const empty = raw === undefined || raw === null || String(raw).trim() === ''
    if (empty) {
      if (!p.optional) errors[p.key] = p.type === 'station' ? 'Choose the station this KPI is calculated for.' : `${p.label} is required.`
      continue
    }
    if (p.type === 'metric' && !/^[A-Za-z0-9_.:-]+$/.test(String(raw).trim())) {
      errors[p.key] = 'Metric names contain letters, digits, "_", ".", ":" or "-" only.'
    }
    if (p.type === 'number') {
      const n = Number(raw)
      if (!Number.isFinite(n)) errors[p.key] = `${p.label} must be a number.`
      else if (p.min !== undefined && n < p.min) errors[p.key] = `${p.label} must be at least ${p.min}.`
      else if (p.max !== undefined && n > p.max) errors[p.key] = `${p.label} must be at most ${p.max}.`
    }
  }
  return errors
}

/** Lua string literal (single-quoted, escaped). */
export const luaString = (value: unknown) =>
  `'${String(value ?? '').replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/\n/g, '\\n')}'`

const num = (value: unknown) => String(Number(value))

const prelude = (template: KpiTemplate, params: KpiParams) => `-- KPI: ${template.name} (${template.unit})
-- ${template.description}
-- Generated by "New KPI"; edit freely. Returns { kpi, station, value, unit, target, status, details }.
local station_id = context_machine_id or ${luaString(params.stationId)}
local station = machines.get(station_id)
if station == nil then
  error('Station ' .. tostring(station_id) .. ' was not found (or is outside your organisation).')
end
local label = station.custom_identifier ~= '' and station.custom_identifier or station.name

local function round(x, digits)
  local m = 10 ^ (digits or 1)
  return math.floor(x * m + 0.5) / m
end

local function latest(metric)
  local v = telemetry.get_latest(station_id, metric)
  if v == nil then
    error('Station ' .. label .. ' has no "' .. metric .. '" telemetry yet.')
  end
  return v
end

local function average(metric, samples)
  local history = telemetry.get_history(station_id, metric, samples)
  if #history == 0 then
    error('Station ' .. label .. ' has no "' .. metric .. '" telemetry yet.')
  end
  local sum = 0
  for i = 1, #history do sum = sum + history[i].value end
  return sum / #history, #history
end
`

const BODIES: Record<KpiTemplate['id'], (p: KpiParams) => string> = {
  oee: p => `
local availability = latest(${luaString(p.availabilityMetric)})
if availability > 1 then availability = availability / 100 end
-- Average of the recent running cycles (a stopped station reports 0).
local history = telemetry.get_history(station_id, ${luaString(p.cycleTimeMetric)}, 20)
local cycle_sum, cycles = 0, 0
for i = 1, #history do
  if history[i].value > 0 then cycle_sum = cycle_sum + history[i].value; cycles = cycles + 1 end
end
local cycle_time = cycles > 0 and cycle_sum / cycles or 0
local performance = cycle_time > 0 and math.min(1, ${num(p.idealCycleTime)} / cycle_time) or 0
local produced = latest(${luaString(p.producedMetric)})
local scrap = telemetry.get_latest(station_id, ${luaString(p.scrapMetric)}) or 0
local quality = produced > 0 and math.max(0, (produced - scrap) / produced) or 0
local oee = availability * performance * quality * 100

print(label .. ': availability ' .. round(availability * 100) .. ' %, performance ' .. round(performance * 100) .. ' %, quality ' .. round(quality * 100) .. ' %')
return {
  kpi = 'OEE', station = label, value = round(oee), unit = '%', target = ${num(p.target)},
  status = oee >= ${num(p.target)} and 'ok' or 'below_target',
  details = { availability = round(availability * 100), performance = round(performance * 100), quality = round(quality * 100) }
}
`,
  availability: p => `
local value, samples = average(${luaString(p.availabilityMetric)}, ${num(p.samples)})
if value <= 1 then value = value * 100 end

print(label .. ': average availability ' .. round(value) .. ' % over ' .. samples .. ' samples')
return {
  kpi = 'Availability', station = label, value = round(value), unit = '%', target = ${num(p.target)},
  status = value >= ${num(p.target)} and 'ok' or 'below_target',
  details = { samples = samples }
}
`,
  scrap_rate: p => `
local produced = latest(${luaString(p.producedMetric)})
local scrap = telemetry.get_latest(station_id, ${luaString(p.scrapMetric)}) or 0
if produced <= 0 then
  error('Station ' .. label .. ' reports no produced parts yet, so there is no scrap rate.')
end
local rate = scrap / produced * 100

print(label .. ': ' .. scrap .. ' scrapped of ' .. produced .. ' produced')
return {
  kpi = 'Scrap rate', station = label, value = round(rate, 2), unit = '%', target = ${num(p.target)},
  status = rate <= ${num(p.target)} and 'ok' or 'above_limit',
  details = { produced = produced, scrap = scrap }
}
`,
  mtbf: p => `
-- Hours since 1970 for an ISO timestamp ("2026-11-12T08:30:00..."); the sandbox has no os.time.
local function iso_hours(s)
  local y, mo, d, h, mi = string.match(s or '', '^(%d+)-(%d+)-(%d+)T(%d+):(%d+)')
  if y == nil then return nil end
  y, mo, d = tonumber(y), tonumber(mo), tonumber(d)
  if mo <= 2 then y = y - 1 end
  local era = math.floor(y / 400)
  local yoe = y - era * 400
  local mp = (mo + 9) % 12
  local doy = math.floor((153 * mp + 2) / 5) + d - 1
  local doe = yoe * 365 + math.floor(yoe / 4) - math.floor(yoe / 100) + doy
  local days = era * 146097 + doe - 719468
  return days * 24 + tonumber(h) + tonumber(mi) / 60
end

local times = {}
local all = tickets.list({ machine_id = station_id })
for i = 1, #all do
  local t = all[i]
  if t.issue_type ~= 'Maintenance' then
    local hours = iso_hours(t.created_at)
    if hours ~= nil then table.insert(times, hours) end
  end
end
table.sort(times, function(a, b) return a > b end)
local n = math.min(#times, ${num(p.failures)})
if n < 2 then
  error('Station ' .. label .. ' has ' .. #times .. ' breakdown ticket(s); MTBF needs at least 2.')
end
local mtbf = (times[1] - times[n]) / (n - 1)

print(label .. ': ' .. n .. ' failures, ' .. round(times[1] - times[n]) .. ' h between the first and the last')
return {
  kpi = 'MTBF', station = label, value = round(mtbf), unit = 'h', target = ${num(p.target)},
  status = mtbf >= ${num(p.target)} and 'ok' or 'below_target',
  details = { failures = n }
}
`,
  cycle_time_deviation: p => `
local actual, samples = average(${luaString(p.cycleTimeMetric)}, ${num(p.samples)})
local target = ${p.targetMetric ? `telemetry.get_latest(station_id, ${luaString(p.targetMetric)}) or ` : ''}${num(p.targetCycleTime)}
local deviation = (actual - target) / target * 100

print(label .. ': average cycle time ' .. round(actual, 2) .. ' s against ' .. round(target, 2) .. ' s target')
return {
  kpi = 'Cycle-time deviation', station = label, value = round(deviation, 1), unit = '%', target = 0,
  status = math.abs(deviation) <= ${num(p.tolerance)} and 'ok' or 'above_limit',
  details = { actual_s = round(actual, 2), target_s = round(target, 2), samples = samples }
}
`
}

export function buildKpiScript(template: KpiTemplate, params: KpiParams): string {
  const errors = validateKpiParams(template, params)
  if (Object.keys(errors).length > 0) throw new Error(Object.values(errors)[0])
  return prelude(template, params) + BODIES[template.id](params)
}

/** A script result that follows the KPI convention above. */
export interface KpiResult {
  kpi: string
  value: number
  unit?: string
  target?: number
  station?: string
  status?: string
  details?: Record<string, unknown>
}

export function asKpiResult(value: unknown): KpiResult | null {
  if (!value || typeof value !== 'object') return null
  const v = value as Record<string, unknown>
  const kpi = v.kpi ?? v.Kpi
  const num = v.value ?? v.Value
  if (typeof kpi !== 'string' || typeof num !== 'number') return null
  return {
    kpi,
    value: num,
    unit: typeof (v.unit ?? v.Unit) === 'string' ? String(v.unit ?? v.Unit) : undefined,
    target: typeof (v.target ?? v.Target) === 'number' ? Number(v.target ?? v.Target) : undefined,
    station: typeof (v.station ?? v.Station) === 'string' ? String(v.station ?? v.Station) : undefined,
    status: typeof (v.status ?? v.Status) === 'string' ? String(v.status ?? v.Status) : undefined,
    details: (v.details ?? v.Details) as Record<string, unknown> | undefined
  }
}

/** Plain-language hint for common Lua errors; null when there is nothing to add. */
export function explainLuaError(message: string | null | undefined): string | null {
  const m = String(message || '')
  if (!m) return null
  const line = m.match(/:\((\d+),/)?.[1] ?? m.match(/line (\d+)/i)?.[1]
  const at = line ? ` (line ${line})` : ''
  if (/arithmetic on a nil value/i.test(m)) return `A value used in a calculation is nil${at}: a metric probably has no telemetry for this station. Check the metric name or test against another station.`
  if (/index a nil value/i.test(m)) return `Something looked up is nil${at}, for example a station or ticket that does not exist. Check ids and names.`
  if (/attempt to call a nil value/i.test(m)) return `A function that does not exist was called${at}. Check its name in the API reference.`
  if (/unexpected symbol|expected|syntax/i.test(m)) return `The script has a syntax error${at}: look for a missing "end", ")" or quote.`
  if (/timeout|instruction|exceeded/i.test(m)) return 'The script ran too long or used too many instructions: limit loops and history sizes.'
  return null
}
