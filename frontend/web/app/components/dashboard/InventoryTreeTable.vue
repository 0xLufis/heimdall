<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { 
  ChevronRight, 
  Search, 
  Settings2, 
  Monitor, 
  Cpu, 
  Activity,
  Check,
  Layout,
  FolderTree
} from 'lucide-vue-next'
import DashboardInventoryTreeComponentRow from './InventoryTreeComponentRow.vue'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import { Separator } from '~/components/ui/separator'
import { Popover, PopoverTrigger, PopoverContent } from '~/components/ui/popover'
import { useInventoryKeys } from '~/composables/useInventoryKeys'

const props = defineProps<{
  primaryKey: 'machine' | 'client'
}>()

const loading = ref(false)
const items = ref<any[]>([])
const searchQuery = ref('')
const expanded = ref<Record<string, boolean>>({})

// Engineering Group Filtering
const responsibilityFilter = ref('all')
const teams = ref<any[]>([])

const { groups: dbGroups, fetchKeys } = useInventoryKeys()

// Default columns that are always present
const defaultColumns = ref({
  name: true,
  lastOnline: true,
  owner: true,
  linkedAsset: true,
  teams: true
})

// Dynamic columns selected by the user
const selectedDynamicColumns = ref<string[]>([])
const expandedGroups = ref<Record<string, boolean>>({
  'Core Attributes': true
})

// Persist column selection to localStorage
const CACHE_KEY = `inventory_cols_${props.primaryKey}`

onMounted(async () => {
  if (typeof localStorage !== 'undefined') {
    const cached = localStorage.getItem(CACHE_KEY)
    if (cached) {
      try {
        selectedDynamicColumns.value = JSON.parse(cached)
      } catch {
        // Ignored
      }
    }
  }
  await Promise.all([fetchKeys(), fetchTeams(), fetchData()])
})

watch(selectedDynamicColumns, (newVal) => {
  if (typeof localStorage !== 'undefined') {
    localStorage.setItem(CACHE_KEY, JSON.stringify(newVal))
  }
}, { deep: true })

const fetchTeams = async () => {
  try {
    const data = await $fetch<any[]>('/api/proxy/inventory/teams')
    if (data && Array.isArray(data)) teams.value = data
  } catch {
    // Dev fallback
    teams.value = [
      { id: 'team-mech', name: 'Mechanical Maintenance' },
      { id: 'team-elec', name: 'Electrical Engineering' },
      { id: 'team-quality', name: 'Quality Automation' },
      { id: 'team-it', name: 'Industrial IT' }
    ]
  }
}

const fetchData = async () => {
  loading.value = true
  try {
    const res = await $fetch<any>('/api/inventory/tree', {
      query: {
        primaryKey: props.primaryKey,
        query: searchQuery.value,
        responsibility: responsibilityFilter.value
      }
    })
    if (res && res.tree) {
      items.value = res.tree
    }
  } catch (e) {
    console.error('Error fetching tree from Nitro BFF handler:', e)
  } finally {
    loading.value = false
  }
}

watch([searchQuery, responsibilityFilter, () => props.primaryKey], () => {
  fetchData()
})

const filteredItems = computed(() => items.value)

const isOnline = (lastOnline: string | null): boolean => {
  if (!lastOnline) return false
  const date = new Date(lastOnline)
  const now = new Date()
  return (now.getTime() - date.getTime()) < (5 * 60 * 1000)
}

const getNestedValue = (item: any, key: string) => {
  if (item[key] !== undefined) return item[key]
  if (item.metadata && item.metadata[key] !== undefined) return item.metadata[key]
  return null
}

const toggleExpand = (id: string) => {
  expanded.value[id] = !expanded.value[id]
}

const toggleDynamicColumn = (key: string) => {
  if (selectedDynamicColumns.value.includes(key)) {
    selectedDynamicColumns.value = selectedDynamicColumns.value.filter(k => k !== key)
  } else {
    selectedDynamicColumns.value.push(key)
  }
}

