<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  Tag as TagIcon,
  Plus,
  Trash2,
  Edit3,
  Check,
  X,
  Layers,
  Cpu,
  Shield,
  ArrowRight,
  Sparkles,
  Building2,
  Monitor,
  FolderTree,
  Wrench,
  Search,
  Move,
  CheckCircle2,
  AlertTriangle,
  RotateCcw
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import MachinePictogram from '~/components/machines/MachinePictogram.vue'
import { getGroupColor } from '~/utils/machineGroupColors'

export interface TagEntry {
  id: string
  name: string
  color: string
  description?: string
}

export interface MachineTagAssignment {
  machineId: string
  tag: string
  isManualOverride: boolean
  source: 'manual' | 'security_group'
}

const props = withDefaults(defineProps<{
  isModal?: boolean
}>(), {
  isModal: false
})

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'groupSelected', groupId: string): void
}>()

// Active View Toggle: 'machines' vs 'groups'
const activeToggle = ref<'machines' | 'groups'>('machines')
const searchQuery = ref('')
const isLoading = ref(false)

// Tag Store Palette
const tagStore = ref<TagEntry[]>([
  { id: 'tag-line1', name: 'Line-01', color: '#6366f1', description: 'Pre-assembly high throughput line' },
  { id: 'tag-line2', name: 'Line-02', color: '#10b981', description: 'Robotic welding and fastening line' },
  { id: 'tag-line6', name: 'Line-06', color: '#ec4899', description: 'Audi battery module assembly cell' },
  { id: 'tag-cell-a', name: 'Cell-A', color: '#8b5cf6', description: 'Dispensing & adhesive application' },
  { id: 'tag-critical', name: 'Critical-Quality', color: '#f59e0b', description: 'Vision inspection & high precision' },
  { id: 'tag-hv', name: 'High-Voltage', color: '#ef4444', description: 'HV battery safety envelope' },
  { id: 'tag-eol', name: 'EOL-Testing', color: '#06b6d4', description: 'End of line functional validation' }
])

// Raw Entities
const machines = ref<any[]>([])
const groups = ref<any[]>([])

// Tag Assignments per Machine: machineId -> Array of assignments
// Manual overrides take precedence over security group mappings!
const machineTagMap = ref<Record<string, MachineTagAssignment[]>>({})

// Group Tag Assignments: groupId -> Array of tag strings
const groupTagMap = ref<Record<string, string[]>>({})

// Drag and drop state
const draggedTag = ref<TagEntry | null>(null)

// Modal for clicking a tag to assign to machines / groups
const isAssignModalOpen = ref(false)
const selectedTagForModal = ref<TagEntry | null>(null)
const assignTargetType = ref<'machines' | 'groups'>('machines')
const assignSelectedIds = ref<string[]>([])
const assignAsManualOverride = ref(true)

// Create Tag State
const isCreatingTag = ref(false)
const newTagName = ref('')
const newTagColor = ref('#6366f1')
const newTagDescription = ref('')

const COLOR_PRESETS = [
  '#6366f1', // Indigo
  '#10b981', // Emerald
  '#f59e0b', // Amber
  '#ef4444', // Rose
  '#8b5cf6', // Violet
  '#06b6d4', // Cyan
  '#ec4899', // Pink
  '#64748b'  // Slate
]

