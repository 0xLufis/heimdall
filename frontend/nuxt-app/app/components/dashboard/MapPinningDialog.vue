<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { 
  Dialog, 
  DialogContent, 
  DialogHeader, 
  DialogTitle, 
  DialogDescription, 
  DialogFooter 
} from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '~/components/ui/select'
import { MonitorIcon, CpuIcon, LinkIcon, ZapIcon, CheckIcon, MapPin, Trash2, Sparkles } from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  handle?: string
  objectName?: string
  machines: any[]
  clients: any[]
  initialType?: 'machine' | 'client'
  initialId?: string
  initialAssociations?: string[]
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'pin', targetType: 'machine' | 'client' | 'lateral', targetId: string, associatedIds: string[], handle?: string): void
  (e: 'unpin', targetType: 'machine' | 'client', targetId: string): void
}>()

const targetType = ref<'machine' | 'client' | 'lateral'>('client')
const targetId = ref('')
const currentHandle = ref('')
const currentObjectName = ref('')
const associatedIds = ref<string[]>([])

const isSaving = ref(false)

// Reset state when opening
watch(() => props.open, (newVal) => {
  if (newVal) {
    targetType.value = props.initialType || 'client'
    targetId.value = props.initialId || ''
    currentHandle.value = props.handle || ''
    currentObjectName.value = props.objectName || ''
    associatedIds.value = props.initialAssociations ? [...props.initialAssociations] : []

    // If an initialId was passed for client, pre-fill its current handle and associations if not already set
    if (props.initialId) {
      if (targetType.value === 'client') {
        const pc = props.clients.find(c => c.id === props.initialId)
        if (pc) {
          if (!currentHandle.value && pc.pinnedObjectHandle) {
            currentHandle.value = pc.pinnedObjectHandle
          }
          if (associatedIds.value.length === 0 && pc.controlledMachines) {
            associatedIds.value = pc.controlledMachines.map((m: any) => m.id)
          }
        }
      } else if (targetType.value === 'machine') {
        const m = props.machines.find(mach => mach.id === props.initialId)
        if (m) {
          if (!currentHandle.value && m.pinnedObjectHandle) {
            currentHandle.value = m.pinnedObjectHandle
          }
          if (associatedIds.value.length === 0 && m.controllers) {
            associatedIds.value = m.controllers.map((c: any) => c.id || c.controllerId)
          }
        }
      }
    }
  }
})

// When targetId changes, if no handle is explicitly provided, fill from target's existing handle
watch(() => targetId.value, (newId) => {
  if (!newId || props.handle) return
  if (targetType.value === 'client') {
    const pc = props.clients.find(c => c.id === newId)
    if (pc?.pinnedObjectHandle && !currentHandle.value) {
      currentHandle.value = pc.pinnedObjectHandle
    }
  } else if (targetType.value === 'machine') {
    const m = props.machines.find(mach => mach.id === newId)
    if (m?.pinnedObjectHandle && !currentHandle.value) {
      currentHandle.value = m.pinnedObjectHandle
    }
  }
})

const handleSave = async () => {
  if (!targetId.value && targetType.value !== 'lateral') return
  isSaving.value = true
  try {
    await emit('pin', targetType.value, targetId.value, associatedIds.value, currentHandle.value)
    emit('update:open', false)
  } finally {
    isSaving.value = false
  }
}

const handleUnpin = async () => {
  if (!targetId.value || targetType.value === 'lateral') return
  isSaving.value = true
  try {
    await emit('unpin', targetType.value, targetId.value)
    emit('update:open', false)
  } finally {
    isSaving.value = false
  }
}

const toggleAssociation = (id: string) => {
  const index = associatedIds.value.indexOf(id)
  if (index === -1) {
    associatedIds.value.push(id)
  } else {
    associatedIds.value.splice(index, 1)
  }
}

