import { randomUUID } from 'node:crypto'
import type {
  InventoryPart,
  PartTrackingType,
  PartLifecycleCondition,
  PartOperationalState,
  PartUsageCostCenter,
  PartAuditRecord,
  AssetTemplateDefinition,
  MachineSparePartItem,
  SparePartsReportingSummary,
  MachineDocumentImportPayload,
  MachineDocumentImportResult,
  MachineDocumentImportItem
} from '../../app/types/inventory'
import {
  getCanonicalInventoryTemplates,
  getCanonicalInventoryParts,
  getCanonicalMachineSpareParts,
  getCanonicalInventoryAuditLogs
} from './datasetLoader'

// ---------------------------------------------------------------------------
// Currency Exchange Rates (Base: EUR)
// ---------------------------------------------------------------------------
export const LIVE_EXCHANGE_RATES: Record<string, number> = {
  EUR: 1.0,
  HUF: 400.0,
  USD: 1.08,
  GBP: 0.85
}

export function convertCurrency(amountEur: number, targetCurrency: string = 'EUR'): number {
  const rate = LIVE_EXCHANGE_RATES[targetCurrency.toUpperCase()] || 1.0
  return Math.round(amountEur * rate * 100) / 100
}

// ---------------------------------------------------------------------------
// In-Memory Master Parts Inventory Store
// ---------------------------------------------------------------------------
class PartsInventoryStore {
  private parts: Map<string, InventoryPart> = new Map()
  private templates: Map<string, AssetTemplateDefinition> = new Map()
  private spareParts: Map<string, MachineSparePartItem> = new Map()
  private auditLog: PartAuditRecord[] = []

  constructor() {
    this.resetToSeed()
  }

  public resetToSeed() {
    this.parts.clear()
    this.templates.clear()
    this.spareParts.clear()
    this.auditLog = [...getCanonicalInventoryAuditLogs()]

    for (const t of getCanonicalInventoryTemplates()) {
      this.templates.set(t.id, { ...t, instances: [] })
    }

    for (const p of getCanonicalInventoryParts()) {
      const minQuantity = p.minQuantity !== undefined ? Number(p.minQuantity) : 1
      const minQuantityScalar = p.minQuantityScalar !== undefined ? Number(p.minQuantityScalar) : 1.0
      const effectiveMinQuantity = p.effectiveMinQuantity !== undefined
        ? p.effectiveMinQuantity
        : Math.round(minQuantity * minQuantityScalar * 100) / 100

      this.parts.set(p.id, {
        ...p,
        minQuantity,
        minQuantityScalar,
        effectiveMinQuantity
      })
    }

    for (const sp of getCanonicalMachineSpareParts()) {
      const minQuantityScalar = sp.minQuantityScalar !== undefined ? Number(sp.minQuantityScalar) : 1.0
      const effectiveMinQuantity = sp.minQuantity !== undefined
        ? Math.round(sp.minQuantity * minQuantityScalar * 100) / 100
        : undefined

      this.spareParts.set(sp.id, {
        ...sp,
        minQuantityScalar,
        effectiveMinQuantity
      })
    }

    this.rebuildTemplateInstances()
  }

  private rebuildTemplateInstances() {
    for (const template of this.templates.values()) {
      template.instances = Array.from(this.parts.values()).filter(p => p.templateId === template.id)
    }
  }