async function fetchData() {
  isLoading.value = true
  try {
    const [machData, grpData] = await Promise.allSettled([
      $fetch<any[]>('/api/machines?includeDecommissioned=true').catch(() => []),
      $fetch<any[]>('/api/machine-groups').catch(() => [])
    ])

    if (machData.status === 'fulfilled' && Array.isArray(machData.value)) {
      machines.value = machData.value
    } else {
      machines.value = [
        { id: 'STATION-OP10-01', name: 'OP10-Load', customIdentifier: 'LINE1-OP10', machineType: 'Assembly', groupId: 'Line 1', status: 'active' },
        { id: 'STATION-SC-L06-03', name: 'OP20-Weld', customIdentifier: 'LINE1-OP20', machineType: 'Welding', groupId: 'Line 1', status: 'active' },
        { id: 'STATION-AOI-L06-02', name: 'OP30-Vision', customIdentifier: 'LINE2-OP30', machineType: 'Test', groupId: 'Line 2', status: 'maintenance' },
        { id: 'IPC-L09-01', name: 'OP40-Fasten', customIdentifier: 'LINE2-OP40', machineType: 'Assembly', groupId: 'Line 2', status: 'active' }
      ]
    }

    if (grpData.status === 'fulfilled' && Array.isArray(grpData.value)) {
      groups.value = grpData.value
    } else {
      groups.value = [
        { id: 'grp-line06', name: 'Line 06 — Module Assembly', description: 'Automated battery module line', machineIds: ['STATION-OP10-01', 'STATION-SC-L06-03'] },
        { id: 'grp-cell-a', name: 'Cell A — Dispensing', description: 'Dispensing and screwing sub-cell', machineIds: ['STATION-SC-L06-03'] },
        { id: 'grp-line09', name: 'Line 09 — Pack Assembly', description: 'Battery pack finishing and test cell', machineIds: ['IPC-L09-01'] }
      ]
    }

    // Initialize mock seed tags and assignments
    initializeAssignments()
  } finally {
    isLoading.value = false
  }
}

function initializeAssignments() {
  const map: Record<string, MachineTagAssignment[]> = {}
  for (const m of machines.value) {
    const assignments: MachineTagAssignment[] = []
    
    // Security group mapped tags (inherited from AD/Security Group)
    if (m.groupId) {
      assignments.push({
        machineId: m.id,
        tag: m.groupId,
        isManualOverride: false,
        source: 'security_group'
      })
    }

    // Manual override sample on OP20-Weld or second station
    if (m.name.includes('Weld') || m.customIdentifier?.includes('OP20')) {
      assignments.push({
        machineId: m.id,
        tag: 'Critical-Quality',
        isManualOverride: true,
        source: 'manual'
      })
    }

    map[m.id] = assignments
  }
  machineTagMap.value = map

  // Groups tags
  const gMap: Record<string, string[]> = {}
  for (const g of groups.value) {
    gMap[g.id] = ['High-Voltage', 'Line-06']
  }
  groupTagMap.value = gMap
}

onMounted(() => {
  fetchData()
})

// Statistics
const totalTagsCount = computed(() => tagStore.value.length)
const totalMachinesCount = computed(() => machines.value.length)
const totalGroupsCount = computed(() => groups.value.length)
const manualOverridesCount = computed(() => {
  let count = 0
  for (const list of Object.values(machineTagMap.value)) {
    count += list.filter(a => a.isManualOverride).length
  }
  return count
})

// Drag and drop handlers
function onDragTagStart(tag: TagEntry, event: DragEvent) {
  draggedTag.value = tag
  if (event.dataTransfer) {
    event.dataTransfer.setData('text/plain', JSON.stringify(tag))
    event.dataTransfer.effectAllowed = 'copy'
  }
}

function handleDropOnMachine(machine: any, event: DragEvent) {
  event.preventDefault()
  if (!draggedTag.value) return
  assignTagToMachine(machine.id, draggedTag.value.name, true)
  draggedTag.value = null
}

function handleDropOnGroup(group: any, event: DragEvent) {
  event.preventDefault()
  if (!draggedTag.value) return
  assignTagToGroup(group.id, draggedTag.value.name)
  draggedTag.value = null
}

// Assignment operations
function assignTagToMachine(machineId: string, tagName: string, isManual: boolean) {
  if (!machineTagMap.value[machineId]) {
    machineTagMap.value[machineId] = []
  }
  const existing = machineTagMap.value[machineId].find(a => a.tag.toLowerCase() === tagName.toLowerCase())
  if (existing) {
    // If setting as manual override, it supersedes existing security group mapping
    if (isManual) {
      existing.isManualOverride = true
      existing.source = 'manual'
    }
  } else {
    machineTagMap.value[machineId].push({
      machineId,
      tag: tagName,
      isManualOverride: isManual,
      source: isManual ? 'manual' : 'security_group'
    })
  }
}

function removeTagFromMachine(machineId: string, tagName: string) {
  if (!machineTagMap.value[machineId]) return
  machineTagMap.value[machineId] = machineTagMap.value[machineId].filter(
    a => a.tag.toLowerCase() !== tagName.toLowerCase()
  )
}

