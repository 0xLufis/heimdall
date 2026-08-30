<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Button } from '@/components/ui/button'
import { RefreshCw, Map, Grid, Plus, X } from 'lucide-vue-next'
import { useControllers } from '~/composables/useControllers'
import { useStations } from '~/composables/useStations'
import ControllerGrid from '~/components/controllers/ControllerGrid.vue'
import ControllerTelemetryCard from '~/components/controllers/ControllerTelemetryCard.vue'
import ControllerCommandModal from '~/components/controllers/ControllerCommandModal.vue'
import InteractiveMapCanvas from '~/components/map/InteractiveMapCanvas.vue'
import OmniSearchBar from '~/components/search/OmniSearchBar.vue'
import type { IndustrialController } from '~/types/domain'
import type { SearchInstanceConfig } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { controllers, isLoading, fetchControllers } = useControllers()
const { stations } = useStations()
const router = useRouter()
const route = useRoute()

const activeViewMode = ref<'grid' | 'map'>('grid')
const selectedController = ref<IndustrialController | null>(null)
const commandTargetController = ref<IndustrialController | null>(null)
const isCommandModalOpen = ref(false)
const searchQuery = ref('')

const clientsSearchConfig: SearchInstanceConfig = {
  instanceId: 'clients',
  placeholder: 'Filter controllers by hostname, MAC, IP, or status (e.g. status:online)...',
  defaultEndpoints: ['/api/proxy/ClientPc'],
  defaultTags: [],
  enableAutoTagging: true
}

const filteredControllers = computed(() => {
  if (!searchQuery.value) return controllers.value
  const q = searchQuery.value.toLowerCase()
  return controllers.value.filter(c =>
    c.hostname.toLowerCase().includes(q) ||
    c.macAddress.toLowerCase().includes(q) ||
    (c.ipAddress && c.ipAddress.toLowerCase().includes(q))
  )
})

const searchedHandles = computed(() => {
  if (!searchQuery.value) return []
  const q = searchQuery.value.toLowerCase()
  const list: string[] = []
  controllers.value.forEach(c => {
    if (
      c.hostname.toLowerCase().includes(q) ||
      c.macAddress.toLowerCase().includes(q) ||
      (c.pinnedObjectHandle && c.pinnedObjectHandle.toLowerCase().includes(q))
    ) {
      if (c.pinnedObjectHandle) list.push(c.pinnedObjectHandle)
      list.push(c.hostname)
    }
  })
  return list
})

const handleSelectController = (pc: IndustrialController) => {
  selectedController.value = pc
  router.replace({
    query: { ...route.query, selected: pc.id, hostname: pc.hostname }
  })
}

const handleCloseTelemetry = () => {
  selectedController.value = null
  router.replace({
    query: { ...route.query, selected: undefined, hostname: undefined }
  })
}

const handleMapObjectClick = (handle: string) => {
  const match = controllers.value.find(c => c.pinnedObjectHandle === handle || c.hostname === handle)
  if (match) selectedController.value = match
}

const handleMapObjectDblClick = (handle: string) => {
  const match = controllers.value.find(c => c.pinnedObjectHandle === handle || c.hostname === handle)
  if (match) selectedController.value = match
}

const handleQueueCommand = (pc: IndustrialController) => {
  commandTargetController.value = pc
  isCommandModalOpen.value = true
}

onMounted(() => {
  if (route.query.selected || route.query.hostname) {
    const targetId = route.query.selected as string
    const targetHost = route.query.hostname as string
    const match = controllers.value.find(c => c.id === targetId || c.hostname === targetHost)
    if (match) {
      selectedController.value = match
    } else if (targetHost) {
      searchQuery.value = targetHost
    }
  }
})

// Keep selected controller telemetry updated without auto-reopening if dismissed
watch(() => controllers.value, (list) => {
  if (selectedController.value) {
    const updated = list.find(c => c.id === selectedController.value?.id)
    if (updated) selectedController.value = updated
  } else if (route.query.selected && !selectedController.value) {
    const match = list.find(c => c.id === route.query.selected || c.hostname === route.query.hostname)
    if (match) selectedController.value = match
  }
})

const onSearch = (q: string) => {
  searchQuery.value = q
}
</script>

<template>
  <div class="space-y-8 pb-12">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-end justify-between gap-6">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Industrial Controllers</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">
          Edge IPC Telemetry, Beckhoff RT NIC Diagnostics & Signed Command Queue
        </p>
      </div>

      <div class="flex items-center gap-3">
        <div class="bg-slate-900 p-1.5 rounded-2xl border border-slate-800 shadow-sm flex gap-1">
          <Button
            variant="ghost"
            @click="activeViewMode = 'grid'"
            :class="activeViewMode === 'grid' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <Grid class="w-3.5 h-3.5 mr-1.5" />
            Grid View
          </Button>

          <Button
            variant="ghost"
            @click="activeViewMode = 'map'"
            :class="activeViewMode === 'map' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="px-4 py-2 rounded-xl text-[10px] font-black uppercase tracking-widest h-auto"
          >
            <Map class="w-3.5 h-3.5 mr-1.5" />
            Plant Map
          </Button>
        </div>

        <Button
          variant="outline"
          @click="fetchControllers(false)"
          class="bg-slate-900 border-slate-800 text-slate-300 rounded-2xl px-5 h-11 hover:bg-slate-800 text-xs font-bold uppercase tracking-wider"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="w-3.5 h-3.5 mr-2" />
          Sync Telemetry
        </Button>
      </div>
    </div>

    <!-- OmniSearch Bar -->
    <div class="max-w-4xl mx-auto w-full">
      <OmniSearchBar
        :config="clientsSearchConfig"
        :immediate="true"
        @search="onSearch"
      />
    </div>

    <!-- Selected Controller Telemetry Highlight Drawer/Card -->
    <div v-if="selectedController" class="relative animate-in fade-in slide-in-from-top-3 duration-300">
      <ControllerTelemetryCard :controller="selectedController" />
      <button
        type="button"
        @click="handleCloseTelemetry"
        class="absolute top-4 right-4 flex items-center gap-1 px-3 py-1.5 rounded-xl bg-slate-950/80 border border-slate-800 text-[10px] font-black text-slate-400 hover:text-white hover:border-slate-700 transition-all uppercase tracking-wider shadow-lg"
      >
        <X class="w-3.5 h-3.5" />
        <span>Close Telemetry</span>
      </button>
    </div>

    <!-- Main View Mode -->
    <template v-if="activeViewMode === 'grid'">
      <ControllerGrid
        :controllers="filteredControllers"
        :loading="isLoading"
        @select="handleSelectController"
        @queue-command="handleQueueCommand"
      />
    </template>

    <template v-else>
      <div class="h-[600px] rounded-3xl overflow-hidden border border-slate-800 shadow-2xl">
        <InteractiveMapCanvas
          :highlighted-handles="searchedHandles"
          :active-pin="selectedController?.pinnedObjectHandle"
          @object-clicked="handleMapObjectClick"
          @object-dblclicked="handleMapObjectDblClick"
        />
      </div>
    </template>

    <!-- Signed Command Modal -->
    <ControllerCommandModal
      :controller="commandTargetController"
      :open="isCommandModalOpen"
      @update:open="isCommandModalOpen = $event"
      @submitted="fetchControllers(false)"
    />
  </div>
</template>