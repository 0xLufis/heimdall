import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useGenericTemplates } from '@/composables/useGenericTemplates'
import TemplateFieldRenderer from '@/components/templates/TemplateFieldRenderer.vue'
import TemplateFormRenderer from '@/components/templates/TemplateFormRenderer.vue'
import TemplateSchemaBuilder from '@/components/templates/TemplateSchemaBuilder.vue'
import TemplateSelector from '@/components/templates/TemplateSelector.vue'
import type { GenericTemplate, TemplateFieldDefinition } from '@/types/templates'

describe('Generic Templating Engine & Reusable Studio Components Suite', () => {
  const mockTemplates: GenericTemplate[] = [
    {
      id: 'tpl-servo-01',
      templateType: 'component',
      code: 'CMP-SERVO-01',
      name: 'High-Speed Servo Axis',
      description: 'Dynamic positioning servo actuator',
      category: 'Motion',
      icon: 'Cpu',
      version: '1.0.0',
      tags: ['Servo', 'Motion', 'EtherCAT'],
      schema: [
        {
          key: 'axis_id',
          label: 'Axis Identifier',
          fieldType: 'string',
          required: true,
          defaultValue: 'AXIS-01',
          section: 'Identity'
        },
        {
          key: 'rated_torque_nm',
          label: 'Rated Torque',
          fieldType: 'number',
          required: true,
          defaultValue: 12.5,
          unit: 'Nm',
          min: 1.0,
          max: 100.0,
          section: 'Specifications'
        },
        {
          key: 'holding_brake',
          label: 'Holding Brake',
          fieldType: 'boolean',
          defaultValue: true,
          section: 'Safety'
        },
        {
          key: 'fieldbus',
          label: 'Fieldbus Protocol',
          fieldType: 'select',
          required: true,
          defaultValue: 'EtherCAT',
          section: 'Connectivity',
          options: [
            { label: 'EtherCAT', value: 'EtherCAT' },
            { label: 'PROFINET', value: 'PROFINET' }
          ]
        }
      ],
      defaultValues: {
        axis_id: 'AXIS-01',
        rated_torque_nm: 12.5,
        holding_brake: true,
        fieldbus: 'EtherCAT'
      },
      isActive: true,
      isBuiltin: true,
      createdAt: '2026-10-21T10:00:00Z',
      updatedAt: '2026-10-21T10:00:00Z'
    },
    {
      id: 'tpl-valve-01',
      templateType: 'component',
      code: 'CMP-VALVE-01',
      name: 'Pneumatic Proportional Valve',
      description: 'Directional valve for tooling clamps',
      category: 'Pneumatics',
      icon: 'Sliders',
      version: '1.0.0',
      tags: ['Pneumatics', 'Valve'],
      schema: [
        {
          key: 'port_size',
          label: 'Port Size',
          fieldType: 'string',
          defaultValue: 'G1/4'
        }
      ],
      defaultValues: {
        port_size: 'G1/4'
      },
      isActive: true,
      isBuiltin: false,
      createdAt: '2026-10-21T10:00:00Z',
      updatedAt: '2026-10-21T10:00:00Z'
    }
  ]

  beforeEach(() => {
    vi.clearAllMocks()
    ;(globalThis as any).$fetch = vi.fn().mockImplementation((url: string, opts?: any) => {
      if (url === '/api/v1/templates') {
        if (opts?.method === 'POST') {
          return Promise.resolve({
            id: 'tpl-new-01',
            ...opts.body,
            createdAt: '2026-10-21T12:00:00Z',
            updatedAt: '2026-10-21T12:00:00Z'
          })
        }
        return Promise.resolve(mockTemplates)
      }
      if (url.includes('/api/v1/templates/tpl-servo-01/resolve')) {
        return Promise.resolve({
          templateId: 'tpl-servo-01',
          code: 'CMP-SERVO-01',
          name: 'High-Speed Servo Axis',
          lineage: [{ id: 'tpl-servo-01', code: 'CMP-SERVO-01', name: 'High-Speed Servo Axis' }],
          effectiveSchema: mockTemplates[0].schema,
          effectiveDefaultValues: mockTemplates[0].defaultValues
        })
      }
      if (url.includes('/api/v1/templates/types')) {
        return Promise.resolve([
          { type: 'component', label: 'Machine Component', description: 'Components', icon: 'Cpu', defaultCategories: ['Motion'] }
        ])
      }
      if (url.includes('/instantiate')) {
        return Promise.resolve({
          success: true,
          instanceData: opts.body?.values || {},
          errors: [],
          appliedSchema: mockTemplates[0].schema
        })
      }
      if (url.includes('/validate')) {
        return Promise.resolve({
          isValid: true,
          errors: [],
          warnings: []
        })
      }
      return Promise.resolve({})
    })
  })

  describe('useGenericTemplates composable', () => {
    it('fetches templates and selects active template', async () => {
      const { templates, activeTemplate, fetchTemplates } = useGenericTemplates()
      await fetchTemplates()
      expect(templates.value.length).toBe(2)
      expect(activeTemplate.value?.code).toBe('CMP-SERVO-01')
    })

    it('creates a new template and prepends to catalog', async () => {
      const { createTemplate, templates } = useGenericTemplates()
      const created = await createTemplate({
        templateType: 'component',
        code: 'CMP-OPTIC-01',
        name: 'Optical Sensor',
        category: 'Optics'
      })
      expect(created.code).toBe('CMP-OPTIC-01')
      expect(templates.value[0].code).toBe('CMP-OPTIC-01')
    })

    it('exports template to valid JSON', () => {
      const { exportTemplateAsJson } = useGenericTemplates()
      const json = exportTemplateAsJson(mockTemplates[0])
      expect(json).toContain('CMP-SERVO-01')
      expect(json).toContain('High-Speed Servo Axis')
      const parsed = JSON.parse(json)
      expect(parsed.code).toBe('CMP-SERVO-01')
      expect(parsed.schema.length).toBe(4)
    })
  })

  describe('TemplateFieldRenderer component', () => {
    it('renders text input with label and placeholder', async () => {
      const field: TemplateFieldDefinition = {
        key: 'device_name',
        label: 'Device Name',
        fieldType: 'string',
        required: true,
        placeholder: 'Enter name...'
      }

      const wrapper = mount(TemplateFieldRenderer, {
        props: {
          field,
          modelValue: 'Main Gantry'
        }
      })

      expect(wrapper.text()).toContain('Device Name')
      expect(wrapper.text()).toContain('*')
      const input = wrapper.find('input')
      expect(input.exists()).toBe(true)
      expect(input.element.value).toBe('Main Gantry')
    })

    it('renders number input with unit badge and emits updates', async () => {
      const field: TemplateFieldDefinition = {
        key: 'speed_rpm',
        label: 'Rotational Velocity',
        fieldType: 'number',
        unit: 'RPM',
        min: 100,
        max: 6000
      }

      const wrapper = mount(TemplateFieldRenderer, {
        props: {
          field,
          modelValue: 3000
        }
      })

      expect(wrapper.text()).toContain('Rotational Velocity')
      expect(wrapper.text()).toContain('RPM')
      const input = wrapper.find('input[type="number"]')
      expect(input.exists()).toBe(true)
      expect(input.element.value).toBe('3000')

      await input.setValue('4500')
      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([4500])
    })

    it('renders boolean switch toggle and toggles state', async () => {
      const field: TemplateFieldDefinition = {
        key: 'auto_start',
        label: 'Automatic Start',
        fieldType: 'boolean',
        defaultValue: false
      }

      const wrapper = mount(TemplateFieldRenderer, {
        props: {
          field,
          modelValue: false
        }
      })

      expect(wrapper.text()).toContain('Automatic Start')
      const switchBtn = wrapper.find('button[role="switch"]')
      expect(switchBtn.exists()).toBe(true)

      await switchBtn.trigger('click')
      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
    })

    it('renders select dropdown with options', async () => {
      const field: TemplateFieldDefinition = {
        key: 'mode',
        label: 'Operating Mode',
        fieldType: 'select',
        options: [
          { label: 'Automatic', value: 'AUTO' },
          { label: 'Manual Jog', value: 'JOG' }
        ]
      }

      const wrapper = mount(TemplateFieldRenderer, {
        props: {
          field,
          modelValue: 'AUTO'
        }
      })

      const select = wrapper.find('select')
      expect(select.exists()).toBe(true)
      expect(select.element.value).toBe('AUTO')
      expect(wrapper.text()).toContain('Automatic')
      expect(wrapper.text()).toContain('Manual Jog')
    })

    it('renders error message when error prop is provided', () => {
      const field: TemplateFieldDefinition = {
        key: 'pressure',
        label: 'Pressure',
        fieldType: 'number'
      }

      const wrapper = mount(TemplateFieldRenderer, {
        props: {
          field,
          error: 'Pressure cannot exceed 10 bar.'
        }
      })

      expect(wrapper.text()).toContain('Pressure cannot exceed 10 bar.')
    })
  })

  describe('TemplateFormRenderer component', () => {
    it('groups fields by section and renders grouped headers', () => {
      const schema: TemplateFieldDefinition[] = [
        { key: 'f1', label: 'Field 1', fieldType: 'string', section: 'General' },
        { key: 'f2', label: 'Field 2', fieldType: 'number', section: 'Specifications' },
        { key: 'f3', label: 'Field 3', fieldType: 'boolean', section: 'Specifications' }
      ]

      const wrapper = mount(TemplateFormRenderer, {
        props: {
          schema,
          modelValue: { f1: 'Val 1', f2: 10, f3: true }
        }
      })

      expect(wrapper.text()).toContain('General')
      expect(wrapper.text()).toContain('Specifications')
      expect(wrapper.text()).toContain('All required template specifications satisfied')
    })

    it('shows missing required field warning in summary bar', () => {
      const schema: TemplateFieldDefinition[] = [
        { key: 'req_field', label: 'Mandatory Setting', fieldType: 'string', required: true }
      ]

      const wrapper = mount(TemplateFormRenderer, {
        props: {
          schema,
          modelValue: {}
        }
      })

      expect(wrapper.text()).toContain('1 required field(s) require input')
      expect(wrapper.text()).toContain('1 missing')
    })
  })

  describe('TemplateSchemaBuilder component', () => {
    it('renders list of schema fields and permits adding a new field', async () => {
      const schema: TemplateFieldDefinition[] = [
        { key: 'power_kw', label: 'Motor Power', fieldType: 'number', order: 1 }
      ]

      const wrapper = mount(TemplateSchemaBuilder, {
        props: { schema }
      })

      expect(wrapper.text()).toContain('Motor Power')
      expect(wrapper.text()).toContain('power_kw')

      // Click Add Field
      const addBtn = wrapper.find('button')
      await addBtn.trigger('click')

      expect(wrapper.emitted('update:schema')).toBeTruthy()
      const emittedSchema = wrapper.emitted('update:schema')?.[0]?.[0] as TemplateFieldDefinition[]
      expect(emittedSchema.length).toBe(2)
      expect(emittedSchema[1].key).toBe('field_2')
    })
  })

  describe('TemplateSelector component', () => {
    it('renders template list and filters by search query', async () => {
      const wrapper = mount(TemplateSelector, {
        props: {
          templates: mockTemplates,
          selectedId: 'tpl-servo-01'
        }
      })

      expect(wrapper.text()).toContain('High-Speed Servo Axis')
      expect(wrapper.text()).toContain('Pneumatic Proportional Valve')

      // Filter by search
      const searchInput = wrapper.find('input')
      await searchInput.setValue('Servo')

      expect(wrapper.text()).toContain('High-Speed Servo Axis')
      expect(wrapper.text()).not.toContain('Pneumatic Proportional Valve')
    })

    it('emits select event when a template card is clicked', async () => {
      const wrapper = mount(TemplateSelector, {
        props: {
          templates: mockTemplates
        }
      })

      const cards = wrapper.findAll('.cursor-pointer')
      expect(cards.length).toBeGreaterThan(0)
      await cards[0].trigger('click')

      expect(wrapper.emitted('select')).toBeTruthy()
      expect((wrapper.emitted('select')?.[0]?.[0] as GenericTemplate).code).toBe('CMP-SERVO-01')
    })
  })
})
