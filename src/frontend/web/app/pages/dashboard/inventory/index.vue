<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  Boxes,
  Cpu,
  HardDrive,
  PackagePlus,
  Layers,
  Bot,
  RefreshCw,
  Search,
  LayoutGrid,
  List,
  FolderTree,
  DollarSign,
  ShieldAlert,
  Send,
  History,
  CheckCircle2,
  AlertTriangle,
  SlidersHorizontal,
  Plus,
  ArrowRight,
  Filter,
  Activity,
  Network
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import InventoryTable from '~/components/dashboard/InventoryTable.vue'
import InventoryCardGrid from '~/components/inventory/InventoryCardGrid.vue'
import StationPhysicalView from '~/components/inventory/StationPhysicalView.vue'
import AssetDetailInspector from '~/components/inventory/AssetDetailInspector.vue'
import AssetIntakeDrawer from '~/components/inventory/AssetIntakeDrawer.vue'
import AssetTemplatesManager from '~/components/inventory/AssetTemplatesManager.vue'
import PartProvisioningStation from '~/components/inventory/PartProvisioningStation.vue'
import SparePartsPolicyManager from '~/components/inventory/SparePartsPolicyManager.vue'
import { usePartsInventory } from '~/composables/usePartsInventory'
import { useInventoryLive } from '~/composables/useInventoryLive'
import type { InventoryPart } from '~/types/inventory'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const {
  parts,
  spareParts,
  templates,
  sparePartsReport,
  kpis,
  activeCurrency,
  formatCurrency,
  fetchParts,
  logPart,
  usePart,
  editPart,
  bulkLogParts,
  fetchTemplates,
  fetchSpareParts,
  fetchAuditLogs,
  auditLogs
} = usePartsInventory()

const currencies = ['EUR', 'USD', 'HUF'] as const
const setCurrency = (curr: 'EUR' | 'USD' | 'HUF') => {
  activeCurrency.value = curr
}
const exchangeRate = computed(() => (activeCurrency.value === 'HUF' ? 400 : activeCurrency.value === 'USD' ? 1.08 : 1.0))

const { isLiveConnected, onInventoryUpdate } = useInventoryLive()

// Section Switcher
type InventorySection = 'registry' | 'templates' | 'provisioning' | 'policy'
const activeSection = ref<InventorySection>('registry')

// Layout Mode inside Registry: Table, Card Grid, or Physical Station Schematic
const layoutMode = ref<'table' | 'grid' | 'schematic'>('table')

// Master-Detail Inspector State (Zero Modal Pop-ups)
const selectedPart = ref<InventoryPart | null>(null)
const inspectorActiveTab = ref<'overview' | 'dispatch' | 'edit' | 'audit'>('overview')

// Unified Intake Workbench Drawer State (Replaces scattered modals)
const showIntakeDrawer = ref(false)

// SolarWinds-style Rollup Status Filters
type RollupFilter = 'all' | 'optimal' | 'low' | 'depleted' | 'serialized' | 'bulk'
const activeRollup = ref<RollupFilter>('all')

// Search & Classification Filters
const classification = ref<'all' | 'hardware' | 'software'>('all')
const searchQuery = ref('')
const loading = ref(false)

// Row/Card selection handler
const handleSelectPart = (part: any) => {
  selectedPart.value = part
  inspectorActiveTab.value = 'overview'
}

// Schematic node click handler
const handleSchematicComponentSelect = (node: any) => {
  const found = parts.value.find(p => p.customIdentifier === node.code || p.id === node.installedPartId)
  if (found) {
    selectedPart.value = found
  } else {
    // Focused virtual node object
    selectedPart.value = {
      id: node.installedPartId || node.id,
      customIdentifier: node.code,
      name: node.name,
      displayName: node.name,
      category: node.subsystem,
      trackingType: 'serialized',
      condition: node.status === 'normal' ? 'new' : node.status === 'warning' ? 'used' : 'donor',
      operationalState: node.status === 'normal' ? 'working' : 'degraded',
      location: 'Station OP-10 Cell',
      priceEur: 850,
      estimatedResellPriceEur: 850,
      quantity: node.spareCount,
      minQuantity: 1,
      manufacturer: { name: 'Cell OEM' },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    } as any
  }
  inspectorActiveTab.value = 'overview'
}