  // -------------------------------------------------------------------------
  // Parts Retrieval & Filtering
  // -------------------------------------------------------------------------
  public getAllParts(params: {
    category?: string
    trackingType?: string
    condition?: string
    operationalState?: string
    stockAlert?: string
    search?: string
    currency?: string
  } = {}): { parts: InventoryPart[]; totalCount: number; kpis: any } {
    const targetCurrency = params.currency || 'EUR'
    let list = Array.from(this.parts.values())

    if (params.category && params.category !== 'all') {
      list = list.filter(p => p.category.toLowerCase() === params.category!.toLowerCase())
    }

    if (params.trackingType && params.trackingType !== 'all') {
      list = list.filter(p => p.trackingType.toLowerCase() === params.trackingType!.toLowerCase())
    }

    if (params.condition && params.condition !== 'all') {
      list = list.filter(p => p.condition.toLowerCase() === params.condition!.toLowerCase())
    }

    if (params.operationalState && params.operationalState !== 'all') {
      list = list.filter(p => p.operationalState?.toLowerCase() === params.operationalState!.toLowerCase())
    }

    if (params.stockAlert && params.stockAlert !== 'all') {
      list = list.filter(p => p.stockAlertStatus.toLowerCase() === params.stockAlert!.toLowerCase())
    }

    if (params.search && params.search.trim()) {
      const q = params.search.toLowerCase().trim()
      list = list.filter(p => 
        p.name.toLowerCase().includes(q) ||
        p.customIdentifier.toLowerCase().includes(q) ||
        p.manufacturer.name.toLowerCase().includes(q) ||
        p.location.toLowerCase().includes(q) ||
        p.serialNumber?.toLowerCase().includes(q) ||
        p.tags?.some(t => t.toLowerCase().includes(q))
      )
    }

    // Attach converted live currency fields and effective min quantity
    const enriched = list.map(p => {
      const scalar = p.minQuantityScalar !== undefined ? Number(p.minQuantityScalar) : 1.0
      const effectiveMinQuantity = p.effectiveMinQuantity !== undefined
        ? p.effectiveMinQuantity
        : Math.round(p.minQuantity * scalar * 100) / 100

      return {
        ...p,
        minQuantityScalar: scalar,
        effectiveMinQuantity,
        customCurrency: targetCurrency,
        priceCustomCurrency: convertCurrency(p.priceEur, targetCurrency)
      }
    })

    // Calculate aggregated KPIs
    const all = Array.from(this.parts.values())
    const kpis = {
      totalPartsCount: all.length,
      bulkCount: all.filter(p => p.trackingType === 'bulk').reduce((acc, curr) => acc + curr.quantity, 0),
      serializedCount: all.filter(p => p.trackingType === 'serialized').length,
      workingCount: all.filter(p => p.operationalState === 'working').length,
      inServiceCount: all.filter(p => p.operationalState === 'in_service').length,
      evaluationCount: all.filter(p => p.operationalState === 'evaluation').length,
      brokenCount: all.filter(p => p.operationalState === 'broken').length,
      newConditionCount: all.filter(p => p.condition === 'new').length,
      usedConditionCount: all.filter(p => p.condition === 'used').length,
      donorConditionCount: all.filter(p => p.condition === 'donor').length,
      obsoleteConditionCount: all.filter(p => p.condition === 'obsolete').length,
      scrapConditionCount: all.filter(p => p.condition === 'scrap').length,
      lowStockAlertCount: all.filter(p => p.stockAlertStatus === 'low_stock').length,
      outOfStockAlertCount: all.filter(p => p.stockAlertStatus === 'out_of_stock').length,
      totalWarehouseValuationEur: all.reduce((sum, p) => sum + (p.priceEur * p.quantity), 0),
      totalWarehouseValuationConverted: convertCurrency(all.reduce((sum, p) => sum + (p.priceEur * p.quantity), 0), targetCurrency)
    }

    return {
      parts: enriched,
      totalCount: enriched.length,
      kpis
    }
  }

  public getPartByIdOrIdentifier(lookup: string, targetCurrency: string = 'EUR'): InventoryPart | undefined {
    const q = lookup.trim().toLowerCase()
    const part = Array.from(this.parts.values()).find(p => 
      p.id.toLowerCase() === q ||
      p.customIdentifier.toLowerCase() === q ||
      p.serialNumber?.toLowerCase() === q ||
      p.qrCodePayload?.toLowerCase() === q ||
      p.rfidPayload?.toLowerCase() === q
    )

    if (!part) return undefined

    const scalar = part.minQuantityScalar !== undefined ? Number(part.minQuantityScalar) : 1.0
    const effectiveMinQuantity = part.effectiveMinQuantity !== undefined
      ? part.effectiveMinQuantity
      : Math.round(part.minQuantity * scalar * 100) / 100

    return {
      ...part,
      minQuantityScalar: scalar,
      effectiveMinQuantity,
      customCurrency: targetCurrency,
      priceCustomCurrency: convertCurrency(part.priceEur, targetCurrency)
    }
  }

