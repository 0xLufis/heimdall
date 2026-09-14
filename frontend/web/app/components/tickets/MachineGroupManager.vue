<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  FolderTree, Plus, Trash2, Edit3, Check, X, ChevronRight,
  ChevronDown, Layers, Cpu, Shield, ArrowRight, Sparkles, Building2
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import type { MachineGroup } from '~/types/maintenance'

const props = withDefaults(defineProps<{
  isModal?: boolean
}>(), {
  isModal: false
})

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'groupSelected', groupId: string): void
}>()

const groups = ref<MachineGroup[]>([])
const isLoading = ref(false)
const selectedGroupId = ref<string | null>(null)
const isCreating = ref(false)
const isEditing = ref(false)

const STANDARD_MACHINE_TYPES = [
  'Automatic Optical Inspection',
  'Gap Filler',
  'Screwing Station',
  'Soldering',
  'Milling',
  'Fitting',
  'Pressing',
  'Manipulator',
  'Tester Cell',
  'Painting'
]

const form = ref<{
  name: string
  description: string
  parentId: string | null
  machineTypes: string[]
  color: string
  leadEngineerName: string
}>({
  name: '',
  description: '',
  parentId: null,
  machineTypes: [],
  color: 'indigo',
  leadEngineerName: ''
})

async function fetchGroups() {
  isLoading.value = true
  try {
    const data = await $fetch<MachineGroup[]>('/api/machine-groups')
    groups.value = data
  } catch (err) {
    console.error('Failed to load machine groups', err)
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  fetchGroups()
})

const rootGroups = computed(() => groups.value.filter(g => !g.parentId))

function getChildGroups(parentId: string): MachineGroup[] {
  return groups.value.filter(g => g.parentId === parentId)
}

function startCreate(parentId: string | null = null) {
  isCreating.value = true
  isEditing.value = false
  selectedGroupId.value = null
  form.value = {
    name: '',
    description: '',
    parentId,
    machineTypes: [],
    color: 'indigo',
    leadEngineerName: ''
  }
}

function startEdit(group: MachineGroup) {
  isEditing.value = true
  isCreating.value = false
  selectedGroupId.value = group.id
  form.value = {
    name: group.name,
    description: group.description || '',
    parentId: group.parentId || null,
    machineTypes: group.machineTypes ? [...group.machineTypes] : [],
    color: group.color || 'indigo',
    leadEngineerName: group.leadEngineerName || ''
  }
}

function toggleMachineType(type: string) {
  const idx = form.value.machineTypes.indexOf(type)
  if (idx === -1) {
    form.value.machineTypes.push(type)
  } else {
    form.value.machineTypes.splice(idx, 1)
  }
}

async function saveGroup() {
  if (!form.value.name.trim()) return

  try {
    if (isCreating.value) {
      await $fetch('/api/machine-groups', {
        method: 'POST',
        body: form.value
      })
    } else if (isEditing.value && selectedGroupId.value) {
      await $fetch(`/api/machine-groups/${selectedGroupId.value}`, {
        method: 'PATCH',
        body: form.value
      })
    }
    await fetchGroups()
    isCreating.value = false
    isEditing.value = false
  } catch (err) {
    console.error('Failed to save machine group', err)
  }
}

async function deleteGroup(id: string) {
  if (!confirm('Are you sure you want to delete this group? Sub-groups will be re-parented.')) return
  try {
    await $fetch(`/api/machine-groups/${id}`, { method: 'DELETE' })
    await fetchGroups()
    if (selectedGroupId.value === id) selectedGroupId.value = null
  } catch (err) {
    console.error('Failed to delete group', err)
  }
}

const totalClustersCount = computed(() => {
  return groups.value.reduce((acc, g) => acc + (g.machineTypes?.length || 0), 0)
})
</script>

