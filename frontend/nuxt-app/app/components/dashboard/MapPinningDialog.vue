<script setup lang="ts">
import { ref, computed } from 'vue'
import { 
  Dialog, 
  DialogContent, 
  DialogHeader, 
  DialogTitle, 
  DialogDescription, 
  DialogFooter 
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { MonitorIcon, CpuIcon, LinkIcon } from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  handle: string
  objectName: string
  machines: any[]
  clients: any[]
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'pin', targetType: 'machine' | 'client', targetId: string): void
}>()

const targetType = ref<'machine' | 'client'>('machine')
const targetId = ref('')

const isSaving = ref(false)

const handleSave = async () => {
  if (!targetId.value) return
  isSaving.value = true
  try {
    await emit('pin', targetType.value, targetId.value)
    emit('update:open', false)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="sm:max-w-[425px] border-indigo-100 bg-white">
      <DialogHeader>
        <DialogTitle class="text-2xl font-black uppercase tracking-tight text-gray-900 flex items-center gap-2">
          <div class="p-2 bg-indigo-50 rounded-xl">
            <LinkIcon class="h-5 w-5 text-indigo-600" />
          </div>
          Connect Object
        </DialogTitle>
        <DialogDescription class="text-xs font-bold text-gray-400 uppercase tracking-widest mt-1">
          Mapping handle <span class="text-indigo-600 font-mono">{{ handle }}</span> ({{ objectName }})
        </DialogDescription>
      </DialogHeader>

      <div class="grid gap-6 py-6">
        <div class="space-y-4">
          <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Identifier Type</Label>
          <div class="flex p-1 bg-gray-50 rounded-2xl border border-gray-100 gap-1">
            <button 
              @click="targetType = 'machine'; targetId = ''"
              :class="targetType === 'machine' ? 'bg-white text-indigo-600 shadow-sm border-gray-100' : 'text-gray-400 hover:text-gray-600'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all border border-transparent"
            >
              <CpuIcon class="h-3.5 w-3.5" />
              Machine
            </button>
            <button 
              @click="targetType = 'client'; targetId = ''"
              :class="targetType === 'client' ? 'bg-white text-indigo-600 shadow-sm border-gray-100' : 'text-gray-400 hover:text-gray-600'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all border border-transparent"
            >
              <MonitorIcon class="h-3.5 w-3.5" />
              Client PC
            </button>
          </div>
        </div>

        <div class="space-y-4">
          <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Select Target Asset</Label>
          <div class="relative">
            <select 
              v-model="targetId"
              class="w-full appearance-none px-4 py-4 bg-gray-50 border border-gray-100 rounded-2xl focus:ring-4 focus:ring-indigo-500/5 focus:border-indigo-500 outline-none transition-all font-bold text-sm"
            >
              <option value="" disabled>Select an available asset...</option>
              <template v-if="targetType === 'machine'">
                <option v-for="m in machines" :key="m.id" :value="m.id">
                  {{ m.customIdentifier }}
                </option>
              </template>
              <template v-else>
                <option v-for="c in clients" :key="c.id" :value="c.id">
                  {{ c.hostname }} ({{ c.macAddress }})
                </option>
              </template>
            </select>
            <div class="absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none text-gray-400">
               <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                 <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
               </svg>
            </div>
          </div>
        </div>
      </div>

      <DialogFooter>
        <Button 
          variant="ghost" 
          @click="emit('update:open', false)"
          class="rounded-2xl py-6 text-xs font-black text-gray-400 uppercase tracking-widest hover:bg-gray-50 transition-all"
        >
          Abort
        </Button>
        <Button 
          @click="handleSave"
          :disabled="!targetId || isSaving"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl px-8 py-6 h-auto shadow-lg shadow-indigo-100 transition-all font-black uppercase tracking-widest text-xs"
        >
          {{ isSaving ? 'Commiting...' : 'Link Asset' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