// Inline action dispatchers (delegated to Master-Detail Inspector)
const handleRowDispatch = (part: any) => {
  selectedPart.value = part
  inspectorActiveTab.value = 'dispatch'
}

const handleRowEdit = (part: any) => {
  selectedPart.value = part
  inspectorActiveTab.value = 'edit'
}

const handleRowAudit = (part: any) => {
  selectedPart.value = part
  inspectorActiveTab.value = 'audit'
}

// Action handlers from inspector & drawer
const handleExecuteUsePart = async (data: any) => {
  await usePart(data.partId, data.costCenter, data.quantity, data)
  await fetchParts()
  // Keep part updated in inspector
  const updated = parts.value.find(p => p.id === data.partId)
  if (updated) selectedPart.value = updated
}

const handleExecuteEditPart = async (updatedPart: any) => {
  await editPart(updatedPart.id, updatedPart)
  await fetchParts()
  const updated = parts.value.find(p => p.id === updatedPart.id)
  if (updated) selectedPart.value = updated
}

const handleSinglePartLogged = async (newPart: any) => {
  await logPart(newPart)
  showIntakeDrawer.value = false
  await fetchParts()
  selectedPart.value = newPart
}

const handleBatchPartsLogged = async (batchParts: any[]) => {
  await bulkLogParts(batchParts)
  showIntakeDrawer.value = false
  await fetchParts()
}

// Rollup Filter Counts
const rollupCounts = computed(() => {
  const all = parts.value || []
  return {
    all: all.length,
    optimal: all.filter(p => (p.quantity || 1) > (p.minQuantity || 1)).length,
    low: all.filter(p => (p.quantity || 1) > 0 && (p.quantity || 1) <= (p.minQuantity || 1)).length,
    depleted: all.filter(p => (p.quantity || 0) <= 0).length,
    serialized: all.filter(p => p.trackingType === 'serialized' || !p.isStockItem).length,
    bulk: all.filter(p => p.trackingType === 'bulk' || p.isStockItem).length
  }
})

// Filtered Inventory Items
const filteredItems = computed(() => {
  let list = parts.value || []

  // 1. Rollup Status
  if (activeRollup.value === 'optimal') {
    list = list.filter(p => (p.quantity || 1) > (p.minQuantity || 1))
  } else if (activeRollup.value === 'low') {
    list = list.filter(p => (p.quantity || 1) > 0 && (p.quantity || 1) <= (p.minQuantity || 1))
  } else if (activeRollup.value === 'depleted') {
    list = list.filter(p => (p.quantity || 0) <= 0)
  } else if (activeRollup.value === 'serialized') {
    list = list.filter(p => p.trackingType === 'serialized' || !p.isStockItem)
  } else if (activeRollup.value === 'bulk') {
    list = list.filter(p => p.trackingType === 'bulk' || p.isStockItem)
  }

  // 2. Classification
  if (classification.value === 'hardware') {
    list = list.filter(p => (p.category || p.itemType || '').toLowerCase() !== 'software')
  } else if (classification.value === 'software') {
    list = list.filter(p => (p.category || p.itemType || '').toLowerCase() === 'software')
  }

  // 3. Search Query
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.trim().toLowerCase()
    list = list.filter(p =>
      (p.name && p.name.toLowerCase().includes(q)) ||
      (p.displayName && p.displayName.toLowerCase().includes(q)) ||
      (p.customIdentifier && p.customIdentifier.toLowerCase().includes(q)) ||
      (p.serialNumber && p.serialNumber.toLowerCase().includes(q)) ||
      (p.location && p.location.toLowerCase().includes(q)) ||
      (p.manufacturer && String(p.manufacturer?.name || p.manufacturer).toLowerCase().includes(q))
    )
  }

  return list
})

onMounted(async () => {
  loading.value = true
  try {
    await Promise.all([
      fetchParts(),
      fetchTemplates(),
      fetchSpareParts()
    ])
  } finally {
    loading.value = false
  }
})

watch(activeSection, async (sec) => {
  if (sec === 'templates') {
    await fetchTemplates()
  } else if (sec === 'policy') {
    await fetchSpareParts()
  } else if (sec === 'registry') {
    await fetchParts()
  }
})

