<script setup lang="ts">
import { ref, computed } from 'vue'
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
    <DialogContent class="sm:max-w-[425px] border-slate-800 bg-slate-900 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
      <DialogHeader class="bg-indigo-950 p-8 border-b border-slate-800">
        <DialogTitle class="text-2xl font-black uppercase tracking-tight text-slate-100 flex items-center gap-2">
          <div class="p-2 bg-indigo-500/20 rounded-xl text-indigo-400">
            <LinkIcon class="h-5 w-5" />
          </div>
          Connect Object
        </DialogTitle>
        <DialogDescription class="text-indigo-400 text-[10px] font-black uppercase tracking-widest mt-2 opacity-80">
          Mapping handle <span class="text-indigo-300 font-mono">{{ handle }}</span> ({{ objectName }})
        </DialogDescription>
      </DialogHeader>

      <div class="grid gap-6 p-8">
        <div class="space-y-4">
          <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Identifier Type</Label>
          <div class="flex p-1 bg-slate-950 rounded-2xl border border-slate-800 gap-1">
            <Button 
              variant="ghost"
              @click="targetType = 'machine'; targetId = ''"
              :class="targetType === 'machine' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300 hover:bg-slate-900'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <CpuIcon class="h-3.5 w-3.5" />
              Machine
            </Button>
            <Button 
              variant="ghost"
              @click="targetType = 'client'; targetId = ''"
              :class="targetType === 'client' ? 'bg-indigo-600 text-white shadow-lg hover:bg-indigo-700 hover:text-white' : 'text-slate-500 hover:text-slate-300 hover:bg-slate-900'"
              class="flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-[10px] font-black uppercase tracking-widest transition-all h-auto border-0"
            >
              <MonitorIcon class="h-3.5 w-3.5" />
              Client PC
            </Button>
          </div>
        </div>

        <div class="space-y-4">
          <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Select Target Asset</Label>
          <Select v-model="targetId">
            <SelectTrigger class="w-full h-14 bg-slate-950 border-slate-800 rounded-2xl font-bold text-sm text-slate-200">
              <SelectValue placeholder="Select an available asset..." />
            </SelectTrigger>
            <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
              <template v-if="targetType === 'machine'">
                <SelectItem v-for="m in machines" :key="m.id" :value="m.id" class="text-xs font-bold">
                  {{ m.customIdentifier }}
                </SelectItem>
              </template>
              <template v-else>
                <SelectItem v-for="c in clients" :key="c.id" :value="c.id" class="text-xs font-bold">
                  {{ c.hostname }} ({{ c.macAddress }})
                </SelectItem>
              </template>
            </SelectContent>
          </Select>
        </div>
      </div>

      <DialogFooter class="p-8 pt-0 flex gap-4">
        <Button 
          variant="ghost" 
          @click="emit('update:open', false)"
          class="flex-1 rounded-2xl h-12 text-[10px] font-black text-slate-500 uppercase tracking-widest hover:bg-slate-800 hover:text-slate-300 transition-all"
        >
          Abort
        </Button>
        <Button 
          @click="handleSave"
          :disabled="!targetId || isSaving"
          class="flex-1 bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl h-12 shadow-xl shadow-indigo-500/20 transition-all font-black uppercase tracking-widest text-[10px]"
        >
          {{ isSaving ? 'Commiting...' : 'Link Asset' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
