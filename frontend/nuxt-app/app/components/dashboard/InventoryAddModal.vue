<script setup lang="ts">
import { ref } from 'vue'
import { PlusIcon } from 'lucide-vue-next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'

const props = defineProps<{
  type: 'hardware' | 'software'
  open: boolean
}>()

const emit = defineEmits(['close', 'save', 'update:open'])

const form = ref({
  name: '',
  manufacturerId: null,
  serialNumber: '',
  description: '',
  costInHuf: 0,
  technicalSpecs: {
    category: '',
    interfaceType: '',
    torqueMax: 0
  }
})

function handleSave() {
  emit('save', form.value)
  emit('close')
}
</script>

<template>
  <Dialog :open="open" @update:open="(val) => emit('update:open', val)">
    <DialogContent class="max-w-lg bg-slate-900 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
      <DialogHeader class="bg-indigo-950 p-8 border-b border-slate-800">
        <DialogTitle class="text-2xl font-black uppercase tracking-tight flex items-center gap-3 text-slate-100">
          <div class="p-2 bg-indigo-500/20 rounded-xl text-indigo-400">
             <PlusIcon class="h-6 w-6" />
          </div>
          Add {{ type }}
        </DialogTitle>
        <DialogDescription class="text-indigo-400 text-[10px] font-black uppercase tracking-widest mt-2 opacity-80">
          Provisioning new inventory asset for the Heimdall system.
        </DialogDescription>
      </DialogHeader>

      <div class="p-8 space-y-6">
        <div class="grid grid-cols-2 gap-6">
          <div class="col-span-2 space-y-2">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Asset Name</Label>
            <Input v-model="form.name" placeholder="e.g. Industrial Screwdriver X1" class="rounded-2xl h-12 border-slate-800 bg-slate-950 text-slate-200 focus:ring-indigo-500/20 font-bold" />
          </div>
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Manufacturer ID</Label>
            <Input v-model="form.manufacturerId" class="rounded-2xl h-12 border-slate-800 bg-slate-950 text-slate-200 focus:ring-indigo-500/20 font-bold" />
          </div>
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Serial Number</Label>
            <Input v-model="form.serialNumber" class="rounded-2xl h-12 border-slate-800 bg-slate-950 text-slate-200 focus:ring-indigo-500/20 font-bold" />
          </div>
          <div class="col-span-2 space-y-2">
            <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Cost Allocation (HUF)</Label>
            <Input v-model="form.costInHuf" type="number" class="rounded-2xl h-12 border-slate-800 bg-slate-950 text-slate-200 focus:ring-indigo-500/20 font-bold" />
          </div>
        </div>
      </div>

      <DialogFooter class="p-8 pt-0 flex gap-4 mt-2">
        <Button variant="ghost" @click="emit('close')" class="flex-1 rounded-2xl h-12 text-[10px] font-black text-slate-500 uppercase tracking-widest hover:bg-slate-800 hover:text-slate-300 transition-all">
          Abort
        </Button>
        <Button @click="handleSave" class="flex-1 bg-indigo-600 text-white rounded-2xl h-12 text-[10px] font-black uppercase tracking-widest hover:bg-indigo-700 shadow-xl shadow-indigo-500/20 transition-all">
          Commit Asset
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
