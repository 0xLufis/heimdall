<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { PlusIcon, SlidersHorizontal, Check, RefreshCw, Layers, HardDrive, Cpu, DollarSign, Activity, Wifi } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import OmniSearchBar from '~/components/search/OmniSearchBar.vue'
import DashboardInventoryEditModal from '~/components/dashboard/InventoryEditModal.vue'
import { useInventoryLive } from '~/composables/useInventoryLive'
import type { SearchInstanceConfig } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { isLiveConnected, lastSyncedAt, onInventoryUpdate } = useInventoryLive()

const activeTab = ref<'hardware' | 'software' | 'hierarchy'>('hardware')
const hierarchyKey = ref<'machine' | 'client'>('machine')
const loading = ref(false)
const items = ref<any[]>([])
const currentQuery = ref('')
const kpis = ref({
  totalGlobalCount: 0,
  totalGlobalHardware: 0,
  totalGlobalSoftware: 0,
  totalGlobalCost: 0
})

const showAddModal = ref(false)
const showEditModal = ref(false)
const selectedEditItem = ref<any | null>(null)

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

watch([pageSize, customPageSize, activeTab], () => {
  currentPage.value = 1
})

const inventorySearchConfig = computed<SearchInstanceConfig>(() => ({
  instanceId: 'inventory',
  placeholder: `Search ${activeTab.value} by name, model, serial, spec (e.g. manufacturer:Siemens)...`,
  defaultEndpoints: ['/api/proxy/inventory/search'],
  defaultTags: activeTab.value !== 'hierarchy' ? [{ key: 'type', value: activeTab.value }] : [],
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
  // Prevent single-character queries from triggering lookup
  const hasTags = q.includes(':')
  const freeText = q.replace(/(\w+):"[^"]*"|(\w+):\S+/g, '').trim()
  if (!hasTags && freeText.length === 1) {
    return
  }
  currentQuery.value = q
  fetchData(q)
}

const fetchData = async (q: string = currentQuery.value) => {
  if (activeTab.value === 'hierarchy') return
  loading.value = true
  try {
    const res = await $fetch<any>('/api/inventory/filter', {
      method: 'GET',
      params: {
        query: q,
        type: activeTab.value
      }
    })
    if (res) {
      items.value = res.items || []
      if (res.kpis) {
        kpis.value = res.kpis
      }
    }
  } catch (e) {
    console.error('Error fetching inventory:', e)
  } finally {
    loading.value = false
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
    await fetchData()
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
    await fetchData()
  }
}

watch(activeTab, () => {
  if (activeTab.value !== 'hierarchy') {
    fetchData()
  }
})

onMounted(() => {
  fetchData('')
})

onInventoryUpdate(() => {
  if (activeTab.value !== 'hierarchy') {
    fetchData(currentQuery.value)
  }
})
</script>

<template>
  <div class="space-y-8 animate-in fade-in duration-300">
    <!-- Header Area with KPI Badges & Controls -->
    <div class="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-2 border-b border-slate-900">
      <div>
        <div class="flex items-center gap-3">
          <h1 class="text-2xl font-black text-slate-100 tracking-tight uppercase">
            Inventory & Asset Infrastructure
          </h1>
        </div>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">
          Hardware components, software licenses, and graph-linked station hierarchies
        </p>

        <!-- KPI Metric Badges -->
        <div class="flex flex-wrap items-center gap-3 mt-4">
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <Layers class="w-3.5 h-3.5 text-indigo-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Total:</span>
            <span class="font-mono font-black text-slate-200">{{ kpis.totalGlobalCount || items.length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <Cpu class="w-3.5 h-3.5 text-emerald-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Hardware:</span>
            <span class="font-mono font-black text-slate-200">{{ kpis.totalGlobalHardware || items.filter(i => i.itemType !== 'software').length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <HardDrive class="w-3.5 h-3.5 text-blue-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Software:</span>
            <span class="font-mono font-black text-slate-200">{{ kpis.totalGlobalSoftware || items.filter(i => i.itemType === 'software').length }}</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <DollarSign class="w-3.5 h-3.5 text-amber-400" />
            <span class="text-[10px] font-bold text-slate-500 uppercase">Valuation:</span>
            <span class="font-mono font-black text-slate-200">{{ formatCurrency(kpis.totalGlobalCost) }} HUF</span>
          </div>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
            <span class="size-2 rounded-full" :class="isLiveConnected ? 'bg-emerald-400 animate-pulse' : 'bg-emerald-500'" />
            <span class="text-[10px] font-bold uppercase tracking-widest" :class="isLiveConnected ? 'text-emerald-400' : 'text-emerald-500'">
              {{ isLiveConnected ? 'Live Telemetry Sync: Connected' : 'Live Sync: Active' }}
            </span>
          </div>
        </div>
      </div>
      
      <!-- Primary View Switcher & Action Button -->
      <div class="flex flex-wrap items-center gap-3 shrink-0">
        <!-- View Mode Switcher -->
        <div class="bg-slate-900 p-1 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button 
            variant="ghost" 
            @click="activeTab = 'hardware'" 
            :class="activeTab === 'hardware' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-9"
          >
            Hardware
          </Button>
          <Button 
            variant="ghost" 
            @click="activeTab = 'software'" 
            :class="activeTab === 'software' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-9"
          >
            Software
          </Button>
          <Button 
            variant="ghost" 
            @click="activeTab = 'hierarchy'" 
            :class="activeTab === 'hierarchy' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-9"
          >
            Hierarchy
          </Button>
        </div>

        <!-- Column Configuration Popover -->
        <Popover v-if="activeTab !== 'hierarchy'">
          <PopoverTrigger as-child>
            <Button variant="outline" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl text-[10px] font-black uppercase tracking-widest h-11 px-4">
              <SlidersHorizontal class="h-4 w-4 mr-2 text-slate-400" />
              Columns
            </Button>
          </PopoverTrigger>
          <PopoverContent class="w-80 p-0 bg-slate-950 border-slate-800 shadow-2xl overflow-hidden" align="end">
            <div class="p-4 border-b border-slate-900 bg-slate-900/50">
              <h4 class="text-[10px] font-black text-slate-200 uppercase tracking-widest">Display Configuration</h4>
              <p class="text-[9px] text-slate-500 uppercase mt-1">Toggle visible data fields</p>
            </div>
            <div class="p-2 max-h-[400px] overflow-y-auto">
              <div 
                v-for="(visible, key) in columns" 
                :key="key" 
                @click="columns[key] = !columns[key]"
                class="flex items-start gap-3 p-2.5 rounded-lg cursor-pointer hover:bg-slate-900 transition-colors border border-transparent hover:border-slate-800 mb-1"
                :class="{'bg-indigo-500/5 border-indigo-500/10': columns[key]}"
              >
                <div class="mt-0.5">
                  <div 
                    class="size-4 rounded border flex items-center justify-center transition-colors" 
                    :class="columns[key] ? 'bg-indigo-600 border-indigo-600' : 'border-slate-700 bg-slate-900'"
                  >
                    <Check v-if="columns[key]" class="size-3 text-white" />
                  </div>
                </div>
                <div class="flex flex-col">
                  <span class="text-xs font-black text-slate-200 uppercase tracking-tight">{{ key }}</span>
                  <span class="text-[9px] text-slate-500 leading-relaxed mt-0.5">
                    {{ getColumnDescription(key) }}
                  </span>
                </div>
              </div>
            </div>
            <div class="p-3 bg-slate-900/30 border-t border-slate-900 flex justify-end">
              <Button variant="ghost" size="sm" @click="resetColumns" class="h-7 text-[9px] font-black uppercase text-slate-500 hover:text-slate-300">
                Reset Defaults
              </Button>
            </div>
          </PopoverContent>
        </Popover>

        <!-- Provision Asset Trigger -->
        <Button 
          v-if="activeTab !== 'hierarchy'" 
          @click="showAddModal = true" 
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-5 h-11 shadow-lg shadow-indigo-600/20 transition-all group border-0"
        >
          <PlusIcon class="h-4 w-4 mr-2 group-hover:rotate-90 transition-transform" />
          <span class="text-xs font-black uppercase tracking-widest">Provision Asset</span>
        </Button>
      </div>
    </div>

    <!-- OmniSearch Bar -->
    <div v-if="activeTab !== 'hierarchy'" class="max-w-4xl mx-auto w-full">
      <OmniSearchBar 
        :config="inventorySearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <!-- Hierarchy Structure Selector (when in Hierarchy view) -->
    <div v-if="activeTab === 'hierarchy'" class="flex items-center gap-3 animate-in fade-in slide-in-from-top-2 duration-300">
      <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Structure By:</span>
      <div class="flex p-1 bg-slate-900 rounded-xl border border-slate-800 gap-1">
        <Button 
          variant="ghost" 
          size="sm"
          @click="hierarchyKey = 'machine'"
          :class="hierarchyKey === 'machine' ? 'bg-slate-800 text-indigo-400 font-black' : 'text-slate-500'"
          class="rounded-lg text-[10px] uppercase px-3"
        >
          Station Centric
        </Button>
        <Button 
          variant="ghost" 
          size="sm"
          @click="hierarchyKey = 'client'"
          :class="hierarchyKey === 'client' ? 'bg-slate-800 text-indigo-400 font-black' : 'text-slate-500'"
          class="rounded-lg text-[10px] uppercase px-3"
        >
          Host PC Centric
        </Button>
      </div>
    </div>

    <!-- Repository Content Views -->
    <template v-if="activeTab !== 'hierarchy'">
      <DashboardInventoryTable 
        :items="paginatedItems" 
        :type="activeTab" 
        :loading="loading"
        :columns="columns"
        @edit="handleEditItem"
      />

      <!-- Pagination Controls Bar -->
      <div v-if="items.length > 0" class="flex flex-col sm:flex-row items-center justify-between gap-4 p-4 rounded-2xl bg-slate-900/60 border border-slate-800 text-xs">
        <div class="flex items-center gap-3 text-slate-400 font-bold uppercase tracking-wider text-[10px]">
          <span>
            Showing 
            <span class="font-mono text-slate-200">{{ Math.min((currentPage - 1) * effectivePageSize + 1, items.length) }}</span> 
            to 
            <span class="font-mono text-slate-200">{{ Math.min(currentPage * effectivePageSize, items.length) }}</span> 
            of 
            <span class="font-mono text-slate-200">{{ items.length }}</span> 
            assets
          </span>
        </div>

        <div class="flex flex-wrap items-center gap-4">
          <!-- Page Size Selector -->
          <div class="flex items-center gap-2">
            <span class="text-[10px] font-black uppercase text-slate-500 tracking-wider">Per Page:</span>
            <div class="flex p-0.5 bg-slate-950 rounded-xl border border-slate-800 gap-1">
              <Button 
                v-for="size in [5, 10, 50, 100, 1000]" 
                :key="size"
                variant="ghost" 
                size="sm"
                @click="pageSize = size"
                :class="pageSize === size ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
                class="h-7 px-2.5 rounded-lg text-[10px] font-black uppercase font-mono"
              >
                {{ size }}
              </Button>
              <Button 
                variant="ghost" 
                size="sm"
                @click="pageSize = 'custom'"
                :class="pageSize === 'custom' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
                class="h-7 px-2.5 rounded-lg text-[10px] font-black uppercase"
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
                class="w-20 h-7 px-2 bg-slate-950 border border-slate-800 rounded-lg text-xs font-mono text-slate-200 focus:outline-none focus:border-indigo-500"
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
              class="h-7 px-2 border-slate-800 bg-slate-950 text-slate-400 hover:text-slate-200 text-[10px] font-black uppercase disabled:opacity-30 rounded-lg"
            >
              First
            </Button>
            <Button 
              variant="outline" 
              size="sm" 
              :disabled="currentPage === 1" 
              @click="setPage(currentPage - 1)"
              class="h-7 px-2.5 border-slate-800 bg-slate-950 text-slate-400 hover:text-slate-200 text-[10px] font-black uppercase disabled:opacity-30 rounded-lg"
            >
              Prev
            </Button>

            <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400 px-2 font-mono">
              {{ currentPage }} / {{ totalPages }}
            </span>

            <Button 
              variant="outline" 
              size="sm" 
              :disabled="currentPage >= totalPages" 
              @click="setPage(currentPage + 1)"
              class="h-7 px-2.5 border-slate-800 bg-slate-950 text-slate-400 hover:text-slate-200 text-[10px] font-black uppercase disabled:opacity-30 rounded-lg"
            >
              Next
            </Button>
            <Button 
              variant="outline" 
              size="sm" 
              :disabled="currentPage >= totalPages" 
              @click="setPage(totalPages)"
              class="h-7 px-2 border-slate-800 bg-slate-950 text-slate-400 hover:text-slate-200 text-[10px] font-black uppercase disabled:opacity-30 rounded-lg"
            >
              Last
            </Button>
          </div>
        </div>
      </div>
    </template>

    <template v-else>
      <DashboardInventoryTreeTable :primary-key="hierarchyKey" />
    </template>

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
  </div>
</template>
