<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue'
import { Edit3, Zap, X, Trash2, Layers, Link as LinkIcon } from 'lucide-vue-next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { Badge } from '~/components/ui/badge'
import { useInventoryProvisioning } from '~/composables/useInventoryProvisioning'
import SearchableSelect from '~/components/ui/searchable-select/SearchableSelect.vue'

const props = defineProps<{
  open: boolean
  item: any | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'close'): void
  (e: 'save', updatedItem: any): void
}>()

const { manufacturers, suppliers, machines, clientPcs, components, fetchReferenceData } = useInventoryProvisioning()

const manufacturerOptions = computed(() => manufacturers.value.map(m => ({ id: m.id, label: m.name })))
const supplierOptions = computed(() => suppliers.value.map(s => ({ id: s.id, label: s.name })))
const machineOptions = computed(() => machines.value.map(m => ({ id: m.id, label: m.customIdentifier })))
const pcOptions = computed(() => clientPcs.value.map(pc => ({ id: pc.id, label: pc.hostname })))
const componentOptions = computed(() => components.value.map(c => ({ id: c.id, label: c.name })))

const form = ref({
  id: '',
  name: '',
  displayName: '',
  technology: '',
  serialNumber: '',
  modelNumber: '',
  costInHUF: 0,
  manufacturerId: null as string | null,
  supplierId: null as string | null,
  machineId: null as string | null,
  clientPcId: null as string | null,
  parentId: null as string | null,
  itemType: 'HardwareComponent'
})

const customData = ref<Record<string, any>>({})
const newFieldKey = ref('')

watch(() => props.item, (item) => {
  if (item) {
    form.value = {
      id: item.id || '',
      name: item.name || '',
      displayName: item.displayName || '',
      technology: item.technology || item.metadata?.Technology || '',
      serialNumber: item.serialNumber || item.data?.SerialNumber || '',
      modelNumber: item.modelNumber || item.data?.ModelNumber || '',
      costInHUF: item.costInHUF || item.cost || item.data?.CostInHUF || 0,
      manufacturerId: item.manufacturerId || item.manufacturer?.id || null,
      supplierId: item.supplierId || item.supplier?.id || null,
      machineId: item.machineId || null,
      clientPcId: item.clientPcId || null,
      parentId: item.parentId || null,
      itemType: item.itemType || 'HardwareComponent'
    }
    customData.value = { ...(item.metadata || item.data || {}) }
    // Clean known fields out of generic customData
    delete customData.value.SerialNumber
    delete customData.value.CostInHUF
  }
}, { immediate: true })

onMounted(() => {
  if (props.open) fetchReferenceData()
})

watch(() => props.open, (isOpen) => {
  if (isOpen) fetchReferenceData()
})

const addCustomField = () => {
  if (newFieldKey.value && !customData.value[newFieldKey.value]) {
    customData.value[newFieldKey.value] = ''
    newFieldKey.value = ''
  }
}

const removeCustomField = (key: string) => {
  delete customData.value[key]
}

const handleSave = () => {
  const payload = {
    ...form.value,
    metadata: {
      ...customData.value,
      SerialNumber: form.value.serialNumber,
      CostInHUF: form.value.costInHUF
    }
  }
  emit('save', payload)
  emit('update:open', false)
  emit('close')
}
</script>

