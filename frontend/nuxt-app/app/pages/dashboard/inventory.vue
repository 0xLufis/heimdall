<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { SearchIcon, SlidersHorizontalIcon, PlusIcon } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useInventory } from '@/composables/useInventory'

definePageMeta({
  layout: 'dashboard'
})

const {
  activeTab,
  loading,
  items,
  searchQuery,
  filterCategory,
  filterMinTorque,
  filterInterface,
  fetchData,
  addComponent
} = useInventory()

const showAddModal = ref(false)

const handleAddComponent = async (formData: any) => {
  const result = await addComponent(activeTab.value, formData)
  if (result.success) {
    showAddModal.value = false
  }
}

onMounted(() => {
    fetchData()
})
</script>

<template>
  <div class="space-y-8">
    <!-- Header Area -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
      <div>
        <h3 class="text-3xl font-black text-gray-900 tracking-tight uppercase">Inventory</h3>
        <p class="text-xs font-bold text-gray-400 mt-1 uppercase tracking-widest">Asset Lifecycle & Component Tracking</p>
      </div>
      
      <div class="flex items-center gap-4">
        <div class="bg-white p-1.5 rounded-2xl border border-gray-100 shadow-sm flex gap-1">
          <button 
            @click="activeTab = 'hardware'" 
            :class="activeTab === 'hardware' ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-100' : 'text-gray-400 hover:text-gray-600'"
            class="px-6 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all"
          >
            Hardware
          </button>
          <button 
            @click="activeTab = 'software'" 
            :class="activeTab === 'software' ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-100' : 'text-gray-400 hover:text-gray-600'"
            class="px-6 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all"
          >
            Software
          </button>
        </div>

        <Button @click="showAddModal = true" class="bg-indigo-900 hover:bg-black text-white rounded-2xl px-6 py-6 h-auto shadow-xl transition-all group">
          <PlusIcon class="h-5 w-5 mr-2 group-hover:rotate-90 transition-transform" />
          <span class="text-xs font-black uppercase tracking-widest">Provision Asset</span>
        </Button>
      </div>
    </div>

    <!-- Filtering Control Panel -->
    <Card class="border-none shadow-sm overflow-hidden bg-gray-50/50">
      <CardContent class="p-8">
        <div class="flex flex-col md:flex-row gap-6">
          <div class="relative flex-grow group">
            <SearchIcon class="absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 group-focus-within:text-indigo-500 transition-colors z-10" />
            <Input 
              v-model="searchQuery" 
              placeholder="Query by asset name, serial, or identifier..." 
              class="w-full pl-12 pr-4 py-8 bg-white border border-gray-100 rounded-2xl focus:ring-4 focus:ring-indigo-500/5 focus:border-indigo-500 outline-none transition-all font-bold text-sm shadow-sm" 
            />
          </div>
          
          <div class="flex gap-3">
            <div class="relative">
              <select v-model="filterCategory" class="appearance-none pl-6 pr-12 py-4 h-full bg-white border border-gray-100 rounded-2xl focus:ring-4 focus:ring-indigo-500/5 focus:border-indigo-500 outline-none transition-all font-bold text-xs uppercase tracking-widest shadow-sm">
                <option value="">All Categories</option>
                <option value="Sensor">Sensors</option>
                <option value="Vision Sensor">Vision Sensors</option>
                <option value="Screwdriver">Screwdrivers</option>
                <option value="Controller">Controllers</option>
                <option value="Industrial Robot">Industrial Robots</option>
              </select>
              <SlidersHorizontalIcon class="absolute right-4 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 pointer-events-none" />
            </div>
            
            <Button @click="fetchData" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-8 py-4 h-auto shadow-lg shadow-indigo-100 transition-all">
              <span class="text-xs font-black uppercase tracking-widest">Apply Filters</span>
            </Button>
          </div>
        </div>

        <!-- Technical Overrides -->
        <div v-if="activeTab === 'hardware'" class="grid grid-cols-1 md:grid-cols-3 gap-8 mt-8 pt-8 border-t border-gray-100">
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Minimum Torque Threshold (Nm)</Label>
            <Input v-model="filterMinTorque" type="number" step="0.1" class="w-full px-4 py-6 bg-white border border-gray-100 rounded-xl focus:ring-2 focus:ring-indigo-500/10 outline-none transition-all font-bold text-xs" />
          </div>
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Network Interface Protocol</Label>
            <select v-model="filterInterface" class="w-full h-10 px-4 py-2 bg-white border border-gray-100 rounded-xl focus:ring-2 focus:ring-indigo-500/10 outline-none transition-all font-bold text-xs uppercase tracking-widest">
              <option value="">Standard (Any)</option>
              <option value="Ethernet/IP">Ethernet/IP</option>
              <option value="Profinet">Profinet</option>
              <option value="USB">USB</option>
            </select>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Asset Repository Table -->
    <DashboardInventoryTable 
      :items="items" 
      :type="activeTab" 
      :loading="loading" 
    />

    <!-- Add Asset Modal Overlay -->
    <DashboardInventoryAddModal 
      v-if="showAddModal" 
      :type="activeTab" 
      @close="showAddModal = false" 
      @save="handleAddComponent"
    />
  </div>
</template>