<template>
  <div class="space-y-4">
    <!-- Header Section (conditional if modal vs page) -->
    <div v-if="isModal" class="pb-4 border-b border-slate-800 flex flex-row items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
          <FolderTree class="h-6 w-6" />
        </div>
        <div>
          <h3 class="text-lg font-black uppercase tracking-tight text-slate-100">
            Machine Groups & Envelope Hierarchy
          </h3>
          <p class="text-xs text-slate-400 mt-0.5">
            Envelop machines in cells, lines, and plants with technology type clusters
          </p>
        </div>
      </div>
      <div class="flex items-center gap-2">
        <Button
          size="sm"
          class="bg-indigo-600 hover:bg-indigo-500 text-white font-bold gap-1.5"
          @click="startCreate(null)"
        >
          <Plus class="h-4 w-4" />
          Add Root Group
        </Button>
        <Button variant="ghost" size="icon" @click="emit('close')" class="text-slate-400 hover:text-white rounded-xl">
          <X class="h-5 w-5" />
        </Button>
      </div>
    </div>

    <!-- Quick Stats Bar when rendered on a Page -->
    <div v-if="!isModal" class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
          <FolderTree class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Total Groups</div>
          <div class="text-lg font-black text-white">{{ groups.length }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-blue-500/10 text-blue-400 border border-blue-500/20">
          <Building2 class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Root Plants</div>
          <div class="text-lg font-black text-white">{{ rootGroups.length }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
          <Layers class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Sub-Lines & Cells</div>
          <div class="text-lg font-black text-white">{{ Math.max(0, groups.length - rootGroups.length) }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-violet-500/10 text-violet-400 border border-violet-500/20">
          <Sparkles class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Cluster Tags</div>
          <div class="text-lg font-black text-white">{{ totalClustersCount }}</div>
        </div>
      </div>
    </div>

    <!-- Main Content Grid -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 mt-2" :class="isModal ? 'max-h-[70vh] overflow-y-auto pr-1' : ''">
      <!-- Hierarchy Tree View (7 cols on page, 6 on modal) -->
      <div :class="isModal ? 'lg:col-span-6 space-y-3' : 'lg:col-span-7 space-y-3'">
        <div class="flex items-center justify-between pb-2 border-b border-slate-800">
          <h4 class="text-xs font-black uppercase tracking-wider text-slate-300 flex items-center gap-2">
            <Layers class="w-4 h-4 text-indigo-400" />
            <span>Group Structure Hierarchy</span>
          </h4>
          <div class="flex items-center gap-2">
            <span class="text-[10px] text-slate-400 font-mono">{{ groups.length }} groups configured</span>
            <Button
              v-if="!isModal"
              size="sm"
              class="h-7 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-bold gap-1 rounded-lg"
              @click="startCreate(null)"
            >
              <Plus class="h-3.5 w-3.5" />
              Add Root Group
            </Button>
          </div>
        </div>

        <div v-if="isLoading" class="p-12 text-center text-slate-500 text-xs border border-dashed border-slate-800 rounded-2xl">
          Loading machine group hierarchy...
        </div>

        <div v-else-if="groups.length === 0" class="p-12 text-center text-slate-500 text-xs border border-dashed border-slate-800 rounded-2xl">
          No groups configured yet. Click "Add Root Group" to begin defining factory envelopes.
        </div>

        <div v-for="root in rootGroups" :key="root.id" class="rounded-2xl border border-slate-800/90 bg-slate-900/70 p-4 space-y-2.5 hover:border-slate-700 transition-colors">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2.5">
              <div class="w-3 h-3 rounded-full bg-blue-500 shadow-sm shadow-blue-500/50"></div>
              <span class="font-bold text-sm text-slate-100">{{ root.name }}</span>
              <Badge variant="outline" class="text-[9px] uppercase font-bold border-blue-500/30 text-blue-300 bg-blue-500/10">
                Plant Level
              </Badge>
            </div>
            <div class="flex items-center gap-1">
              <button
                class="p-1.5 rounded-lg text-slate-400 hover:text-indigo-400 hover:bg-indigo-950/30 transition"
                title="Add sub-group"
                @click="startCreate(root.id)"
              >
                <Plus class="h-3.5 w-3.5" />
              </button>
              <button
                class="p-1.5 rounded-lg text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition"
                title="Edit group"
                @click="startEdit(root)"
              >
                <Edit3 class="h-3.5 w-3.5" />
              </button>
              <button
                class="p-1.5 rounded-lg text-slate-400 hover:text-rose-400 hover:bg-rose-950/30 transition"
                title="Delete group"
                @click="deleteGroup(root.id)"
              >
                <Trash2 class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>

          <p v-if="root.description" class="text-xs text-slate-400 pl-5">{{ root.description }}</p>

          <!-- Nested Subgroups Level 1 (Lines) -->
          <div
            v-for="sub in getChildGroups(root.id)"
            :key="sub.id"
            class="ml-5 pl-4 border-l-2 border-indigo-500/30 py-2 space-y-2"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <div class="w-2.5 h-2.5 rounded-full bg-indigo-400"></div>
                <span class="font-semibold text-xs text-slate-200">{{ sub.name }}</span>
                <Badge v-if="sub.leadEngineerName" variant="outline" class="text-[10px] border-indigo-500/30 text-indigo-300 py-0 px-1.5 bg-indigo-500/10">
                  Lead: {{ sub.leadEngineerName }}
                </Badge>
              </div>
              <div class="flex items-center gap-1">
                <button
                  class="p-1 rounded text-slate-400 hover:text-indigo-400 hover:bg-indigo-950/30 transition"
                  title="Add cell sub-group"
                  @click="startCreate(sub.id)"
                >
                  <Plus class="h-3 w-3" />
                </button>
                <button
                  class="p-1 rounded text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition"
                  title="Edit group"
                  @click="startEdit(sub)"
                >
                  <Edit3 class="h-3 w-3" />
                </button>
                <button
                  class="p-1 rounded text-slate-400 hover:text-rose-400 hover:bg-rose-950/30 transition"
                  title="Delete group"
                  @click="deleteGroup(sub.id)"
                >
                  <Trash2 class="h-3 w-3" />
                </button>
              </div>
            </div>

            <!-- Technology cluster tags -->
            <div v-if="sub.machineTypes && sub.machineTypes.length > 0" class="flex flex-wrap gap-1.5 pl-4">
              <span
                v-for="mt in sub.machineTypes"
                :key="mt"
                class="text-[9px] font-mono px-2 py-0.5 rounded-md bg-slate-800 text-slate-300 border border-slate-700/60"
              >
                {{ mt }}
              </span>
            </div>

            <!-- Nested Subgroups Level 2 (Cells inside Line) -->
            <div
              v-for="cell in getChildGroups(sub.id)"
              :key="cell.id"
              class="ml-4 pl-3 border-l-2 border-purple-500/30 py-1.5 space-y-1.5"
            >
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-1.5">
                  <div class="w-2 h-2 rounded-full bg-purple-400"></div>
                  <span class="font-medium text-xs text-slate-300">{{ cell.name }}</span>
                </div>
                <div class="flex items-center gap-1">
                  <button
                    class="p-1 text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition"
                    title="Edit group"
                    @click="startEdit(cell)"
                  >
                    <Edit3 class="h-3 w-3" />
                  </button>
                  <button
                    class="p-1 text-slate-400 hover:text-rose-400 hover:bg-rose-950/30 transition"
                    title="Delete group"
                    @click="deleteGroup(cell.id)"
                  >
                    <Trash2 class="h-3 w-3" />
                  </button>
                </div>
              </div>
              <div v-if="cell.machineTypes && cell.machineTypes.length > 0" class="flex flex-wrap gap-1 pl-3">
                <span
                  v-for="cmt in cell.machineTypes"
                  :key="cmt"
                  class="text-[8px] font-mono px-1.5 py-0.5 rounded bg-purple-500/10 text-purple-300 border border-purple-500/20"
                >
                  {{ cmt }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Create / Edit Form Panel (5 cols on page, 6 on modal) -->
      <div :class="isModal ? 'lg:col-span-6' : 'lg:col-span-5'">
        <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 space-y-4 sticky top-6">
          <div class="flex items-center justify-between pb-3 border-b border-slate-800">
            <h4 class="text-xs font-black uppercase tracking-wider text-slate-200 flex items-center gap-2">
              <Cpu class="w-4 h-4 text-indigo-400" />
              <span>{{ isCreating ? 'Create New Envelope Group' : isEditing ? 'Edit Envelope Group' : 'Group Configuration' }}</span>
            </h4>
            <Badge v-if="isCreating || isEditing" variant="outline" class="text-[9px] uppercase font-bold border-indigo-500/40 text-indigo-300 bg-indigo-500/10">
              {{ isCreating ? 'New' : 'Editing' }}
            </Badge>
          </div>

          <div v-if="!isCreating && !isEditing" class="p-10 text-center text-slate-500 text-xs space-y-3">
            <FolderTree class="w-8 h-8 text-slate-700 mx-auto" />
            <p>Select a group from the hierarchy tree to inspect or edit its properties, or start a new group.</p>
            <Button
              size="sm"
              class="bg-indigo-600 hover:bg-indigo-500 text-white font-bold text-xs"
              @click="startCreate(null)"
            >
              <Plus class="h-3.5 w-3.5 mr-1" />
              Create Root Group
            </Button>
          </div>

          <div v-else class="space-y-4">
            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Group Name</label>
              <Input
                v-model="form.name"
                placeholder="e.g. Line 06 — Module Assembly (AUDI)"
                class="bg-slate-900 border-slate-800 text-slate-100 text-xs rounded-xl"
              />
            </div>

            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Parent Envelope Group</label>
              <select
                v-model="form.parentId"
                class="w-full h-9 rounded-xl bg-slate-900 border border-slate-800 text-slate-200 text-xs px-3 focus:outline-none focus:border-indigo-500"
              >
                <option :value="null">None (Root Level Plant)</option>
                <option v-for="g in groups.filter(item => item.id !== selectedGroupId)" :key="g.id" :value="g.id">
                  {{ g.name }}
                </option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Lead Engineer / Dedicated Owner</label>
              <Input
                v-model="form.leadEngineerName"
                placeholder="e.g. Engineer Orwell"
                class="bg-slate-900 border-slate-800 text-slate-100 text-xs rounded-xl"
              />
            </div>

            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Description</label>
              <textarea
                v-model="form.description"
                placeholder="Operational purpose, line capacity, or special tooling notes..."
                class="w-full rounded-xl bg-slate-900 border border-slate-800 text-slate-100 text-xs p-3 min-h-[70px] focus:outline-none focus:border-indigo-500"
              ></textarea>
            </div>

            <!-- Machine Type Clusters A through X -->
            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase flex items-center justify-between">
                <span>Dedicated Machine Types (Cluster A through X)</span>
                <span class="text-[10px] text-indigo-400 font-mono">{{ form.machineTypes.length }} selected</span>
              </label>
              <div class="grid grid-cols-2 gap-1.5 p-2 rounded-xl bg-slate-900 border border-slate-800 max-h-48 overflow-y-auto">
                <button
                  v-for="mt in STANDARD_MACHINE_TYPES"
                  :key="mt"
                  type="button"
                  class="flex items-center gap-2 p-1.5 rounded-lg text-left transition-colors text-[11px]"
                  :class="form.machineTypes.includes(mt) ? 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-bold' : 'text-slate-400 hover:bg-slate-800/50'"
                  @click="toggleMachineType(mt)"
                >
                  <div
                    class="w-3.5 h-3.5 rounded border flex items-center justify-center shrink-0"
                    :class="form.machineTypes.includes(mt) ? 'bg-indigo-600 border-indigo-500 text-white' : 'border-slate-700'"
                  >
                    <Check v-if="form.machineTypes.includes(mt)" class="w-2.5 h-2.5" />
                  </div>
                  <span class="truncate">{{ mt }}</span>
                </button>
              </div>
            </div>

            <!-- Form Actions -->
            <div class="flex items-center justify-end gap-2 pt-3 border-t border-slate-800">
              <Button
                variant="outline"
                size="sm"
                class="border-slate-800 text-slate-400 hover:bg-slate-800 rounded-xl"
                @click="isCreating = false; isEditing = false"
              >
                Cancel
              </Button>
              <Button
                size="sm"
                class="bg-indigo-600 hover:bg-indigo-500 text-white font-bold rounded-xl"
                @click="saveGroup"
              >
                {{ isCreating ? 'Create Group' : 'Save Changes' }}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
