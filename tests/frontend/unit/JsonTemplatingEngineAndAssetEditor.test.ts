import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import {
  evaluateExpression,
  evaluateTemplate,
  extractVariablesFromTemplate,
  mapTemplateToAssetForm,
  validateJsonString,
  TEMPLATE_FILTERS,
  getSystemVariables
} from '~/utils/jsonTemplatingEngine'
import { DEFAULT_ASSET_TEMPLATES } from '~/utils/defaultAssetTemplates'
import AssetTabbedEditor from '~/components/dashboard/AssetTabbedEditor.vue'

describe('Industrial JSON Templating Engine & Asset Editor Suite', () => {

  describe('Core JSON Templating Engine Expressions & Filters', () => {
    it('evaluates basic variable interpolation and string expressions', () => {
      const expr = 'PLC-{{station}}-{{unit}}'
      const context = { station: 'OP10', unit: '01' }
      const res = evaluateExpression(expr, context)
      expect(res).toBe('PLC-OP10-01')
    })

    it('evaluates filter pipes: uppercase, lowercase, and slugify', () => {
      expect(evaluateExpression('{{code | uppercase}}', { code: 'op10-load' })).toBe('OP10-LOAD')
      expect(evaluateExpression('{{code | lowercase}}', { code: 'SIEMENS-S7' })).toBe('siemens-s7')
      expect(evaluateExpression('{{name | slugify}}', { name: 'Main CNC Spindle Motor #1' })).toBe('main-cnc-spindle-motor-1')
    })

    it('evaluates filter pipes: default fallback value', () => {
      expect(evaluateExpression('{{voltage | default: "24V DC"}}', {})).toBe('24V DC')
      expect(evaluateExpression('{{voltage | default: "24V DC"}}', { voltage: '400V 3-Phase' })).toBe('400V 3-Phase')
    })

    it('evaluates filter pipes: prefix and suffix', () => {
      expect(evaluateExpression('{{code | prefix: "PRE-"}}', { code: '123' })).toBe('PRE-123')
      expect(evaluateExpression('{{code | suffix: "-POST"}}', { code: '123' })).toBe('123-POST')
    })

    it('evaluates dynamic system variables like $date, $uuid, $randomHex', () => {
      const res = evaluateExpression('SN-{{$randomHex}}', {})
      expect(res).toMatch(/^SN-[0-9A-F]{8}$/)

      const dateRes = evaluateExpression('{{$date}}', {})
      expect(dateRes).toMatch(/^\d{4}-\d{2}-\d{2}$/)

      const uuidRes = evaluateExpression('{{$uuid}}', {})
      expect(uuidRes.length).toBe(36)
    })

    it('extracts variable names from complex template object', () => {
      const templateObj = {
        name: 'PLC-{{stationCode}}-01',
        cost: '{{costInHUF}}',
        metadata: {
          IPAddress: '192.168.{{subnet}}.{{host}}',
          Voltage: '{{voltage | default: "24V"}}',
          Serial: 'SN-{{$randomHex}}'
        }
      }

      const extracted = extractVariablesFromTemplate(templateObj)
      expect(extracted).toContain('stationCode')
      expect(extracted).toContain('costInHUF')
      expect(extracted).toContain('subnet')
      expect(extracted).toContain('host')
      expect(extracted).toContain('voltage')
      expect(extracted).not.toContain('$randomHex')
    })

    it('recursively evaluates complete template object and preserves types', () => {
      const templateObj = {
        name: 'MTR-{{station | uppercase}}-01',
        costInHUF: '{{cost}}',
        quantity: 1,
        itemType: 'HardwareComponent',
        metadata: {
          PowerKW: 15,
          Voltage: '{{voltage | default: "400V"}}',
          IsActive: 'true'
        }
      }

      const result = evaluateTemplate(templateObj, {
        station: 'op10',
        cost: 1850000
      })

      expect(result.success).toBe(true)
      expect(result.data.name).toBe('MTR-OP10-01')
      expect(result.data.costInHUF).toBe(1850000)
      expect(result.data.metadata.Voltage).toBe('400V')
      expect(result.data.metadata.IsActive).toBe(true)
    })

    it('maps evaluated JSON accurately into standard Heimdall form payload', () => {
      const evaluated = {
        name: 'PLC-OP10-01',
        displayName: 'Main Station PLC',
        technology: 'Siemens SIMATIC',
        serialNumber: 'SN-12345678',
        modelNumber: '6ES7516-3AN02-0AB0',
        costInHUF: 1450000,
        itemType: 'HardwareComponent',
        metadata: {
          CPUModel: 'CPU 1516-3 PN/DP',
          IPAddress: '192.168.10.10',
          Station: 'OP10'
        }
      }

      const form = mapTemplateToAssetForm(evaluated)
      expect(form.name).toBe('PLC-OP10-01')
      expect(form.displayName).toBe('Main Station PLC')
      expect(form.technology).toBe('Siemens SIMATIC')
      expect(form.serialNumber).toBe('SN-12345678')
      expect(form.costInHUF).toBe(1450000)
      expect(form.metadata.CPUModel).toBe('CPU 1516-3 PN/DP')
      expect(form.metadata.SerialNumber).toBeUndefined()
    })
  })

  describe('Default Industrial Asset Templates Catalog', () => {
    it('contains comprehensive set of OT/IT templates across diverse categories', () => {
      expect(DEFAULT_ASSET_TEMPLATES.length).toBeGreaterThanOrEqual(10)
      
      const categories = DEFAULT_ASSET_TEMPLATES.map(t => t.category)
      expect(categories).toContain('Controller')
      expect(categories).toContain('Vision')
      expect(categories).toContain('Motion')
      expect(categories).toContain('Sensor')
      expect(categories).toContain('Software')
      expect(categories).toContain('Network')
      expect(categories).toContain('Dispensing')
    })

    it('ensures all default templates evaluate cleanly without syntax errors', () => {
      for (const tpl of DEFAULT_ASSET_TEMPLATES) {
        const defaultContext: Record<string, any> = {}
        for (const v of tpl.variables) {
          defaultContext[v.name] = v.defaultValue !== undefined ? v.defaultValue : 'TEST'
        }

        const evalRes = evaluateTemplate(tpl.template, defaultContext)
        expect(evalRes.errors).toHaveLength(0)
        expect(evalRes.data).toBeDefined()
        expect(typeof evalRes.data.name).toBe('string')
        expect(evalRes.data.name.length).toBeGreaterThan(0)
      }
    })
  })

  describe('Tabbed Asset Editor Component Rendering & Interaction', () => {
    beforeEach(() => {
      vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string) => {
        if (url.includes('/api/proxy/inventory/manufacturers')) {
          return Promise.resolve([{ id: 'm-1', name: 'Siemens' }])
        }
        if (url.includes('/api/proxy/inventory/suppliers')) {
          return Promise.resolve([{ id: 's-1', name: 'Rexel Industrial' }])
        }
        if (url.includes('/api/proxy/inventory/machines')) {
          return Promise.resolve([{ id: 'mach-1', customIdentifier: 'OP10-CELL' }])
        }
        if (url.includes('/api/proxy/inventory/client-pcs')) {
          return Promise.resolve([{ id: 'pc-1', hostname: 'IPC-L1-01' }])
        }
        return Promise.resolve([])
      }))
    })

    it('renders all 5 tabs and allows seamless tab switching', async () => {
      const wrapper = mount(AssetTabbedEditor, {
        props: {
          open: true,
          mode: 'create',
          initialType: 'hardware'
        },
        global: {
          stubs: {
            Dialog: { template: '<div><slot /></div>' },
            DialogContent: { template: '<div><slot /></div>' },
            DialogHeader: { template: '<div><slot /></div>' },
            DialogTitle: { template: '<div><slot /></div>' },
            DialogDescription: { template: '<div><slot /></div>' },
            Badge: { template: '<span><slot /></span>' },
            SearchableSelect: { template: '<div class="searchable-select"></div>' }
          }
        }
      })

      expect(wrapper.text()).toContain('Identity & Core')
      expect(wrapper.text()).toContain('Topology & Graph')
      expect(wrapper.text()).toContain('Commercial')
      expect(wrapper.text()).toContain('Specs & Params')
      expect(wrapper.text()).toContain('JSON & Templates')

      // Switch to Templates tab
      const templateTabBtn = wrapper.findAll('button').find(b => b.text().includes('JSON & Templates'))
      expect(templateTabBtn).toBeDefined()
      await templateTabBtn!.trigger('click')

      expect(wrapper.text()).toContain('All Templates')
      expect(wrapper.text()).toContain('Siemens S7-1500 Modular PLC')
    })

    it('populates form with item prop when editing existing record', () => {
      const mockItem = {
        id: 'comp-99',
        name: 'KUKA-ROBOT-01',
        displayName: 'Articulated Arm Cell 1',
        technology: 'KUKA Robotics',
        serialNumber: 'SN-KUKA-9922',
        costInHUF: 8900000,
        itemType: 'HardwareComponent',
        metadata: {
          Payload: '16 kg',
          Reach: '1610 mm'
        }
      }

      const wrapper = mount(AssetTabbedEditor, {
        props: {
          open: true,
          mode: 'edit',
          item: mockItem
        },
        global: {
          stubs: {
            Dialog: { template: '<div><slot /></div>' },
            DialogContent: { template: '<div><slot /></div>' },
            DialogHeader: { template: '<div><slot /></div>' },
            DialogTitle: { template: '<div><slot /></div>' },
            DialogDescription: { template: '<div><slot /></div>' },
            Badge: { template: '<span><slot /></span>' },
            SearchableSelect: { template: '<div class="searchable-select"></div>' }
          }
        }
      })

      expect(wrapper.text()).toContain('Edit Asset Record')
      const nameInput = wrapper.find('input')
      expect(nameInput.exists()).toBe(true)
    })
  })
})
