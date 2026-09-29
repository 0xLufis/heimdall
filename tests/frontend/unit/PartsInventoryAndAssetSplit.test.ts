import { describe, it, expect, beforeEach } from 'vitest'
import { partsInventoryStore, convertCurrency, LIVE_EXCHANGE_RATES } from '../../../frontend/web/server/utils/partsInventoryStore'
import type { PartUsageCostCenter } from '../../../frontend/web/app/types/inventory'

describe('Parts Inventory and Asset Split (docs/TODO/inventory.TODO.md)', () => {
  beforeEach(() => {
    partsInventoryStore.resetToSeed()
  })

  // -------------------------------------------------------------------------
  // 1. Split Parts Inventory: Bulk & Serialized Tracking, Lifecycle & Operational States
  // -------------------------------------------------------------------------
  describe('Parts Inventory Classification & Lifecycles', () => {
    it('supports tracking both bulk consumables and serialized asset units', () => {
      const { parts, kpis } = partsInventoryStore.getAllParts()

      const serialized = parts.filter(p => p.trackingType === 'serialized')
      const bulk = parts.filter(p => p.trackingType === 'bulk')

      expect(serialized.length).toBeGreaterThan(0)
      expect(bulk.length).toBeGreaterThan(0)
      expect(kpis.serializedCount).toBe(serialized.length)
      expect(kpis.bulkCount).toBeGreaterThanOrEqual(15) // sum of bulk quantities
    })

    it('tracks lifecycle conditions: new, used, donor, obsolete, scrap ("can be get rid of")', () => {
      const { parts } = partsInventoryStore.getAllParts()

      const newParts = parts.filter(p => p.condition === 'new')
      const usedParts = parts.filter(p => p.condition === 'used')
      const donorParts = parts.filter(p => p.condition === 'donor')
      const scrapParts = parts.filter(p => p.condition === 'scrap')

      expect(newParts.length).toBeGreaterThan(0)
      expect(usedParts.length).toBeGreaterThan(0)
      expect(donorParts.length).toBeGreaterThan(0)
      expect(scrapParts.length).toBeGreaterThan(0)

      // Scrap parts are marked as unserviceable / can be got rid of
      const scrapUnit = scrapParts[0]
      expect(scrapUnit.condition).toBe('scrap')
      expect(scrapUnit.operationalState).toBe('broken')
      expect(scrapUnit.stockAlertStatus).toBe('out_of_stock')
    })

    it('tracks serialized operational states: working, in_service, evaluation, broken', () => {
      const { parts } = partsInventoryStore.getAllParts()
      const serialized = parts.filter(p => p.trackingType === 'serialized')

      const working = serialized.filter(p => p.operationalState === 'working')
      const evaluation = serialized.filter(p => p.operationalState === 'evaluation')
      const broken = serialized.filter(p => p.operationalState === 'broken')

      expect(working.length).toBeGreaterThan(0)
      expect(evaluation.length).toBeGreaterThan(0)
      expect(broken.length).toBeGreaterThan(0)
    })

    it('calculates stock alerts based on minimum quantities and zero/scrap states', () => {
      // prt-blk-202 has qty 3 with min 10 -> low_stock
      const lowStockPart = partsInventoryStore.getPartByIdOrIdentifier('FST-VLV-M5')
      expect(lowStockPart?.stockAlertStatus).toBe('low_stock')

      // prt-blk-203 has qty 0 with min 15 -> out_of_stock
      const outOfStockPart = partsInventoryStore.getPartByIdOrIdentifier('IND-PRX-M12')
      expect(outOfStockPart?.stockAlertStatus).toBe('out_of_stock')

      // prt-ser-101 has qty 1 with min 1 -> optimal
      const optimalPart = partsInventoryStore.getPartByIdOrIdentifier('IPC-1001')
      expect(optimalPart?.stockAlertStatus).toBe('optimal')
    })
  })

  // -------------------------------------------------------------------------
  // 2. Logging Parts: Single Intake & Bulk Logging (Visual / JSON)
  // -------------------------------------------------------------------------
  describe('Log Part and Bulk Intake Features', () => {
    it('logs single part with automatic sequential identifier generation from template', () => {
      const actor = { id: 'usr-eng-1', name: 'Gábor Varga' }
      const newPart = partsInventoryStore.logPart({
        templateId: 'tmpl-ipc-controller',
        name: 'Beckhoff CX5140 Station C',
        condition: 'new',
        operationalState: 'working',
        location: 'Warehouse Central / Shelf C1',
        trackingType: 'serialized'
      }, actor)

      expect(newPart.id).toBeDefined()
      expect(newPart.customIdentifier).toMatch(/^IPC-\d{4}$/)
      expect(newPart.quantity).toBe(1) // Fixed to 1 for serialized
      expect(newPart.manufacturer.name).toBe('Beckhoff Automation')

      // Audit log check
      const logs = partsInventoryStore.getAuditLog(newPart.id)
      expect(logs.length).toBe(1)
      expect(logs[0].action).toBe('log_part')
      expect(logs[0].actorName).toBe('Gábor Varga')
    })

    it('bulk logs multiple parts simultaneously (visual row grid or JSON array)', () => {
      const actor = { id: 'usr-tech-1', name: 'Elena Rostova' }
      const batch = [
        {
          name: 'Siemens ET 200SP BaseUnit',
          category: 'Hardware' as const,
          trackingType: 'bulk' as const,
          condition: 'new' as const,
          manufacturer: { name: 'Siemens' },
          quantity: 25,
          minQuantity: 5,
          location: 'Bin 42',
          priceEur: 45
        },
        {
          name: 'KUKA Teach Pendant Cable 10m',
          category: 'Hardware' as const,
          trackingType: 'serialized' as const,
          condition: 'new' as const,
          operationalState: 'working' as const,
          manufacturer: { name: 'KUKA' },
          location: 'Rack 12',
          priceEur: 320
        }
      ]

      const created = partsInventoryStore.bulkLogParts(batch, actor)
      expect(created.length).toBe(2)
      expect(created[0].quantity).toBe(25)
      expect(created[1].quantity).toBe(1)

      const globalLogs = partsInventoryStore.getAuditLog()
      expect(globalLogs.some(l => l.partName.includes('Siemens ET 200SP'))).toBe(true)
      expect(globalLogs.some(l => l.partName.includes('KUKA Teach Pendant'))).toBe(true)
    })
  })

  // -------------------------------------------------------------------------
  // 3. "Use Part" Feature & Strict Mandatory Cost Center Enforcement
  // -------------------------------------------------------------------------
  describe('Use Part Feature & Mandatory Cost Centers', () => {
    it('successfully consumes a bulk part and records mandatory cost centers in immutable audit log', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IO-LINK-TERM-16')!
      const initialQty = part.quantity

      const costCenter: PartUsageCostCenter = {
        prodLine: 'Line 1 - OP10 CNC Milling Cell',
        project: 'PRJ-2026-NMC-EXPANSION',
        department: 'Controls & Robotics',
        technician: 'Elena Rostova',
        notes: 'Replacing damaged IO-Link terminal block'
      }

      const actor = { id: 'usr-tech-1', name: 'Elena Rostova' }
      const result = partsInventoryStore.usePart(part.id, costCenter, 2, actor)

      expect(result.success).toBe(true)
      expect(result.part.quantity).toBe(initialQty - 2)

      // Audit Record verification
      expect(result.auditRecord.action).toBe('use_part')
      expect(result.auditRecord.costCenter?.prodLine).toBe('Line 1 - OP10 CNC Milling Cell')
      expect(result.auditRecord.costCenter?.project).toBe('PRJ-2026-NMC-EXPANSION')
      expect(result.auditRecord.costCenter?.department).toBe('Controls & Robotics')
      expect(result.auditRecord.quantityDelta).toBe(-2)
    })

    it('transitions serialized part to in_service deployed status upon usage', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IPC-1001')!
      expect(part.quantity).toBe(1)
      expect(part.operationalState).toBe('working')

      const costCenter: PartUsageCostCenter = {
        prodLine: 'Line 2 - Battery Module Assembly',
        project: 'LINE2-AUTOMATION',
        department: 'Plant Maintenance'
      }

      const result = partsInventoryStore.usePart(part.id, costCenter, 1, { id: 'usr-1', name: 'Gábor Varga' })
      expect(result.success).toBe(true)
      expect(result.part.quantity).toBe(0)
      expect(result.part.operationalState).toBe('in_service')
      expect(result.part.location).toContain('Line 2 - Battery Module Assembly')
      expect(result.part.stockAlertStatus).toBe('out_of_stock')
    })

    it('allows usage when ONLY production line (prodLine) is provided (OR relation)', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IO-LINK-TERM-16')!
      const initialQty = part.quantity
      const costCenter: PartUsageCostCenter = {
        prodLine: 'Line 3 - Assembly Cell'
      }

      const result = partsInventoryStore.usePart(part.id, costCenter, 1, { id: 'u1', name: 'User' })
      expect(result.success).toBe(true)
      expect(result.part.quantity).toBe(initialQty - 1)
      expect(result.auditRecord.costCenter?.prodLine).toBe('Line 3 - Assembly Cell')
      expect(result.auditRecord.costCenter?.project).toBeUndefined()
      expect(result.auditRecord.costCenter?.department).toBeUndefined()
    })

    it('allows usage when ONLY project is provided (OR relation)', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IO-LINK-TERM-16')!
      const initialQty = part.quantity
      const costCenter: PartUsageCostCenter = {
        project: 'PRJ-2026-NMC-EXPANSION'
      }

      const result = partsInventoryStore.usePart(part.id, costCenter, 1, { id: 'u1', name: 'User' })
      expect(result.success).toBe(true)
      expect(result.part.quantity).toBe(initialQty - 1)
      expect(result.auditRecord.costCenter?.project).toBe('PRJ-2026-NMC-EXPANSION')
      expect(result.auditRecord.costCenter?.prodLine).toBeUndefined()
    })

    it('allows usage when ONLY department is provided (OR relation)', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IO-LINK-TERM-16')!
      const initialQty = part.quantity
      const costCenter: PartUsageCostCenter = {
        department: 'Controls & Robotics'
      }

      const result = partsInventoryStore.usePart(part.id, costCenter, 1, { id: 'u1', name: 'User' })
      expect(result.success).toBe(true)
      expect(result.part.quantity).toBe(initialQty - 1)
      expect(result.auditRecord.costCenter?.department).toBe('Controls & Robotics')
    })

    it('throws validation error if ALL cost center fields are missing (OR relation violated)', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IO-LINK-TERM-16')!
      const invalidCostCenter = {
        prodLine: '',
        project: '   ',
        department: ''
      }

      expect(() => {
        partsInventoryStore.usePart(part.id, invalidCostCenter as any, 1, { id: 'u1', name: 'User' })
      }).toThrowError(/Mandatory cost center tagging required/i)
    })

    it('prevents over-consumption exceeding available stock', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('FST-VLV-M5')! // qty 3
      const costCenter: PartUsageCostCenter = {
        prodLine: 'Line 1'
      }

      expect(() => {
        partsInventoryStore.usePart(part.id, costCenter, 10, { id: 'u1', name: 'User' })
      }).toThrowError(/only 3 units available/i)
    })
  })

  // -------------------------------------------------------------------------
  // 4. Asset Templating with Inheritance, Instance Tree, and Scalar Wear Math
  // -------------------------------------------------------------------------
  describe('Revised Asset Templating & Instances', () => {
    it('supports top-level categorization (Hardware vs Software)', () => {
      const templates = partsInventoryStore.getTemplates()
      const hw = templates.filter(t => t.topLevelCategory === 'Hardware')
      const sw = templates.filter(t => t.topLevelCategory === 'Software')

      expect(hw.length).toBeGreaterThan(0)
      expect(sw.length).toBeGreaterThan(0)
    })

    it('supports inherited templates merging with base templates', () => {
      const tree = partsInventoryStore.getTemplateInstanceTree('tmpl-ipc-controller')
      expect(tree).toBeDefined()
      expect(tree?.template.extendsTemplateId).toBe('tmpl-base-hardware')
      expect(tree?.parentTemplate?.id).toBe('tmpl-base-hardware')
      expect(tree?.parentTemplate?.topLevelCategory).toBe('Hardware')
    })

    it('renders a tree view of instantiated serialized objects for a template', () => {
      const tree = partsInventoryStore.getTemplateInstanceTree('tmpl-ipc-controller')
      expect(tree?.instances.length).toBeGreaterThanOrEqual(2)

      const instanceIdentifiers = tree?.instances.map(i => i.customIdentifier)
      expect(instanceIdentifiers).toContain('IPC-1001')
      expect(instanceIdentifiers).toContain('IPC-1002')
    })

    it('calculates estimated resell price via scalar wear depreciation math: basePrice * (1 - wear%)', () => {
      // Base price = 2100, wear = 25% -> 2100 * 0.75 = 1575
      const part = partsInventoryStore.getPartByIdOrIdentifier('IPC-1002')!
      expect(part.priceEur).toBe(2100)
      expect(part.wearDepreciationPercentage).toBe(25)
      expect(part.estimatedResellPriceEur).toBe(1575)

      // Update wear depreciation to 50%
      const updated = partsInventoryStore.updateResellPrice(part.id, {
        wearDepreciationPercentage: 50
      })
      expect(updated.estimatedResellPriceEur).toBe(1050) // 2100 * 0.50
    })

    it('allows overriding estimated resell price directly on the instance level', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IPC-1002')!
      const updated = partsInventoryStore.updateResellPrice(part.id, {
        resellPriceOverrideEur: 800
      })
      expect(updated.resellPriceOverrideEur).toBe(800)
      expect(updated.estimatedResellPriceEur).toBe(800)
    })

    it('allows looking up part by either UUID or custom identifier template string', () => {
      const byIdentifier = partsInventoryStore.getPartByIdOrIdentifier('IPC-1001')
      expect(byIdentifier).toBeDefined()
      expect(byIdentifier?.id).toBe('prt-ser-101')

      const byUuid = partsInventoryStore.getPartByIdOrIdentifier('prt-ser-101')
      expect(byUuid).toBeDefined()
      expect(byUuid?.customIdentifier).toBe('IPC-1001')
    })
  })

  // -------------------------------------------------------------------------
  // 5. Machine Document Import: ChatGPT / AI JSON & Split Controller IPC
  // -------------------------------------------------------------------------
  describe('Machine Document Import (ChatGPT / AI BOM)', () => {
    it('parses ChatGPT markdown-fenced JSON responses into machine assets', () => {
      const chatgptOutput = `
Here is the extracted BOM from the machine technical manual:
\`\`\`json
[
  {
    "name": "Siemens SIMATIC S7-1516F Safety PLC",
    "category": "Hardware",
    "manufacturer": "Siemens",
    "modelNumber": "6ES7516-3FN02-0AB0",
    "serialNumber": "SN-SIE-PLC-901",
    "quantity": 1,
    "location": "Cabinet 1",
    "estimatedPriceEur": 3800
  },
  {
    "name": "Cognex In-Sight 7000 Camera",
    "category": "Hardware",
    "manufacturer": "Cognex",
    "quantity": 1,
    "estimatedPriceEur": 2900
  }
]
\`\`\`
Hope this helps! Let me know if you need further details.
      `

      const parsed = partsInventoryStore.parseDocumentContent(chatgptOutput)
      expect(parsed.length).toBe(2)
      expect(parsed[0].name).toBe('Siemens SIMATIC S7-1516F Safety PLC')
      expect(parsed[0].manufacturer).toBe('Siemens')
      expect(parsed[0].serialNumber).toBe('SN-SIE-PLC-901')
    })

    it('handles modular split assets (e.g. IPC controller splitting into child hardware & software)', () => {
      const rawPayload = {
        machineId: 'mach-op10-cnc',
        machineName: 'Line 1 - OP10 CNC Cell',
        source: 'chatgpt' as const,
        rawContent: `\`\`\`json
[
  {
    "name": "Beckhoff C6030 Industrial PC Controller",
    "category": "Hardware",
    "manufacturer": "Beckhoff Automation",
    "serialNumber": "SN-IPC-6030-881",
    "quantity": 1,
    "estimatedPriceEur": 2650,
    "childComponents": [
      {
        "name": "TwinCAT 3 NC PTP PLC Runtime",
        "category": "Software",
        "manufacturer": "Beckhoff Automation",
        "estimatedPriceEur": 1100
      },
      {
        "name": "240GB Industrial SSD",
        "category": "Hardware",
        "manufacturer": "Innodisk",
        "estimatedPriceEur": 180
      }
    ]
  }
]
\`\`\``
      }

      const result = partsInventoryStore.importMachineDocument(rawPayload, { id: 'u1', name: 'Engineer' })

      expect(result.success).toBe(true)
      expect(result.importedAssetCount).toBe(3) // 1 parent IPC + 2 modular split children
      expect(result.splitAssetCount).toBe(2)

      // Verify created assets are linked to machine
      const created = result.createdAssets
      expect(created[0].name).toBe('Beckhoff C6030 Industrial PC Controller')
      expect(created[0].isMachineLinked).toBe(true)
      expect(created[0].linkedMachineId).toBe('mach-op10-cnc')

      // Child assets inherit hierarchy
      expect(created[1].name).toContain('TwinCAT 3 NC PTP PLC Runtime')
      expect(created[1].tags).toContain('SplitAsset')
    })
  })

  // -------------------------------------------------------------------------
  // 6. Machine Spare Parts & Policy System (Fractional Ratios & Upper Bounds)
  // -------------------------------------------------------------------------
  describe('Machine Spare Parts & Policy System', () => {
    it('cross-references machine spare parts with live parts inventory & alternative parts', () => {
      const { spareParts, reporting } = partsInventoryStore.getMachineSpareParts('EUR')

      expect(spareParts.length).toBeGreaterThanOrEqual(3)
      expect(reporting.totalDefinedSpareParts).toBeGreaterThanOrEqual(3)

      // sp-101 (Beckhoff CX5140) has on-hand = 1 and alternative (CX5130) = 1 -> covered!
      const sp101 = spareParts.find(s => s.id === 'sp-101')!
      expect(sp101.onHandSpares).toBe(1)
      expect(sp101.alternativeSparesAvailable).toBe(1)
      expect(sp101.coverageStatus).toBe('covered')
    })

    it('calculates fractional minimum policy per active machine (e.g. 1 per 3 = 0.333)', () => {
      // 6 active machines with 1/3 ratio -> ceil(6 * 1/3) = 2 required spares
      const { spareParts } = partsInventoryStore.getMachineSpareParts('EUR')
      const sp101 = spareParts.find(s => s.id === 'sp-101')!

      expect(sp101.activeMachinesInProduction).toBe(6)
      expect(sp101.fractionalRatio).toBeCloseTo(1 / 3, 2)
      expect(sp101.requiredSparesCalculated).toBe(2)
    })

    it('applies hard quantity upper bound override to policy calculation', () => {
      // Suppose active machines = 12 with 1/2 ratio -> formula = 6
      // But hard upperBoundQuantity = 2 -> overrides calculated 6 to 2!
      partsInventoryStore.updateSparePartPolicy('sp-101', {
        activeMachinesInProduction: 12,
        fractionalRatio: 0.5,
        upperBoundQuantity: 2
      })

      const { spareParts } = partsInventoryStore.getMachineSpareParts('EUR')
      const updated = spareParts.find(s => s.id === 'sp-101')!

      expect(updated.requiredSparesCalculated).toBe(2)
    })

    it('applies cost upper bound override to policy calculation', () => {
      // Siemens Spindle Motor (unit price 4200 EUR)
      // Active = 6, ratio = 0.5 -> formula = 3 (cost = 12,600 EUR)
      // Set cost upper bound = 5,000 EUR -> floor(5000 / 4200) = 1 spare max!
      partsInventoryStore.updateSparePartPolicy('sp-102', {
        activeMachinesInProduction: 6,
        fractionalRatio: 0.5,
        upperBoundQuantity: 5,
        upperBoundCostEur: 5000
      })

      const { spareParts } = partsInventoryStore.getMachineSpareParts('EUR')
      const updated = spareParts.find(s => s.id === 'sp-102')!

      expect(updated.requiredSparesCalculated).toBe(1) // Overridden by cost cap!
    })

    it('generates spare parts reporting with holding valuation in EUR and currency conversions', () => {
      const { reporting } = partsInventoryStore.getMachineSpareParts('EUR')

      expect(reporting.totalHoldingValuationEur).toBeGreaterThan(0)
      expect(reporting.currencyConversions.HUF).toBe(reporting.totalHoldingValuationEur * 400)
      expect(reporting.currencyConversions.USD).toBe(Math.round(reporting.totalHoldingValuationEur * 1.08 * 100) / 100)
    })
  })

  // -------------------------------------------------------------------------
  // 7. Live Currency Conversion & Code Scan Lookups
  // -------------------------------------------------------------------------
  describe('Live Currency & Code Scan Interface', () => {
    it('converts base EUR valuations to custom currency (HUF, USD, GBP)', () => {
      const baseEur = 1000
      expect(convertCurrency(baseEur, 'HUF')).toBe(400000)
      expect(convertCurrency(baseEur, 'USD')).toBe(1080)
      expect(convertCurrency(baseEur, 'GBP')).toBe(850)
    })

    it('supports looking up part via QR code payload, barcode, or RFID tag', () => {
      // Lookup by QR payload
      const qrMatch = partsInventoryStore.getPartByIdOrIdentifier('HEIMDALL:PART:IPC-1001:UUID:prt-ser-101')
      expect(qrMatch).toBeDefined()
      expect(qrMatch?.customIdentifier).toBe('IPC-1001')

      // Lookup by RFID tag payload
      const rfidMatch = partsInventoryStore.getPartByIdOrIdentifier('E280116060000204A1B2C301')
      expect(rfidMatch).toBeDefined()
      expect(rfidMatch?.customIdentifier).toBe('IPC-1001')

      // Lookup by custom barcode identifier
      const barcodeMatch = partsInventoryStore.getPartByIdOrIdentifier('FST-VLV-M5')
      expect(barcodeMatch).toBeDefined()
      expect(barcodeMatch?.name).toContain('Festo')
    })
  })

  // -------------------------------------------------------------------------
  // 8. Scalar Floating Minimum Quantities & Buffer Multipliers
  // -------------------------------------------------------------------------
  describe('Scalar Floating Minimum Quantity & Safety Buffer Multiplier', () => {
    it('supports floating-point scalar min quantities (e.g. 1.5, 0.33, 2.5)', () => {
      const actor = { id: 'usr-eng-1', name: 'Test Actor' }
      const part = partsInventoryStore.logPart({
        name: 'Precision Hydraulic Oil (Liters)',
        category: 'Hardware',
        trackingType: 'bulk',
        quantity: 5.5,
        minQuantity: 2.5,
        minQuantityScalar: 1.0
      }, actor)

      expect(part.minQuantity).toBe(2.5)
      expect(part.quantity).toBe(5.5)
      expect(part.effectiveMinQuantity).toBe(2.5)
      expect(part.stockAlertStatus).toBe('optimal')
    })

    it('evaluates stock alerts using effective min quantity calculated from safety buffer scalar multiplier', () => {
      const actor = { id: 'usr-eng-1', name: 'Test Actor' }
      // Base min quantity = 10, safety scalar multiplier = 1.25 -> effective min = 12.5
      // With stock quantity 11, quantity <= effectiveMinQuantity (11 <= 12.5) -> low_stock!
      const part = partsInventoryStore.logPart({
        name: 'M6 High-Tensile Bolts (Box of 100)',
        category: 'Hardware',
        trackingType: 'bulk',
        quantity: 11,
        minQuantity: 10,
        minQuantityScalar: 1.25
      }, actor)

      expect(part.minQuantity).toBe(10)
      expect(part.minQuantityScalar).toBe(1.25)
      expect(part.effectiveMinQuantity).toBe(12.5)
      expect(part.stockAlertStatus).toBe('low_stock')
    })

    it('updates min quantity and scalar buffer dynamically recalculating stock alerts', () => {
      const part = partsInventoryStore.getPartByIdOrIdentifier('IO-LINK-TERM-16')! // qty 12, min 5
      expect(part.stockAlertStatus).toBe('optimal')

      // Set minQuantity to 10 with 1.5x buffer scalar -> effective min = 15
      // Current qty 12 <= 15 -> low_stock!
      const updated = partsInventoryStore.updatePartQuantities(part.id, {
        minQuantity: 10,
        minQuantityScalar: 1.5
      })

      expect(updated.minQuantity).toBe(10)
      expect(updated.minQuantityScalar).toBe(1.5)
      expect(updated.effectiveMinQuantity).toBe(15)
      expect(updated.stockAlertStatus).toBe('low_stock')
    })

    it('supports scalar floating min quantities in machine spare parts policy calculation', () => {
      // Set spare part sp-103 to have a minQuantity of 5 with 1.2x buffer multiplier -> effective min = 6
      partsInventoryStore.updateSparePartPolicy('sp-103', {
        minQuantity: 5,
        minQuantityScalar: 1.2,
        upperBoundQuantity: 10
      })

      const { spareParts } = partsInventoryStore.getMachineSpareParts('EUR')
      const updated = spareParts.find(s => s.id === 'sp-103')!

      expect(updated.minQuantity).toBe(5)
      expect(updated.minQuantityScalar).toBe(1.2)
      expect(updated.effectiveMinQuantity).toBe(6)
      expect(updated.requiredSparesCalculated).toBeGreaterThanOrEqual(6)
    })
  })
})
