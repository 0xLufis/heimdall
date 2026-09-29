export interface Manufacturer {
  id: string
  name: string
  website?: string
  supportContact?: string
}

export interface Supplier {
  id: string
  name: string
  website?: string
  contactPerson?: string
  email?: string
}

export interface ComponentTopLevelFlags {
  type?: 'controlling' | 'sensor' | 'vision' | 'screwing' | 'coating' | 'dispensing'
  owner?: 'in-house' | 'outsourced' | 'mixed'
  customFlags?: Record<string, any>
}

export interface InventoryComponent {
  id: string
  name: string
  displayName?: string
  quantity: number
  entityCreator?: string
  entityUpdater?: string
  costCenter?: string
  costCenterOU?: string
  technology?: string
  topLevelFlags?: ComponentTopLevelFlags
  data?: any
  manufacturerId?: string
  manufacturer?: Manufacturer
  supplierId?: string
  supplier?: Supplier
  parentId?: string
  parent?: InventoryComponent
  children: InventoryComponent[]
  lateralLinkId?: string
  lateralLink?: InventoryComponent
  machineId?: string
  clientPcId?: string
}

export interface Machine {
  id: string
  organizationId?: string
  customIdentifier: string
  pinnedObjectHandle?: string
  clientPcs: any[]
  components: InventoryComponent[]
}

// --------------------------------------------------------------------------
// Split Parts Inventory & Asset Types (docs/TODO/inventory.TODO.md)
// --------------------------------------------------------------------------

export type PartTrackingType = 'bulk' | 'serialized'

/**
 * Condition of the part across its lifecycle:
 * - 'new': Factory new part
 * - 'used': Previously deployed but functional
 * - 'donor': Kept for spare donor components/sub-assemblies
 * - 'obsolete': Deprecated or discontinued by manufacturer
 * - 'scrap': End-of-life disposal ("can be get rid of")
 */
export type PartLifecycleCondition = 'new' | 'used' | 'donor' | 'obsolete' | 'scrap'

/**
 * Operational state for serialized parts:
 * - 'working': Fully operational and verified
 * - 'in_service': In service / maintenance (external or internal)
 * - 'evaluation': Waiting for evaluation if serviceable
 * - 'broken': Broken or defective
 */
export type PartOperationalState = 'working' | 'in_service' | 'evaluation' | 'broken'

export type PartOwnerType = 'user' | 'organization'

export interface PartOwner {
  type: PartOwnerType
  id: string
  name: string
  email?: string
}

/**
 * Mandatory cost center fields when using/consuming a part.
 */
export interface PartUsageCostCenter {
  prodLine?: string // e.g. "Line 1 - Battery Cell Stacking"
  project?: string // e.g. "PRJ-2026-NMC-EXPANSION"
  department?: string // e.g. "Assembly Automation & Robotics"
  technician?: string
  notes?: string
}

export interface AlternatePartSummary {
  id: string
  name: string
  customIdentifier: string
  quantity: number
  priceEur: number
}

export interface InventoryPart {
  id: string // Object UUID
  customIdentifier: string // Human readable e.g. "ID-00104" or "PRT-OP10-01"
  templateId?: string // Linked AssetTemplate ID if instantiated from template
  name: string
  displayName?: string
  category: 'Hardware' | 'Software'
  subCategory?: string
  trackingType: PartTrackingType // 'bulk' | 'serialized'
  condition: PartLifecycleCondition // 'new' | 'used' | 'donor' | 'obsolete' | 'scrap'
  operationalState?: PartOperationalState // 'working' | 'in_service' | 'evaluation' | 'broken'
  manufacturer: {
    id?: string
    name: string
  }
  supplier?: {
    id?: string
    name: string
  }
  owner: PartOwner // De-facto owner is organization owner if not user-assigned
  location: string // Storage warehouse / bin location
  priceEur: number // Base price in EUR
  customCurrency?: string // Live converted currency code (e.g. HUF, USD, GBP)
  priceCustomCurrency?: number // Calculated price in custom currency
  estimatedResellPriceEur: number // Calculated via wear depreciation math or explicit override
  resellPriceOverrideEur?: number // Manual override if set
  wearDepreciationPercentage?: number // 0-100% depreciation factor for scalar calculation
  quantity: number // Fixed to 1 for instantiated serialized items, numeric for bulk (supports floating scalar)
  minQuantity: number // Minimum threshold for stock alerts (supports floating scalar e.g. 1.5, 0.33)
  minQuantityScalar?: number // Floating scalar safety buffer multiplier (default 1.0, e.g. 1.25x)
  effectiveMinQuantity?: number // Calculated threshold = Math.round(minQuantity * (minQuantityScalar || 1.0) * 100) / 100
  maxQuantity?: number
  stockAlertStatus: 'optimal' | 'low_stock' | 'out_of_stock'
  alternatePartIds?: string[]
  alternateParts?: AlternatePartSummary[]
  isMachineLinked?: boolean
  linkedMachineId?: string
  linkedMachineName?: string
  tags?: string[]
  qrCodePayload?: string
  rfidPayload?: string
  serialNumber?: string
  createdAt: string
  updatedAt: string
}

