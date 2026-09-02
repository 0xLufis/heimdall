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