<template>
  <Dialog :open="open" @update:open="(val) => emit('update:open', val)">
    <DialogContent class="max-w-2xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-[2rem] shadow-2xl">
      <DialogHeader class="bg-indigo-950/30 p-6 sm:p-8 border-b border-slate-900">
        <DialogTitle class="text-2xl sm:text-3xl font-black uppercase tracking-tight flex items-center gap-3.5 text-slate-100">
          <div class="p-3 bg-indigo-500/20 rounded-2xl text-indigo-400">
            <Edit3 class="h-6 w-6" />
          </div>
          <span>Edit Asset Record</span>
        </DialogTitle>
        <DialogDescription class="text-indigo-400/70 text-xs font-bold uppercase tracking-widest mt-2">
          Update asset parameters, commercial attributes, and infrastructure graph links.
        </DialogDescription>
      </DialogHeader>

      <div class="p-6 sm:p-8 max-h-[60vh] overflow-y-auto custom-scrollbar space-y-6">
        <!-- Identity Section -->
        <div class="space-y-4">
          <div class="flex items-center gap-2">
            <div class="h-px flex-1 bg-slate-800"></div>
            <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Identity & Designation</span>
            <div class="h-px flex-1 bg-slate-800"></div>
          </div>
          
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Internal Name / Handle</Label>
              <Input v-model="form.name" class="rounded-xl h-10 bg-slate-900 border-slate-800 text-slate-200 font-bold" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Display Name</Label>
              <Input v-model="form.displayName" class="rounded-xl h-10 bg-slate-900 border-slate-800 text-slate-200 font-bold" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Serial Number</Label>
              <Input v-model="form.serialNumber" class="rounded-xl h-10 bg-slate-900 border-slate-800 text-slate-200 font-mono text-xs" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Model / Part Number</Label>
              <Input v-model="form.modelNumber" class="rounded-xl h-10 bg-slate-900 border-slate-800 text-slate-200 font-mono text-xs" />
            </div>
          </div>
        </div>

        <!-- Relationships Section -->
        <div class="space-y-4">
          <div class="flex items-center gap-2">
            <div class="h-px flex-1 bg-slate-800"></div>
            <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Graph Links & Hosts</span>
            <div class="h-px flex-1 bg-slate-800"></div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Linked Host PC</Label>
              <SearchableSelect v-model="form.clientPcId" :options="pcOptions" placeholder="Select Client PC" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Linked Machine / Cell</Label>
              <SearchableSelect v-model="form.machineId" :options="machineOptions" placeholder="Select Machine" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Parent Asset</Label>
              <SearchableSelect v-model="form.parentId" :options="componentOptions" placeholder="Select Parent Asset" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Manufacturer</Label>
              <SearchableSelect v-model="form.manufacturerId" :options="manufacturerOptions" placeholder="Select Manufacturer" />
            </div>
          </div>
        </div>

        <!-- Commercial Section -->
        <div class="space-y-4">
          <div class="flex items-center gap-2">
            <div class="h-px flex-1 bg-slate-800"></div>
            <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Commercial & Cost</span>
            <div class="h-px flex-1 bg-slate-800"></div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Supplier</Label>
              <SearchableSelect v-model="form.supplierId" :options="supplierOptions" placeholder="Select Supplier" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Capital Valuation (HUF)</Label>
              <Input v-model="form.costInHUF" type="number" class="rounded-xl h-10 bg-slate-900 border-slate-800 text-slate-200 font-mono font-bold" />
            </div>
          </div>
        </div>

        <!-- Custom Metadata -->
        <div class="space-y-3">
          <div class="flex items-center gap-2">
            <div class="h-px flex-1 bg-slate-800"></div>
            <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Parameters & Specs</span>
            <div class="h-px flex-1 bg-slate-800"></div>
          </div>

          <div v-for="(val, key) in customData" :key="key" class="flex items-center gap-2">
            <Badge variant="outline" class="h-8 px-2.5 bg-slate-900 border-slate-800 text-slate-400 font-mono text-[10px] font-bold">{{ key }}</Badge>
            <Input v-model="customData[key]" class="flex-1 rounded-xl h-8 bg-slate-950 border-slate-800 text-slate-200 text-xs font-mono" />
            <Button variant="ghost" size="icon" class="h-8 w-8 text-slate-600 hover:text-rose-400" @click="removeCustomField(key)">
              <X class="w-3.5 h-3.5" />
            </Button>
          </div>

          <div class="flex gap-2 pt-2 border-t border-slate-900">
            <Input v-model="newFieldKey" placeholder="Parameter Name (e.g. Voltage, Power)" class="flex-1 rounded-xl h-8 bg-slate-950 border-slate-800 text-slate-400 text-[10px] uppercase font-bold" />
            <Button variant="outline" size="sm" class="h-8 px-3 rounded-xl border-slate-800 bg-slate-900 text-[10px] font-bold uppercase tracking-wider" @click="addCustomField">
              Add Field
            </Button>
          </div>
        </div>
      </div>

      <!-- Action Footer -->
      <div class="p-6 bg-slate-900/50 border-t border-slate-900 flex gap-3">
        <Button variant="ghost" @click="emit('update:open', false)" class="flex-1 rounded-xl h-11 text-xs font-black text-slate-400 uppercase tracking-wider hover:bg-slate-800 border border-slate-800">
          Cancel
        </Button>
        <Button @click="handleSave" class="flex-1 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl h-11 text-xs font-black uppercase tracking-wider shadow-lg shadow-indigo-600/20 transition-all flex items-center justify-center gap-2">
          <Zap class="w-4 h-4" />
          Save Changes
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
</style>
