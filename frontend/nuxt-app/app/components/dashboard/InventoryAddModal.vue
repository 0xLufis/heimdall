<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { PlusIcon, ZapIcon, LinkIcon, LayersIcon } from 'lucide-vue-next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { Badge } from '~/components/ui/badge'
import { useInventoryProvisioning } from '~/composables/useInventoryProvisioning'
import SearchableSelect from '~/components/ui/SearchableSelect.vue'

const props = defineProps<{
  type: 'hardware' | 'software'
  open: boolean
}>()

const emit = defineEmits(['close', 'save', 'update:open'])

const { manufacturers, suppliers, machines, clientPcs, components, fetchReferenceData } = useInventoryProvisioning()

const manufacturerOptions = computed(() => manufacturers.value.map(m => ({ id: m.id, label: m.name })))
const supplierOptions = computed(() => suppliers.value.map(s => ({ id: s.id, label: s.name })))
const machineOptions = computed(() => machines.value.map(m => ({ id: m.id, label: m.customIdentifier })))
const pcOptions = computed(() => clientPcs.value.map(pc => ({ id: pc.id, label: pc.hostname })))
const componentOptions = computed(() => components.value.map(c => ({ id: c.id, label: c.name })))

const customData = ref<Record<string, any>>({})
const newFieldKey = ref('')

function addCustomField() {
  if (newFieldKey.value && !customData.value[newFieldKey.value]) {
    customData.value[newFieldKey.value] = ''
    newFieldKey.value = ''
  }
}

function removeCustomField(key: string) {
  delete customData.value[key]
}

const form = ref({
  name: '',
  displayName: '',
  technology: '',
  quantity: 1,
  manufacturerId: null as string | null,
  supplierId: null as string | null,
  machineId: null as string | null,
  clientPcId: null as string | null,
  parentId: null as string | null,
  lateralLinkId: null as string | null,
  data: {
    SerialNumber: '',
    CostInHUF: 0,
  }
})

onMounted(() => {
  if (props.open) {
    fetchReferenceData()
  }
})

// Refetch if open changes
watch(() => props.open, (newVal) => {
  if (newVal) {
    fetchReferenceData()
  }
})

function handleSave() {
  const payload = {
    name: form.value.name,
    displayName: form.value.displayName,
    technology: form.value.technology,
    manufacturerId: form.value.manufacturerId || null,
    supplierId: form.value.supplierId || null,
    machineId: form.value.machineId || null,
    clientPcId: form.value.clientPcId || null,
    parentId: (form.value.parentId === 'none' || !form.value.parentId) ? null : form.value.parentId,
    serialNumber: form.value.data.SerialNumber,
    costInHUF: form.value.data.CostInHUF,
    itemType: props.type === 'software' ? 'SoftwareComponent' : 'HardwareComponent',
    data: customData.value
  }
  emit('save', payload)
  emit('close')
}
</script>

