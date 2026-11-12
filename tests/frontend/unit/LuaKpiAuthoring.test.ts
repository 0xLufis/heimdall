import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import LuaKpiWizard from '../../../src/frontend/web/app/components/lua/LuaKpiWizard.vue'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import {
  KPI_TEMPLATES,
  findKpiTemplate,
  defaultKpiParams,
  validateKpiParams,
  buildKpiScript,
  luaString,
  asKpiResult,
  explainLuaError
} from '../../../src/frontend/web/app/utils/lua/kpiTemplates'
import { LUA_API_REFERENCE } from '../../../src/frontend/web/app/utils/lua/luaApiReference'

const STATION = '7406a390-67bc-5335-89b5-2acf43d2ff94'

describe('New KPI templates', () => {
  it('offers OEE, availability, scrap rate, MTBF and cycle-time deviation', () => {
    expect(KPI_TEMPLATES.map(t => t.id)).toEqual(['oee', 'availability', 'scrap_rate', 'mtbf', 'cycle_time_deviation'])
  })

  it('asks for a station and reports readable validation messages', () => {
    const oee = findKpiTemplate('oee')!
    const params = defaultKpiParams(oee)
    expect(validateKpiParams(oee, params)).toEqual({ stationId: 'Choose the station this KPI is calculated for.' })

    const errors = validateKpiParams(oee, { ...params, stationId: STATION, idealCycleTime: 0, scrapMetric: 'scrap parts', target: 'x' })
    expect(errors.idealCycleTime).toMatch(/at least/)
    expect(errors.scrapMetric).toMatch(/Metric names/)
    expect(errors.target).toMatch(/must be a number/)
    expect(() => buildKpiScript(oee, params)).toThrow(/Choose the station/)
  })

  it('generates a script per template that only reads plant data and returns the KPI convention', () => {
    const readOnly = new Set(LUA_API_REFERENCE.flatMap(g => g.functions.filter(f => !f.mutates).map(f => `${g.global}.${f.name}`)))
    for (const template of KPI_TEMPLATES) {
      const code = buildKpiScript(template, { ...defaultKpiParams(template), stationId: STATION })
      expect(code, template.id).toContain(`local station_id = context_machine_id or '${STATION}'`)
      expect(code, template.id).toMatch(/return \{\n\s+kpi = '/)
      const calls = [...code.matchAll(/\b(machines|telemetry|tickets|groupings|events|ui)\.([a-z_]+)\(/g)].map(m => `${m[1]}.${m[2]}`)
      expect(calls.length, template.id).toBeGreaterThan(0)
      for (const call of calls) expect(readOnly.has(call), `${template.id} calls ${call}`).toBe(true)
      // Balanced blocks: every function / if / for opens one "end".
      const bare = code.replace(/--[^\n]*/g, '').replace(/'(?:[^'\\\n]|\\.)*'/g, "''")
      const opens = (bare.match(/\b(function|if|for)\b/g) || []).length
      const ends = (bare.match(/\bend\b/g) || []).length
      expect(ends, template.id).toBe(opens)
    }
  })

  it('puts the parameters into the script', () => {
    const code = buildKpiScript(findKpiTemplate('scrap_rate')!, { stationId: STATION, producedMetric: 'good_parts', scrapMetric: 'rejects', target: 1.5 })
    expect(code).toContain("latest('good_parts')")
    expect(code).toContain("telemetry.get_latest(station_id, 'rejects')")
    expect(code).toContain('rate <= 1.5')
    const cycle = buildKpiScript(findKpiTemplate('cycle_time_deviation')!, { ...defaultKpiParams(findKpiTemplate('cycle_time_deviation')!), stationId: STATION, targetMetric: '' })
    expect(cycle).toContain('local target = 30')
  })

  it('escapes Lua string literals', () => {
    expect(luaString("it's \\ odd\n")).toBe("'it\\'s \\\\ odd\\n'")
  })

  it('recognises a KPI result and explains common errors', () => {
    expect(asKpiResult({ kpi: 'OEE', value: 71.3, unit: '%', target: 85, status: 'below_target' })).toMatchObject({ kpi: 'OEE', value: 71.3, target: 85 })
    expect(asKpiResult({ Kpi: 'MTBF', Value: 40 })).toMatchObject({ kpi: 'MTBF', value: 40 })
    expect(asKpiResult('done')).toBeNull()
    expect(asKpiResult({ value: 3 })).toBeNull()

    expect(explainLuaError('chunk_1:(12,4-20): attempt to perform arithmetic on a nil value')).toMatch(/line 12.*metric/)
    expect(explainLuaError('attempt to call a nil value (field get_lates)')).toMatch(/API reference/)
    expect(explainLuaError("chunk_1:(3,0): unexpected symbol near 'end'")).toMatch(/syntax error \(line 3\)/)
    expect(explainLuaError('Station X has no "oee" telemetry yet.')).toBeNull()
  })
})

describe('Lua API reference', () => {
  it('lists exactly the functions the backend sandbox registers', () => {
    const apiDir = join(__dirname, '../../../src/shared/lua/Apis')
    const backend: Record<string, string[]> = {}
    for (const file of readdirSync(apiDir).filter(f => f.endsWith('.cs'))) {
      const source = readFileSync(join(apiDir, file), 'utf8')
      const global = source.match(/Globals\["([a-z_]+)"\]/)?.[1]
      if (!global) continue
      backend[global] = [...source.matchAll(/table\["([a-z_]+)"\] = \((?:Func|Action)</g)].map(m => m[1]).sort()
    }
    const frontend = Object.fromEntries(LUA_API_REFERENCE.map(g => [g.global, g.functions.map(f => f.name).sort()]))
    expect(frontend).toEqual(backend)
  })
})

describe('New KPI wizard', () => {
  it('validates the parameters and emits an editable script for the chosen station', async () => {
    const wrapper = mount(LuaKpiWizard, { props: { stations: [{ id: STATION, label: 'LINE-01-OP090' }] } })

    await wrapper.find('[role="radio"][aria-checked="false"]').trigger('click') // availability
    await wrapper.find('#kpi-param-samples').setValue('0')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('create')).toBeUndefined()
    expect(wrapper.find('[role="alert"]').text()).toMatch(/at least 1/)

    await wrapper.find('#kpi-param-samples').setValue('30')
    await wrapper.find('form').trigger('submit')
    const [payload] = wrapper.emitted('create')![0] as any[]
    expect(payload.name).toBe('Availability – LINE-01-OP090')
    expect(payload.code).toContain(`context_machine_id or '${STATION}'`)
    expect(payload.code).toContain("average('availability_pct', 30)")
  }, 20000)
})