export type PartAuditAction = 
  | 'log_part' 
  | 'bulk_log' 
  | 'use_part' 
  | 'condition_update' 
  | 'operational_state_update' 
  | 'resell_price_update' 
  | 'relocate' 
  | 'scrap' 
  | 'policy_update'

export interface PartAuditRecord {
  id: string
  partId: string
  partIdentifier: string
  partName: string
  action: PartAuditAction
  timestamp: string
  actorId: string
  actorName: string
  quantityDelta?: number
  costCenter?: PartUsageCostCenter
  previousState?: Record<string, any>
  newState?: Record<string, any>
  notes?: string
}

// --------------------------------------------------------------------------
// Asset Templating with Inheritance & Instances (docs/TODO/inventory.TODO.md)
// --------------------------------------------------------------------------

export interface AssetTemplateFieldSchema {
  key: string
  label: string
  type: 'string' | 'number' | 'boolean' | 'select'
  options?: string[]
  defaultValue?: any
  required?: boolean
  description?: string
}

export interface AssetTemplateDefinition {
  id: string
  name: string
  topLevelCategory: 'Hardware' | 'Software'
  extendsTemplateId?: string // ID of parent template for inheritance
  identifierPattern: string // Template string e.g. "ID-{number}", "MTR-{number}", "{uuid}"
  sequentialCounter: number
  description: string
  icon?: string
  fixedFields: {
    manufacturer: string
    supplier?: string
    category: string
    basePriceEur: number
    specs: Record<string, any>
    defaultOwner: PartOwner
  }
  instanceSpecificFieldsSchema: AssetTemplateFieldSchema[]
  instances?: InventoryPart[]
  tags?: string[]
  createdAt: string
  updatedAt: string
}

// --------------------------------------------------------------------------
// Machine Document Import Types (ChatGPT / LLM / BOM JSON)
// --------------------------------------------------------------------------

export interface MachineDocumentImportItem {
  name: string
  category: 'Hardware' | 'Software'
  manufacturer: string
  modelNumber?: string
  serialNumber?: string
  quantity: number
  location?: string
  estimatedPriceEur?: number
  childComponents?: MachineDocumentImportItem[] // Supports modular/split controller IPC assets
  specs?: Record<string, any>
}

export interface MachineDocumentImportPayload {
  machineId: string
  machineName: string
  source: 'chatgpt' | 'llm' | 'bom_text' | 'json'
  rawContent: string
  parsedItems?: MachineDocumentImportItem[]
}

export interface MachineDocumentImportResult {
  success: boolean
  machineId: string
  machineName: string
  importedAssetCount: number
  splitAssetCount: number
  createdAssets: any[]
  errors?: string[]
}

// --------------------------------------------------------------------------
// Machine Spare Parts & Policy System (docs/TODO/inventory.TODO.md)
// --------------------------------------------------------------------------

export interface MachineSparePartItem {
  id: string
  machineId: string
  machineName: string
  partId: string
  partName: string
  customIdentifier: string
  unitsPerMachine: number // Quantity of this part installed per machine
  fractionalRatio: number // e.g. 1/3 = 0.3333 (1 spare per 3 machines in production)
  minQuantity?: number // Optional base minimum spare quantity (supports floating scalar e.g. 1.5)
  minQuantityScalar?: number // Floating safety buffer scalar multiplier (default 1.0, e.g. 1.25x)
  effectiveMinQuantity?: number // Calculated minimum spares = Math.round((minQuantity * (minQuantityScalar || 1.0)) * 100) / 100
  upperBoundQuantity?: number // Hard cap overriding calculated fractional policy
  upperBoundCostEur?: number // Hard cost cap overriding calculated fractional policy
  activeMachinesInProduction: number
  requiredSparesCalculated: number // ceil(activeMachinesInProduction * fractionalRatio) capped by upper bounds
  onHandSpares: number
  alternativeSparesAvailable: number
  coverageStatus: 'covered' | 'shortage' | 'critical' | 'surplus'
  unitPriceEur: number
  totalHoldingValuationEur: number
  alternatePartIds?: string[]
}

export interface SparePartsReportingSummary {
  totalDefinedSpareParts: number
  totalCovered: number
  totalShortages: number
  criticalShortages: number
  totalHoldingValuationEur: number
  currencyConversions: {
    HUF: number
    USD: number
    GBP: number
  }
}

// --------------------------------------------------------------------------
// RBAC Roles
// --------------------------------------------------------------------------

export type InventoryRbacRole = 'use_part' | 'log_part' | 'inventory_manager'

