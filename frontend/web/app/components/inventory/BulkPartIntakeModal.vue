<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  X,
  Layers,
  Plus,
  Trash2,
  FileCode,
  Table,
  CheckCircle2,
  AlertTriangle
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { InventoryPart, PartLifecycleCondition, PartTrackingType } from '~/types/inventory'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'bulkLogged', parts: Array<Partial<InventoryPart>>): void
}>()

const activeMode = ref<'visual' | 'json'>('visual')

// Visual Row Grid State
interface VisualRow {
  name: string
  trackingType: PartTrackingType
  condition: PartLifecycleCondition
  manufacturer: string
  quantity: number
  location: string
  priceEur: number
}

const visualRows = ref<VisualRow[]>([
  {
    name: 'IFM O5D100 Optical Distance Sensor',
    trackingType: 'serialized',
    condition: 'new',
    manufacturer: 'IFM Electronic',
    quantity: 1,
    location: 'Warehouse Central / Bin 12',
    priceEur: 220
  },
  {
    name: 'Festo Push-in Fitting QS-G1/8-6',
    trackingType: 'bulk',
    condition: 'new',
    manufacturer: 'Festo',
    quantity: 50,
    location: 'Warehouse Central / Bin 04',
    priceEur: 4
  }
])

const addRow = () => {
  visualRows.value.push({
    name: '',
    trackingType: 'serialized',
    condition: 'new',
    manufacturer: 'Generic OEM',
    quantity: 1,
    location: 'Warehouse Central / Intake Bay',
    priceEur: 100
  })
}

const removeRow = (index: number) => {
  visualRows.value.splice(index, 1)
}

// JSON Mode State
const jsonContent = ref(`[
  {
    "name": "Cognex Dataman 280 Fixed Barcode Reader",
    "category": "Hardware",
    "trackingType": "serialized",
    "condition": "new",
    "operationalState": "working",
    "manufacturer": { "name": "Cognex" },
    "location": "Warehouse Central / Rack 3",
    "priceEur": 1450,
    "quantity": 1
  },
  {
    "name": "Siemens ET 200SP Digital Input Module 8x24V",
    "category": "Hardware",
    "trackingType": "bulk",
    "condition": "new",
    "manufacturer": { "name": "Siemens" },
    "location": "Warehouse Central / Bin 31",
    "priceEur": 85,
    "quantity": 10
  }
]`)

const jsonError = ref<string | null>(null)

const parsedJsonPreview = computed(() => {
  jsonError.value = null
  if (!jsonContent.value.trim()) return []
  try {
    const parsed = JSON.parse(jsonContent.value)
    if (!Array.isArray(parsed)) {
      jsonError.value = 'Root JSON must be an array of part objects.'
      return []
    }
    return parsed
  } catch (err: any) {
    jsonError.value = `JSON Syntax Error: ${err.message}`
    return []
  }
})

const isVisualValid = computed(() => {
  return visualRows.value.length > 0 && visualRows.value.every(r => r.name.trim().length > 0 && r.quantity > 0)
})

const handleVisualSubmit = () => {
  if (!isVisualValid.value) return
  const parts: Array<Partial<InventoryPart>> = visualRows.value.map(row => ({
    name: row.name.trim(),
    trackingType: row.trackingType,
    condition: row.condition,
    operationalState: row.trackingType === 'serialized' ? (row.condition === 'scrap' ? 'broken' : 'working') : undefined,
    manufacturer: { name: row.manufacturer.trim() },
    location: row.location.trim(),
    priceEur: row.priceEur,
    quantity: row.trackingType === 'serialized' ? 1 : row.quantity
  }))

  emit('bulkLogged', parts)
  emit('update:open', false)
}