function toggleManualOverride(machineId: string, tagName: string) {
  const existing = machineTagMap.value[machineId]?.find(a => a.tag.toLowerCase() === tagName.toLowerCase())
  if (existing) {
    existing.isManualOverride = !existing.isManualOverride
    existing.source = existing.isManualOverride ? 'manual' : 'security_group'
  }
}

function assignTagToGroup(groupId: string, tagName: string) {
  if (!groupTagMap.value[groupId]) {
    groupTagMap.value[groupId] = []
  }
  if (!groupTagMap.value[groupId].includes(tagName)) {
    groupTagMap.value[groupId].push(tagName)
  }
}

function removeTagFromGroup(groupId: string, tagName: string) {
  if (!groupTagMap.value[groupId]) return
  groupTagMap.value[groupId] = groupTagMap.value[groupId].filter(t => t !== tagName)
}

// Tag Selection Pop-up Modal (Clicking on a tag from the Tag Store)
function openTagAssignmentModal(tag: TagEntry) {
  selectedTagForModal.value = tag
  assignSelectedIds.value = []
  assignAsManualOverride.value = true
  isAssignModalOpen.value = true
}

function confirmTagAssignment() {
  if (!selectedTagForModal.value) return
  const tag = selectedTagForModal.value

  if (assignTargetType.value === 'machines') {
    for (const mId of assignSelectedIds.value) {
      assignTagToMachine(mId, tag.name, assignAsManualOverride.value)
    }
  } else {
    for (const gId of assignSelectedIds.value) {
      assignTagToGroup(gId, tag.name)
    }
  }

  isAssignModalOpen.value = false
}

// New Tag Creation
function handleCreateTag() {
  if (!newTagName.value.trim()) return
  const name = newTagName.value.trim()
  const exists = tagStore.value.some(t => t.name.toLowerCase() === name.toLowerCase())
  if (!exists) {
    tagStore.value.push({
      id: `tag-${Date.now()}`,
      name,
      color: newTagColor.value,
      description: newTagDescription.value.trim()
    })
  }
  newTagName.value = ''
  newTagDescription.value = ''
  isCreatingTag.value = false
}

function deleteTagFromStore(tagId: string) {
  tagStore.value = tagStore.value.filter(t => t.id !== tagId)
}

// Filtered Lists
const filteredMachines = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return machines.value
  return machines.value.filter(m => {
    const nameMatch = (m.name || '').toLowerCase().includes(q)
    const idMatch = (m.customIdentifier || m.id || '').toLowerCase().includes(q)
    const typeMatch = (m.machineType || '').toLowerCase().includes(q)
    const tags = (machineTagMap.value[m.id] || []).map(a => a.tag.toLowerCase())
    const tagMatch = tags.some(t => t.includes(q))
    return nameMatch || idMatch || typeMatch || tagMatch
  })
})

const filteredGroups = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return groups.value
  return groups.value.filter(g => {
    const nameMatch = (g.name || '').toLowerCase().includes(q)
    const descMatch = (g.description || '').toLowerCase().includes(q)
    const tags = (groupTagMap.value[g.id] || []).map(t => t.toLowerCase())
    const tagMatch = tags.some(t => t.includes(q))
    return nameMatch || descMatch || tagMatch
  })
})
</script>

