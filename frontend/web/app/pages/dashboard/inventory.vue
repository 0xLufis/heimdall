<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { 
  PlusIcon, 
  SlidersHorizontal, 
  Check, 
  RefreshCw, 
  Layers, 
  HardDrive, 
  Cpu, 
  DollarSign, 
  Activity, 
  Wifi,
  FolderTree,
  Boxes,
  Wrench,
  PackageCheck,
  QrCode,
  Bot,
  History,
  Send,
  PackagePlus,
  ShieldCheck
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import HeimdallSearchBar from '~/components/search/HeimdallSearchBar.vue'
import DashboardInventoryEditModal from '~/components/dashboard/InventoryEditModal.vue'
import DashboardInventoryStationComponentTreeModal from '~/components/dashboard/inventory/StationComponentTreeModal.vue'
import DashboardInventoryAddModal from '~/components/dashboard/InventoryAddModal.vue'
import DashboardInventoryTable from '~/components/dashboard/InventoryTable.vue'
import PartsInventoryList from '~/components/inventory/PartsInventoryList.vue'
import UsePartModal from '~/components/inventory/UsePartModal.vue'
import LogPartModal from '~/components/inventory/LogPartModal.vue'
import BulkPartIntakeModal from '~/components/inventory/BulkPartIntakeModal.vue'
import AssetTemplatesManager from '~/components/inventory/AssetTemplatesManager.vue'
import MachineDocumentImportModal from '~/components/inventory/MachineDocumentImportModal.vue'
import SparePartsPolicyManager from '~/components/inventory/SparePartsPolicyManager.vue'
import InventoryCodeScannerModal from '~/components/inventory/InventoryCodeScannerModal.vue'
import PartAuditLogDrawer from '~/components/inventory/PartAuditLogDrawer.vue'
import PartProvisioningStation from '~/components/inventory/PartProvisioningStation.vue'
import CreateEditTemplateModal from '~/components/inventory/CreateEditTemplateModal.vue'
import { useInventoryLive } from '~/composables/useInventoryLive'
import { usePartsInventory } from '~/composables/usePartsInventory'
import type { SearchInstanceConfig } from '~/types/search'
import type { InventoryPart, PartUsageCostCenter } from '~/types/inventory'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { isLiveConnected, lastSyncedAt, onInventoryUpdate } = useInventoryLive()

// Dual orthogonal dimensions: Classification (Hardware vs Software) x Tracking (Serialized Parts vs Bulk Stock)
const classification = ref<'all' | 'hardware' | 'software'>('all')
const tracking = ref<'all' | 'serialized' | 'stock'>('all')

const activeTab = computed<'all' | 'hardware' | 'software' | 'parts' | 'stock'>({
  get() {
    if (classification.value === 'hardware' && tracking.value === 'all') return 'hardware'
    if (classification.value === 'software' && tracking.value === 'all') return 'software'
    if (classification.value === 'all' && tracking.value === 'serialized') return 'parts'
    if (classification.value === 'all' && tracking.value === 'stock') return 'stock'
    return 'all'
  },
  set(val) {
    if (val === 'hardware') {
      classification.value = 'hardware'
      tracking.value = 'all'
    } else if (val === 'software') {
      classification.value = 'software'
      tracking.value = 'all'
    } else if (val === 'parts') {
      classification.value = 'all'
      tracking.value = 'serialized'
    } else if (val === 'stock') {
      classification.value = 'all'
      tracking.value = 'stock'
    } else {
      classification.value = 'all'
      tracking.value = 'all'
    }
  }
})

const masterInventory = ref<any[]>([])
const initialLoading = ref(true)
const backgroundSyncing = ref(false)
const loading = computed(() => initialLoading.value)

// 4 background pre-views created from master inventory
const backgroundViews = computed(() => {
  const all = masterInventory.value
  return {
    all,
    hardware: all.filter(i => (i.itemType || '').toLowerCase() === 'hardware'),
    software: all.filter(i => (i.itemType || '').toLowerCase() === 'software'),
    parts: all.filter(i => !i.isStockItem && (i.itemType || '').toLowerCase() !== 'software'),
    stock: all.filter(i => i.isStockItem === true)
  }
})

const currentQuery = ref('')
const kpis = ref({
  totalGlobalCount: 0,
  totalGlobalHardware: 0,
  totalGlobalSoftware: 0,
  totalGlobalParts: 0,
  totalGlobalStock: 0,
  totalGlobalCost: 0
})

const showTreeModal = ref(false)
const showAddModal = ref(false)
const showEditModal = ref(false)
const selectedEditItem = ref<any | null>(null)

// Instant in-memory filtered items with combinable Classification x Tracking (ZERO flicker, ZERO roundtrips on click)
const filteredItems = computed(() => {
  let list = masterInventory.value

  // 1. Classification facet (Hardware, Software, or All)
  if (classification.value === 'hardware') {
    list = backgroundViews.value.hardware
  } else if (classification.value === 'software') {
    list = backgroundViews.value.software
  }

  // 2. Tracking facet (Serialized Parts, Bulk Stock, or All)
  if (tracking.value === 'serialized') {
    list = list.filter(i => !i.isStockItem)
  } else if (tracking.value === 'stock') {
    list = list.filter(i => i.isStockItem === true)
  }

  // 3. Instant client-side HeimdallSearch & keyword filtering
  if (currentQuery.value && currentQuery.value.trim()) {
    const q = currentQuery.value.toLowerCase().trim()
    const tokens = q.split(/\s+/).filter(Boolean)
    list = list.filter(item => {
      const blob = [
        item.name,
        item.displayName,
        item.serialNumber,
        item.customIdentifier,
        item.manufacturer?.name,
        item.storageLocation,
        item.technology,
        item.equipmentStatus,
        ...(item.responsibleTeams?.map((t: any) => t.name) || []),
        ...Object.entries(item.metadata || {}).map(([k, v]) => `${k}:${v}`)
      ].filter(Boolean).join(' ').toLowerCase()

      return tokens.every(tok => {
        if (tok.includes(':')) {
          const [, val] = tok.split(':')
          if (val) return blob.includes(val.replace(/^["']|["']$/g, ''))
        }
        return blob.includes(tok)
      })
    })
  }

  return list
})

const items = computed(() => filteredItems.value)

// Pagination State (supports 5, 10, 50, 100, 1000, and custom)
const currentPage = ref(1)
const pageSize = ref<number | 'custom'>(10)
const customPageSize = ref(100)

const effectivePageSize = computed(() => {
  if (pageSize.value === 'custom') {
    return Math.max(1, Number(customPageSize.value) || 10)
  }
  return Number(pageSize.value)
})

const totalPages = computed(() => {
  return Math.max(1, Math.ceil(items.value.length / effectivePageSize.value))
})

const paginatedItems = computed(() => {
  const start = (currentPage.value - 1) * effectivePageSize.value
  return items.value.slice(start, start + effectivePageSize.value)
})

const setPage = (page: number) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page
  }
}

// Reset page when switching views in memory (no network calls on tab/mode clicks!)
watch([pageSize, customPageSize, classification, tracking], () => {
  currentPage.value = 1
})

const inventorySearchConfig = computed<SearchInstanceConfig>(() => ({
  instanceId: 'inventory',
  placeholder: 'Search inventory by name, serial, model, manufacturer, location, spec...',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  defaultTags: [], // Kept empty to prevent stale tag pills breaking category switching
  enableAutoTagging: true
}))

const columns = ref<Record<string, boolean>>({
  manufacturer: true,
  modelNumber: false,
  purchaseDate: true,
  cost: true,
  specs: true,
  tags: true,
})

const getColumnDescription = (key: string) => {
  const descs: Record<string, string> = {
    manufacturer: 'Display the brand or OEM manufacturer of the asset',
    modelNumber: 'Show specific model or part numbers',
    purchaseDate: 'Lifecycle tracking and warranty start dates',
    cost: 'Financial capital investment in HUF currency',
    specs: 'Technical parameters like torque, voltage, or resolution',
    tags: 'Custom attributes and JSONB data points'
  }
  return descs[key] || 'Data field'
}

const resetColumns = () => {
  columns.value = {
    manufacturer: true,
    modelNumber: false,
    purchaseDate: true,
    cost: true,
    specs: true,
    tags: true,
  }
}

const onSearch = (q: string) => {
  currentQuery.value = q
  currentPage.value = 1
}

// Query master inventory (uses Redis on server/backend, cached in memory on client)
const fetchMasterInventory = async () => {
  if (masterInventory.value.length === 0) {
    initialLoading.value = true
  } else {
    backgroundSyncing.value = true
  }
  try {
    const res = await $fetch<any>('/api/inventory/filter', {
      method: 'GET',
      params: {
        classification: 'all',
        tracking: 'all'
      }
    })
    if (res) {
      masterInventory.value = res.items || []
      if (res.kpis) {
        kpis.value = res.kpis
      }
    }
  } catch (e) {
    console.error('Error fetching inventory:', e)
  } finally {
    initialLoading.value = false
    backgroundSyncing.value = false
  }
}

const formatCurrency = (val: number) => {
  if (!val && val !== 0) return '0'
  return new Intl.NumberFormat('hu-HU').format(val)
}

const addComponent = async (type: string, formData: any) => {
  try {
    await $fetch('/api/proxy/inventory', {
      method: 'POST',
      body: formData,
    })
    await fetchMasterInventory()
  } catch (e) {
    console.error('Error adding component:', e)
  }
}

const handleEditItem = (item: any) => {
  selectedEditItem.value = item
  showEditModal.value = true
}

const handleSaveEdit = async (updatedItem: any) => {
  try {
    if (updatedItem.id) {
      await $fetch(`/api/proxy/inventory/components/${updatedItem.id}`, {
        method: 'PUT',
        body: updatedItem
      })
    }
  } catch (e) {
    console.error('Error updating inventory item:', e)
  } finally {
    await fetchMasterInventory()
  }
}

// Split Parts Inventory State & Composables (docs/TODO/inventory.TODO.md)
const activeSection = ref<'parts' | 'provisioning' | 'templates' | 'spare_parts' | 'assets' | 'audit'>('parts')
const provisioningPreselectedId = ref<string | undefined>(undefined)

const {
  parts: partsList,
  templates: templatesList,
  spareParts: sparePartsList,
  sparePartsReport,
  auditLogs,
  loading: partsLoading,
  kpis: partsKpis,
  activeCurrency,
  formatCurrency: formatPartsCurrency,
  fetchParts,
  logPart: handleLogPart,
  bulkLogParts: handleBulkLogParts,
  usePart: handleUsePartAction,
  fetchTemplates,
  fetchSpareParts,
  fetchAuditLogs
} = usePartsInventory()

const showUsePartModal = ref(false)
const selectedPartForUse = ref<InventoryPart | null>(null)
const showLogPartModal = ref(false)
const showBulkIntakeModal = ref(false)
const showMachineImportModal = ref(false)
const showCodeScannerModal = ref(false)
const showAuditDrawer = ref(false)
const showCreateTemplateModal = ref(false)

const handleOpenUsePart = (part: InventoryPart) => {
  selectedPartForUse.value = part
  showUsePartModal.value = true
}

const openProvisioningForPart = (part: InventoryPart) => {
  provisioningPreselectedId.value = part.id
  activeSection.value = 'provisioning'
}

const onConfirmUsePart = async (data: { partId: string; costCenter: PartUsageCostCenter; quantity: number }) => {
  await handleUsePartAction(data.partId, data.costCenter, data.quantity)
  await fetchParts()
  await fetchMasterInventory()
}

const onPartProvisioned = async (record: any) => {
  await fetchParts()
  await fetchMasterInventory()
  await fetchAuditLogs()
  await fetchTemplates()
}

const onConfirmLogPart = async (payload: Partial<InventoryPart>) => {
  await handleLogPart(payload)
  await fetchParts()
  await fetchTemplates()
}

const onConfirmBulkLogged = async (items: Array<Partial<InventoryPart>>) => {
  await handleBulkLogParts(items)
  await fetchParts()
  await fetchTemplates()
}

const onMachineImportSuccess = async () => {
  await fetchMasterInventory()
  await fetchParts()
  await fetchTemplates()
}

const onScannerLogWithCode = (code: string) => {
  showLogPartModal.value = true
}

const onScannerUsePart = (part: InventoryPart) => {
  provisioningPreselectedId.value = part.id
  showCodeScannerModal.value = false
  activeSection.value = 'provisioning'
}

onMounted(() => {
  fetchMasterInventory()
  fetchParts()
  fetchTemplates()
  fetchSpareParts()
  fetchAuditLogs()
})

let updateDebounceTimer: any = null
onInventoryUpdate(() => {
  if (updateDebounceTimer) clearTimeout(updateDebounceTimer)
  updateDebounceTimer = setTimeout(() => {
    fetchMasterInventory()
    fetchParts()
  }, 1000)
})
</script>

<template>
  <div class="space-y-6 animate-in fade-in duration-300">
    <!-- Header Area with Primary Section Navigation Tabs & Controls -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-2 border-b border-border">
      <div>
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-primary/10 border border-primary/20 text-primary">
            <PackageCheck class="h-6 w-6" />
          </div>
          <div>
            <h1 class="text-2xl font-bold tracking-tight text-foreground">
              Inventory & Asset Infrastructure
            </h1>
            <p class="text-sm text-muted-foreground mt-0.5">
              Split parts inventory, production machine assets, template inheritance, and spare part policies
            </p>
          </div>
        </div>

        <!-- Primary Top-Level Section Navigation Tabs -->
        <div class="flex flex-wrap items-center gap-1.5 mt-4 p-1 bg-muted/60 rounded-xl border border-border w-fit">
          <button
            type="button"
            @click="activeSection = 'parts'"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer"
            :class="activeSection === 'parts' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Boxes class="size-3.5" />
            <span>Warehouse Inventory</span>
          </button>

          <button
            type="button"
            @click="activeSection = 'provisioning'"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer relative"
            :class="activeSection === 'provisioning' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Wrench class="size-3.5 text-amber-500" />
            <span>Provisioning Station</span>
            <span class="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
          </button>

          <button
            type="button"
            @click="activeSection = 'templates'"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer"
            :class="activeSection === 'templates' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <FolderTree class="size-3.5" />
            <span>Asset Templates</span>
          </button>

          <button
            type="button"
            @click="activeSection = 'spare_parts'"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer"
            :class="activeSection === 'spare_parts' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <ShieldCheck class="size-3.5 text-emerald-500" />
            <span>Spare Parts & Policies</span>
          </button>

          <button
            type="button"
            @click="activeSection = 'assets'"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer"
            :class="activeSection === 'assets' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Cpu class="size-3.5" />
            <span>Production Machines</span>
          </button>

          <button
            type="button"
            @click="activeSection = 'audit'"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer"
            :class="activeSection === 'audit' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <History class="size-3.5" />
            <span>Audit Ledger</span>
          </button>
        </div>
      </div>

      <!-- Header Action Controls & Currency Switcher -->
      <div class="flex flex-wrap items-center gap-2.5 shrink-0">
        <!-- Live Currency Switcher -->
        <div class="flex items-center p-0.5 bg-muted/60 rounded-lg border border-border">
          <button
            v-for="curr in (['EUR', 'HUF', 'USD', 'GBP'] as const)"
            :key="curr"
            type="button"
            @click="activeCurrency = curr; fetchParts()"
            class="px-2 py-0.5 rounded text-[11px] font-mono font-bold transition-all cursor-pointer"
            :class="activeCurrency === curr ? 'bg-primary text-primary-foreground shadow-2xs' : 'text-muted-foreground hover:text-foreground'"
          >
            {{ curr }}
          </button>
        </div>

        <Button
          variant="outline"
          size="sm"
          @click="showCodeScannerModal = true"
          class="border-primary/40 bg-card hover:bg-primary/10 text-primary rounded-lg text-xs font-medium h-8 px-3 gap-1.5 shadow-xs cursor-pointer"
        >
          <QrCode class="h-3.5 w-3.5" />
          <span>Scanner</span>
        </Button>

        <Button
          variant="outline"
          size="sm"
          @click="showMachineImportModal = true"
          class="border-emerald-500/40 bg-card hover:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 rounded-lg text-xs font-medium h-8 px-3 gap-1.5 shadow-xs cursor-pointer"
        >
          <Bot class="h-3.5 w-3.5" />
          <span>AI Import</span>
        </Button>

        <Button
          size="sm"
          @click="showLogPartModal = true"
          class="bg-primary text-primary-foreground hover:bg-primary/90 rounded-lg text-xs font-semibold h-8 px-3 gap-1.5 shadow-xs cursor-pointer"
        >
          <PackagePlus class="h-3.5 w-3.5" />
          <span>Intake Part</span>
        </Button>
      </div>
    </div>

    <!-- Global Floor & Warehouse KPI Hero Bar -->
    <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
      <!-- Valuation -->
      <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-1">
        <div class="text-[11px] text-muted-foreground uppercase font-semibold tracking-wider flex items-center justify-between">
          <span>Valuation</span>
          <span class="font-mono text-primary text-[10px]">{{ activeCurrency }}</span>
        </div>
        <div class="font-mono text-lg font-bold text-foreground">
          {{ formatPartsCurrency(partsKpis.totalWarehouseValuationConverted) }}
        </div>
        <div class="text-[10px] text-muted-foreground">
          Base: €{{ formatPartsCurrency(partsKpis.totalWarehouseValuationEur, 'EUR') }}
        </div>
      </div>

      <!-- Total Parts -->
      <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-1">
        <div class="text-[11px] text-muted-foreground uppercase font-semibold tracking-wider">
          Total Stock
        </div>
        <div class="font-mono text-lg font-bold text-foreground">
          {{ partsKpis.totalPartsCount }}
        </div>
        <div class="text-[10px] text-muted-foreground flex items-center gap-1.5">
          <span>{{ partsKpis.serializedCount }} serialized</span>
          <span>•</span>
          <span>{{ partsKpis.bulkCount }} bulk</span>
        </div>
      </div>

      <!-- Operational Readiness -->
      <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-1">
        <div class="text-[11px] text-muted-foreground uppercase font-semibold tracking-wider">
          Operational State
        </div>
        <div class="font-mono text-lg font-bold text-emerald-600 dark:text-emerald-400">
          {{ partsKpis.workingCount }} Ready
        </div>
        <div class="text-[10px] text-muted-foreground">
          {{ partsKpis.inServiceCount }} in service, {{ partsKpis.brokenCount }} broken
        </div>
      </div>

      <!-- Stock Alerts -->
      <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-1">
        <div class="text-[11px] text-muted-foreground uppercase font-semibold tracking-wider">
          Stock Alerts
        </div>
        <div class="font-mono text-lg font-bold" :class="partsKpis.lowStockAlertCount + partsKpis.outOfStockAlertCount > 0 ? 'text-amber-500' : 'text-foreground'">
          {{ partsKpis.lowStockAlertCount + partsKpis.outOfStockAlertCount }} Alerts
        </div>
        <div class="text-[10px] text-muted-foreground">
          {{ partsKpis.lowStockAlertCount }} low stock, {{ partsKpis.outOfStockAlertCount }} out
        </div>
      </div>

      <!-- Blueprint Templates -->
      <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-1">
        <div class="text-[11px] text-muted-foreground uppercase font-semibold tracking-wider">
          Asset Templates
        </div>
        <div class="font-mono text-lg font-bold text-foreground">
          {{ templatesList.length }}
        </div>
        <div class="text-[10px] text-muted-foreground">
          {{ templatesList.filter(t => t.extendsTemplateId).length }} inherited models
        </div>
      </div>

      <!-- Spare Policy Coverage -->
      <div class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-1">
        <div class="text-[11px] text-muted-foreground uppercase font-semibold tracking-wider">
          Critical Spares
        </div>
        <div class="font-mono text-lg font-bold text-foreground">
          {{ sparePartsReport?.totalCovered || 0 }} / {{ sparePartsList.length }}
        </div>
        <div class="text-[10px]" :class="(sparePartsReport?.criticalShortages || 0) > 0 ? 'text-destructive font-semibold' : 'text-muted-foreground'">
          {{ sparePartsReport?.criticalShortages || 0 }} critical shortages
        </div>
      </div>
    </div>

    <!-- 1. Production Assets Section -->
    <div v-if="activeSection === 'assets'" class="space-y-6">
      <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-2 border-b border-border">
        <div>
          <!-- KPI Metric Badges (Interactive Filters) -->
          <div class="flex flex-wrap items-center gap-2 mt-4">
          <button 
            type="button"
            @click="classification = 'all'; tracking = 'all'"
            class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
            :class="classification === 'all' && tracking === 'all' ? 'bg-primary/15 border-primary text-primary dark:text-primary-foreground shadow-xs ring-1 ring-primary/40' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:border-border/80'"
          >
            <Layers class="w-3.5 h-3.5 text-primary" />
            <span>Total:</span>
            <span class="font-mono font-semibold text-foreground">{{ kpis.totalGlobalCount || items.length }}</span>
          </button>

          <button 
            type="button"
            @click="classification = (classification === 'hardware' ? 'all' : 'hardware')"
            class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
            :class="classification === 'hardware' ? 'bg-emerald-500/15 border-emerald-500 text-emerald-700 dark:text-emerald-300 shadow-xs ring-1 ring-emerald-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:border-border/80'"
          >
            <Cpu class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
            <span>Hardware:</span>
            <span class="font-mono font-semibold text-foreground">{{ kpis.totalGlobalHardware }}</span>
          </button>

          <button 
            type="button"
            @click="classification = (classification === 'software' ? 'all' : 'software')"
            class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
            :class="classification === 'software' ? 'bg-blue-500/15 border-blue-500 text-blue-700 dark:text-blue-300 shadow-xs ring-1 ring-blue-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:border-border/80'"
          >
            <HardDrive class="w-3.5 h-3.5 text-blue-600 dark:text-blue-400" />
            <span>Software:</span>
            <span class="font-mono font-semibold text-foreground">{{ kpis.totalGlobalSoftware }}</span>
          </button>

          <button 
            type="button"
            @click="tracking = (tracking === 'serialized' ? 'all' : 'serialized')"
            class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
            :class="tracking === 'serialized' ? 'bg-teal-500/15 border-teal-500 text-teal-700 dark:text-teal-300 shadow-xs ring-1 ring-teal-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:border-border/80'"
          >
            <Wrench class="w-3.5 h-3.5 text-teal-600 dark:text-teal-400" />
            <span>Serialized:</span>
            <span class="font-mono font-semibold text-foreground">{{ kpis.totalGlobalParts }}</span>
          </button>

          <button 
            type="button"
            @click="tracking = (tracking === 'stock' ? 'all' : 'stock')"
            class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
            :class="tracking === 'stock' ? 'bg-purple-500/15 border-purple-500 text-purple-700 dark:text-purple-300 shadow-xs ring-1 ring-purple-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:border-border/80'"
          >
            <Boxes class="w-3.5 h-3.5 text-purple-600 dark:text-purple-400" />
            <span>Bulk Stock:</span>
            <span class="font-mono font-semibold text-foreground">{{ kpis.totalGlobalStock }}</span>
          </button>

          <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-card border border-border text-xs font-medium">
            <DollarSign class="w-3.5 h-3.5 text-amber-600 dark:text-amber-400" />
            <span class="text-muted-foreground">Valuation:</span>
            <span class="font-mono font-semibold text-foreground">{{ formatCurrency(kpis.totalGlobalCost) }} HUF</span>
          </div>

          <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-card border border-border text-xs font-medium">
            <span class="size-2 rounded-full" :class="isLiveConnected ? 'bg-emerald-500 animate-pulse' : 'bg-emerald-500'" />
            <span :class="isLiveConnected ? 'text-emerald-600 dark:text-emerald-400' : 'text-emerald-600 dark:text-emerald-400'">
              {{ isLiveConnected ? 'Live Telemetry Connected' : 'Live Sync Active' }}
            </span>
          </div>
        </div>
      </div>
      
      <!-- Primary View Switcher: Combinable Classification x Tracking -->
      <div class="flex flex-wrap items-center gap-2.5 shrink-0">
        <!-- Classification Facet -->
        <div class="bg-card p-1 rounded-lg border border-border shadow-xs flex items-center gap-1">
          <span class="text-xs text-muted-foreground font-medium px-2">Class:</span>
          <Button 
            variant="ghost" 
            size="sm"
            @click="classification = 'all'" 
            :class="classification === 'all' ? 'bg-primary text-primary-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 cursor-pointer"
          >
            All
          </Button>
          <Button 
            variant="ghost" 
            size="sm"
            @click="classification = 'hardware'" 
            :class="classification === 'hardware' ? 'bg-primary text-primary-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 flex items-center gap-1.5 cursor-pointer"
          >
            <Cpu class="w-3 h-3" />
            Hardware
          </Button>
          <Button 
            variant="ghost" 
            size="sm"
            @click="classification = 'software'" 
            :class="classification === 'software' ? 'bg-primary text-primary-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 flex items-center gap-1.5 cursor-pointer"
          >
            <HardDrive class="w-3 h-3" />
            Software
          </Button>
        </div>

        <!-- Tracking Facet -->
        <div class="bg-card p-1 rounded-lg border border-border shadow-xs flex items-center gap-1">
          <span class="text-xs text-muted-foreground font-medium px-2">Tracking:</span>
          <Button 
            variant="ghost" 
            size="sm"
            @click="tracking = 'all'" 
            :class="tracking === 'all' ? 'bg-primary text-primary-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 cursor-pointer"
          >
            All
          </Button>
          <Button 
            variant="ghost" 
            size="sm"
            @click="tracking = 'serialized'" 
            :class="tracking === 'serialized' ? 'bg-primary text-primary-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 flex items-center gap-1.5 cursor-pointer"
          >
            <Wrench class="w-3 h-3" />
            Serialized
          </Button>
          <Button 
            variant="ghost" 
            size="sm"
            @click="tracking = 'stock'" 
            :class="tracking === 'stock' ? 'bg-primary text-primary-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            class="px-2.5 py-1 rounded-md text-xs font-medium h-7 flex items-center gap-1.5 cursor-pointer"
          >
            <Boxes class="w-3 h-3" />
            Bulk Stock
          </Button>
        </div>

        <!-- Visualise Component Tree Modal Trigger -->
        <Button 
          variant="outline" 
          size="sm"
          @click="showTreeModal = true"
          class="border-border bg-card hover:bg-accent text-foreground hover:text-primary rounded-lg text-xs font-medium h-8 px-3 gap-1.5 shadow-xs cursor-pointer"
        >
          <FolderTree class="h-3.5 w-3.5 text-primary" />
          <span>Visualise Tree</span>
        </Button>

        <!-- Column Configuration Popover -->
        <Popover>
          <PopoverTrigger as-child>
            <Button variant="outline" size="sm" class="border-border bg-card text-muted-foreground hover:text-foreground hover:bg-accent rounded-lg text-xs font-medium h-8 px-3 cursor-pointer">
              <SlidersHorizontal class="h-3.5 w-3.5 mr-1.5 text-muted-foreground" />
              Columns
            </Button>
          </PopoverTrigger>
          <PopoverContent class="w-80 p-0 bg-popover border-border shadow-xl overflow-hidden" align="end">
            <div class="p-3 border-b border-border bg-muted/40">
              <h4 class="text-xs font-semibold text-foreground">Display Configuration</h4>
              <p class="text-xs text-muted-foreground mt-0.5">Toggle visible data fields</p>
            </div>
            <div class="p-2 max-h-[360px] overflow-y-auto">
              <div 
                v-for="(visible, key) in columns" 
                :key="key" 
                @click="columns[key] = !columns[key]"
                class="flex items-start gap-3 p-2 rounded-lg cursor-pointer hover:bg-muted/60 transition-colors border border-transparent hover:border-border mb-1"
                :class="{'bg-primary/10 border-primary/20': columns[key]}"
              >
                <div class="mt-0.5">
                  <div 
                    class="size-4 rounded border flex items-center justify-center transition-colors" 
                    :class="columns[key] ? 'bg-primary border-primary text-primary-foreground' : 'border-muted-foreground/40 bg-background'"
                  >
                    <Check v-if="columns[key]" class="size-3 text-primary-foreground" />
                  </div>
                </div>
                <div class="flex flex-col">
                  <span class="text-xs font-semibold text-foreground capitalize">{{ key }}</span>
                  <span class="text-xs text-muted-foreground leading-relaxed mt-0.5">
                    {{ getColumnDescription(key) }}
                  </span>
                </div>
              </div>
            </div>
            <div class="p-2.5 bg-muted/30 border-t border-border flex justify-end">
              <Button variant="ghost" size="sm" @click="resetColumns" class="h-7 text-xs font-medium text-muted-foreground hover:text-foreground cursor-pointer">
                Reset Defaults
              </Button>
            </div>
          </PopoverContent>
        </Popover>

        <!-- Provision Asset Trigger -->
        <Button 
          size="sm"
          @click="showAddModal = true" 
          class="bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg px-3.5 h-8 shadow-xs transition-all group border-0 cursor-pointer"
        >
          <PlusIcon class="h-3.5 w-3.5 mr-1.5 group-hover:rotate-90 transition-transform" />
          <span class="text-xs font-medium">Provision Asset</span>
        </Button>
      </div>
    </div>

    <!-- HeimdallSearch Bar -->
    <div class="max-w-4xl mx-auto w-full">
      <HeimdallSearchBar 
        :config="inventorySearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <!-- Repository Content Table -->
    <DashboardInventoryTable 
      :items="paginatedItems" 
      :type="activeTab" 
      :classification="classification"
      :tracking="tracking"
      :loading="loading"
      :columns="columns"
      @edit="handleEditItem"
    />

    <!-- Pagination Controls Bar -->
    <div v-if="items.length > 0" class="flex flex-col sm:flex-row items-center justify-between gap-4 p-3.5 rounded-xl bg-card border border-border text-xs">
      <div class="flex items-center gap-2 text-muted-foreground font-medium text-xs">
        <span>
          Showing 
          <span class="font-mono text-foreground font-semibold">{{ Math.min((currentPage - 1) * effectivePageSize + 1, items.length) }}</span> 
          to 
          <span class="font-mono text-foreground font-semibold">{{ Math.min(currentPage * effectivePageSize, items.length) }}</span> 
          of 
          <span class="font-mono text-foreground font-semibold">{{ items.length }}</span> 
          assets
        </span>
      </div>

      <div class="flex flex-wrap items-center gap-4">
        <!-- Page Size Selector -->
        <div class="flex items-center gap-2">
          <span class="text-xs text-muted-foreground font-medium">Per Page:</span>
          <div class="flex p-0.5 bg-muted/60 rounded-lg border border-border gap-1">
            <Button 
              v-for="size in [5, 10, 50, 100, 1000]" 
              :key="size"
              variant="ghost" 
              size="sm"
              @click="pageSize = size"
              :class="pageSize === size ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
              class="h-7 px-2 rounded-md text-xs font-medium font-mono cursor-pointer"
            >
              {{ size }}
            </Button>
            <Button 
              variant="ghost" 
              size="sm"
              @click="pageSize = 'custom'"
              :class="pageSize === 'custom' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
              class="h-7 px-2 rounded-md text-xs font-medium cursor-pointer"
            >
              Custom
            </Button>
          </div>

          <div v-if="pageSize === 'custom'" class="flex items-center gap-1">
            <input 
              v-model.number="customPageSize"
              type="number"
              min="1"
              max="10000"
              placeholder="Count"
              class="w-20 h-7 px-2 bg-background border border-border rounded-md text-xs font-mono text-foreground focus:outline-hidden focus:border-primary"
            />
          </div>
        </div>

        <!-- Navigation Controls -->
        <div class="flex items-center gap-1.5">
          <Button 
            variant="outline" 
            size="sm" 
            :disabled="currentPage === 1" 
            @click="setPage(1)"
            class="h-7 px-2.5 border-border bg-card text-muted-foreground hover:text-foreground hover:bg-accent text-xs font-medium disabled:opacity-30 rounded-md cursor-pointer"
          >
            First
          </Button>
          <Button 
            variant="outline" 
            size="sm" 
            :disabled="currentPage === 1" 
            @click="setPage(currentPage - 1)"
            class="h-7 px-2.5 border-border bg-card text-muted-foreground hover:text-foreground hover:bg-accent text-xs font-medium disabled:opacity-30 rounded-md cursor-pointer"
          >
            Prev
          </Button>

          <span class="text-xs font-medium text-muted-foreground px-2 font-mono">
            {{ currentPage }} / {{ totalPages }}
          </span>

          <Button 
            variant="outline" 
            size="sm" 
            :disabled="currentPage >= totalPages" 
            @click="setPage(currentPage + 1)"
            class="h-7 px-2.5 border-border bg-card text-muted-foreground hover:text-foreground hover:bg-accent text-xs font-medium disabled:opacity-30 rounded-md cursor-pointer"
          >
            Next
          </Button>
          <Button 
            variant="outline" 
            size="sm" 
            :disabled="currentPage >= totalPages" 
            @click="setPage(totalPages)"
            class="h-7 px-2.5 border-border bg-card text-muted-foreground hover:text-foreground hover:bg-accent text-xs font-medium disabled:opacity-30 rounded-md cursor-pointer"
          >
            Last
          </Button>
        </div>
      </div>
    </div>
    </div>

    <!-- 2. Parts Inventory Section -->
    <div v-else-if="activeSection === 'parts'" class="space-y-4">
      <PartsInventoryList
        :parts="partsList"
        :loading="partsLoading"
        :kpis="partsKpis"
        @usePart="openProvisioningForPart"
        @logPart="showLogPartModal = true"
        @bulkIntake="showBulkIntakeModal = true"
        @scanCode="showCodeScannerModal = true"
        @viewAudit="showAuditDrawer = true"
        @filterChange="fetchParts"
      />
    </div>

    <!-- 2b. Dedicated Part Provisioning Station Section -->
    <div v-else-if="activeSection === 'provisioning'" class="space-y-4">
      <PartProvisioningStation
        :parts="partsList"
        :preselected-part-id="provisioningPreselectedId"
        @scan-requested="showCodeScannerModal = true"
        @part-provisioned="onPartProvisioned"
      />
    </div>

    <!-- 3. Asset Templates Section -->
    <div v-else-if="activeSection === 'templates'" class="space-y-4">
      <AssetTemplatesManager
        :templates="templatesList"
        @refresh="() => { fetchTemplates(); fetchParts(); }"
      />
    </div>

    <!-- 4. Machine Spare Parts & Policies Section -->
    <div v-else-if="activeSection === 'spare_parts'" class="space-y-4">
      <SparePartsPolicyManager
        :spareParts="sparePartsList"
        :reporting="sparePartsReport"
        @refresh="fetchSpareParts"
      />
    </div>

    <!-- 5. Audit History Ledger Section -->
    <div v-else-if="activeSection === 'audit'" class="space-y-4">
      <div class="p-4 rounded-xl bg-card border border-border shadow-xs">
        <h3 class="text-sm font-bold text-foreground flex items-center gap-2 mb-3">
          <History class="size-4 text-primary" />
          <span>Global Parts Consumption & Intake Ledger</span>
        </h3>
        <div class="space-y-2.5 max-h-[600px] overflow-y-auto custom-scrollbar">
          <div
            v-for="log in auditLogs"
            :key="log.id"
            class="p-3 rounded-lg border border-border bg-muted/20 text-xs space-y-1.5"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="font-mono font-bold text-primary">{{ log.partIdentifier }}</span>
                <span class="font-semibold text-foreground">{{ log.partName }}</span>
                <span class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase bg-primary/10 text-primary">
                  {{ log.action.replace('_', ' ') }}
                </span>
              </div>
              <span class="font-mono text-muted-foreground text-[11px]">{{ log.timestamp }}</span>
            </div>
            <div v-if="log.costCenter" class="p-2 rounded bg-amber-500/5 border border-amber-500/20 grid grid-cols-1 sm:grid-cols-3 gap-1 text-[11px] font-mono text-muted-foreground">
              <div><strong class="text-foreground">Line:</strong> {{ log.costCenter.prodLine }}</div>
              <div><strong class="text-foreground">Project:</strong> {{ log.costCenter.project }}</div>
              <div><strong class="text-foreground">Dept:</strong> {{ log.costCenter.department }}</div>
            </div>
            <div v-if="log.notes" class="text-[11px] text-muted-foreground italic">
              "{{ log.notes }}" - {{ log.actorName }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- On-Demand Station Component Tree Visualizer Modal -->
    <DashboardInventoryStationComponentTreeModal
      :open="showTreeModal"
      @update:open="showTreeModal = $event"
    />

    <!-- Add Asset Modal Overlay -->
    <DashboardInventoryAddModal 
      :open="showAddModal"
      @update:open="showAddModal = $event"
      :type="activeTab === 'software' ? 'software' : 'hardware'" 
      @save="addComponent(activeTab, $event)"
    />

    <!-- Edit Asset Modal Overlay -->
    <DashboardInventoryEditModal
      :open="showEditModal"
      :item="selectedEditItem"
      @update:open="showEditModal = $event"
      @save="handleSaveEdit"
    />

    <!-- Use Part Modal Overlay (Mandatory Cost Center) -->
    <UsePartModal
      :open="showUsePartModal"
      :part="selectedPartForUse"
      @update:open="showUsePartModal = $event"
      @used="onConfirmUsePart"
    />

    <!-- Log Part Modal Overlay -->
    <LogPartModal
      :open="showLogPartModal"
      :templates="templatesList"
      @update:open="showLogPartModal = $event"
      @logged="onConfirmLogPart"
    />

    <!-- Bulk Part Intake Modal Overlay (Visual & JSON) -->
    <BulkPartIntakeModal
      :open="showBulkIntakeModal"
      @update:open="showBulkIntakeModal = $event"
      @bulkLogged="onConfirmBulkLogged"
    />

    <!-- AI Machine Document Import Modal Overlay -->
    <MachineDocumentImportModal
      :open="showMachineImportModal"
      @update:open="showMachineImportModal = $event"
      @imported="onMachineImportSuccess"
    />

    <!-- Code Scanner Modal Overlay (QR, Barcode, RFID) -->
    <InventoryCodeScannerModal
      :open="showCodeScannerModal"
      @update:open="showCodeScannerModal = $event"
      @usePart="onScannerUsePart"
      @logPartWithCode="onScannerLogWithCode"
    />

    <!-- Part Audit Log Drawer -->
    <PartAuditLogDrawer
      :open="showAuditDrawer"
      :auditLogs="auditLogs"
      @update:open="showAuditDrawer = $event"
    />

    <!-- Global Create / Edit Template Modal -->
    <CreateEditTemplateModal
      :open="showCreateTemplateModal"
      :available-templates="templatesList"
      @update:open="showCreateTemplateModal = $event"
      @saved="() => { fetchTemplates(); fetchParts(); }"
    />
  </div>
</template>
