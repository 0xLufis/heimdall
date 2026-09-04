<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  FolderTree, Plus, Trash2, Edit3, Check, X, ChevronRight,
  ChevronDown, Layers, Cpu, Shield, ArrowRight
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription
} from '~/components/ui/dialog'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '~/components/ui/select'
import type { MachineGroup } from '~/types/maintenance'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'groupSelected', groupId: string): void
}>()

const isOpen = computed({
  get: () => props.open,
  set: (v) => { if (!v) emit('close') }
})

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
  if (props.open) fetchGroups()
})

const rootGroups = computed(() => groups.value.filter(g => !g.parentId))

function getChildGroups(parentId: string): MachineGroup[] {
  return groups.value.filter(g => g.parentId === parentId)
}

function startCreate(parentId: string | null = null) {
  isCreating.value = true
  isEditing.value = false
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
</script>

<template>
  <Dialog v-model:open="isOpen">
    <DialogContent class="max-w-4xl bg-slate-950 border-slate-800 text-slate-100 p-6 shadow-2xl">
      <DialogHeader class="pb-4 border-b border-slate-800 flex flex-row items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
            <FolderTree class="h-6 w-6" />
          </div>
          <div>
            <DialogTitle class="text-lg font-black uppercase tracking-tight text-slate-100">
              Machine Groups & Envelope Hierarchy
            </DialogTitle>
            <DialogDescription class="text-xs text-slate-400 mt-0.5">
              Envelop machines in cells, lines, and plants with technology type clusters
            </DialogDescription>
          </div>
        </div>
        <Button
          size="sm"
          class="bg-indigo-600 hover:bg-indigo-500 text-white font-bold gap-1.5"
          @click="startCreate(null)"
        >
          <Plus class="h-4 w-4" />
          Add Root Group
        </Button>
      </DialogHeader>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mt-4 max-h-[70vh] overflow-y-auto pr-1">
        <!-- Hierarchy Tree View -->
        <div class="space-y-3">
          <h4 class="text-xs font-black uppercase tracking-wider text-slate-400 flex items-center justify-between">
            <span>Group Structure</span>
            <span class="text-[10px] text-slate-500 font-mono">{{ groups.length }} groups configured</span>
          </h4>

          <div v-if="groups.length === 0" class="p-8 text-center text-slate-600 text-xs border border-dashed border-slate-800 rounded-xl">
            No groups configured yet. Click "Add Root Group" to begin.
          </div>

          <div v-for="root in rootGroups" :key="root.id" class="rounded-xl border border-slate-800/80 bg-slate-900/60 p-3 space-y-2">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <div class="w-2.5 h-2.5 rounded-full bg-blue-500"></div>
                <span class="font-bold text-sm text-slate-200">{{ root.name }}</span>
              </div>
              <div class="flex items-center gap-1">
                <button
                  class="p-1 text-slate-400 hover:text-indigo-400 transition"
                  title="Add sub-group"
                  @click="startCreate(root.id)"
                >
                  <Plus class="h-3.5 w-3.5" />
                </button>
                <button
                  class="p-1 text-slate-400 hover:text-slate-200 transition"
                  title="Edit group"
                  @click="startEdit(root)"
                >
                  <Edit3 class="h-3.5 w-3.5" />
                </button>
                <button
                  class="p-1 text-slate-400 hover:text-rose-400 transition"
                  title="Delete group"
                  @click="deleteGroup(root.id)"
                >
                  <Trash2 class="h-3.5 w-3.5" />
                </button>
              </div>
            </div>

            <p v-if="root.description" class="text-xs text-slate-400 pl-4">{{ root.description }}</p>

            <!-- Nested Subgroups Level 1 -->
            <div
              v-for="sub in getChildGroups(root.id)"
              :key="sub.id"
              class="ml-4 pl-3 border-l-2 border-slate-800 py-1.5 space-y-1.5"
            >
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <div class="w-2 h-2 rounded-full bg-indigo-400"></div>
                  <span class="font-semibold text-xs text-slate-200">{{ sub.name }}</span>
                  <Badge v-if="sub.leadEngineerName" variant="outline" class="text-[10px] border-indigo-500/30 text-indigo-300 py-0 px-1.5">
                    Lead: {{ sub.leadEngineerName }}
                  </Badge>
                </div>
                <div class="flex items-center gap-1">
                  <button
                    class="p-1 text-slate-400 hover:text-indigo-400 transition"
                    title="Add sub-group"
                    @click="startCreate(sub.id)"
                  >
                    <Plus class="h-3 w-3" />
                  </button>
                  <button
                    class="p-1 text-slate-400 hover:text-slate-200 transition"
                    title="Edit group"
                    @click="startEdit(sub)"
                  >
                    <Edit3 class="h-3 w-3" />
                  </button>
                  <button
                    class="p-1 text-slate-400 hover:text-rose-400 transition"
                    title="Delete group"
                    @click="deleteGroup(sub.id)"
                  >
                    <Trash2 class="h-3 w-3" />
                  </button>
                </div>
              </div>

              <!-- Technology cluster tags -->
              <div v-if="sub.machineTypes && sub.machineTypes.length > 0" class="flex flex-wrap gap-1 pl-4">
                <span
                  v-for="mt in sub.machineTypes"
                  :key="mt"
                  class="text-[9px] font-mono px-1.5 py-0.5 rounded bg-slate-800/80 text-slate-400 border border-slate-700/50"
                >
                  {{ mt }}
                </span>
              </div>

              <!-- Nested Subgroups Level 2 (e.g. Cells inside Line) -->
              <div
                v-for="cell in getChildGroups(sub.id)"
                :key="cell.id"
                class="ml-4 pl-3 border-l-2 border-slate-700 py-1 space-y-1"
              >
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-1.5">
                    <div class="w-1.5 h-1.5 rounded-full bg-purple-400"></div>
                    <span class="font-medium text-xs text-slate-300">{{ cell.name }}</span>
                  </div>
                  <div class="flex items-center gap-1">
                    <button
                      class="p-1 text-slate-400 hover:text-slate-200 transition"
                      title="Edit group"
                      @click="startEdit(cell)"
                    >
                      <Edit3 class="h-3 w-3" />
                    </button>
                    <button
                      class="p-1 text-slate-400 hover:text-rose-400 transition"
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
                    class="text-[8px] font-mono px-1 rounded bg-purple-500/10 text-purple-300 border border-purple-500/20"
                  >
                    {{ cmt }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Create / Edit Form Panel -->
        <div class="rounded-xl border border-slate-800 bg-slate-900/40 p-4 space-y-4">
          <h4 class="text-xs font-black uppercase tracking-wider text-slate-300 pb-2 border-b border-slate-800">
            {{ isCreating ? 'Create New Group' : isEditing ? 'Edit Group Settings' : 'Group Configuration' }}
          </h4>

          <div v-if="!isCreating && !isEditing" class="p-8 text-center text-slate-500 text-xs">
            Select a group from the list on the left to edit, or click "Add Root Group" to create one.
          </div>

          <div v-else class="space-y-4">
            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Group Name</label>
              <Input
                v-model="form.name"
                placeholder="e.g. Line 06 — Module Assembly (AUDI)"
                class="bg-slate-900 border-slate-800 text-slate-100 text-xs"
              />
            </div>

            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Parent Envelope Group</label>
              <select
                v-model="form.parentId"
                class="w-full h-9 rounded-lg bg-slate-900 border border-slate-800 text-slate-200 text-xs px-3 focus:outline-none focus:border-indigo-500"
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
                class="bg-slate-900 border-slate-800 text-slate-100 text-xs"
              />
            </div>

            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase">Description</label>
              <textarea
                v-model="form.description"
                placeholder="Operational purpose, line capacity, or special tooling notes..."
                class="w-full rounded-lg bg-slate-900 border border-slate-800 text-slate-100 text-xs p-2.5 min-h-[60px] focus:outline-none focus:border-indigo-500"
              ></textarea>
            </div>

            <!-- Machine Type Clusters A through X -->
            <div class="space-y-1.5">
              <label class="text-[11px] font-bold text-slate-400 uppercase flex items-center justify-between">
                <span>Dedicated Machine Types (Cluster A through X)</span>
                <span class="text-[10px] text-indigo-400 font-mono">{{ form.machineTypes.length }} selected</span>
              </label>
              <div class="grid grid-cols-2 gap-1.5 p-2 rounded-lg bg-slate-900 border border-slate-800/80 max-h-40 overflow-y-auto">
                <button
                  v-for="mt in STANDARD_MACHINE_TYPES"
                  :key="mt"
                  type="button"
                  class="flex items-center gap-2 p-1.5 rounded text-left transition-colors text-[11px]"
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
            <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-800">
              <Button
                variant="outline"
                size="sm"
                class="border-slate-800 text-slate-400 hover:bg-slate-800"
                @click="isCreating = false; isEditing = false"
              >
                Cancel
              </Button>
              <Button
                size="sm"
                class="bg-indigo-600 hover:bg-indigo-500 text-white font-bold"
                @click="saveGroup"
              >
                {{ isCreating ? 'Create Group' : 'Save Changes' }}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </DialogContent>
  </Dialog>
</template>
