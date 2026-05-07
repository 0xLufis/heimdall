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
import { Label } from '~/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '~/components/ui/select'
import { MonitorIcon, CpuIcon, LinkIcon, ZapIcon, CheckIcon } from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  handle: string
  objectName: string
  machines: any[]
  clients: any[]
  initialType?: 'machine' | 'client'
  initialId?: string
  initialAssociations?: string[]
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'pin', targetType: 'machine' | 'client' | 'lateral', targetId: string, associatedIds: string[]): void
}>()

const targetType = ref<'machine' | 'client' | 'lateral'>('machine')
const targetId = ref('')
const associatedIds = ref<string[]>([])

const isSaving = ref(false)

// Reset state when opening
watch(() => props.open, (newVal) => {
  if (newVal) {
    targetType.value = props.initialType || 'machine'
    targetId.value = props.initialId || ''
    associatedIds.value = props.initialAssociations ? [...props.initialAssociations] : []
  }
})

const handleSave = async () => {
  if (!targetId.value && targetType.value !== 'lateral') return
  isSaving.value = true
  try {
    await emit('pin', targetType.value, targetId.value, associatedIds.value)
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

// Find existing pins for lateral link sources
const pinnedObjects = computed(() => {
    const list: any[] = []
    props.clients.forEach(c => {
        if (c.pinnedObjectHandle) list.push({ id: c.id, name: c.hostname, handle: c.pinnedObjectHandle, type: 'client' })
    })
    props.machines.forEach(m => {
        if (m.pinnedObjectHandle) list.push({ id: m.id, name: m.customIdentifier, handle: m.pinnedObjectHandle, type: 'machine' })
    })
    return list
})
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="sm:max-w-[500px] border-slate-800 bg-slate-900 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
      <DialogHeader class="bg-indigo-950 p-8 border-b border-slate-800">
        <DialogTitle class="text-2xl font-black uppercase tracking-tight text-slate-100 flex items-center gap-2">
          <div class="p-2 bg-indigo-500/20 rounded-xl text-indigo-400">
            <LinkIcon class="h-5 w-5" />
          </div>
          Coordinate Mapping
        </DialogTitle>
        <DialogDescription class="text-indigo-400 text-[10px] font-black uppercase tracking-widest mt-2 opacity-80">
          Linking handle <span class="text-indigo-300 font-mono">{{ handle }}</span> ({{ objectName }})
        </DialogDescription>
      </DialogHeader>

      <div class="grid gap-6 p-8 overflow-y-auto max-h-[60vh]">
        <div class="space-y-4">
          <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Mapping Mode</Label>
          <div class="flex p-1 bg-slate-950 rounded-2xl border border-slate-800 gap-1">
            <Button 
              variant="ghost"
              @click="targetType = 'machine'; targetId = ''"
              :class="targetType === 'machine' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <CpuIcon class="h-3.5 w-3.5" />
              Station
            </Button>
            <Button 
              variant="ghost"
              @click="targetType = 'client'; targetId = ''"
              :class="targetType === 'client' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <MonitorIcon class="h-3.5 w-3.5" />
              Controller
            </Button>
            <Button 
              variant="ghost"
              @click="targetType = 'lateral'; targetId = ''"
              :class="targetType === 'lateral' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <ZapIcon class="h-3.5 w-3.5" />
              Lateral
            </Button>
          </div>
        </div>

        <!-- Machine/Client Selection -->
        <div v-if="targetType !== 'lateral'" class="space-y-6">
          <div class="space-y-4">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Primary Asset (Anchor)</Label>
            <Select v-model="targetId">
              <SelectTrigger class="w-full h-14 bg-slate-950 border-slate-800 rounded-2xl font-bold text-sm text-slate-200 focus:ring-indigo-500">
                <SelectValue placeholder="Select target..." />
              </SelectTrigger>
              <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                <template v-if="targetType === 'machine'">
                  <SelectItem v-for="m in machines" :key="m.id" :value="m.id" class="text-xs font-bold focus:bg-indigo-600 focus:text-white">
                    {{ m.customIdentifier }}
                  </SelectItem>
                </template>
                <template v-else>
                  <SelectItem v-for="c in clients" :key="c.id" :value="c.id" class="text-xs font-bold focus:bg-indigo-600 focus:text-white">
                    {{ c.hostname }}
                  </SelectItem>
                </template>
              </SelectContent>
            </Select>
          </div>

          <!-- Multiple Associations -->
          <div v-if="targetId" class="space-y-4 animate-in fade-in slide-in-from-top-2 duration-300">
             <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">
                {{ targetType === 'machine' ? 'Link Controlling PCs' : 'Link Controlled Stations' }}
             </Label>
             <div class="grid grid-cols-1 gap-2 max-h-40 overflow-y-auto pr-2 custom-scrollbar">
                <template v-if="targetType === 'machine'">
                   <div v-for="c in clients" :key="c.id" 
                        class="flex items-center space-x-3 p-3 bg-slate-950 rounded-xl border border-slate-800 hover:border-slate-700 transition-colors cursor-pointer group/item"
                        @click="toggleAssociation(c.id)"
                   >
                      <div class="h-4 w-4 rounded border border-slate-700 transition-colors flex items-center justify-center"
                           :class="associatedIds.includes(c.id) ? 'bg-indigo-600 border-indigo-600' : 'bg-transparent group-hover/item:border-slate-500'">
                         <CheckIcon v-if="associatedIds.includes(c.id)" class="h-3 w-3 text-white" />
                      </div>
                      <div class="flex flex-col">
                         <span class="text-xs font-bold text-slate-200">{{ c.hostname }}</span>
                         <span class="text-[9px] text-slate-500 uppercase font-mono">{{ c.macAddress }}</span>
                      </div>
                   </div>
                </template>
                <template v-else>
                   <div v-for="m in machines" :key="m.id" 
                        class="flex items-center space-x-3 p-3 bg-slate-950 rounded-xl border border-slate-800 hover:border-slate-700 transition-colors cursor-pointer group/item"
                        @click="toggleAssociation(m.id)"
                   >
                      <div class="h-4 w-4 rounded border border-slate-700 transition-colors flex items-center justify-center"
                           :class="associatedIds.includes(m.id) ? 'bg-indigo-600 border-indigo-600' : 'bg-transparent group-hover/item:border-slate-500'">
                         <CheckIcon v-if="associatedIds.includes(m.id)" class="h-3 w-3 text-white" />
                      </div>
                      <span class="text-xs font-bold text-slate-200">{{ m.customIdentifier }}</span>
                   </div>
                </template>
             </div>
          </div>
        </div>

        <!-- Lateral Link Mode -->
        <div v-else class="space-y-6 animate-in fade-in duration-300">
           <div class="p-4 bg-indigo-950/30 border border-indigo-900/50 rounded-2xl text-[10px] text-indigo-300 font-bold uppercase tracking-wider leading-relaxed">
              Define a lateral relationship between the current object ({{ handle }}) and another pinned coordinate.
           </div>
           
           <div class="space-y-4">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Link Target</Label>
              <Select v-model="targetId">
                <SelectTrigger class="w-full h-14 bg-slate-950 border-slate-800 rounded-2xl font-bold text-sm text-slate-200 focus:ring-indigo-500">
                  <SelectValue placeholder="Select peer object..." />
                </SelectTrigger>
                <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                  <SelectItem v-for="obj in pinnedObjects.filter(o => o.handle !== handle)" :key="obj.id" :value="obj.id" class="text-xs font-bold focus:bg-indigo-600 focus:text-white">
                    {{ obj.name }} ({{ obj.type }})
                  </SelectItem>
                </SelectContent>
              </Select>
           </div>
        </div>
      </div>

      <DialogFooter class="p-8 pt-0 flex gap-4">
        <Button 
          variant="ghost" 
          @click="emit('update:open', false)"
          class="flex-1 rounded-2xl h-12 text-[10px] font-black text-slate-500 uppercase tracking-widest hover:bg-slate-800 hover:text-slate-300 transition-all"
        >
          Cancel
        </Button>
        <Button 
          @click="handleSave"
          :disabled="(!targetId && targetType !== 'lateral') || isSaving"
          class="flex-1 bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl h-12 shadow-xl shadow-indigo-500/20 transition-all font-black uppercase tracking-widest text-[10px]"
        >
          {{ isSaving ? 'Commiting...' : 'Apply Mapping' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
