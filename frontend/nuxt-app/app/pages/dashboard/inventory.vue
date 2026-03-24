<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { PlusIcon, ViewIcon } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuCheckboxItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { useInventory } from '@/composables/useInventory'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const {
  activeTab,
  loading,
  items,
  columns,
  searchQuery,
  fetchData,
  addComponent
} = useInventory()

const showAddModal = ref(false)

onMounted(fetchData)
</script>

<template>
  <div class="space-y-8">
    <!-- Header Area -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Inventory</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">Asset Lifecycle & Component Tracking</p>
      </div>
      
      <div class="flex items-center gap-4">
        <div class="bg-slate-900 p-1.5 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button 
            variant="ghost"
            @click="activeTab = 'hardware'" 
            :class="activeTab === 'hardware' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300'"
            class="px-6 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto"
          >
            Hardware
          </Button>
          <Button 
            variant="ghost"
            @click="activeTab = 'software'" 
            :class="activeTab === 'software' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300'"
            class="px-6 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto"
          >
            Software
          </Button>
        </div>

        <Button @click="showAddModal = true" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-6 py-6 h-auto shadow-xl transition-all group border-0">
          <PlusIcon class="h-5 w-5 mr-2 group-hover:rotate-90 transition-transform" />
          <span class="text-xs font-black uppercase tracking-widest">Provision Asset</span>
        </Button>
      </div>
    </div>

    <!-- Modular Search -->
    <DashboardInventorySearchCard v-model="searchQuery" :key="activeTab" />
    
    <div class="flex justify-between items-center">
      <!-- Column Toggler -->
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button variant="outline" class="border-slate-700 hover:bg-slate-800">
            <ViewIcon class="h-4 w-4 mr-2" />
            Toggle Columns
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent class="w-56">
          <DropdownMenuCheckboxItem v-for="(visible, key) in columns" :key="key" v-model:checked="columns[key]">
            {{ key }}
          </DropdownMenuCheckboxItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <Button @click="fetchData" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-8 h-14 transition-all border border-transparent shadow-lg">
        <span class="text-xs font-black uppercase tracking-widest">Execute Query</span>
      </Button>
    </div>

    <!-- Asset Repository Table -->
    <DashboardInventoryTable 
      :items="items" 
      :type="activeTab" 
      :loading="loading"
      :columns="columns"
    />

    <!-- Add Asset Modal Overlay -->
    <DashboardInventoryAddModal 
      :open="showAddModal"
      @update:open="showAddModal = $event"
      :type="activeTab" 
      @save="addComponent(activeTab, $event)"
    />
  </div>
</template>
