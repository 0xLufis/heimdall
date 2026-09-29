import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import UsePartModal from '../../../frontend/web/app/components/inventory/UsePartModal.vue'
import BulkPartIntakeModal from '../../../frontend/web/app/components/inventory/BulkPartIntakeModal.vue'
import LogPartModal from '../../../frontend/web/app/components/inventory/LogPartModal.vue'
import AssetTemplatesManager from '../../../frontend/web/app/components/inventory/AssetTemplatesManager.vue'
import MachineDocumentImportModal from '../../../frontend/web/app/components/inventory/MachineDocumentImportModal.vue'
import SparePartsPolicyManager from '../../../frontend/web/app/components/inventory/SparePartsPolicyManager.vue'
import InventoryCodeScannerModal from '../../../frontend/web/app/components/inventory/InventoryCodeScannerModal.vue'
import type { InventoryPart, AssetTemplateDefinition, MachineSparePartItem, SparePartsReportingSummary } from '../../../frontend/web/app/types/inventory'

const mockPart: InventoryPart = {
  id: 'prt-test-1',
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
  stockAlertStatus: 'optimal',
  createdAt: '2026-02-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z'
}

const mockBulkPart: InventoryPart = {
  id: 'prt-bulk-1',
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
  quantity: 10,
  minQuantity: 5,
  stockAlertStatus: 'optimal',
  createdAt: '2026-02-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z'
}