<template>
  <Dialog :open="open" @update:open="(val) => emit('update:open', val)">
    <DialogContent class="max-w-2xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-[2rem] shadow-2xl">
      <DialogHeader class="bg-indigo-950/30 p-8 border-b border-slate-900">
        <DialogTitle class="text-3xl font-black uppercase tracking-tighter flex items-center gap-4 text-slate-100">
          <div class="p-3 bg-indigo-500/20 rounded-2xl text-indigo-400 shadow-inner">
             <PlusIcon class="h-8 w-8" />
          </div>
          Provision {{ type }}
        </DialogTitle>
        <DialogDescription class="text-indigo-400/60 text-xs font-bold uppercase tracking-widest mt-3">
          Deploying new node into the unified infrastructure graph.
        </DialogDescription>
      </DialogHeader>

      <div class="p-8 max-h-[60vh] overflow-y-auto custom-scrollbar">
        <div class="grid grid-cols-2 gap-8">
          <!-- Identity Section -->
          <div class="col-span-2 space-y-4">
             <div class="flex items-center gap-2 mb-2">
                <div class="h-px flex-1 bg-slate-800"></div>
                <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Identity & Metadata</span>
                <div class="h-px flex-1 bg-slate-800"></div>
             </div>
             <div class="grid grid-cols-2 gap-6">
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Internal Name</Label>
                  <Input v-model="form.name" placeholder="e.g. S7-1500-ROOT" class="rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all font-bold" />
                </div>
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Display Name</Label>
                  <Input v-model="form.displayName" placeholder="e.g. Main PLC Controller" class="rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all font-bold" />
                </div>
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Technology Stack</Label>
                  <Input v-model="form.technology" placeholder="e.g. Industrial Automation" class="rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all font-bold" />
                </div>
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Quantity</Label>
                  <Input v-model="form.quantity" type="number" class="rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all font-bold" />
                </div>
             </div>
          </div>

          <!-- Relationships Section -->
          <div class="col-span-2 space-y-4">
             <div class="flex items-center gap-2 mb-2">
                <div class="h-px flex-1 bg-slate-800"></div>
                <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Graph Relationships</span>
                <div class="h-px flex-1 bg-slate-800"></div>
             </div>
             
             <div class="grid grid-cols-2 gap-6">
                <!-- System Link -->
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Link to System (Host)</Label>
                  <SearchableSelect 
                    v-model="form.clientPcId" 
                    :options="pcOptions" 
                    placeholder="Search/Select Client PC"
                  />
                </div>

                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Link to Machine</Label>
                  <SearchableSelect 
                    v-model="form.machineId" 
                    :options="machineOptions" 
                    placeholder="Search/Select Machine"
                  />
                </div>

                <!-- Structural Link -->
                <div class="space-y-2">
                  <div class="flex items-center gap-1.5 ml-1">
                    <LayersIcon class="size-3 text-indigo-400" />
                    <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Parent Component</Label>
                  </div>
                  <SearchableSelect 
                    v-model="form.parentId" 
                    :options="componentOptions" 
                    placeholder="Search/Select Parent (Optional)"
                  />
                </div>

                <div class="space-y-2">
                  <div class="flex items-center gap-1.5 ml-1">
                    <LinkIcon class="size-3 text-amber-400" />
                    <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Lateral Link (Dependency)</Label>
                  </div>
                  <SearchableSelect 
                    v-model="form.lateralLinkId" 
                    :options="componentOptions" 
                    placeholder="Search/Select Dependency"
                  />
                </div>
             </div>
          </div>

          <!-- Commercial Section -->
          <div class="col-span-2 space-y-4">
             <div class="flex items-center gap-2 mb-2">
                <div class="h-px flex-1 bg-slate-800"></div>
                <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Commercial & Logistics</span>
                <div class="h-px flex-1 bg-slate-800"></div>
             </div>
             
             <div class="grid grid-cols-2 gap-6">
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Manufacturer</Label>
                  <SearchableSelect 
                    v-model="form.manufacturerId" 
                    :options="manufacturerOptions" 
                    placeholder="Search/Type Manufacturer"
                  />
                </div>

                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Supplier</Label>
                  <SearchableSelect 
                    v-model="form.supplierId" 
                    :options="supplierOptions" 
                    placeholder="Search/Type Supplier"
                  />
                </div>
                
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Serial Number</Label>
                  <Input v-model="form.data.SerialNumber" class="rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 font-mono focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all" />
                </div>
                <div class="space-y-2">
                  <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Unit Cost (HUF)</Label>
                  <Input v-model="form.data.CostInHUF" type="number" class="rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all font-bold" />
                </div>
             </div>
          </div>

          <!-- Custom Metadata Section -->
          <div class="col-span-2 space-y-4">
             <div class="flex items-center gap-2 mb-2">
                <div class="h-px flex-1 bg-slate-800"></div>
                <span class="text-[10px] font-black text-slate-600 uppercase tracking-widest">Custom Metadata</span>
                <div class="h-px flex-1 bg-slate-800"></div>
             </div>
             
             <div class="space-y-3">
                <div v-for="(val, key) in customData" :key="key" class="flex items-center gap-3">
                   <Badge variant="outline" class="h-9 px-3 bg-slate-900 border-slate-800 text-slate-400 font-bold">{{ key }}</Badge>
                   <Input v-model="customData[key]" class="flex-1 rounded-xl h-9 border-slate-800 bg-slate-950 text-slate-300 text-xs focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all" />
                   <Button variant="ghost" size="icon" class="h-9 w-9 text-slate-600 hover:text-red-400" @click="removeCustomField(key)">
                      <PlusIcon class="rotate-45 size-4" />
                   </Button>
                </div>
                
                <div class="flex gap-2 mt-4 pt-2 border-t border-slate-900">
                   <Input v-model="newFieldKey" placeholder="New Field Name" class="flex-1 rounded-xl h-9 border-slate-800 bg-slate-950 text-slate-400 text-[10px] uppercase font-black focus:ring-2 focus:ring-indigo-500/20 focus:ring-offset-0 border-slate-800 focus:border-indigo-500/50 transition-all" />
                   <Button variant="outline" size="sm" class="h-9 px-4 rounded-xl border-slate-800 bg-slate-900 text-[10px] font-black uppercase tracking-widest hover:bg-slate-800 transition-all" @click="addCustomField">
                      Add Field
                   </Button>
                </div>
             </div>
          </div>
        </div>
      </div>

      <div class="p-8 pt-4 bg-slate-900/50 border-t border-slate-900 flex gap-4">
        <Button variant="ghost" @click="emit('close')" class="flex-1 rounded-2xl h-14 text-[10px] font-black text-slate-500 uppercase tracking-widest hover:bg-slate-800 hover:text-slate-300 transition-all border border-slate-800">
          Abort Deployment
        </Button>
        <Button @click="handleSave" class="flex-1 bg-indigo-600 text-white rounded-2xl h-14 text-[10px] font-black uppercase tracking-widest hover:bg-indigo-700 shadow-xl shadow-indigo-500/30 transition-all flex gap-2">
          <ZapIcon class="size-4" />
          Commit to Infrastructure
        </Button>
      </div>
    </DialogContent>
  </Dialog>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #1e293b;
  border-radius: 10px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #334155;
}
</style>
