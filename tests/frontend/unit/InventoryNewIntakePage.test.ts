import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import InventoryNewPage from '../../../src/frontend/web/app/pages/dashboard/inventory/new.vue'
import type { AssetTemplateDefinition } from '../../../src/frontend/web/app/types/inventory'

const mockPush = vi.fn()

vi.stubGlobal('definePageMeta', vi.fn())
vi.stubGlobal('navigateTo', mockPush)
vi.stubGlobal('useRouter', () => ({
  push: mockPush
}))

const mockTemplates: AssetTemplateDefinition[] = [
  {
    id: 'tmpl-ipc-1',
    name: 'Siemens IPC Controller',
    topLevelCategory: 'Hardware',
    identifierPattern: 'IPC-{number}',
    sequentialCounter: 201,
    description: 'Industrial rackmount controller PC',
    fixedFields: {
      manufacturer: 'Siemens AG',
      supplier: 'Siemens Direct',
      category: 'Hardware',
      basePriceEur: 1800,
      specs: {
        Voltage: '24V DC',
        'Form Factor': '19-inch 2U Rack',
        'Bus Protocol': 'PROFINET'
      },
      defaultOwner: { type: 'organization', id: 'org-1', name: 'Plant Operations' }
    },
    instanceSpecificFieldsSchema: [
      { key: 'macAddress', label: 'MAC Address', type: 'string', required: true }
    ],
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z'
  }
]

const mockLogPart = vi.fn().mockResolvedValue({ id: 'prt-new-1', name: 'Test Part' })
const mockBulkLogParts = vi.fn().mockResolvedValue([{ id: 'prt-batch-1' }, { id: 'prt-batch-2' }])

vi.mock('../../../src/frontend/web/app/composables/usePartsInventory', () => ({
  usePartsInventory: () => ({
    templates: ref(mockTemplates),
    indexedFields: ref({
      manufacturers: ['Siemens AG', 'Festo'],
      suppliers: ['Siemens Direct', 'Phoenix Contact'],
      categories: ['Hardware', 'Software'],
      locations: ['Crib A - Shelf 02', 'Crib B - Bin 04'],
      costCenters: ['CC-PROD-LINE-01'],
      machines: []
    }),
    fetchTemplates: vi.fn().mockResolvedValue(mockTemplates),
    fetchIndexedFields: vi.fn().mockResolvedValue({}),
    logPart: mockLogPart,
    bulkLogParts: mockBulkLogParts,
    loading: ref(false)
  })
}))

vi.mock('vue-sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn()
  }
}))

describe('Inventory New Intake Page (/dashboard/inventory/new)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const createWrapper = () => {
    return mount(InventoryNewPage, {
      global: {
        stubs: {
          NuxtLink: {
            props: ['to'],
            template: '<a :href="to"><slot /></a>'
          },
          Button: {
            template: '<button><slot /></button>'
          },
          Badge: {
            template: '<span><slot /></span>'
          },
          Input: {
            props: ['modelValue'],
            emits: ['update:modelValue'],
            template: '<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
          },
          Label: {
            template: '<label><slot /></label>'
          }
        }
      }
    })
  }

  it('renders breadcrumbs and page header correctly', () => {
    const wrapper = createWrapper()
    const text = wrapper.text()

    expect(text).toContain('Dashboard')
    expect(text).toContain('Inventory')
    expect(text).toContain('Ingest New Asset')
    expect(text).toContain('Industrial Asset & Parts Ingestion Workbench')
  })

  it('renders mode switch buttons for Single Item Intake and Batch CSV / JSON Import', async () => {
    const wrapper = createWrapper()
    expect(wrapper.text()).toContain('Single Item Intake')
    expect(wrapper.text()).toContain('Batch CSV / JSON Import')

    // Initial state is Single Item Intake
    expect(wrapper.text()).toContain('Identity & Classification')
    expect(wrapper.text()).toContain('Storage Logistics & Stock Levels')

    // Switch to Batch mode
    const buttons = wrapper.findAll('button')
    const batchBtn = buttons.find(b => b.text().includes('Batch CSV / JSON Import'))
    expect(batchBtn).toBeDefined()
    await batchBtn?.trigger('click')

    expect(wrapper.text()).toContain('Batch Ingestion Payload')
    expect(wrapper.text()).toContain('Parser Diagnostics')
  })

  it('inherits specs and identifier pattern when selecting a blueprint template', async () => {
    const wrapper = createWrapper()

    const select = wrapper.find('select')
    expect(select.exists()).toBe(true)

    // Select Siemens IPC Controller template
    await select.setValue('tmpl-ipc-1')
    await wrapper.vm.$nextTick()

    const text = wrapper.text()
    expect(text).toContain('Siemens IPC Controller')
    expect(text).toContain('Template Instance Fields (Siemens IPC Controller)')
    expect(text).toContain('MAC Address')
  })

  it('validates required fields before allowing submission in single item intake', async () => {
    const wrapper = createWrapper()

    // Clear SKU and Name to trigger validation errors
    const inputs = wrapper.findAll('input')
    const skuInput = inputs.find(i => i.attributes('placeholder')?.includes('HW-1042'))
    const nameInput = inputs.find(i => i.attributes('placeholder')?.includes('KUKA'))

    if (skuInput) await skuInput.setValue('')
    if (nameInput) await nameInput.setValue('')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('Part Number / SKU is required.')
    expect(wrapper.text()).toContain('Asset name is required.')
  })

  it('supports loading sample batch data and parses JSON records correctly', async () => {
    const wrapper = createWrapper()

    // Switch to Batch mode
    const buttons = wrapper.findAll('button')
    const batchBtn = buttons.find(b => b.text().includes('Batch CSV / JSON Import'))
    await batchBtn?.trigger('click')

    // Click Load Sample button
    const loadSampleBtn = wrapper.findAll('button').find(b => b.text().includes('Load Sample'))
    expect(loadSampleBtn).toBeDefined()
    await loadSampleBtn?.trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('3 Ready')
    expect(wrapper.text()).toContain('3 Records Detected')
    expect(wrapper.text()).toContain('Beckhoff CX5140 Modular Controller IPC')
  })

  it('successfully submits single item and navigates to /dashboard/inventory', async () => {
    const wrapper = createWrapper()

    // Populate required fields
    const inputs = wrapper.findAll('input')
    const skuInput = inputs.find(i => i.attributes('placeholder')?.includes('HW-1042'))
    const nameInput = inputs.find(i => i.attributes('placeholder')?.includes('KUKA'))

    if (skuInput) await skuInput.setValue('HW-9988')
    if (nameInput) await nameInput.setValue('Precision Servomotor Cell 1')
    await wrapper.vm.$nextTick()

    // Find and trigger Ingest Asset button
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Ingest Asset to Registry'))
    expect(submitBtn).toBeDefined()
    await submitBtn?.trigger('click')

    expect(mockLogPart).toHaveBeenCalled()
    expect(mockPush).toHaveBeenCalledWith('/dashboard/inventory')
  })
})
