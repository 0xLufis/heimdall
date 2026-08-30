<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { PlusIcon, ViewIcon, Check } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import OmniSearchBar from '~/components/search/OmniSearchBar.vue'
import type { SearchInstanceConfig } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const activeTab = ref<'hardware' | 'software' | 'hierarchy'>('hardware')
const hierarchyKey = ref<'machine' | 'client'>('machine')
const loading = ref(false)
const items = ref<any[]>([])
const currentQuery = ref('')

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
    manufacturer: 'Display the brand or OEM of the asset',
    modelNumber: 'Show specific model or part numbers',
    purchaseDate: 'Lifecycle tracking and warranty start dates',
    cost: 'Financial investment in HUF currency',
    specs: 'Technical parameters like torque or resolution',
    tags: 'Custom attributes and JSONB data points'
  }
  return descs[key] || 'Generic data field'
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
  let effectiveQuery = q
  if (activeTab.value === 'hardware' && !effectiveQuery.includes('type:')) {
    effectiveQuery = `${effectiveQuery} type:hardware`.trim()
  } else if (activeTab.value === 'software' && !effectiveQuery.includes('type:')) {
    effectiveQuery = `${effectiveQuery} type:software`.trim()
  }
  
  fetchData(effectiveQuery)
}

const fetchData = async (q: string = '') => {
  if (activeTab.value === 'hierarchy') return
  loading.value = true
  try {
    const res = await $fetch<any>('/api/inventory/filter', {
      method: 'POST',
      body: {
        query: q,
        type: activeTab.value
      }
    })
    if (res && res.items) items.value = res.items
  } catch (e) {
    console.error('Error fetching inventory:', e)
  } finally {
    loading.value = false
  }
}

const addComponent = async (type: string, formData: any) => {
  try {
    await $fetch('/api/proxy/inventory', {
      method: 'POST',
      body: formData,
    })
    onSearch(currentQuery.value)
  } catch (e) {
    console.error('Error adding component:', e)
  }
}

watch(activeTab, () => {
  if (activeTab.value !== 'hierarchy') {
    onSearch('')
  }
})

const showAddModal = ref(false)

onMounted(() => {
  onSearch('')
})
</script>