const toggleGroup = (group: string) => {
  expandedGroups.value[group] = !expandedGroups.value[group]
}
</script>

<template>
  <div class="space-y-4">
    <!-- Toolbar -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div class="flex items-center gap-2 flex-1">
        <div class="relative flex-1 max-w-sm">
          <Search class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
          <Input 
            v-model="searchQuery" 
            placeholder="Filter stations or host nodes..." 
            class="pl-10 bg-slate-900 border-slate-800 rounded-xl"
          />
        </div>

        <div class="flex p-1 bg-slate-900 rounded-xl border border-slate-800 gap-1 overflow-x-auto whitespace-nowrap scrollbar-hide">
          <Button 
            variant="ghost" 
            size="sm"
            @click="responsibilityFilter = 'all'"
            :class="responsibilityFilter === 'all' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="rounded-lg text-[9px] font-black uppercase px-3 flex items-center gap-1.5"
          >
            All Teams
          </Button>
          <Button 
            v-for="team in teams" 
            :key="team.id"
            variant="ghost" 
            size="sm"
            @click="responsibilityFilter = team.id"
            :class="responsibilityFilter === team.id ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="rounded-lg text-[9px] font-black uppercase px-3 flex items-center gap-1.5"
          >
            {{ team.name }}
          </Button>
        </div>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <Popover>
          <PopoverTrigger as-child>
            <Button variant="outline" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl text-[10px] font-black uppercase tracking-widest">
              <Settings2 class="h-4 w-4 mr-2" />
              Columns
            </Button>
          </PopoverTrigger>
          <PopoverContent align="end" class="w-80 p-0 bg-slate-950 border-slate-800 shadow-2xl overflow-hidden text-slate-200">
            <div class="p-4 border-b border-slate-900 bg-slate-900/50">
               <h4 class="text-[10px] font-black uppercase tracking-widest text-slate-400">Hierarchy View Config</h4>
            </div>
            
            <div class="p-2 max-h-[500px] overflow-y-auto custom-scrollbar">
               <div class="px-3 py-2 text-[9px] font-black text-slate-600 uppercase tracking-widest flex items-center gap-2">
                 <Layout class="size-3 text-slate-400" />
                 Base Fields
               </div>
               <div v-for="(visible, key) in defaultColumns" :key="key" 
                  @click="defaultColumns[key as keyof typeof defaultColumns] = !defaultColumns[key as keyof typeof defaultColumns]"
                  class="flex items-start gap-3 p-2 rounded-lg cursor-pointer hover:bg-slate-900 transition-colors border border-transparent hover:border-slate-800 ml-2"
               >
                  <div class="size-4 rounded border flex items-center justify-center mt-0.5" 
                    :class="defaultColumns[key as keyof typeof defaultColumns] ? 'bg-indigo-600 border-indigo-600' : 'border-slate-700 bg-slate-900'">
                    <Check v-if="defaultColumns[key as keyof typeof defaultColumns]" class="size-3 text-white" />
                  </div>
                  <span class="text-xs font-bold text-slate-300 uppercase">{{ key }}</span>
               </div>

               <Separator class="my-2 bg-slate-900" />
               <div class="px-3 py-1 text-[9px] font-black text-slate-600 uppercase tracking-widest flex items-center gap-2">
                 <FolderTree class="size-3 text-slate-400" />
                 Metadata Fields
               </div>
               
               <div v-for="group in dbGroups" :key="group.group" class="mt-1">
                  <div 
                    @click="toggleGroup(group.group)"
                    class="flex items-center gap-2 px-3 py-1.5 hover:bg-slate-900/50 rounded-lg cursor-pointer transition-colors group/group"
                  >
                    <ChevronRight 
                      class="size-3 text-slate-600 transition-transform" 
                      :class="{'rotate-90': expandedGroups[group.group]}" 
                    />
                    <span class="text-[10px] font-black text-slate-400 uppercase tracking-tight group-hover/group:text-slate-200">{{ group.group }}</span>
                    <Badge variant="outline" class="ml-auto text-[8px] border-slate-800 text-slate-600">{{ group.keys.length }}</Badge>
                  </div>

                  <div v-if="expandedGroups[group.group]" class="pl-6 space-y-0.5 mt-1 border-l border-slate-900 ml-4.5">
                    <div 
                      v-for="key in group.keys" 
                      :key="key"
                      @click="toggleDynamicColumn(key)"
                      class="flex items-center gap-3 p-2 rounded-lg cursor-pointer hover:bg-slate-900 transition-colors border border-transparent hover:border-slate-800"
                    >
                      <div class="size-3.5 rounded border flex items-center justify-center" 
                        :class="selectedDynamicColumns.includes(key) ? 'bg-emerald-600 border-emerald-600' : 'border-slate-700 bg-slate-900'">
                        <Check v-if="selectedDynamicColumns.includes(key)" class="size-2.5 text-white" />
                      </div>
                      <span class="text-[11px] font-medium text-slate-400" :class="{'text-slate-200': selectedDynamicColumns.includes(key)}">{{ key }}</span>
                    </div>
                  </div>
               </div>
            </div>
          </PopoverContent>
        </Popover>

        <Button @click="fetchData" variant="outline" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl">
          <Activity class="h-4 w-4 mr-2" :class="{'animate-pulse text-emerald-500': loading}" />
          Refresh
        </Button>
      </div>
    </div>

    <!-- Table -->
    <div class="rounded-2xl border border-slate-800 bg-slate-950 overflow-hidden shadow-2xl">
      <Table>
        <TableHeader class="bg-slate-900/50">
          <TableRow class="border-b border-slate-800 hover:bg-transparent">
            <TableHead class="w-12 text-center"></TableHead>
            <TableHead v-if="defaultColumns.name" class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-500 py-4">
               {{ primaryKey === 'client' ? 'Reporting Node' : 'Process Station' }}
            </TableHead>
            <TableHead v-if="primaryKey === 'client' && defaultColumns.lastOnline" class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-500">
               Connection
            </TableHead>
            <TableHead v-if="defaultColumns.owner" class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-500">
               Org Unit
            </TableHead>
            <TableHead v-if="defaultColumns.linkedAsset" class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-500">
               {{ primaryKey === 'client' ? 'Controlled Machines' : 'Assigned PCs' }}
            </TableHead>
            <TableHead v-if="defaultColumns.teams" class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-500">
               Responsible Teams
            </TableHead>
            <TableHead v-for="col in selectedDynamicColumns" :key="col" class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-500">
               {{ col }}
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="filteredItems.length === 0 && !loading">
            <TableRow>
              <TableCell colspan="7" class="h-32 text-center text-slate-500 uppercase font-black text-xs tracking-widest">
                No hierarchy entities found matching the active filters.
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <template v-for="item in filteredItems" :key="item.id">
              <TableRow class="border-b border-slate-800 hover:bg-slate-900/30 transition-colors group">
                <TableCell class="text-center">
                  <Button @click="toggleExpand(item.id)" variant="ghost" size="icon" class="h-8 w-8 text-slate-600 hover:bg-slate-800 rounded-lg">
                    <ChevronRight class="h-4 w-4 transition-transform duration-300" :class="{'rotate-90 text-indigo-400': expanded[item.id]}" />
                  </Button>
                </TableCell>
                
                <!-- Primary Name -->
                <TableCell v-if="defaultColumns.name">
                  <div class="flex items-center gap-4">
                    <div class="p-2.5 bg-slate-900 rounded-xl text-slate-400 group-hover:text-indigo-400 transition-all border border-slate-800 group-hover:border-indigo-500/30">
                      <Monitor v-if="primaryKey === 'client'" class="h-4.5 w-4.5" />
                      <Cpu v-else class="h-4.5 w-4.5" />
                    </div>
                    <div class="flex flex-col">
                      <span class="text-sm font-black text-slate-100 uppercase tracking-tight group-hover:text-white">
                        {{ item.name || item.hostname || item.customIdentifier }}
                      </span>
                      <span v-if="item.displayName" class="text-[10px] text-slate-400 font-bold uppercase tracking-wider">{{ item.displayName }}</span>
                    </div>
                  </div>
                </TableCell>

                <!-- Status (Online/Offline) -->
                <TableCell v-if="primaryKey === 'client' && defaultColumns.lastOnline">
                   <div class="flex items-center gap-2">
                      <span class="w-2 h-2 rounded-full" :class="isOnline(item.lastOnline) ? 'bg-emerald-500' : 'bg-muted-foreground/30'"></span>
                      <span class="text-[10px] font-mono font-medium uppercase tracking-wider" :class="isOnline(item.lastOnline) ? 'text-emerald-500' : 'text-muted-foreground'">
                         {{ isOnline(item.lastOnline) ? 'ONLINE' : 'OFFLINE' }}
                      </span>
                   </div>
                </TableCell>

                <!-- Owner -->
                <TableCell v-if="defaultColumns.owner">
                   <Badge variant="outline" class="text-[8px] font-mono uppercase tracking-wider border-border text-muted-foreground bg-muted/40 px-3 py-1 rounded-full shadow-sm">
                      {{ item.organizationId || 'Heimdall Root' }}
                   </Badge>
                </TableCell>

                <!-- Linked Assets -->
                <TableCell v-if="defaultColumns.linkedAsset">
                   <div class="flex flex-wrap gap-1.5">
                      <template v-if="primaryKey === 'client'">
                         <Badge v-for="m in item.controlledMachines" :key="m.id" variant="secondary" class="bg-indigo-950/20 text-indigo-400 border-indigo-900/20 text-[8px] font-black uppercase tracking-wider px-3 py-1 rounded-full shadow-sm">
                            {{ m.customIdentifier || m.name }}
                         </Badge>
                      </template>
                      <template v-else>
                         <Badge v-for="c in item.controllers" :key="c.id" variant="secondary" class="bg-indigo-950/20 text-indigo-400 border-indigo-900/20 text-[8px] font-black uppercase tracking-wider px-3 py-1 rounded-full shadow-sm">
                            {{ c.hostname || c.name }}
                         </Badge>
                      </template>
                   </div>
                </TableCell>

                <!-- Teams -->
                <TableCell v-if="defaultColumns.teams">
                  <div class="flex flex-wrap gap-1.5">
                    <Badge v-for="team in item.responsibleTeams" :key="team.id" variant="outline" class="text-[7.5px] font-black uppercase tracking-wider border-indigo-500/20 text-indigo-400 bg-indigo-500/5 px-3 py-1 rounded-full shadow-sm">
                      {{ team.name }}
                    </Badge>
                  </div>
                </TableCell>

                <!-- Dynamic Columns -->
                <TableCell v-for="col in selectedDynamicColumns" :key="col" class="text-xs font-bold text-slate-400 font-mono">
                   {{ getNestedValue(item, col) || '-' }}
                </TableCell>
              </TableRow>

              <!-- Expanded Components Tree -->
              <template v-if="expanded[item.id]">
                 <DashboardInventoryTreeComponentRow 
                    v-for="comp in [...(item.children || []), ...(item.inventoryItems || [])]" 
                    :key="comp.id" 
                    :component="comp" 
                    :depth="1"
                    :selected-dynamic-columns="selectedDynamicColumns"
                    :primary-key="primaryKey"
                    :default-columns="defaultColumns"
                    :active-responsibility-filter="responsibilityFilter"
                    :search-query="searchQuery"
                 />
              </template>
            </template>
          </template>
        </TableBody>
      </Table>
    </div>
  </div>
</template>