<template>
  <div class="space-y-6">
    <!-- Top Stats & Metric Cards -->
    <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <div class="p-3.5 rounded-xl border border-border bg-card shadow-2xs space-y-1">
        <span class="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">Total Tags</span>
        <div class="text-xl font-black text-foreground">{{ totalTagsCount }}</div>
      </div>
      <div class="p-3.5 rounded-xl border border-border bg-card shadow-2xs space-y-1">
        <span class="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">Machines in Fleet</span>
        <div class="text-xl font-black text-foreground">{{ totalMachinesCount }}</div>
      </div>
      <div class="p-3.5 rounded-xl border border-border bg-card shadow-2xs space-y-1">
        <span class="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">Machine Groups</span>
        <div class="text-xl font-black text-foreground">{{ totalGroupsCount }}</div>
      </div>
      <div class="p-3.5 rounded-xl border border-border bg-card shadow-2xs space-y-1">
        <span class="text-[11px] font-medium text-amber-500 uppercase tracking-wider flex items-center gap-1">
          <Wrench class="size-3" />
          <span>Manual Overrides</span>
        </span>
        <div class="text-xl font-black text-amber-500">{{ manualOverridesCount }}</div>
      </div>
    </div>

    <!-- TAG STORE UI & PALETTE -->
    <div class="p-4 rounded-xl border border-border bg-card shadow-xs space-y-3">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-border/60 pb-3">
        <div class="flex items-center gap-2">
          <div class="p-1.5 rounded-lg bg-primary/10 text-primary border border-primary/20">
            <TagIcon class="size-4" />
          </div>
          <div>
            <h3 class="text-sm font-bold text-foreground">Tag Store & Palette</h3>
            <p class="text-xs text-muted-foreground">Click a tag for the assignment pop-up or drag & drop directly onto machines or groups.</p>
          </div>
        </div>

        <Button
          size="sm"
          variant="outline"
          class="h-7 text-xs px-2.5 gap-1.5 cursor-pointer shrink-0"
          @click="isCreatingTag = !isCreatingTag"
        >
          <Plus class="size-3.5" />
          <span>New Tag</span>
        </Button>
      </div>

      <!-- Create Tag inline form -->
      <div v-if="isCreatingTag" class="p-3 rounded-lg border border-primary/30 bg-primary/5 space-y-3">
        <div class="flex flex-col sm:flex-row items-center gap-3">
          <Input
            v-model="newTagName"
            placeholder="Tag name (e.g. Line-08, Cleanroom-A)..."
            class="h-8 text-xs flex-1"
          />
          <Input
            v-model="newTagDescription"
            placeholder="Optional description..."
            class="h-8 text-xs flex-1"
          />
          <div class="flex items-center gap-1.5 shrink-0">
            <button
              v-for="c in COLOR_PRESETS"
              :key="c"
              type="button"
              @click="newTagColor = c"
              class="size-5 rounded-full border transition-transform cursor-pointer"
              :style="{ backgroundColor: c, borderColor: newTagColor === c ? '#fff' : 'transparent', transform: newTagColor === c ? 'scale(1.2)' : 'scale(1)' }"
            />
          </div>
          <div class="flex items-center gap-2">
            <Button size="sm" class="h-8 text-xs px-3" @click="handleCreateTag" :disabled="!newTagName.trim()">
              Create
            </Button>
            <Button size="sm" variant="ghost" class="h-8 text-xs px-2" @click="isCreatingTag = false">
              Cancel
            </Button>
          </div>
        </div>
      </div>

      <!-- Tag Store Badges (Clickable & Draggable) -->
      <div class="flex flex-wrap items-center gap-2">
        <div
          v-for="tag in tagStore"
          :key="tag.id"
          draggable="true"
          @dragstart="onDragTagStart(tag, $event)"
          @click="openTagAssignmentModal(tag)"
          class="inline-flex items-center gap-2 px-2.5 py-1 rounded-lg border text-xs font-medium cursor-grab active:cursor-grabbing hover:shadow-xs transition-all select-none group"
          :style="{
            borderColor: tag.color + '60',
            backgroundColor: tag.color + '15',
            color: tag.color
          }"
          :title="`Drag onto a machine/group or click to assign. ${tag.description || ''}`"
        >
          <Move class="size-3 opacity-50 group-hover:opacity-100" />
          <span class="font-bold">#{{ tag.name }}</span>
          <button
            type="button"
            @click.stop="deleteTagFromStore(tag.id)"
            class="opacity-0 group-hover:opacity-100 hover:text-destructive transition-opacity cursor-pointer"
            title="Delete tag from store"
          >
            <X class="size-3" />
          </button>
        </div>
      </div>
    </div>

    <!-- MAIN INTERACTION SECTION: GROUPS / MACHINES TOGGLE -->
    <div class="space-y-4">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <!-- 2-way View Toggle -->
        <div class="flex items-center bg-muted/60 p-1 rounded-xl border border-border w-fit shadow-2xs">
          <button
            type="button"
            @click="activeToggle = 'machines'"
            class="px-4 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer"
            :class="activeToggle === 'machines' ? 'bg-card text-foreground shadow-xs border border-border/80' : 'text-muted-foreground hover:text-foreground'"
          >
            <Cpu class="size-3.5" />
            <span>Machines ({{ machines.length }})</span>
          </button>

          <button
            type="button"
            @click="activeToggle = 'groups'"
            class="px-4 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer"
            :class="activeToggle === 'groups' ? 'bg-card text-foreground shadow-xs border border-border/80' : 'text-muted-foreground hover:text-foreground'"
          >
            <Layers class="size-3.5" />
            <span>Groups ({{ groups.length }})</span>
          </button>
        </div>

        <!-- Filter / Search Input -->
        <div class="relative w-full sm:w-64">
          <Search class="absolute left-2.5 top-2.5 size-3.5 text-muted-foreground" />
          <Input
            v-model="searchQuery"
            :placeholder="`Filter ${activeToggle}...`"
            class="h-8.5 pl-8 text-xs bg-card"
          />
        </div>
      </div>

      <!-- VIEW A: MACHINES VIEW -->
      <div v-if="activeToggle === 'machines'" class="space-y-3">
        <div class="text-xs font-semibold uppercase tracking-wider text-muted-foreground px-1 flex items-center justify-between">
          <span>Machinery Tag Mappings & Manual Overrides</span>
          <span class="text-[11px] font-normal normal-case text-amber-500 flex items-center gap-1">
            <Wrench class="size-3" />
            <span>Manual overrides take precedence over security group rules</span>
          </span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div
            v-for="mach in filteredMachines"
            :key="mach.id"
            @dragover.prevent
            @drop="handleDropOnMachine(mach, $event)"
            class="p-4 rounded-xl border border-border bg-card shadow-2xs hover:border-primary/40 transition-all flex flex-col justify-between gap-3 relative group"
          >
            <div class="space-y-2.5">
              <!-- Machine Header -->
              <div class="flex items-start justify-between gap-2">
                <div class="flex items-center gap-2">
                  <div
                    class="p-1.5 rounded-lg border flex items-center justify-center shrink-0"
                    :style="{
                      backgroundColor: getGroupColor(mach.groupId).bg,
                      borderColor: getGroupColor(mach.groupId).border,
                      color: getGroupColor(mach.groupId).text
                    }"
                  >
                    <MachinePictogram :type="mach.machineType" class="size-5" />
                  </div>
                  <div>
                    <h4 class="text-sm font-bold text-foreground">{{ mach.name }}</h4>
                    <span class="text-[11px] font-mono text-muted-foreground">{{ mach.customIdentifier || mach.id }}</span>
                  </div>
                </div>

                <Badge variant="outline" class="text-[10px] uppercase font-mono">
                  {{ mach.status || 'active' }}
                </Badge>
              </div>

              <!-- Assigned Tags and Overrides -->
              <div class="space-y-1.5 pt-1">
                <span class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Assigned Tags:</span>
                <div class="flex flex-wrap items-center gap-1.5 min-h-[28px] p-1.5 rounded-lg border border-dashed border-border/80 bg-muted/20">
                  <template v-if="(machineTagMap[mach.id] || []).length > 0">
                    <div
                      v-for="assign in machineTagMap[mach.id]"
                      :key="assign.tag"
                      class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[11px] font-medium border shadow-2xs group/tag cursor-pointer"
                      :class="assign.isManualOverride ? 'bg-amber-500/10 border-amber-500/40 text-amber-500' : 'bg-muted/60 border-border text-foreground'"
                      :title="assign.isManualOverride ? 'Manual Override (Takes precedence)' : 'Inherited from Security Group'"
                      @click="toggleManualOverride(mach.id, assign.tag)"
                    >
                      <component :is="assign.isManualOverride ? Wrench : Shield" class="size-2.5" />
                      <span>#{{ assign.tag }}</span>
                      <span v-if="assign.isManualOverride" class="text-[9px] font-bold px-1 py-0.2 rounded bg-amber-500/20 text-amber-500">
                        Override
                      </span>
                      <button
                        type="button"
                        @click.stop="removeTagFromMachine(mach.id, assign.tag)"
                        class="ml-0.5 opacity-60 hover:opacity-100 hover:text-destructive cursor-pointer"
                        title="Remove tag"
                      >
                        <X class="size-2.5" />
                      </button>
                    </div>
                  </template>
                  <span v-else class="text-[11px] text-muted-foreground italic px-1">
                    Drag tag here or drop from store
                  </span>
                </div>
              </div>
            </div>

            <!-- Card Footer -->
            <div class="pt-2 border-t border-border/60 flex items-center justify-between text-xs text-muted-foreground">
              <span class="text-[10px] font-mono">Type: {{ mach.machineType || 'General' }}</span>
              <NuxtLink :to="`/dashboard/machines/${mach.id}`" class="text-primary hover:underline text-[11px] font-medium flex items-center gap-1">
                <span>View Machine</span>
                <ArrowRight class="size-3" />
              </NuxtLink>
            </div>
          </div>
        </div>
      </div>

      <!-- VIEW B: GROUPS VIEW -->
      <div v-else-if="activeToggle === 'groups'" class="space-y-3">
        <div class="text-xs font-semibold uppercase tracking-wider text-muted-foreground px-1 flex items-center justify-between">
          <span>Factory Groups & Line Clusters</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div
            v-for="grp in filteredGroups"
            :key="grp.id"
            @dragover.prevent
            @drop="handleDropOnGroup(grp, $event)"
            class="p-4 rounded-xl border border-border bg-card shadow-2xs hover:border-primary/40 transition-all flex flex-col justify-between gap-3 relative"
          >
            <div class="space-y-2.5">
              <div class="flex items-start justify-between gap-2">
                <div class="flex items-center gap-2">
                  <div class="p-1.5 rounded-lg bg-indigo-500/10 text-indigo-500 border border-indigo-500/20">
                    <FolderTree class="size-5" />
                  </div>
                  <div>
                    <h4 class="text-sm font-bold text-foreground">{{ grp.name }}</h4>
                    <p class="text-[11px] text-muted-foreground line-clamp-1">{{ grp.description || 'Production cluster' }}</p>
                  </div>
                </div>
              </div>

              <!-- Group Tags -->
              <div class="space-y-1.5 pt-1">
                <span class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Group Tags:</span>
                <div class="flex flex-wrap items-center gap-1.5 min-h-[28px] p-1.5 rounded-lg border border-dashed border-border/80 bg-muted/20">
                  <template v-if="(groupTagMap[grp.id] || []).length > 0">
                    <div
                      v-for="t in groupTagMap[grp.id]"
                      :key="t"
                      class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[11px] font-medium border bg-indigo-500/10 border-indigo-500/30 text-indigo-400 shadow-2xs"
                    >
                      <TagIcon class="size-2.5" />
                      <span>#{{ t }}</span>
                      <button
                        type="button"
                        @click.stop="removeTagFromGroup(grp.id, t)"
                        class="ml-0.5 opacity-60 hover:opacity-100 hover:text-destructive cursor-pointer"
                        title="Remove tag"
                      >
                        <X class="size-2.5" />
                      </button>
                    </div>
                  </template>
                  <span v-else class="text-[11px] text-muted-foreground italic px-1">
                    Drop tags here to assign to group
                  </span>
                </div>
              </div>
            </div>

            <!-- Group Footer -->
            <div class="pt-2 border-t border-border/60 flex items-center justify-between text-xs text-muted-foreground">
              <span class="text-[11px]">Machines: {{ (grp.machineIds || []).length }}</span>
              <button
                type="button"
                @click="emit('groupSelected', grp.id)"
                class="text-primary hover:underline text-[11px] font-medium flex items-center gap-1 cursor-pointer"
              >
                <span>Select Group</span>
                <ArrowRight class="size-3" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- POP-UP ASSIGNMENT MODAL (Triggered when clicking a tag in the store) -->
    <div
      v-if="isAssignModalOpen && selectedTagForModal"
      class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in"
      @click.self="isAssignModalOpen = false"
    >
      <div class="bg-card border border-border rounded-2xl max-w-lg w-full p-5 shadow-2xl space-y-4">
        <div class="flex items-center justify-between border-b border-border pb-3">
          <div class="flex items-center gap-2">
            <div
              class="size-4 rounded-full"
              :style="{ backgroundColor: selectedTagForModal.color }"
            />
            <h3 class="text-base font-bold text-foreground">
              Assign Tag: #{{ selectedTagForModal.name }}
            </h3>
          </div>
          <button
            type="button"
            @click="isAssignModalOpen = false"
            class="text-muted-foreground hover:text-foreground cursor-pointer"
          >
            <X class="size-4" />
          </button>
        </div>

        <!-- Target Type Selector -->
        <div class="flex items-center gap-3">
          <span class="text-xs font-semibold text-muted-foreground">Assign To:</span>
          <div class="flex items-center bg-muted/60 p-0.5 rounded-lg border border-border">
            <button
              type="button"
              @click="assignTargetType = 'machines'"
              class="px-3 py-1 rounded-md text-xs font-medium transition-all cursor-pointer"
              :class="assignTargetType === 'machines' ? 'bg-card text-foreground shadow-2xs font-bold' : 'text-muted-foreground'"
            >
              Machines
            </button>
            <button
              type="button"
              @click="assignTargetType = 'groups'"
              class="px-3 py-1 rounded-md text-xs font-medium transition-all cursor-pointer"
              :class="assignTargetType === 'groups' ? 'bg-card text-foreground shadow-2xs font-bold' : 'text-muted-foreground'"
            >
              Groups
            </button>
          </div>
        </div>

        <!-- Manual Override Toggle Checkbox -->
        <label v-if="assignTargetType === 'machines'" class="flex items-center gap-2.5 p-2.5 rounded-lg border border-amber-500/30 bg-amber-500/10 cursor-pointer">
          <input
            type="checkbox"
            v-model="assignAsManualOverride"
            class="rounded border-amber-500 text-amber-500 focus:ring-amber-500"
          />
          <div class="text-xs">
            <span class="font-bold text-amber-500">Apply as Manual Override</span>
            <p class="text-[11px] text-muted-foreground">Takes precedence over inherited Active Directory security group rules.</p>
          </div>
        </label>

        <!-- Multi-select Items List -->
        <div class="space-y-2">
          <span class="text-xs font-semibold text-muted-foreground">Select Target {{ assignTargetType === 'machines' ? 'Machines' : 'Groups' }}:</span>
          <div class="max-h-60 overflow-y-auto border border-border rounded-xl p-2 space-y-1.5 bg-muted/20">
            <template v-if="assignTargetType === 'machines'">
              <label
                v-for="m in machines"
                :key="m.id"
                class="flex items-center justify-between p-2 rounded-lg hover:bg-muted/50 transition-colors cursor-pointer text-xs"
              >
                <div class="flex items-center gap-2">
                  <input
                    type="checkbox"
                    :value="m.id"
                    v-model="assignSelectedIds"
                    class="rounded border-border text-primary"
                  />
                  <span class="font-bold text-foreground">{{ m.name }}</span>
                  <span class="font-mono text-muted-foreground text-[10px]">({{ m.customIdentifier || m.id }})</span>
                </div>
                <span class="text-[10px] text-muted-foreground">{{ m.machineType || 'General' }}</span>
              </label>
            </template>
            <template v-else>
              <label
                v-for="g in groups"
                :key="g.id"
                class="flex items-center justify-between p-2 rounded-lg hover:bg-muted/50 transition-colors cursor-pointer text-xs"
              >
                <div class="flex items-center gap-2">
                  <input
                    type="checkbox"
                    :value="g.id"
                    v-model="assignSelectedIds"
                    class="rounded border-border text-primary"
                  />
                  <span class="font-bold text-foreground">{{ g.name }}</span>
                </div>
                <span class="text-[10px] text-muted-foreground">{{ (g.machineIds || []).length }} machines</span>
              </label>
            </template>
          </div>
        </div>

        <!-- Dialog Footer Actions -->
        <div class="flex items-center justify-end gap-2 pt-2 border-t border-border">
          <Button variant="ghost" size="sm" class="text-xs" @click="isAssignModalOpen = false">
            Cancel
          </Button>
          <Button
            size="sm"
            class="text-xs px-4"
            @click="confirmTagAssignment"
            :disabled="assignSelectedIds.length === 0"
          >
            Apply Tag ({{ assignSelectedIds.length }})
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