onInventoryUpdate(() => {
  fetchParts()
})
</script>

<template>
  <div class="space-y-5 pb-12">
    <!-- Top Header & Consolidated Industrial Action Bar (No Random Buttons!) -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-2">
          <Boxes class="w-6 h-6 text-primary" />
          <span>Industrial Inventory & Node Assets</span>
        </h1>
        <p class="text-xs text-muted-foreground mt-0.5">
          Master equipment registry, kinematic components, hardware BOM tracking, and live crib stock
        </p>
      </div>

      <div class="flex items-center gap-2.5">
        <!-- Currency Selector -->
        <div class="flex items-center p-0.5 bg-muted/40 border border-border rounded-lg">
          <button
            v-for="curr in currencies"
            :key="curr"
            type="button"
            @click="setCurrency(curr)"
            class="px-2 py-0.5 text-xs font-mono font-bold rounded-md transition-colors cursor-pointer"
            :class="activeCurrency === curr ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            {{ curr }}
          </button>
        </div>

        <!-- Ingest & Intake Asset (Full Page or In-Page Workbench Drawer) -->
        <NuxtLink to="/dashboard/inventory/new">
          <Button
            size="sm"
            class="h-8 text-xs gap-1.5 bg-primary text-primary-foreground hover:bg-primary/90 font-bold cursor-pointer shadow-xs"
          >
            <Plus class="w-3.5 h-3.5" />
            <span>Ingest & Intake Asset</span>
          </Button>
        </NuxtLink>

        <Button
          size="sm"
          variant="outline"
          @click="showIntakeDrawer = true"
          class="h-8 text-xs gap-1.5 cursor-pointer border-border"
          title="Open in-page quick intake drawer"
        >
          <SlidersHorizontal class="w-3.5 h-3.5 text-primary" />
          <span class="hidden sm:inline">Quick Drawer</span>
        </Button>
      </div>
    </div>

    <!-- SolarWinds-style Rollup Status Filter Bar (Immediate Filtering, Zero Overlays) -->
    <div class="flex flex-wrap items-center gap-2 p-1.5 bg-muted/20 border border-border rounded-xl">
      <button
        type="button"
        @click="activeRollup = 'all'"
        class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
        :class="activeRollup === 'all' ? 'bg-background text-foreground shadow-xs font-bold border border-border' : 'text-muted-foreground hover:text-foreground'"
      >
        <span>All Units</span>
        <Badge variant="outline" class="text-[10px] px-1 py-0 font-mono">{{ rollupCounts.all }}</Badge>
      </button>

      <button
        type="button"
        @click="activeRollup = 'optimal'"
        class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
        :class="activeRollup === 'optimal' ? 'bg-emerald-950/40 text-emerald-400 border border-emerald-500/40 shadow-xs font-bold' : 'text-muted-foreground hover:text-emerald-400'"
      >
        <CheckCircle2 class="w-3.5 h-3.5 text-emerald-400" />
        <span>Optimal Stock</span>
        <Badge variant="outline" class="text-[10px] px-1 py-0 font-mono border-emerald-500/30 text-emerald-400">{{ rollupCounts.optimal }}</Badge>
      </button>

      <button
        type="button"
        @click="activeRollup = 'low'"
        class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
        :class="activeRollup === 'low' ? 'bg-amber-950/40 text-amber-400 border border-amber-500/40 shadow-xs font-bold' : 'text-muted-foreground hover:text-amber-400'"
      >
        <AlertTriangle class="w-3.5 h-3.5 text-amber-400" />
        <span>Low Stock Alert</span>
        <Badge variant="outline" class="text-[10px] px-1 py-0 font-mono border-amber-500/30 text-amber-400">{{ rollupCounts.low }}</Badge>
      </button>

      <button
        type="button"
        @click="activeRollup = 'depleted'"
        class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
        :class="activeRollup === 'depleted' ? 'bg-rose-950/40 text-rose-400 border border-rose-500/40 shadow-xs font-bold' : 'text-muted-foreground hover:text-rose-400'"
      >
        <ShieldAlert class="w-3.5 h-3.5 text-rose-400" />
        <span>Critical Depleted</span>
        <Badge variant="outline" class="text-[10px] px-1 py-0 font-mono border-rose-500/30 text-rose-400">{{ rollupCounts.depleted }}</Badge>
      </button>

      <div class="h-4 w-px bg-border mx-1" />

      <button
        type="button"
        @click="activeRollup = 'serialized'"
        class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
        :class="activeRollup === 'serialized' ? 'bg-primary/20 text-primary border border-primary/40 shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
      >
        <Cpu class="w-3.5 h-3.5 text-primary" />
        <span>Serialized Equipment</span>
        <Badge variant="outline" class="text-[10px] px-1 py-0 font-mono">{{ rollupCounts.serialized }}</Badge>
      </button>

      <button
        type="button"
        @click="activeRollup = 'bulk'"
        class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
        :class="activeRollup === 'bulk' ? 'bg-cyan-950/40 text-cyan-400 border border-cyan-500/40 shadow-xs font-bold' : 'text-muted-foreground hover:text-cyan-400'"
      >
        <Layers class="w-3.5 h-3.5 text-cyan-400" />
        <span>Bulk Consumables</span>
        <Badge variant="outline" class="text-[10px] px-1 py-0 font-mono">{{ rollupCounts.bulk }}</Badge>
      </button>
    </div>

    <!-- Section Navigation Tabs -->
    <div class="flex items-center gap-1 border-b border-border pb-1 overflow-x-auto">
      <button
        type="button"
        @click="activeSection = 'registry'"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeSection === 'registry' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Boxes class="w-3.5 h-3.5" />
        <span>Parts & Asset Registry</span>
        <Badge variant="outline" class="text-[9px] px-1 py-0 font-mono">{{ filteredItems.length }}</Badge>
      </button>

      <button
        type="button"
        @click="activeSection = 'templates'"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeSection === 'templates' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Layers class="w-3.5 h-3.5" />
        <span>Blueprint Templates</span>
        <Badge variant="outline" class="text-[9px] px-1 py-0 font-mono">{{ templates?.length || 0 }}</Badge>
      </button>

      <button
        type="button"
        @click="activeSection = 'provisioning'"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeSection === 'provisioning' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Send class="w-3.5 h-3.5" />
        <span>Provisioning Station</span>
      </button>

      <button
        type="button"
        @click="activeSection = 'policy'"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeSection === 'policy' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <ShieldAlert class="w-3.5 h-3.5" />
        <span>Spares Policy & SLA</span>
      </button>
    </div>

    <!-- Section 1: Master-Detail Workstation Layout (Zero Pop-ups, 100% Context) -->
    <div v-if="activeSection === 'registry'" class="space-y-4">
      <!-- Search & Layout Controls Bar -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3 bg-muted/20 border border-border rounded-xl">
        <!-- Search Input -->
        <div class="relative flex-1 max-w-sm">
          <Search class="absolute left-3 top-2.5 w-3.5 h-3.5 text-muted-foreground" />
          <Input
            v-model="searchQuery"
            placeholder="Search part ID, serial number, storage crib location..."
            class="pl-9 text-xs h-8 bg-card border-border shadow-xs"
          />
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <!-- Classification Pill Filter -->
          <div class="flex items-center p-0.5 bg-muted/40 border border-border rounded-lg text-xs">
            <button
              type="button"
              @click="classification = 'all'"
              class="px-2.5 py-1 rounded-md transition-colors cursor-pointer"
              :class="classification === 'all' ? 'bg-background text-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            >
              All Classes
            </button>
            <button
              type="button"
              @click="classification = 'hardware'"
              class="px-2.5 py-1 rounded-md transition-colors cursor-pointer"
              :class="classification === 'hardware' ? 'bg-background text-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            >
              Hardware
            </button>
            <button
              type="button"
              @click="classification = 'software'"
              class="px-2.5 py-1 rounded-md transition-colors cursor-pointer"
              :class="classification === 'software' ? 'bg-background text-foreground shadow-xs font-medium' : 'text-muted-foreground hover:text-foreground'"
            >
              Software
            </button>
          </div>

          <!-- Layout Switcher: Table vs Cards vs Schematic -->
          <div class="flex items-center p-0.5 bg-muted/40 border border-border rounded-lg">
            <button
              type="button"
              @click="layoutMode = 'table'"
              class="p-1.5 rounded-md transition-colors cursor-pointer"
              :class="layoutMode === 'table' ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
              title="Dense Table View"
            >
              <List class="w-3.5 h-3.5" />
            </button>
            <button
              type="button"
              @click="layoutMode = 'grid'"
              class="p-1.5 rounded-md transition-colors cursor-pointer"
              :class="layoutMode === 'grid' ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
              title="Card Grid View"
            >
              <LayoutGrid class="w-3.5 h-3.5" />
            </button>
            <button
              type="button"
              @click="layoutMode = 'schematic'"
              class="p-1.5 rounded-md transition-colors cursor-pointer"
              :class="layoutMode === 'schematic' ? 'bg-background text-foreground shadow-xs text-primary' : 'text-muted-foreground hover:text-foreground'"
              title="Physical Cell Schematic"
            >
              <Network class="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      </div>

      <!-- MASTER-DETAIL DUAL-PANE WORKSTATION CONTAINER -->
      <div class="grid grid-cols-12 gap-5 items-start">
        <!-- Master Pane: Schematic, Data Table, or Cards -->
        <div
          class="transition-all duration-200"
          :class="selectedPart ? 'col-span-12 lg:col-span-7 xl:col-span-8' : 'col-span-12'"
        >
          <!-- 1. Physical Cell Schematic Mode -->
          <StationPhysicalView
            v-if="layoutMode === 'schematic'"
            :active-component-id="selectedPart?.customIdentifier"
            @select-component="handleSchematicComponentSelect"
          />

          <!-- 2. Dense Table View Mode -->
          <InventoryTable
            v-else-if="layoutMode === 'table'"
            :items="filteredItems"
            :loading="loading"
            :active-currency="activeCurrency"
            :exchange-rate="exchangeRate"
            :selected-id="selectedPart?.id"
            @select="handleSelectPart"
            @use="handleRowDispatch"
            @edit="handleRowEdit"
            @audit="handleRowAudit"
          />

          <!-- 3. Card Grid View Mode -->
          <InventoryCardGrid
            v-else
            :items="filteredItems"
            :loading="loading"
            :active-currency="activeCurrency"
            :exchange-rate="exchangeRate"
            :selected-id="selectedPart?.id"
            @select-item="handleSelectPart"
            @use-item="handleRowDispatch"
            @edit-item="handleRowEdit"
            @audit-item="handleRowAudit"
          />
        </div>

        <!-- Detail Inspector Pane: SolarWinds-style Contextual Inspector (Zero Pop-ups!) -->
        <div
          v-if="selectedPart"
          class="col-span-12 lg:col-span-5 xl:col-span-4 sticky top-4 border border-border rounded-2xl overflow-hidden shadow-xl bg-card"
        >
          <AssetDetailInspector
            :part="selectedPart"
            :currency="activeCurrency"
            :exchange-rate="exchangeRate"
            @close="selectedPart = null"
            @use-part="handleExecuteUsePart"
            @edit-part="handleExecuteEditPart"
          />
        </div>
      </div>
    </div>

    <!-- Section 2: Blueprint Templates -->
    <div v-else-if="activeSection === 'templates'">
      <AssetTemplatesManager
        :templates="templates"
        @refresh="fetchTemplates"
      />
    </div>

    <!-- Section 3: Provisioning Station -->
    <div v-else-if="activeSection === 'provisioning'">
      <PartProvisioningStation
        :parts="parts"
        @scan-requested="showIntakeDrawer = true"
      />
    </div>

    <!-- Section 4: Spares Policy & SLA -->
    <div v-else-if="activeSection === 'policy'">
      <SparePartsPolicyManager
        :spare-parts="spareParts"
        :reporting="sparePartsReport"
        @refresh="fetchSpareParts"
      />
    </div>

    <!-- ==================== UNIFIED INTAKE WORKBENCH (SLIDE-IN DRAWER, ZERO OVERLAYS) ==================== -->
    <AssetIntakeDrawer
      :open="showIntakeDrawer"
      :templates="templates"
      @close="showIntakeDrawer = false"
      @part-logged="handleSinglePartLogged"
      @bulk-logged="handleBatchPartsLogged"
    />
  </div>
</template>