  // -------------------------------------------------------------------------
  // Log Part (Single & Bulk Intake)
  // -------------------------------------------------------------------------
  public logPart(payload: Partial<InventoryPart>, actor: { id: string; name: string }): InventoryPart {
    const id = payload.id || randomUUID()
    const template = payload.templateId ? this.templates.get(payload.templateId) : undefined

    // Generate sequential or custom identifier
    let customIdentifier = payload.customIdentifier
    if (!customIdentifier) {
      if (template?.identifierPattern) {
        template.sequentialCounter++
        customIdentifier = template.identifierPattern.replace('{number}', String(template.sequentialCounter).padStart(4, '0'))
      } else {
        customIdentifier = `PRT-${Math.floor(1000 + Math.random() * 9000)}`
      }
    }

    const trackingType: PartTrackingType = payload.trackingType || 'serialized'
    const quantity = trackingType === 'serialized' ? 1 : Math.max(1, payload.quantity || 1)
    const minQuantity = payload.minQuantity !== undefined ? Number(payload.minQuantity) : 1
    const minQuantityScalar = payload.minQuantityScalar !== undefined ? Number(payload.minQuantityScalar) : 1.0
    const effectiveMinQuantity = Math.round((minQuantity * minQuantityScalar) * 100) / 100
    const priceEur = payload.priceEur ?? (template?.fixedFields?.basePriceEur || 100)

    // Calculate initial estimated resell price via wear depreciation math
    const wear = payload.wearDepreciationPercentage ?? (payload.condition === 'new' ? 0 : 25)
    const estimatedResellPriceEur = payload.resellPriceOverrideEur !== undefined
      ? payload.resellPriceOverrideEur
      : Math.round(priceEur * (1 - (wear / 100)) * 100) / 100

    const condition: PartLifecycleCondition = payload.condition || 'new'
    const operationalState: PartOperationalState = payload.operationalState || (condition === 'scrap' ? 'broken' : 'working')

    let stockAlertStatus: 'optimal' | 'low_stock' | 'out_of_stock' = 'optimal'
    if (quantity === 0 || condition === 'scrap') {
      stockAlertStatus = 'out_of_stock'
    } else if (quantity <= effectiveMinQuantity) {
      stockAlertStatus = 'low_stock'
    }

    const defaultOwner = template?.fixedFields?.defaultOwner || { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' }

    const newPart: InventoryPart = {
      id,
      customIdentifier,
      templateId: payload.templateId,
      name: payload.name || template?.name || 'Industrial Spare Part',
      displayName: payload.displayName || payload.name,
      category: payload.category || template?.topLevelCategory || 'Hardware',
      subCategory: payload.subCategory || template?.fixedFields?.category || 'General',
      trackingType,
      condition,
      operationalState,
      manufacturer: payload.manufacturer || { name: template?.fixedFields?.manufacturer || 'OEM' },
      supplier: payload.supplier || (template?.fixedFields?.supplier ? { name: template.fixedFields.supplier } : undefined),
      owner: payload.owner || defaultOwner,
      location: payload.location || 'Warehouse Intake Bay',
      priceEur,
      estimatedResellPriceEur,
      resellPriceOverrideEur: payload.resellPriceOverrideEur,
      wearDepreciationPercentage: wear,
      quantity,
      minQuantity,
      minQuantityScalar,
      effectiveMinQuantity,
      stockAlertStatus,
      alternatePartIds: payload.alternatePartIds || [],
      isMachineLinked: payload.isMachineLinked,
      linkedMachineId: payload.linkedMachineId,
      linkedMachineName: payload.linkedMachineName,
      serialNumber: payload.serialNumber || (trackingType === 'serialized' ? `SN-${randomUUID().substring(0, 8).toUpperCase()}` : undefined),
      qrCodePayload: `HEIMDALL:PART:${customIdentifier}:UUID:${id}`,
      rfidPayload: payload.rfidPayload,
      tags: payload.tags || ['Intake'],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }

    this.parts.set(id, newPart)
    this.rebuildTemplateInstances()

    // Append to immutable audit log
    this.auditLog.unshift({
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: id,
      partIdentifier: customIdentifier,
      partName: newPart.name,
      action: 'log_part',
      timestamp: new Date().toISOString(),
      actorId: actor.id,
      actorName: actor.name,
      quantityDelta: quantity,
      newState: {
        condition,
        operationalState,
        location: newPart.location,
        priceEur,
        quantity
      },
      notes: `Logged into warehouse stock (${trackingType})`
    })

    return newPart
  }

  public bulkLogParts(items: Array<Partial<InventoryPart>>, actor: { id: string; name: string }): InventoryPart[] {
    const created: InventoryPart[] = []
    for (const item of items) {
      created.push(this.logPart(item, actor))
    }
    return created
  }

  // -------------------------------------------------------------------------
  // Use Part (Mandatory Cost Center Validation: OR relation not AND)
  // -------------------------------------------------------------------------
  public usePart(
    partId: string,
    costCenter: PartUsageCostCenter,
    quantityToUse: number = 1,
    actor: { id: string; name: string }
  ): { success: boolean; part: InventoryPart; auditRecord: PartAuditRecord } {
    const part = this.parts.get(partId)
    if (!part) {
      throw new Error(`Part with ID "${partId}" was not found.`)
    }

    // MANDATORY COST CENTER VALIDATION: OR relation not AND (Prod Line, Project, or Department)
    const hasProdLine = Boolean(costCenter?.prodLine && costCenter.prodLine.trim())
    const hasProject = Boolean(costCenter?.project && costCenter.project.trim())
    const hasDepartment = Boolean(costCenter?.department && costCenter.department.trim())

    if (!hasProdLine && !hasProject && !hasDepartment) {
      throw new Error('Mandatory cost center tagging required: specify at least one of Production Line (prodLine), Project (project), or Department (department).')
    }

    const previousQuantity = part.quantity
    const previousState = {
      quantity: part.quantity,
      operationalState: part.operationalState,
      location: part.location
    }

    const costCenterSummaryList: string[] = []
    if (hasProdLine) costCenterSummaryList.push(`Line: ${costCenter.prodLine!.trim()}`)
    if (hasProject) costCenterSummaryList.push(`Project: ${costCenter.project!.trim()}`)
    if (hasDepartment) costCenterSummaryList.push(`Dept: ${costCenter.department!.trim()}`)
    const costCenterSummary = costCenterSummaryList.join(', ')

    if (part.trackingType === 'bulk') {
      if (part.quantity < quantityToUse) {
        throw new Error(`Cannot use ${quantityToUse} units. Only ${part.quantity} units available in stock.`)
      }
      part.quantity -= quantityToUse
    } else {
      // Serialized part: state transitions to deployed / in_service
      if (part.quantity < 1) {
        throw new Error(`Serialized part "${part.customIdentifier}" is already consumed or deployed.`)
      }
      part.quantity = 0
      part.operationalState = 'in_service'
      part.location = `Deployed: ${costCenterSummary}`
    }

    // Update stock alert status against effective min quantity
    const effectiveMin = part.effectiveMinQuantity !== undefined
      ? part.effectiveMinQuantity
      : Math.round((part.minQuantity * (part.minQuantityScalar ?? 1.0)) * 100) / 100

    if (part.quantity === 0) {
      part.stockAlertStatus = 'out_of_stock'
    } else if (part.quantity <= effectiveMin) {
      part.stockAlertStatus = 'low_stock'
    } else {
      part.stockAlertStatus = 'optimal'
    }

    part.updatedAt = new Date().toISOString()
    this.parts.set(part.id, part)
    this.rebuildTemplateInstances()

    const auditRecord: PartAuditRecord = {
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: part.id,
      partIdentifier: part.customIdentifier,
      partName: part.name,
      action: 'use_part',
      timestamp: new Date().toISOString(),
      actorId: actor.id,
      actorName: actor.name,
      quantityDelta: -quantityToUse,
      costCenter: {
        prodLine: hasProdLine ? costCenter.prodLine!.trim() : undefined,
        project: hasProject ? costCenter.project!.trim() : undefined,
        department: hasDepartment ? costCenter.department!.trim() : undefined,
        technician: costCenter.technician || actor.name,
        notes: costCenter.notes
      },
      previousState,
      newState: {
        quantity: part.quantity,
        operationalState: part.operationalState,
        location: part.location,
        stockAlertStatus: part.stockAlertStatus
      },
      notes: `Used for ${costCenterSummary} by ${actor.name}`
    }

    this.auditLog.unshift(auditRecord)

    return { success: true, part, auditRecord }
  }

  // -------------------------------------------------------------------------
  // Min Quantity & Scalar Buffer Updates
  // -------------------------------------------------------------------------
  public updatePartQuantities(
    partId: string,
    updates: { minQuantity?: number; minQuantityScalar?: number; quantity?: number },
    actor?: { id: string; name: string }
  ): InventoryPart {
    const part = this.parts.get(partId)
    if (!part) throw new Error(`Part "${partId}" not found.`)

    if (updates.quantity !== undefined && part.trackingType === 'bulk') {
      part.quantity = Math.max(0, Number(updates.quantity))
    }
    if (updates.minQuantity !== undefined) {
      part.minQuantity = Number(updates.minQuantity)
    }
    if (updates.minQuantityScalar !== undefined) {
      part.minQuantityScalar = Number(updates.minQuantityScalar)
    }

    const scalar = part.minQuantityScalar !== undefined ? part.minQuantityScalar : 1.0
    part.effectiveMinQuantity = Math.round((part.minQuantity * scalar) * 100) / 100

    if (part.quantity === 0 || part.condition === 'scrap') {
      part.stockAlertStatus = 'out_of_stock'
    } else if (part.quantity <= part.effectiveMinQuantity) {
      part.stockAlertStatus = 'low_stock'
    } else {
      part.stockAlertStatus = 'optimal'
    }

    part.updatedAt = new Date().toISOString()
    this.parts.set(part.id, part)
    return part
  }

  // -------------------------------------------------------------------------
  // Condition & Operational State Updates
  // -------------------------------------------------------------------------
  public updatePartCondition(
    partId: string,
    condition: PartLifecycleCondition,
    operationalState?: PartOperationalState,
    actor?: { id: string; name: string }
  ): InventoryPart {
    const part = this.parts.get(partId)
    if (!part) throw new Error(`Part "${partId}" not found.`)

    const previous = { condition: part.condition, operationalState: part.operationalState }
    part.condition = condition
    if (operationalState) {
      part.operationalState = operationalState
    } else if (condition === 'scrap') {
      part.operationalState = 'broken'
    }

    // If scrapped, update stock alert
    if (condition === 'scrap') {
      part.stockAlertStatus = 'out_of_stock'
    }

    part.updatedAt = new Date().toISOString()
    this.parts.set(part.id, part)

    this.auditLog.unshift({
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: part.id,
      partIdentifier: part.customIdentifier,
      partName: part.name,
      action: condition === 'scrap' ? 'scrap' : 'condition_update',
      timestamp: new Date().toISOString(),
      actorId: actor?.id || 'sys',
      actorName: actor?.name || 'System',
      previousState: previous,
      newState: { condition: part.condition, operationalState: part.operationalState },
      notes: `Lifecycle condition updated to "${condition}"`
    })

    return part
  }

  // -------------------------------------------------------------------------
  // Resell Price & Wear Depreciation Scalar Math Override
  // -------------------------------------------------------------------------
  public updateResellPrice(
    partId: string,
    params: { wearDepreciationPercentage?: number; resellPriceOverrideEur?: number },
    actor?: { id: string; name: string }
  ): InventoryPart {
    const part = this.parts.get(partId)
    if (!part) throw new Error(`Part "${partId}" not found.`)

    const prevPrice = part.estimatedResellPriceEur
    if (params.wearDepreciationPercentage !== undefined) {
      part.wearDepreciationPercentage = Math.min(100, Math.max(0, params.wearDepreciationPercentage))
      // Scalar math depreciation: basePrice * (1 - wear%)
      part.estimatedResellPriceEur = Math.round(part.priceEur * (1 - (part.wearDepreciationPercentage / 100)) * 100) / 100
      part.resellPriceOverrideEur = undefined
    }

    if (params.resellPriceOverrideEur !== undefined) {
      part.resellPriceOverrideEur = params.resellPriceOverrideEur
      part.estimatedResellPriceEur = params.resellPriceOverrideEur
    }

    part.updatedAt = new Date().toISOString()
    this.parts.set(part.id, part)

    this.auditLog.unshift({
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: part.id,
      partIdentifier: part.customIdentifier,
      partName: part.name,
      action: 'resell_price_update',
      timestamp: new Date().toISOString(),
      actorId: actor?.id || 'sys',
      actorName: actor?.name || 'System',
      previousState: { estimatedResellPriceEur: prevPrice },
      newState: {
        estimatedResellPriceEur: part.estimatedResellPriceEur,
        wearDepreciationPercentage: part.wearDepreciationPercentage,
        resellPriceOverrideEur: part.resellPriceOverrideEur
      },
      notes: `Resell valuation adjusted to €${part.estimatedResellPriceEur}`
    })

    return part
  }

  // -------------------------------------------------------------------------
  // Asset Templates & Inheritance (CRUD & Tree)
  // -------------------------------------------------------------------------
  public getTemplates(): AssetTemplateDefinition[] {
    this.rebuildTemplateInstances()
    return Array.from(this.templates.values())
  }

  public getTemplateById(templateId: string): AssetTemplateDefinition | undefined {
    this.rebuildTemplateInstances()
    return this.templates.get(templateId)
  }

  public getTemplateInstanceTree(templateId: string): { template: AssetTemplateDefinition; parentTemplate?: AssetTemplateDefinition; instances: InventoryPart[] } | undefined {
    this.rebuildTemplateInstances()
    const template = this.templates.get(templateId)
    if (!template) return undefined

    const parentTemplate = template.extendsTemplateId ? this.templates.get(template.extendsTemplateId) : undefined
    const instances = template.instances || []

    return {
      template,
      parentTemplate,
      instances
    }
  }

  public createTemplate(
    payload: Partial<AssetTemplateDefinition>,
    actor?: { id: string; name: string }
  ): AssetTemplateDefinition {
    if (!payload.name || !payload.name.trim()) {
      throw new Error('Template name is required.')
    }

    const cleanName = payload.name.trim()
    const slug = cleanName.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')
    const id = payload.id && payload.id.trim()
      ? payload.id.trim()
      : 'tmpl-' + slug + '-' + randomUUID().substring(0, 4)

    if (this.templates.has(id)) {
      throw new Error(`Template with ID "${id}" already exists.`)
    }

    const now = new Date().toISOString()
    const newTemplate: AssetTemplateDefinition = {
      id,
      name: cleanName,
      topLevelCategory: payload.topLevelCategory || 'Hardware',
      extendsTemplateId: payload.extendsTemplateId || undefined,
      identifierPattern: payload.identifierPattern || 'AST-{number}',
      sequentialCounter: payload.sequentialCounter !== undefined ? Number(payload.sequentialCounter) : 100,
      description: payload.description || '',
      icon: payload.icon || (payload.topLevelCategory === 'Software' ? 'Code' : 'Cpu'),
      fixedFields: {
        manufacturer: payload.fixedFields?.manufacturer || '',
        supplier: payload.fixedFields?.supplier || '',
        category: payload.fixedFields?.category || payload.topLevelCategory || 'Hardware',
        basePriceEur: payload.fixedFields?.basePriceEur !== undefined ? Number(payload.fixedFields.basePriceEur) : 0,
        specs: payload.fixedFields?.specs || {},
        defaultOwner: payload.fixedFields?.defaultOwner || { type: 'organization', id: 'org-root', name: 'Heimdall Manufacturing Org' }
      },
      instanceSpecificFieldsSchema: Array.isArray(payload.instanceSpecificFieldsSchema)
        ? payload.instanceSpecificFieldsSchema
        : [
            { key: 'serialNumber', label: 'Serial Number', type: 'string', required: true },
            { key: 'location', label: 'Warehouse / Location', type: 'string', required: true }
          ],
      tags: Array.isArray(payload.tags) ? payload.tags : [],
      instances: [],
      createdAt: now,
      updatedAt: now
    }

    this.templates.set(id, newTemplate)

    this.auditLog.unshift({
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: id,
      partIdentifier: id,
      partName: newTemplate.name,
      action: 'log_part',
      timestamp: now,
      actorId: actor?.id || 'sys',
      actorName: actor?.name || 'System',
      quantityDelta: 0,
      notes: `Asset template "${newTemplate.name}" created`
    })

    return newTemplate
  }

  public updateTemplate(
    id: string,
    updates: Partial<AssetTemplateDefinition>,
    actor?: { id: string; name: string }
  ): AssetTemplateDefinition {
    const template = this.templates.get(id)
    if (!template) {
      throw new Error(`Asset template "${id}" not found.`)
    }

    if (updates.name !== undefined && updates.name.trim()) template.name = updates.name.trim()
    if (updates.topLevelCategory !== undefined) template.topLevelCategory = updates.topLevelCategory
    if (updates.extendsTemplateId !== undefined) template.extendsTemplateId = updates.extendsTemplateId || undefined
    if (updates.identifierPattern !== undefined) template.identifierPattern = updates.identifierPattern
    if (updates.sequentialCounter !== undefined) template.sequentialCounter = Number(updates.sequentialCounter)
    if (updates.description !== undefined) template.description = updates.description
    if (updates.icon !== undefined) template.icon = updates.icon
    if (updates.fixedFields !== undefined) {
      template.fixedFields = {
        ...template.fixedFields,
        ...updates.fixedFields
      }
    }
    if (updates.instanceSpecificFieldsSchema !== undefined) {
      template.instanceSpecificFieldsSchema = updates.instanceSpecificFieldsSchema
    }
    if (updates.tags !== undefined) {
      template.tags = updates.tags
    }
    template.updatedAt = new Date().toISOString()

    this.templates.set(id, template)
    this.rebuildTemplateInstances()

    this.auditLog.unshift({
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: id,
      partIdentifier: id,
      partName: template.name,
      action: 'condition_update',
      timestamp: template.updatedAt,
      actorId: actor?.id || 'sys',
      actorName: actor?.name || 'System',
      quantityDelta: 0,
      notes: `Asset template "${template.name}" updated`
    })

    return template
  }

  public deleteTemplate(id: string, actor?: { id: string; name: string }): boolean {
    const template = this.templates.get(id)
    if (!template) {
      throw new Error(`Asset template "${id}" not found.`)
    }

    const activeInstances = Array.from(this.parts.values()).filter(p => p.templateId === id)
    if (activeInstances.length > 0) {
      throw new Error(`Cannot delete template "${template.name}" because ${activeInstances.length} parts are currently instantiated with it.`)
    }

    const childTemplates = Array.from(this.templates.values()).filter(t => t.extendsTemplateId === id)
    if (childTemplates.length > 0) {
      throw new Error(`Cannot delete template "${template.name}" because child templates (${childTemplates.map(c => c.name).join(', ')}) extend it.`)
    }

    this.templates.delete(id)

    this.auditLog.unshift({
      id: `aud-${randomUUID().substring(0, 8)}`,
      partId: id,
      partIdentifier: id,
      partName: template.name,
      action: 'scrap',
      timestamp: new Date().toISOString(),
      actorId: actor?.id || 'sys',
      actorName: actor?.name || 'System',
      quantityDelta: 0,
      notes: `Asset template "${template.name}" deleted`
    })

    return true
  }

  // -------------------------------------------------------------------------
  // Machine Document Import (ChatGPT / LLM / BOM JSON Parser)
  // -------------------------------------------------------------------------
  public importMachineDocument(payload: MachineDocumentImportPayload, actor: { id: string; name: string }): MachineDocumentImportResult {
    let parsedList: MachineDocumentImportItem[] = []

    if (payload.parsedItems && payload.parsedItems.length > 0) {
      parsedList = payload.parsedItems
    } else if (payload.rawContent) {
      parsedList = this.parseDocumentContent(payload.rawContent)
    }

    if (parsedList.length === 0) {
      return {
        success: false,
        machineId: payload.machineId,
        machineName: payload.machineName,
        importedAssetCount: 0,
        splitAssetCount: 0,
        createdAssets: [],
        errors: ['No valid assets could be extracted from the machine document. Check JSON or BOM format.']
      }
    }

    const createdParts: InventoryPart[] = []
    let splitCount = 0

    // Process top-level items and modular split child components (e.g. IPC controller splitting into SSD/TwinCAT)
    for (const item of parsedList) {
      const topLevelPart = this.logPart({
        name: item.name,
        category: item.category || 'Hardware',
        trackingType: 'serialized',
        condition: 'new',
        operationalState: 'working',
        manufacturer: { name: item.manufacturer || 'OEM' },
        location: item.location || `${payload.machineName} Cabinet`,
        priceEur: item.estimatedPriceEur || 1000,
        serialNumber: item.serialNumber,
        isMachineLinked: true,
        linkedMachineId: payload.machineId,
        linkedMachineName: payload.machineName,
        tags: ['Imported', 'MachineDocument', payload.machineName]
      }, actor)

      createdParts.push(topLevelPart)

      // Handle split child components (e.g. IPC controller with modular sub-components)
      if (item.childComponents && item.childComponents.length > 0) {
        for (const child of item.childComponents) {
          splitCount++
          const childPart = this.logPart({
            name: `${item.name} > ${child.name}`,
            category: child.category || 'Hardware',
            trackingType: 'serialized',
            condition: 'new',
            operationalState: 'working',
            manufacturer: { name: child.manufacturer || item.manufacturer || 'OEM' },
            location: `${topLevelPart.customIdentifier} Sub-Assembly`,
            priceEur: child.estimatedPriceEur || 300,
            serialNumber: child.serialNumber,
            isMachineLinked: true,
            linkedMachineId: payload.machineId,
            linkedMachineName: payload.machineName,
            tags: ['SplitAsset', topLevelPart.customIdentifier]
          }, actor)
          createdParts.push(childPart)
        }
      }
    }

    return {
      success: true,
      machineId: payload.machineId,
      machineName: payload.machineName,
      importedAssetCount: createdParts.length,
      splitAssetCount: splitCount,
      createdAssets: createdParts
    }
  }

  /**
   * Robust parser that accepts ChatGPT JSON (including markdown fences) or tabular BOM text
   */
  public parseDocumentContent(content: string): MachineDocumentImportItem[] {
    const trimmed = content.trim()

    // 1. Try JSON extraction (handles markdown ```json ... ``` fences)
    const jsonMatch = trimmed.match(/```(?:json)?\s*([\s\S]*?)\s*```/) || [null, trimmed]
    const candidateJson = (jsonMatch[1] || trimmed).trim()

    try {
      const parsed = JSON.parse(candidateJson)
      const list = Array.isArray(parsed) ? parsed : (parsed.items || parsed.components || [parsed])
      return list.map((item: any) => ({
        name: item.name || item.description || item.partName || 'Unknown Component',
        category: (item.category || item.type || 'Hardware').toLowerCase().includes('soft') ? 'Software' : 'Hardware',
        manufacturer: item.manufacturer || item.brand || item.oem || 'Generic OEM',
        modelNumber: item.modelNumber || item.model || item.partNumber,
        serialNumber: item.serialNumber || item.serial,
        quantity: Number(item.quantity) || 1,
        location: item.location || 'Control Cabinet',
        estimatedPriceEur: Number(item.priceEur || item.cost || item.estimatedPrice || 500),
        childComponents: Array.isArray(item.childComponents || item.children) 
          ? (item.childComponents || item.children).map((c: any) => ({
              name: c.name || c.description,
              category: (c.category || 'Hardware').toLowerCase().includes('soft') ? 'Software' : 'Hardware',
              manufacturer: c.manufacturer || item.manufacturer,
              quantity: 1,
              estimatedPriceEur: Number(c.priceEur || 250)
            }))
          : undefined
      }))
    } catch {
      // 2. Fallback: Parse line-by-line BOM text (e.g. "Siemens S7-1500 PLC | Siemens | 1 | Control Cabinet")
      const lines = trimmed.split('\n').filter(l => l.trim().length > 0 && !l.startsWith('#'))
      const extracted: MachineDocumentImportItem[] = []

      for (const line of lines) {
        const parts = line.split(/[|;,]/).map(s => s.trim())
        if (parts.length >= 2) {
          extracted.push({
            name: parts[0],
            manufacturer: parts[1],
            quantity: parts[2] ? parseInt(parts[2], 10) || 1 : 1,
            category: parts[0].toLowerCase().includes('license') || parts[0].toLowerCase().includes('twincat') ? 'Software' : 'Hardware',
            location: parts[3] || 'Cabinet',
            estimatedPriceEur: 500
          })
        }
      }

      return extracted
    }
  }

  // -------------------------------------------------------------------------
  // Machine Spare Parts & Policy System
  // -------------------------------------------------------------------------
  public getMachineSpareParts(targetCurrency: string = 'EUR'): {
    spareParts: MachineSparePartItem[]
    reporting: SparePartsReportingSummary
  } {
    const list: MachineSparePartItem[] = []

    for (const sp of this.spareParts.values()) {
      // Cross reference with live parts inventory
      const masterPart = this.parts.get(sp.partId)
      const onHand = masterPart && masterPart.operationalState === 'working' ? masterPart.quantity : 0

      // Alternative parts cross reference
      let alternativesCount = 0
      if (sp.alternatePartIds && sp.alternatePartIds.length > 0) {
        for (const altId of sp.alternatePartIds) {
          const altPart = this.parts.get(altId)
          if (altPart && altPart.operationalState === 'working') {
            alternativesCount += altPart.quantity
          }
        }
      }

      // Calculate required minimum spares via fractional ratio and optional minQuantity scalar
      let calculatedNeeded = Math.ceil(sp.activeMachinesInProduction * sp.fractionalRatio)

      if (sp.minQuantity !== undefined) {
        const scalar = sp.minQuantityScalar !== undefined ? Number(sp.minQuantityScalar) : 1.0
        const effectiveMin = Math.round(sp.minQuantity * scalar * 100) / 100
        sp.effectiveMinQuantity = effectiveMin
        calculatedNeeded = Math.max(calculatedNeeded, Math.ceil(effectiveMin))
      }

      // Apply Hard Upper Bound Overrides
      if (sp.upperBoundQuantity !== undefined && sp.upperBoundQuantity > 0) {
        calculatedNeeded = Math.min(calculatedNeeded, sp.upperBoundQuantity)
      }

      if (sp.upperBoundCostEur !== undefined && sp.upperBoundCostEur > 0 && sp.unitPriceEur > 0) {
        const maxAffordable = Math.floor(sp.upperBoundCostEur / sp.unitPriceEur)
        if (maxAffordable > 0) {
          calculatedNeeded = Math.min(calculatedNeeded, maxAffordable)
        }
      }

      calculatedNeeded = Math.max(1, calculatedNeeded)

      // Determine coverage status
      let coverageStatus: 'covered' | 'shortage' | 'critical' | 'surplus' = 'covered'
      const totalAvailable = onHand + alternativesCount

      if (onHand >= calculatedNeeded) {
        coverageStatus = onHand > calculatedNeeded * 1.5 ? 'surplus' : 'covered'
      } else if (totalAvailable >= calculatedNeeded) {
        coverageStatus = 'covered' // Covered by alternatives
      } else if (onHand === 0 && totalAvailable === 0) {
        coverageStatus = 'critical'
      } else {
        coverageStatus = 'shortage'
      }

      const totalValuation = onHand * sp.unitPriceEur

      const updatedSp: MachineSparePartItem = {
        ...sp,
        requiredSparesCalculated: calculatedNeeded,
        onHandSpares: onHand,
        alternativeSparesAvailable: alternativesCount,
        coverageStatus,
        totalHoldingValuationEur: totalValuation
      }

      this.spareParts.set(sp.id, updatedSp)
      list.push(updatedSp)
    }

    const totalValuationEur = list.reduce((acc, curr) => acc + curr.totalHoldingValuationEur, 0)

    const reporting: SparePartsReportingSummary = {
      totalDefinedSpareParts: list.length,
      totalCovered: list.filter(s => s.coverageStatus === 'covered' || s.coverageStatus === 'surplus').length,
      totalShortages: list.filter(s => s.coverageStatus === 'shortage').length,
      criticalShortages: list.filter(s => s.coverageStatus === 'critical').length,
      totalHoldingValuationEur: totalValuationEur,
      currencyConversions: {
        HUF: convertCurrency(totalValuationEur, 'HUF'),
        USD: convertCurrency(totalValuationEur, 'USD'),
        GBP: convertCurrency(totalValuationEur, 'GBP')
      }
    }

    return { spareParts: list, reporting }
  }

  public updateSparePartPolicy(id: string, updates: Partial<MachineSparePartItem>): MachineSparePartItem {
    const sp = this.spareParts.get(id)
    if (!sp) throw new Error(`Spare part policy "${id}" not found.`)

    if (updates.fractionalRatio !== undefined) sp.fractionalRatio = updates.fractionalRatio
    if (updates.minQuantity !== undefined) sp.minQuantity = updates.minQuantity
    if (updates.minQuantityScalar !== undefined) sp.minQuantityScalar = updates.minQuantityScalar
    if (updates.upperBoundQuantity !== undefined) sp.upperBoundQuantity = updates.upperBoundQuantity
    if (updates.upperBoundCostEur !== undefined) sp.upperBoundCostEur = updates.upperBoundCostEur
    if (updates.activeMachinesInProduction !== undefined) sp.activeMachinesInProduction = updates.activeMachinesInProduction

    this.spareParts.set(id, sp)
    this.getMachineSpareParts() // Recalculate
    return this.spareParts.get(id)!
  }

  // -------------------------------------------------------------------------
  // Audit Log
  // -------------------------------------------------------------------------
  public getAuditLog(partId?: string): PartAuditRecord[] {
    if (partId) {
      return this.auditLog.filter(a => a.partId === partId)
    }
    return this.auditLog
  }
}

// Global Singleton for Nitro BFF
export const partsInventoryStore = new PartsInventoryStore()