const handleJsonSubmit = () => {
  if (jsonError.value || parsedJsonPreview.value.length === 0) return
  emit('bulkLogged', parsedJsonPreview.value)
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
    <div class="relative w-full max-w-4xl bg-card border border-border rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/30">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <Layers class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-semibold text-foreground">
              Bulk Log Parts Intake
            </h2>
            <p class="text-xs text-muted-foreground mt-0.5">
              Intake multiple parts simultaneously via interactive visual grid or JSON array
            </p>
          </div>
        </div>
        <button
          type="button"
          @click="emit('update:open', false)"
          class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
        >
          <X class="size-5" />
        </button>
      </div>

      <!-- Mode Switcher Tabs -->
      <div class="flex items-center gap-2 px-6 pt-3 border-b border-border bg-muted/10">
        <button
          type="button"
          @click="activeMode = 'visual'"
          class="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold border-b-2 transition-all cursor-pointer"
          :class="activeMode === 'visual' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground'"
        >
          <Table class="size-3.5" />
          <span>Visual Table Mode ({{ visualRows.length }} rows)</span>
        </button>
        <button
          type="button"
          @click="activeMode = 'json'"
          class="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold border-b-2 transition-all cursor-pointer"
          :class="activeMode === 'json' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground'"
        >
          <FileCode class="size-3.5" />
          <span>JSON Import Mode</span>
        </button>
      </div>

      <!-- Body Content -->
      <div class="p-6 overflow-y-auto flex-1 space-y-4">
        <!-- Visual Mode -->
        <div v-if="activeMode === 'visual'" class="space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-xs text-muted-foreground">
              Configure multiple parts in the table and submit batch intake.
            </span>
            <Button
              type="button"
              variant="outline"
              size="sm"
              @click="addRow"
              class="h-7 text-xs gap-1 cursor-pointer"
            >
              <Plus class="size-3" />
              <span>Add Row</span>
            </Button>
          </div>

          <div class="border border-border rounded-lg overflow-x-auto">
            <table class="w-full text-xs text-left">
              <thead class="bg-muted/40 text-muted-foreground border-b border-border font-medium">
                <tr>
                  <th class="p-2 min-w-[180px]">Part Name *</th>
                  <th class="p-2 w-28">Type</th>
                  <th class="p-2 w-28">Condition</th>
                  <th class="p-2 min-w-[120px]">Manufacturer</th>
                  <th class="p-2 w-20">Qty</th>
                  <th class="p-2 min-w-[140px]">Location</th>
                  <th class="p-2 w-24">Price (€)</th>
                  <th class="p-2 w-10 text-center"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border">
                <tr v-for="(row, idx) in visualRows" :key="idx" class="hover:bg-muted/20">
                  <td class="p-2">
                    <Input v-model="row.name" placeholder="Component name" class="h-8 text-xs" />
                  </td>
                  <td class="p-2">
                    <select v-model="row.trackingType" class="w-full h-8 px-2 rounded-md bg-background border border-border text-xs">
                      <option value="serialized">Serialized</option>
                      <option value="bulk">Bulk</option>
                    </select>
                  </td>
                  <td class="p-2">
                    <select v-model="row.condition" class="w-full h-8 px-2 rounded-md bg-background border border-border text-xs">
                      <option value="new">New</option>
                      <option value="used">Used</option>
                      <option value="donor">Donor</option>
                      <option value="obsolete">Obsolete</option>
                      <option value="scrap">Scrap</option>
                    </select>
                  </td>
                  <td class="p-2">
                    <Input v-model="row.manufacturer" class="h-8 text-xs" />
                  </td>
                  <td class="p-2">
                    <Input v-model.number="row.quantity" type="number" min="1" :disabled="row.trackingType === 'serialized'" class="h-8 text-xs font-mono" />
                  </td>
                  <td class="p-2">
                    <Input v-model="row.location" class="h-8 text-xs" />
                  </td>
                  <td class="p-2">
                    <Input v-model.number="row.priceEur" type="number" min="0" class="h-8 text-xs font-mono" />
                  </td>
                  <td class="p-2 text-center">
                    <button
                      type="button"
                      @click="removeRow(idx)"
                      class="text-muted-foreground hover:text-destructive p-1 rounded-sm cursor-pointer"
                      title="Remove row"
                    >
                      <Trash2 class="size-3.5" />
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- JSON Mode -->
        <div v-else class="space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-xs text-muted-foreground">
              Paste a JSON array of parts. Supports attributes: name, trackingType, condition, manufacturer, priceEur, quantity, etc.
            </span>
            <span v-if="parsedJsonPreview.length > 0" class="text-xs font-mono text-emerald-600 dark:text-emerald-400 font-semibold">
              {{ parsedJsonPreview.length }} valid parts parsed
            </span>
          </div>

          <textarea
            v-model="jsonContent"
            rows="10"
            class="w-full p-3 font-mono text-xs rounded-lg bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary custom-scrollbar"
            placeholder="[ { &quot;name&quot;: &quot;...&quot;, &quot;quantity&quot;: 1 } ]"
          />

          <div v-if="jsonError" class="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs flex items-center gap-2">
            <AlertTriangle class="size-4 shrink-0" />
            <span>{{ jsonError }}</span>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-border bg-muted/20">
        <Button
          type="button"
          variant="outline"
          size="sm"
          @click="emit('update:open', false)"
          class="text-xs h-8 cursor-pointer"
        >
          Cancel
        </Button>
        <Button
          type="button"
          size="sm"
          :disabled="activeMode === 'visual' ? !isVisualValid : (!!jsonError || parsedJsonPreview.length === 0)"
          @click="activeMode === 'visual' ? handleVisualSubmit() : handleJsonSubmit()"
          class="bg-primary text-primary-foreground hover:bg-primary/90 text-xs h-8 gap-1.5 cursor-pointer disabled:opacity-50"
        >
          <CheckCircle2 class="size-3.5" />
          <span>Confirm Bulk Intake ({{ activeMode === 'visual' ? visualRows.length : parsedJsonPreview.length }})</span>
        </Button>
      </div>
    </div>
  </div>
</template>
