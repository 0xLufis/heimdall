<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { SearchIcon, SlidersHorizontalIcon, PlusIcon } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useInventory } from '@/composables/useInventory'

definePageMeta({
  layout: 'shadcn-dashboard'
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

    <!-- Filtering Control Panel -->
    <Card class="border-slate-800 shadow-sm overflow-hidden bg-slate-900/50">
      <CardContent class="p-8">
        <div class="flex flex-col md:flex-row gap-6">
          <div class="relative flex-grow group">
            <SearchIcon class="absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500 group-focus-within:text-indigo-500 transition-colors z-10" />
            <Input 
              v-model="searchQuery" 
              placeholder="Query by asset name, serial, or identifier..." 
              class="w-full pl-12 pr-4 h-14 bg-slate-950 border-slate-800 rounded-2xl focus:ring-4 focus:ring-indigo-500/10 transition-all font-bold text-sm shadow-sm text-slate-200" 
            />
          </div>
          
          <div class="flex gap-3">
            <Select v-model="filterCategory">
              <SelectTrigger class="w-[200px] h-14 bg-slate-950 border-slate-800 rounded-2xl font-bold text-xs uppercase tracking-widest text-slate-400">
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                <SelectItem value="">All Categories</SelectItem>
                <SelectItem value="Sensor">Sensors</SelectItem>
                <SelectItem value="Vision Sensor">Vision Sensors</SelectItem>
                <SelectItem value="Screwdriver">Screwdrivers</SelectItem>
                <SelectItem value="Controller">Controllers</SelectItem>
                <SelectItem value="Industrial Robot">Industrial Robots</SelectItem>
              </SelectContent>
            </Select>
            
            <Button @click="fetchData" class="bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-2xl px-8 h-14 transition-all border border-slate-700">
              <span class="text-xs font-black uppercase tracking-widest">Apply Filters</span>
            </Button>
          </div>
        </div>

        <!-- Technical Overrides -->
        <div v-if="activeTab === 'hardware'" class="grid grid-cols-1 md:grid-cols-3 gap-8 mt-8 pt-8 border-t border-slate-800">
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Minimum Torque Threshold (Nm)</Label>
            <Input v-model="filterMinTorque" type="number" step="0.1" class="w-full h-12 bg-slate-950 border-slate-800 rounded-xl focus:ring-2 focus:ring-indigo-500/10 transition-all font-bold text-xs text-slate-200" />
          </div>
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Network Interface Protocol</Label>
            <Select v-model="filterInterface">
              <SelectTrigger class="w-full h-12 bg-slate-950 border-slate-800 rounded-xl font-bold text-xs uppercase tracking-widest text-slate-400">
                <SelectValue placeholder="Protocol" />
              </SelectTrigger>
              <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                <SelectItem value="">Standard (Any)</SelectItem>
                <SelectItem value="Ethernet/IP">Ethernet/IP</SelectItem>
                <SelectItem value="Profinet">Profinet</SelectItem>
                <SelectItem value="USB">USB</SelectItem>
              </SelectContent>
            </Select>
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
      :open="showAddModal"
      @update:open="(val) => showAddModal = val"
      :type="activeTab" 
      @close="showAddModal = false" 
      @save="handleAddComponent"
    />
  </div>
</template>