<template>
  <div class="space-y-8">
    <!-- Header Area -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Inventory</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">Asset Lifecycle, Copia PLC Projects & Component Tracking</p>
      </div>
      
      <div class="flex items-center gap-4">
        <div class="bg-slate-900 p-1.5 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button 
            variant="ghost"
            @click="activeTab = 'hardware'" 
            :class="activeTab === 'hardware' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto"
          >
            Hardware
          </Button>
          <Button 
            variant="ghost"
            @click="activeTab = 'software'" 
            :class="activeTab === 'software' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto"
          >
            Software
          </Button>
          <Button 
            variant="ghost"
            @click="activeTab = 'hierarchy'" 
            :class="activeTab === 'hierarchy' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto"
          >
            Hierarchy
          </Button>
        </div>

        <Button v-if="activeTab !== 'hierarchy'" @click="showAddModal = true" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-6 py-6 h-auto shadow-xl transition-all group border-0">
          <PlusIcon class="h-5 w-5 mr-2 group-hover:rotate-90 transition-transform" />
          <span class="text-xs font-black uppercase tracking-widest">Provision Asset</span>
        </Button>
      </div>
    </div>

    <!-- OmniSearch Bar -->
    <div class="max-w-4xl mx-auto w-full">
      <OmniSearchBar 
        :config="inventorySearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <div class="flex justify-between items-center">
      <div class="flex items-center gap-3">
         <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">View Mode:</span>
         <div class="flex p-1 bg-slate-900 rounded-xl border border-slate-800 gap-1">
            <Button 
              variant="ghost" 
              size="sm"
              @click="activeTab = 'hardware'"
              :class="activeTab === 'hardware' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
              class="rounded-lg text-[9px] font-black uppercase px-3"
            >Hardware</Button>
            <Button 
              variant="ghost" 
              size="sm"
              @click="activeTab = 'software'"
              :class="activeTab === 'software' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
              class="rounded-lg text-[9px] font-black uppercase px-3"
            >Software</Button>
            <Button 
              variant="ghost" 
              size="sm"
              @click="activeTab = 'hierarchy'"
              :class="activeTab === 'hierarchy' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
              class="rounded-lg text-[9px] font-black uppercase px-3"
            >Hierarchy</Button>
         </div>
      </div>

      <div class="flex items-center gap-2">
        <!-- Column Toggler Popover -->
        <Popover>
            <PopoverTrigger as-child>
            <Button variant="outline" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl text-[10px] font-black uppercase tracking-widest h-10">
                <ViewIcon class="h-4 w-4 mr-2" />
                Columns
            </Button>
            </PopoverTrigger>
            <PopoverContent class="w-80 p-0 bg-slate-950 border-slate-800 shadow-2xl overflow-hidden" align="end">
              <div class="p-4 border-b border-slate-900 bg-slate-900/50">
                 <h4 class="text-[10px] font-black text-slate-200 uppercase tracking-widest">Display Configuration</h4>
                 <p class="text-[9px] text-slate-500 uppercase mt-1">Toggle visible data fields</p>
              </div>
              <div class="p-2 max-h-[400px] overflow-y-auto">
                 <div v-for="(visible, key) in columns" :key="key" 
                    @click="columns[key] = !columns[key]"
                    class="flex items-start gap-3 p-3 rounded-lg cursor-pointer hover:bg-slate-900 transition-colors border border-transparent hover:border-slate-800 mb-1"
                    :class="{'bg-indigo-500/5 border-indigo-500/10': columns[key]}"
                 >
                    <div class="mt-0.5">
                       <div class="size-4 rounded border flex items-center justify-center transition-colors" 
                          :class="columns[key] ? 'bg-indigo-600 border-indigo-600' : 'border-slate-700 bg-slate-900'">
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

        <Button v-if="activeTab !== 'hierarchy'" @click="showAddModal = true" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-5 h-10 shadow-lg transition-all group border-0">
          <PlusIcon class="h-4 w-4 mr-2 group-hover:rotate-90 transition-transform" />
          <span class="text-[10px] font-black uppercase tracking-widest">Add Asset</span>
        </Button>
      </div>
    </div>

    <!-- Hierarchy Structure Selector -->
    <div v-if="activeTab === 'hierarchy'" class="flex items-center gap-3 animate-in fade-in slide-in-from-top-2 duration-300">
       <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Structure By:</span>
       <div class="flex p-1 bg-slate-900 rounded-xl border border-slate-800 gap-1">
          <Button 
            variant="ghost" 
            size="sm"
            @click="hierarchyKey = 'machine'"
            :class="hierarchyKey === 'machine' ? 'bg-slate-800 text-indigo-400' : 'text-slate-600'"
            class="rounded-lg text-[9px] font-black uppercase"
          >Station</Button>
          <Button 
            variant="ghost" 
            size="sm"
            @click="hierarchyKey = 'client'"
            :class="hierarchyKey === 'client' ? 'bg-slate-800 text-indigo-400' : 'text-slate-600'"
            class="rounded-lg text-[9px] font-black uppercase"
          >PC</Button>
       </div>
    </div>

    <template v-if="activeTab !== 'hierarchy'">
        <!-- Asset Repository Table -->
        <DashboardInventoryTable 
          :items="items" 
          :type="activeTab" 
          :loading="loading"
          :columns="columns"
        />
    </template>

    <template v-else>
       <DashboardInventoryTreeTable :primary-key="hierarchyKey" />
    </template>

    <!-- Add Asset Modal Overlay -->
    <DashboardInventoryAddModal 
      :open="showAddModal"
      @update:open="showAddModal = $event"
      :type="activeTab" 
      @save="addComponent(activeTab, $event)"
    />
  </div>
</template>
