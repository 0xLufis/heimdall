import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { partsInventoryStore } from '../../../frontend/web/server/utils/partsInventoryStore'
import {
  getCanonicalInventoryTemplates,
  getCanonicalInventoryParts,
  getCanonicalMachineSpareParts,
  getCanonicalInventoryAuditLogs
} from '../../../frontend/web/server/utils/datasetLoader'
import PartProvisioningStation from '../../../frontend/web/app/components/inventory/PartProvisioningStation.vue'
import CreateEditTemplateModal from '../../../frontend/web/app/components/inventory/CreateEditTemplateModal.vue'
import type { InventoryPart, AssetTemplateDefinition } from '../../../frontend/web/app/types/inventory'

const mockActor = { id: 'usr-eng-1', name: 'Elena Rostova' }

describe('Canonical Inventory Dataset & Store Architecture', () => {
  beforeEach(() => {
    partsInventoryStore.resetToSeed()
  })

  it('loads canonical dataset correctly from datasetLoader fixtures', () => {
    const templates = getCanonicalInventoryTemplates()
    const parts = getCanonicalInventoryParts()
    const spareParts = getCanonicalMachineSpareParts()
    const auditLogs = getCanonicalInventoryAuditLogs()

    expect(templates.length).toBeGreaterThanOrEqual(8)
    expect(parts.length).toBeGreaterThanOrEqual(12)
    expect(spareParts.length).toBeGreaterThanOrEqual(4)
    expect(auditLogs.length).toBeGreaterThanOrEqual(2)

    // Verify key canonical identifiers exist
    expect(templates.some(t => t.id === 'tmpl-ipc-controller')).toBe(true)
    expect(templates.some(t => t.id === 'tmpl-vision-sensor')).toBe(true)
    expect(parts.some(p => p.customIdentifier === 'IPC-1001')).toBe(true)
    expect(parts.some(p => p.customIdentifier === 'FST-VLV-M5')).toBe(true)
  })

  it('initializes partsInventoryStore with canonical parts, templates, and spare parts policies', () => {
    const { parts: allParts } = partsInventoryStore.getAllParts({})
    const allTemplates = partsInventoryStore.getTemplates()
    const { spareParts } = partsInventoryStore.getMachineSpareParts()

    expect(allParts.length).toBeGreaterThanOrEqual(12)
    expect(allTemplates.length).toBeGreaterThanOrEqual(8)
    expect(spareParts.length).toBeGreaterThanOrEqual(4)

    // Check scalar floating min quantity propagation on canonical parts
    const ipcPart = allParts.find(p => p.customIdentifier === 'IPC-1001')
    expect(ipcPart).toBeDefined()
    expect(ipcPart?.minQuantityScalar).toBeDefined()
    expect(ipcPart?.effectiveMinQuantity).toBeDefined()
  })
})