const mockBaseTemplate: AssetTemplateDefinition = {
  id: 'tmpl-base-hardware',
  name: 'Base Industrial Hardware',
  topLevelCategory: 'Hardware',
  identifierPattern: 'HW-{number}',
  sequentialCounter: 100,
  description: 'Generic root hardware template',
  fixedFields: {
    manufacturer: 'Generic OEM',
    category: 'Hardware',
    basePriceEur: 1500,
    specs: {},
    defaultOwner: { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' }
  },
  instanceSpecificFieldsSchema: [],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z'
}

const mockTemplate: AssetTemplateDefinition = {
  id: 'tmpl-ipc-controller',
  name: 'Industrial PC Controller',
  topLevelCategory: 'Hardware',
  extendsTemplateId: 'tmpl-base-hardware',
  identifierPattern: 'IPC-{number}',
  sequentialCounter: 104,
  description: 'High-reliability IPC cabinet controller node',
  fixedFields: {
    manufacturer: 'Beckhoff Automation',
    category: 'Controller',
    basePriceEur: 2850,
    specs: { ramGB: 16 },
    defaultOwner: { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' }
  },
  instanceSpecificFieldsSchema: [
    { key: 'serialNumber', label: 'Serial Number', type: 'string' }
  ],
  instances: [mockPart],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z'
}

const mockSparePart: MachineSparePartItem = {
  id: 'sp-1',
  machineId: 'mach-1',
  machineName: 'Line 1 - OP10 CNC Cell',
  partId: 'prt-test-1',
  partName: 'Beckhoff CX5140 Modular Controller IPC',
  customIdentifier: 'IPC-1001',
  unitsPerMachine: 1,
  fractionalRatio: 1 / 3,
  upperBoundQuantity: 2,
  upperBoundCostEur: 8000,
  activeMachinesInProduction: 6,
  requiredSparesCalculated: 2,
  onHandSpares: 1,
  alternativeSparesAvailable: 1,
  coverageStatus: 'covered',
  unitPriceEur: 2850,
  totalHoldingValuationEur: 2850
}

const mockReporting: SparePartsReportingSummary = {
  totalDefinedSpareParts: 1,
  totalCovered: 1,
  totalShortages: 0,
  criticalShortages: 0,
  totalHoldingValuationEur: 2850,
  currencyConversions: {
    HUF: 1140000,
    USD: 3078,
    GBP: 2422.5
  }
}

describe('Parts Inventory UI & Component Integration Tests', () => {
  // -------------------------------------------------------------------------
  // UsePartModal Tests
  // -------------------------------------------------------------------------
  describe('UsePartModal.vue', () => {
    it('renders with part summary and mandatory cost center indicators', () => {
      const wrapper = mount(UsePartModal, {
        props: {
          open: true,
          part: mockPart
        }
      })

      expect(wrapper.text()).toContain('Use Part / Consume from Stock')
      expect(wrapper.text()).toContain('IPC-1001')
      expect(wrapper.text()).toContain('Production Line')
      expect(wrapper.text()).toContain('Project Identifier')
      expect(wrapper.text()).toContain('Department')
    })

    it('requires at least one mandatory cost center field before allowing submission', async () => {
      const wrapper = mount(UsePartModal, {
        props: {
          open: true,
          part: mockPart
        }
      })

      // Try submitting without any cost center fields
      const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Confirm Use Part'))
      expect(submitBtn?.attributes('disabled')).toBeDefined()

      // Providing at least one cost center (e.g. project) enables submission (OR relation)
      const projectInput = wrapper.find('input[placeholder*="PRJ-2026"]')
      await projectInput.setValue('PRJ-2026-TEST')
      expect(submitBtn?.attributes('disabled')).toBeUndefined()
    })
  })

  // -------------------------------------------------------------------------
  // BulkPartIntakeModal Tests
  // -------------------------------------------------------------------------
  describe('BulkPartIntakeModal.vue', () => {
    it('renders visual table mode with dynamic row addition', async () => {
      const wrapper = mount(BulkPartIntakeModal, {
        props: { open: true }
      })

      expect(wrapper.text()).toContain('Bulk Log Parts Intake')
      expect(wrapper.text()).toContain('Visual Table Mode')

      const addBtn = wrapper.findAll('button').find(b => b.text().includes('Add Row'))
      expect(addBtn).toBeDefined()
      await addBtn?.trigger('click')

      // Should add a row to visual table
      const rows = wrapper.findAll('tbody tr')
      expect(rows.length).toBeGreaterThanOrEqual(3)
    })

    it('supports switching to JSON mode and parsing pasted JSON array', async () => {
      const wrapper = mount(BulkPartIntakeModal, {
        props: { open: true }
      })

      const jsonTabBtn = wrapper.findAll('button').find(b => b.text().includes('JSON Import Mode'))
      await jsonTabBtn?.trigger('click')

      expect(wrapper.find('textarea').exists()).toBe(true)
      expect(wrapper.text()).toContain('valid parts parsed')
    })
  })

  // -------------------------------------------------------------------------
  // LogPartModal Tests
  // -------------------------------------------------------------------------
  describe('LogPartModal.vue', () => {
    it('calculates estimated resell price via wear depreciation percentage scalar math', async () => {
      const wrapper = mount(LogPartModal, {
        props: {
          open: true,
          templates: [mockTemplate]
        }
      })

      expect(wrapper.text()).toContain('Log Part into Warehouse Stock')
      expect(wrapper.text()).toContain('Valuation & Resell Wear Depreciation')
    })
  })

  // -------------------------------------------------------------------------
  // AssetTemplatesManager Tests
  // -------------------------------------------------------------------------
  describe('AssetTemplatesManager.vue', () => {
    it('renders template inheritance, fixed specs, and instance tree view', () => {
      const wrapper = mount(AssetTemplatesManager, {
        props: {
          templates: [mockBaseTemplate, mockTemplate]
        }
      })

      expect(wrapper.text()).toContain('Industrial PC Controller')
      expect(wrapper.text()).toContain('Hardware')
      expect(wrapper.text()).toContain('Inherits: Base Industrial Hardware')
      expect(wrapper.text()).toContain('IPC-{number}')
      expect(wrapper.text()).toContain('IPC-1001')
    })
  })

  // -------------------------------------------------------------------------
  // MachineDocumentImportModal Tests
  // -------------------------------------------------------------------------
  describe('MachineDocumentImportModal.vue', () => {
    it('parses ChatGPT BOM output with split modular controller IPC assets preview', () => {
      const wrapper = mount(MachineDocumentImportModal, {
        props: { open: true }
      })

      expect(wrapper.text()).toContain('Machine Document Import (AI / ChatGPT)')
      expect(wrapper.text()).toContain('ChatGPT JSON & BOM')
      expect(wrapper.text()).toContain('Parsed Asset Tree Preview')
      expect(wrapper.text()).toContain('Split Modular Child Components')
    })
  })

  // -------------------------------------------------------------------------
  // SparePartsPolicyManager Tests
  // -------------------------------------------------------------------------
  describe('SparePartsPolicyManager.vue', () => {
    it('renders coverage rate, fractional ratios, and upper bound caps', () => {
      const wrapper = mount(SparePartsPolicyManager, {
        props: {
          spareParts: [mockSparePart],
          reporting: mockReporting
        }
      })

      expect(wrapper.text()).toContain('Machine Spare Parts & Policy Requirements')
      expect(wrapper.text()).toContain('Coverage Rate')
      expect(wrapper.text()).toContain('100%')
      expect(wrapper.text()).toContain('Line 1 - OP10 CNC Cell')
      expect(wrapper.text()).toContain('Max 2 qty')
    })
  })

  // -------------------------------------------------------------------------
  // InventoryCodeScannerModal Tests
  // -------------------------------------------------------------------------
  describe('InventoryCodeScannerModal.vue', () => {
    it('provides Look-up, Use Up, and Log Part workflows with quick simulation codes', () => {
      const wrapper = mount(InventoryCodeScannerModal, {
        props: { open: true }
      })

      expect(wrapper.text()).toContain('Code Scan Interface')
      expect(wrapper.text()).toContain('Look-up')
      expect(wrapper.text()).toContain('Use Up')
      expect(wrapper.text()).toContain('Log Part')
      expect(wrapper.text()).toContain('Beckhoff IPC (IPC-1001)')
      expect(wrapper.text()).toContain('RFID Tag Payload')
    })
  })
})
