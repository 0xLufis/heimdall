<script setup lang="ts">
import { ref } from 'vue'
import { PlusIcon } from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

defineProps<{
  type: 'hardware' | 'software'
}>()

defineEmits(['close', 'save'])

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
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-indigo-950/40 backdrop-blur-md p-4">
    <Card class="w-full max-w-lg shadow-2xl border-none overflow-hidden animate-in fade-in zoom-in duration-200">
      <CardHeader class="bg-indigo-900 text-white p-8">
        <CardTitle class="text-2xl font-black uppercase tracking-tight flex items-center gap-3">
          <div class="p-2 bg-white/10 rounded-xl">
             <PlusIcon class="h-6 w-6" />
          </div>
          Add {{ type }}
        </CardTitle>
        <p class="text-indigo-200 text-xs font-bold uppercase tracking-widest mt-2 opacity-70">Provisioning new inventory asset</p>
      </CardHeader>
      <CardContent class="p-8 space-y-6">
        <div class="grid grid-cols-2 gap-6">
          <div class="col-span-2 space-y-2">
            <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Asset Name</Label>
            <Input v-model="form.name" placeholder="e.g. Industrial Screwdriver X1" class="rounded-2xl py-6 focus:ring-4 focus:ring-indigo-500/10 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold" />
          </div>
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Manufacturer ID</Label>
            <Input v-model="form.manufacturerId" class="rounded-2xl py-6 focus:ring-4 focus:ring-indigo-500/10 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold" />
          </div>
          <div class="space-y-2">
            <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Serial Number</Label>
            <Input v-model="form.serialNumber" class="rounded-2xl py-6 focus:ring-4 focus:ring-indigo-500/10 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold" />
          </div>
          <div class="col-span-2 space-y-2">
            <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Cost Allocation (HUF)</Label>
            <Input v-model="form.costInHuf" type="number" class="rounded-2xl py-6 focus:ring-4 focus:ring-indigo-500/10 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold" />
          </div>
        </div>

        <div class="flex gap-4 mt-8">
          <Button variant="ghost" @click="$emit('close')" class="flex-1 rounded-2xl py-6 text-xs font-black text-gray-400 uppercase tracking-widest hover:bg-gray-50 transition-all border-2 border-transparent">
            Abort
          </Button>
          <Button @click="$emit('save', form)" class="flex-1 bg-indigo-600 text-white rounded-2xl py-6 text-xs font-black uppercase tracking-widest hover:bg-indigo-700 shadow-xl shadow-indigo-200 transition-all">
            Commit Asset
          </Button>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
