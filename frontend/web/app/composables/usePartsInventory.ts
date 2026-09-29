import { ref, computed } from 'vue'
import type {
  InventoryPart,
  AssetTemplateDefinition,
  MachineSparePartItem,
  SparePartsReportingSummary,
  PartAuditRecord,
  PartUsageCostCenter,
  PartLifecycleCondition,
  PartOperationalState,
  MachineDocumentImportPayload,
  MachineDocumentImportResult
} from '~/types/inventory'
import { useAuthSession } from '~/composables/useAuthSession'

export function usePartsInventory() {
  const { user, userRole, isAdmin, isTechnician, isEngineer, isShiftLeader, isGroupLeader, isEngineeringAdmin, isPlantEngineeringManager, isSeniorEngineeringManager, isOperativePlanner } = useAuthSession()

  const parts = ref<InventoryPart[]>([])
  const selectedPart = ref<InventoryPart | null>(null)
  const templates = ref<AssetTemplateDefinition[]>([])
  const spareParts = ref<MachineSparePartItem[]>([])
  const sparePartsReport = ref<SparePartsReportingSummary | null>(null)
  const auditLogs = ref<PartAuditRecord[]>([])
  const loading = ref(false)
  const activeCurrency = ref<'EUR' | 'HUF' | 'USD' | 'GBP'>('EUR')

  const kpis = ref({
    totalPartsCount: 0,
    bulkCount: 0,
    serializedCount: 0,
    workingCount: 0,
    inServiceCount: 0,
    evaluationCount: 0,
    brokenCount: 0,
    newConditionCount: 0,
    usedConditionCount: 0,
    donorConditionCount: 0,
    obsoleteConditionCount: 0,
    scrapConditionCount: 0,
    lowStockAlertCount: 0,
    outOfStockAlertCount: 0,
    totalWarehouseValuationEur: 0,
    totalWarehouseValuationConverted: 0
  })

  // RBAC permissions (docs/TODO/inventory.TODO.md)
  const canUsePart = computed(() => {
    const role = (userRole.value || '').toLowerCase()
    return (
      role === 'use_part' ||
      role === 'inventory_manager' ||
      isTechnician.value ||
      isEngineer.value ||
      isShiftLeader.value ||
      isGroupLeader.value ||
      isAdmin.value
    )
  })

  const canLogPart = computed(() => {
    const role = (userRole.value || '').toLowerCase()
    return (
      role === 'log_part' ||
      role === 'inventory_manager' ||
      isTechnician.value ||
      isEngineer.value ||
      isOperativePlanner.value ||
      isAdmin.value
    )
  })

  const canManageInventory = computed(() => {
    const role = (userRole.value || '').toLowerCase()
    return (
      role === 'inventory_manager' ||
      isAdmin.value ||
      isEngineeringAdmin.value ||
      isPlantEngineeringManager.value ||
      isSeniorEngineeringManager.value
    )
  })

  // Current actor identity helper
  const getActor = () => ({
    id: user.value?.id || 'usr-current',
    name: user.value?.name || 'Technician'
  })

  // -------------------------------------------------------------------------
  // Parts Fetch & Filtering
  // -------------------------------------------------------------------------
  const fetchParts = async (filters: Record<string, any> = {}) => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/parts', {
        method: 'GET',
        params: {
          ...filters,
          currency: activeCurrency.value
        }
      })
      if (res) {
        parts.value = res.parts || []
        if (res.kpis) {
          kpis.value = res.kpis
        }
      }
    } catch (err) {
      console.error('[usePartsInventory] fetchParts error:', err)
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Log Part (Single)
  // -------------------------------------------------------------------------
  const logPart = async (payload: Partial<InventoryPart>) => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/parts', {
        method: 'POST',
        body: {
          ...payload,
          actor: getActor()
        }
      })
      await fetchParts()
      return res.part
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Bulk Log Parts
  // -------------------------------------------------------------------------
  const bulkLogParts = async (items: Array<Partial<InventoryPart>>) => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/parts/bulk-log', {
        method: 'POST',
        body: {
          items,
          actor: getActor()
        }
      })
      await fetchParts()
      return res.parts
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Use Part (Mandatory Cost Center)
  // -------------------------------------------------------------------------
  const usePart = async (partId: string, costCenter: PartUsageCostCenter, quantity: number = 1) => {
    loading.value = true
    try {
      const res = await $fetch<any>(`/api/inventory/parts/${partId}/use`, {
        method: 'POST',
        body: {
          costCenter,
          quantity,
          actor: getActor()
        }
      })
      await fetchParts()
      return res
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Update Condition & Operational State
  // -------------------------------------------------------------------------
  const updatePartCondition = async (
    partId: string,
    condition: PartLifecycleCondition,
    operationalState?: PartOperationalState
  ) => {
    loading.value = true
    try {
      const res = await $fetch<any>(`/api/inventory/parts/${partId}/patch`, {
        method: 'PATCH',
        body: {
          condition,
          operationalState,
          actor: getActor()
        }
      })
      await fetchParts()
      return res.part
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Update Resell Price & Wear Depreciation
  // -------------------------------------------------------------------------
  const updateResellPrice = async (
    partId: string,
    params: { wearDepreciationPercentage?: number; resellPriceOverrideEur?: number }
  ) => {
    loading.value = true
    try {
      const res = await $fetch<any>(`/api/inventory/parts/${partId}/patch`, {
        method: 'PATCH',
        body: {
          ...params,
          actor: getActor()
        }
      })
      await fetchParts()
      return res.part
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Asset Templates & Instances
  // -------------------------------------------------------------------------
  const fetchTemplates = async () => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/templates/tree', {
        method: 'GET'
      })
      if (res && res.templates) {
        templates.value = res.templates
      }
    } catch (err) {
      console.error('[usePartsInventory] fetchTemplates error:', err)
    } finally {
      loading.value = false
    }
  }

  const createTemplate = async (template: Partial<AssetTemplateDefinition>) => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/templates', {
        method: 'POST',
        body: {
          template,
          actor: getActor()
        }
      })
      await fetchTemplates()
      return res.template
    } finally {
      loading.value = false
    }
  }

  const updateTemplate = async (templateId: string, updates: Partial<AssetTemplateDefinition>) => {
    loading.value = true
    try {
      const res = await $fetch<any>(`/api/inventory/templates/${templateId}`, {
        method: 'PATCH',
        body: {
          updates,
          actor: getActor()
        }
      })
      await fetchTemplates()
      return res.template
    } finally {
      loading.value = false
    }
  }

  const deleteTemplate = async (templateId: string) => {
    loading.value = true
    try {
      const res = await $fetch<any>(`/api/inventory/templates/${templateId}`, {
        method: 'DELETE',
        body: {
          actor: getActor()
        }
      })
      await fetchTemplates()
      return res.success
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Machine Spare Parts & Policies
  // -------------------------------------------------------------------------
  const fetchSpareParts = async () => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/spare-parts', {
        method: 'GET',
        params: { currency: activeCurrency.value }
      })
      if (res) {
        spareParts.value = res.spareParts || []
        sparePartsReport.value = res.reporting || null
      }
    } catch (err) {
      console.error('[usePartsInventory] fetchSpareParts error:', err)
    } finally {
      loading.value = false
    }
  }

  const updateSparePartPolicy = async (id: string, updates: Partial<MachineSparePartItem>) => {
    loading.value = true
    try {
      const res = await $fetch<any>('/api/inventory/spare-parts/policies', {
        method: 'POST',
        body: { id, ...updates }
      })
      await fetchSpareParts()
      return res.sparePart
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Machine Document Import (ChatGPT / AI JSON / BOM)
  // -------------------------------------------------------------------------
  const importMachineDocument = async (payload: MachineDocumentImportPayload): Promise<MachineDocumentImportResult> => {
    loading.value = true
    try {
      const res = await $fetch<MachineDocumentImportResult>('/api/inventory/machine-import', {
        method: 'POST',
        body: {
          ...payload,
          actor: getActor()
        }
      })
      await fetchParts()
      return res
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Code Scan Lookup (QR / Barcode / RFID)
  // -------------------------------------------------------------------------
  const lookupCode = async (code: string): Promise<InventoryPart | null> => {
    if (!code || !code.trim()) return null
    try {
      const res = await $fetch<any>('/api/inventory/lookup', {
        method: 'GET',
        params: { code: code.trim(), currency: activeCurrency.value }
      })
      return res.part || null
    } catch (err) {
      return null
    }
  }

  // -------------------------------------------------------------------------
  // Audit History
  // -------------------------------------------------------------------------
  const fetchAuditLogs = async (partId?: string) => {
    loading.value = true
    try {
      const endpoint = partId ? `/api/inventory/parts/${partId}/audit` : '/api/inventory/parts/audit'
      const res = await $fetch<any>(endpoint, { method: 'GET' })
      if (res && res.auditLog) {
        auditLogs.value = res.auditLog
      }
    } catch (err) {
      console.error('[usePartsInventory] fetchAuditLogs error:', err)
    } finally {
      loading.value = false
    }
  }

  // -------------------------------------------------------------------------
  // Currency Formatter
  // -------------------------------------------------------------------------
  const formatCurrency = (val: number, currency: string = activeCurrency.value) => {
    if (val === undefined || val === null) return '0'
    const locale = currency === 'HUF' ? 'hu-HU' : currency === 'USD' ? 'en-US' : 'de-DE'
    return new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(val)
  }

  return {
    parts,
    selectedPart,
    templates,
    spareParts,
    sparePartsReport,
    auditLogs,
    loading,
    activeCurrency,
    kpis,
    canUsePart,
    canLogPart,
    canManageInventory,
    fetchParts,
    logPart,
    bulkLogParts,
    usePart,
    updatePartCondition,
    updateResellPrice,
    fetchTemplates,
    createTemplate,
    updateTemplate,
    deleteTemplate,
    fetchSpareParts,
    updateSparePartPolicy,
    importMachineDocument,
    lookupCode,
    fetchAuditLogs,
    formatCurrency
  }
}