describe('Asset Templates Lifecycle CRUD & OOP Inheritance', () => {
  beforeEach(() => {
    partsInventoryStore.resetToSeed()
  })

  it('creates a new asset template and records it in the audit ledger', () => {
    const newTemplate = partsInventoryStore.createTemplate({
      name: 'Delta Parallel Robot Delta-X',
      topLevelCategory: 'Hardware',
      identifierPattern: 'ROB-DLT-{number}',
      sequentialCounter: 50,
      description: 'High-speed delta pick and place robot arm',
      icon: 'Cpu',
      fixedFields: {
        manufacturer: 'Omron Adept',
        supplier: 'Omron Direct',
        category: 'Robotics',
        basePriceEur: 18500,
        specs: { payloadKg: 3, repeatabilityMm: 0.05 },
        defaultOwner: { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' }
      },
      instanceSpecificFieldsSchema: [
        { key: 'serialNumber', label: 'Robot Serial', type: 'string', required: true },
        { key: 'toolingType', label: 'End Effector Gripper', type: 'string', required: false }
      ],
      tags: ['Robot', 'Delta', 'HighSpeed']
    }, mockActor)

    expect(newTemplate.id).toBeDefined()
    expect(newTemplate.name).toBe('Delta Parallel Robot Delta-X')
    expect(newTemplate.sequentialCounter).toBe(50)

    const retrieved = partsInventoryStore.getTemplateById(newTemplate.id)
    expect(retrieved).toBeDefined()
    expect(retrieved?.fixedFields.manufacturer).toBe('Omron Adept')

    // Check audit log
    const auditLogs = partsInventoryStore.getAuditLog()
    expect(auditLogs[0].partName).toBe('Delta Parallel Robot Delta-X')
  })

  it('updates an existing asset template specs and description', () => {
    const updated = partsInventoryStore.updateTemplate('tmpl-ipc-controller', {
      description: 'Updated Beckhoff industrial PC with enhanced RAM',
      fixedFields: {
        manufacturer: 'Beckhoff Automation GmbH',
        category: 'Realtime Controller',
        supplier: 'Beckhoff Direct',
        basePriceEur: 3200,
        specs: { ramGB: 32, formFactor: 'DIN-Rail Mount' },
        defaultOwner: { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' }
      }
    }, mockActor)

    expect(updated.description).toBe('Updated Beckhoff industrial PC with enhanced RAM')
    expect(updated.fixedFields.basePriceEur).toBe(3200)

    const fetched = partsInventoryStore.getTemplateById('tmpl-ipc-controller')
    expect(fetched?.description).toBe('Updated Beckhoff industrial PC with enhanced RAM')
  })

  it('prevents deleting a template when active parts are instantiated with it', () => {
    expect(() => {
      partsInventoryStore.deleteTemplate('tmpl-ipc-controller', mockActor)
    }).toThrow(/Cannot delete template.*parts are currently instantiated/)
  })

  it('prevents deleting a root template when child templates inherit from it', () => {
    // Delete any parts using child templates or child itself first to test parent inheritance protection
    expect(() => {
      partsInventoryStore.deleteTemplate('tmpl-base-hardware', mockActor)
    }).toThrow(/Cannot delete template/)
  })

  it('successfully deletes an unreferenced template', () => {
    const tempTmpl = partsInventoryStore.createTemplate({
      name: 'Temporary Unused Template',
      topLevelCategory: 'Hardware'
    }, mockActor)

    const deleted = partsInventoryStore.deleteTemplate(tempTmpl.id, mockActor)
    expect(deleted).toBe(true)
    expect(partsInventoryStore.getTemplateById(tempTmpl.id)).toBeUndefined()
  })
})

describe('PartProvisioningStation Component', () => {
  const sampleParts: InventoryPart[] = [
    {
      id: 'prt-prov-1',
      customIdentifier: 'IPC-1001',
      name: 'Beckhoff CX5140 Modular Controller IPC',
      category: 'Hardware',
      trackingType: 'serialized',
      condition: 'new',
      operationalState: 'working',
      manufacturer: { name: 'Beckhoff Automation' },
      owner: { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' },
      location: 'Warehouse Central / Rack 2',
      priceEur: 2850,
      estimatedResellPriceEur: 2850,
      quantity: 1,
      minQuantity: 1,
      minQuantityScalar: 1.5,
      effectiveMinQuantity: 1.5,
      stockAlertStatus: 'optimal',
      createdAt: '2026-02-01T00:00:00Z',
      updatedAt: '2026-02-01T00:00:00Z'
    },
    {
      id: 'prt-prov-2',
      customIdentifier: 'FST-VLV-M5',
      name: 'Festo Compact Solenoid Valve',
      category: 'Hardware',
      trackingType: 'bulk',
      condition: 'new',
      operationalState: 'working',
      manufacturer: { name: 'Festo' },
      owner: { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' },
      location: 'Warehouse Central / Bin 18',
      priceEur: 85,
      estimatedResellPriceEur: 85,
      quantity: 15,
      minQuantity: 5,
      minQuantityScalar: 1.2,
      effectiveMinQuantity: 6.0,
      stockAlertStatus: 'optimal',
      createdAt: '2026-02-01T00:00:00Z',
      updatedAt: '2026-02-01T00:00:00Z'
    }
  ]

  it('renders part catalog and allows selecting a part into the workstation', async () => {
    const wrapper = mount(PartProvisioningStation, {
      props: {
        parts: sampleParts
      },
      global: {
        stubs: {
          Button: { template: '<button><slot /></button>' },
          Input: {
            props: ['modelValue'],
            emits: ['update:modelValue'],
            template: '<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
          },
          Label: { template: '<label><slot /></label>' }
        }
      }
    })

    expect(wrapper.text()).toContain('Part Provisioning & Floor Workstation')
    expect(wrapper.text()).toContain('IPC-1001')
    expect(wrapper.text()).toContain('FST-VLV-M5')

    // Click on IPC-1001 to select into active workstation
    const partCards = wrapper.findAll('.cursor-pointer')
    const ipcCard = partCards.find(c => c.text().includes('IPC-1001'))
    await ipcCard?.trigger('click')

    expect(wrapper.text()).toContain('Beckhoff CX5140 Modular Controller IPC')
    expect(wrapper.text()).toContain('Mandatory Cost Center Allocation')
  })

  it('enforces mandatory cost center tagging with OR relation (not AND)', async () => {
    const wrapper = mount(PartProvisioningStation, {
      props: {
        parts: sampleParts,
        preselectedPartId: 'prt-prov-2'
      },
      global: {
        stubs: {
          Button: { template: '<button><slot /></button>' },
          Input: {
            props: ['modelValue'],
            emits: ['update:modelValue'],
            template: '<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
          },
          Label: { template: '<label><slot /></label>' }
        }
      }
    })

    // Initially all cost center fields are empty: tag is required
    expect(wrapper.text()).toContain('Tag Required (OR)')

    // 1. Only prodLine filled -> valid
    const inputs = wrapper.findAll('input')
    // Find inputs for prodLine, project, department
    const vm = wrapper.vm as any
    vm.prodLine = 'Line 1 - Battery Assembly'
    await wrapper.vm.$nextTick()
    expect(vm.isCostCenterValid).toBe(true)
    expect(wrapper.text()).toContain('Cost Center Tagged')

    // 2. Only project filled -> valid
    vm.prodLine = ''
    vm.project = 'PRJ-TESLA-MOD3'
    await wrapper.vm.$nextTick()
    expect(vm.isCostCenterValid).toBe(true)

    // 3. Only department filled -> valid
    vm.project = ''
    vm.department = 'Automation & Robotics'
    await wrapper.vm.$nextTick()
    expect(vm.isCostCenterValid).toBe(true)

    // 4. All empty -> invalid
    vm.department = ''
    await wrapper.vm.$nextTick()
    expect(vm.isCostCenterValid).toBe(false)
  })
})

describe('CreateEditTemplateModal Component', () => {
  it('mounts properly and provides template creation form with schema builder', () => {
    const wrapper = mount(CreateEditTemplateModal, {
      props: {
        open: true,
        mode: 'create',
        availableTemplates: []
      },
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogHeader: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<h2><slot /></h2>' },
          DialogDescription: { template: '<p><slot /></p>' },
          DialogFooter: { template: '<div><slot /></div>' },
          Button: { template: '<button><slot /></button>' },
          Input: {
            props: ['modelValue'],
            emits: ['update:modelValue'],
            template: '<input :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
          },
          Label: { template: '<label><slot /></label>' }
        }
      }
    })

    expect(wrapper.text()).toContain('Create Asset Template')
    expect(wrapper.text()).toContain('1. Identity & Scheme')
    expect(wrapper.text()).toContain('2. OEM & Fixed Specs')
    expect(wrapper.text()).toContain('3. Instance Schema')
  })
})