// Check if currently selected asset has an existing pin
const isCurrentlyPinned = computed(() => {
  if (!targetId.value) return false
  if (targetType.value === 'client') {
    const pc = props.clients.find(c => c.id === targetId.value)
    return Boolean(pc?.pinnedObjectHandle)
  } else if (targetType.value === 'machine') {
    const m = props.machines.find(mach => mach.id === targetId.value)
    return Boolean(m?.pinnedObjectHandle)
  }
  return false
})

// Find existing pins for lateral link sources
const pinnedObjects = computed(() => {
  const list: any[] = []
  props.clients.forEach(c => {
    if (c.pinnedObjectHandle) list.push({ id: c.id, name: c.hostname || c.name, handle: c.pinnedObjectHandle, type: 'client' })
  })
  props.machines.forEach(m => {
    if (m.pinnedObjectHandle) list.push({ id: m.id, name: m.customIdentifier || m.name, handle: m.pinnedObjectHandle, type: 'machine' })
  })
  return list
})
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="sm:max-w-[540px] border-slate-800 bg-slate-900 text-slate-100 p-0 overflow-hidden rounded-[2rem] shadow-2xl">
      <DialogHeader class="bg-indigo-950/70 p-7 border-b border-slate-800">
        <DialogTitle class="text-2xl font-black uppercase tracking-tight text-slate-100 flex items-center gap-2.5">
          <div class="p-2.5 bg-indigo-500/20 rounded-2xl text-indigo-400 border border-indigo-500/30">
            <MapPin class="h-5 w-5" />
          </div>
          <span>Spatial CAD & DXF Pinning</span>
        </DialogTitle>
        <DialogDescription class="text-indigo-400 text-xs font-bold uppercase tracking-widest mt-1.5 opacity-90">
          Link Controller PCs & Stations to AutoCAD Layout Object Handles
        </DialogDescription>
      </DialogHeader>

      <div class="grid gap-6 p-7 overflow-y-auto max-h-[62vh] custom-scrollbar">
        
        <!-- Mapping Target Category -->
        <div class="space-y-3">
          <Label class="text-[10px] font-black text-slate-400 uppercase tracking-widest ml-1">Asset Mapping Mode</Label>
          <div class="flex p-1 bg-slate-950 rounded-2xl border border-slate-800 gap-1">
            <Button 
              type="button"
              variant="ghost"
              @click="targetType = 'client'"
              :class="targetType === 'client' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <MonitorIcon class="h-3.5 w-3.5" />
              Controller PC
            </Button>
            <Button 
              type="button"
              variant="ghost"
              @click="targetType = 'machine'"
              :class="targetType === 'machine' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <CpuIcon class="h-3.5 w-3.5" />
              Station / Machine
            </Button>
            <Button 
              type="button"
              variant="ghost"
              @click="targetType = 'lateral'"
              :class="targetType === 'lateral' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-400 hover:text-slate-200'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <ZapIcon class="h-3.5 w-3.5" />
              Lateral Link
            </Button>
          </div>
        </div>

        <!-- DXF Handle Tag Input / Indicator -->
        <div class="space-y-2">
          <div class="flex items-center justify-between">
            <Label class="text-[10px] font-black text-slate-400 uppercase tracking-widest ml-1">AutoCAD (DXF) Tag Handle</Label>
            <span v-if="currentObjectName" class="text-[9px] font-mono text-indigo-400 truncate max-w-[200px]">
              Block: {{ currentObjectName }}
            </span>
          </div>
          <div class="relative">
            <Input
              v-model="currentHandle"
              placeholder="e.g. OP10_MAIN_PLC or OP20_ROBOT_PANEL"
              class="w-full h-12 bg-slate-950 border-slate-800 rounded-2xl font-mono font-bold text-xs text-indigo-300 pl-10 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
            />
            <MapPin class="size-4 text-indigo-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          </div>
          <p class="text-[9px] text-slate-500 ml-1">The unique spatial entity handle identifier extracted from the DXF floor plan.</p>
        </div>

        <!-- Machine/Client Selection -->
        <div v-if="targetType !== 'lateral'" class="space-y-6">
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-slate-400 uppercase tracking-widest ml-1">
              Select {{ targetType === 'client' ? 'Controller PC' : 'Production Station' }} to Map
            </Label>
            <Select v-model="targetId">
              <SelectTrigger class="w-full h-12 bg-slate-950 border-slate-800 rounded-2xl font-bold text-xs text-slate-200 focus:ring-indigo-500">
                <SelectValue placeholder="Select target node..." />
              </SelectTrigger>
              <SelectContent class="bg-slate-950 border-slate-800 text-slate-300 max-h-60">
                <template v-if="targetType === 'client'">
                  <SelectItem v-for="c in clients" :key="c.id" :value="c.id" class="text-xs font-bold focus:bg-indigo-600 focus:text-white">
                    {{ c.hostname || c.name }} {{ c.ipAddress ? `(${c.ipAddress})` : '' }} {{ c.pinnedObjectHandle ? `[Pinned: ${c.pinnedObjectHandle}]` : '' }}
                  </SelectItem>
                </template>
                <template v-else>
                  <SelectItem v-for="m in machines" :key="m.id" :value="m.id" class="text-xs font-bold focus:bg-indigo-600 focus:text-white">
                    {{ m.customIdentifier || m.name }} {{ m.pinnedObjectHandle ? `[Pinned: ${m.pinnedObjectHandle}]` : '' }}
                  </SelectItem>
                </template>
              </SelectContent>
            </Select>
          </div>

          <!-- Multiple Associations -->
          <div v-if="targetId" class="space-y-3 animate-in fade-in slide-in-from-top-2 duration-300">
            <div class="flex items-center justify-between">
              <Label class="text-[10px] font-black text-slate-400 uppercase tracking-widest ml-1">
                {{ targetType === 'client' ? 'Associate Controlled Stations (Multi-Select)' : 'Associate Controlling IPCs (Multi-Select)' }}
              </Label>
              <span class="text-[9px] text-slate-500 font-mono">{{ associatedIds.length }} Selected</span>
            </div>
            
            <div class="grid grid-cols-1 gap-2 max-h-44 overflow-y-auto pr-2 custom-scrollbar bg-slate-950/60 p-2 rounded-2xl border border-slate-800/80">
              <template v-if="targetType === 'client'">
                <div
                  v-for="m in machines"
                  :key="m.id"
                  @click="toggleAssociation(m.id)"
                  class="flex items-center justify-between p-2.5 bg-slate-900 rounded-xl border border-slate-800/80 hover:border-indigo-500/40 transition-colors cursor-pointer group/item"
                >
                  <div class="flex items-center gap-2.5 truncate">
                    <div
                      class="h-4 w-4 rounded-md border flex items-center justify-center shrink-0 transition-colors"
                      :class="associatedIds.includes(m.id) ? 'bg-indigo-600 border-indigo-600' : 'border-slate-700 bg-slate-950 group-hover/item:border-slate-500'"
                    >
                      <CheckIcon v-if="associatedIds.includes(m.id)" class="h-3 w-3 text-white" />
                    </div>
                    <span class="text-xs font-bold text-slate-200 truncate">{{ m.customIdentifier || m.name }}</span>
                  </div>
                  <span v-if="m.pinnedObjectHandle" class="text-[8px] font-mono text-slate-500 bg-slate-950 px-2 py-0.5 rounded-full border border-slate-800 shrink-0">
                    {{ m.pinnedObjectHandle }}
                  </span>
                </div>
              </template>
              <template v-else>
                <div
                  v-for="c in clients"
                  :key="c.id"
                  @click="toggleAssociation(c.id)"
                  class="flex items-center justify-between p-2.5 bg-slate-900 rounded-xl border border-slate-800/80 hover:border-indigo-500/40 transition-colors cursor-pointer group/item"
                >
                  <div class="flex items-center gap-2.5 truncate">
                    <div
                      class="h-4 w-4 rounded-md border flex items-center justify-center shrink-0 transition-colors"
                      :class="associatedIds.includes(c.id) ? 'bg-indigo-600 border-indigo-600' : 'border-slate-700 bg-slate-950 group-hover/item:border-slate-500'"
                    >
                      <CheckIcon v-if="associatedIds.includes(c.id)" class="h-3 w-3 text-white" />
                    </div>
                    <div class="flex flex-col truncate">
                      <span class="text-xs font-bold text-slate-200 truncate">{{ c.hostname || c.name }}</span>
                      <span class="text-[8px] text-slate-500 font-mono">{{ c.macAddress || c.ipAddress }}</span>
                    </div>
                  </div>
                  <span v-if="c.pinnedObjectHandle" class="text-[8px] font-mono text-slate-500 bg-slate-950 px-2 py-0.5 rounded-full border border-slate-800 shrink-0">
                    {{ c.pinnedObjectHandle }}
                  </span>
                </div>
              </template>
            </div>
          </div>
        </div>

        <!-- Lateral Link Mode -->
        <div v-else class="space-y-5 animate-in fade-in duration-300">
          <div class="p-4 bg-indigo-950/30 border border-indigo-900/50 rounded-2xl text-[10px] text-indigo-300 font-bold uppercase tracking-wider leading-relaxed">
            Define a lateral peer dependency between DXF handle <span class="font-mono text-white">{{ currentHandle || 'N/A' }}</span> and another pinned node.
          </div>
          
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-slate-400 uppercase tracking-widest ml-1">Peer Link Target</Label>
            <Select v-model="targetId">
              <SelectTrigger class="w-full h-12 bg-slate-950 border-slate-800 rounded-2xl font-bold text-xs text-slate-200 focus:ring-indigo-500">
                <SelectValue placeholder="Select peer object..." />
              </SelectTrigger>
              <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                <SelectItem v-for="obj in pinnedObjects.filter(o => o.handle !== currentHandle)" :key="obj.id" :value="obj.id" class="text-xs font-bold focus:bg-indigo-600 focus:text-white">
                  {{ obj.name }} ({{ obj.type }} • {{ obj.handle }})
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      <DialogFooter class="p-7 pt-2 flex flex-col sm:flex-row items-center gap-3 border-t border-slate-800/80 bg-slate-950/50">
        <Button 
          v-if="isCurrentlyPinned && targetType !== 'lateral'"
          type="button"
          variant="outline" 
          @click="handleUnpin"
          :disabled="isSaving"
          class="w-full sm:w-auto rounded-xl h-11 text-[10px] font-black text-rose-400 border-rose-900/40 bg-rose-950/20 hover:bg-rose-950/50 uppercase tracking-wider flex items-center gap-1.5 px-4"
        >
          <Trash2 class="size-3.5" />
          <span>Unpin DXF</span>
        </Button>

        <div class="flex-1 hidden sm:block"></div>

        <div class="flex items-center gap-2.5 w-full sm:w-auto justify-end">
          <Button 
            type="button"
            variant="ghost" 
            @click="emit('update:open', false)"
            class="rounded-xl h-11 text-[10px] font-black text-slate-400 uppercase tracking-widest hover:bg-slate-800 hover:text-slate-200 transition-all px-4"
          >
            Cancel
          </Button>
          <Button 
            type="button"
            @click="handleSave"
            :disabled="(!targetId && targetType !== 'lateral') || isSaving || !currentHandle"
            class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl h-11 shadow-lg shadow-indigo-500/20 transition-all font-black uppercase tracking-wider text-[10px] px-6 flex items-center gap-2"
          >
            <CheckIcon class="size-3.5" />
            <span>{{ isSaving ? 'Committing...' : 'Apply Mapping' }}</span>
          </Button>
        </div>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 5px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(15, 23, 42, 0.4);
  border-radius: 9999px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(99, 102, 241, 0.5);
  border-radius: 9999px;
}
</style>
