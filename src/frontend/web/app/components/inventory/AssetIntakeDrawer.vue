<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  X,
  PackagePlus,
  Layers,
  Bot,
  QrCode,
  CheckCircle2,
  AlertTriangle,
  Upload,
  Plus,
  Trash2,
  Cpu,
  Barcode,
  Search,
  Sparkles,
  Camera,
  ArrowRight
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import type { AssetTemplateDefinition } from '~/types/inventory'

const props = defineProps<{
  open: boolean
  templates?: AssetTemplateDefinition[]
  machines?: Array<{ id: string; name: string }>
}>()

const emit = defineEmits<{
  'close': []
  'partLogged': [part: any]
  'bulkLogged': [parts: any[]]
}>()

type IntakeMode = 'single' | 'batch' | 'scanner'
const activeMode = ref<IntakeMode>('single')

// ── 1. Single Part Intake Form ──────────────────────────────────────────────
const singleForm = ref({
  customIdentifier: '',
  name: '',
  category: 'Hardware',
  trackingType: 'serialized' as 'serialized' | 'bulk',
  condition: 'new' as 'new' | 'used' | 'donor' | 'obsolete',
  location: 'Warehouse Central / Rack 1',
  priceEur: 150,
  quantity: 1,
  minQuantity: 1,
  manufacturerName: 'Generic OEM',
  serialNumber: '',
  templateId: ''
})

const isSingleSubmitting = ref(false)

const handleTemplateSelect = (tmplId: string) => {
  singleForm.value.templateId = tmplId
  if (!props.templates) return
  const found = props.templates.find(t => t.id === tmplId)
  if (found) {
    if (found.name) singleForm.value.name = found.name
    if (found.topLevelCategory) singleForm.value.category = found.topLevelCategory
    if (found.fixedFields?.manufacturer) singleForm.value.manufacturerName = String(found.fixedFields.manufacturer)
    if (found.identifierPattern) {
      singleForm.value.customIdentifier = found.identifierPattern.replace('{number}', String(Math.floor(1000 + Math.random() * 9000)))
    }
  }
}

const submitSinglePart = () => {
  isSingleSubmitting.value = true
  try {
    const payload = {
      id: `prt-${Date.now()}`,
      customIdentifier: singleForm.value.customIdentifier || `PRT-${Math.floor(1000 + Math.random() * 9000)}`,
      name: singleForm.value.name || 'New Industrial Component',
      displayName: singleForm.value.name || 'New Industrial Component',
      category: singleForm.value.category,
      trackingType: singleForm.value.trackingType,
      condition: singleForm.value.condition,
      operationalState: 'working',
      location: singleForm.value.location,
      priceEur: Number(singleForm.value.priceEur) || 0,
      estimatedResellPriceEur: Number(singleForm.value.priceEur) || 0,
      quantity: Number(singleForm.value.quantity) || 1,
      minQuantity: Number(singleForm.value.minQuantity) || 1,
      manufacturer: { name: singleForm.value.manufacturerName || 'OEM' },
      serialNumber: singleForm.value.serialNumber || (singleForm.value.trackingType === 'serialized' ? `SN-${Date.now()}` : undefined),
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }

    emit('partLogged', payload)
    toast.success(`Registered asset: ${payload.displayName} (${payload.customIdentifier})`)
    // Reset form
    singleForm.value.name = ''
    singleForm.value.customIdentifier = ''
    singleForm.value.serialNumber = ''
  } catch (err: any) {
    toast.error(err?.message || 'Failed to register component')
  } finally {
    isSingleSubmitting.value = false
  }
}

// ── 2. Batch & Document AI Intake ───────────────────────────────────────────
const rawBatchInput = ref(`[
  { "customIdentifier": "SMC-PNEU-VALVE-01", "name": "SMC 5-Port Solenoid Valve", "category": "Hardware", "quantity": 10, "priceEur": 85, "location": "Crib 3B" },
  { "customIdentifier": "KEYENCE-LR-TB-02", "name": "Keyence Laser Sensor Head", "category": "Hardware", "quantity": 2, "priceEur": 420, "location": "Cabinet A" }
]`)
const batchItemsCount = computed(() => {
  try {
    const parsed = JSON.parse(rawBatchInput.value)
    return Array.isArray(parsed) ? parsed.length : 0
  } catch {
    return 0
  }
})

const submitBatchIntake = () => {
  try {
    const parsed = JSON.parse(rawBatchInput.value)
    if (Array.isArray(parsed) && parsed.length > 0) {
      emit('bulkLogged', parsed)
      toast.success(`Imported batch of ${parsed.length} items`)
    } else {
      toast.error('Batch must contain at least one valid item')
    }
  } catch (e: any) {
    console.error('Invalid JSON batch payload', e)
    toast.error('Invalid JSON batch payload: ' + e.message)
  }
}

// ── 3. Barcode / QR Optical Scanner ────────────────────────────────────────
const scannedCode = ref('')
const scanSimulateList = ['IPC-1001', 'FST-VLV-M5', 'BOSCH-SERVO-REX', 'SICK-W16-SENSOR']

const handleSimulatedScan = (code: string) => {
  scannedCode.value = code
  singleForm.value.customIdentifier = code
  activeMode.value = 'single'
}
</script>

<template>
  <div v-if="open" class="fixed inset-y-0 right-0 z-50 w-full max-w-xl bg-card border-l border-border shadow-2xl flex flex-col select-none animate-in slide-in-from-right duration-200">
    <!-- Drawer Top Header -->
    <div class="p-4 border-b border-border bg-muted/30 flex items-center justify-between">
      <div class="flex items-center gap-2.5">
        <div class="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 text-primary flex items-center justify-center">
          <PackagePlus class="w-4 h-4" />
        </div>
        <div>
          <h2 class="text-sm font-bold text-foreground">Asset Ingestion & Registration</h2>
          <p class="text-[11px] text-muted-foreground">Unified hardware intake, batch BOM loading, and barcode registration</p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <NuxtLink
          to="/dashboard/inventory/new"
          @click="emit('close')"
          class="text-[11px] font-semibold text-primary hover:underline flex items-center gap-1 px-2.5 py-1 rounded-lg bg-primary/10 border border-primary/20 transition-colors"
          title="Open dedicated full page intake workbench"
        >
          <span>Full Page</span>
          <ArrowRight class="w-3 h-3" />
        </NuxtLink>

        <button
          type="button"
          @click="emit('close')"
          class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
          title="Close intake panel"
        >
          <X class="w-4 h-4" />
        </button>
      </div>
    </div>

    <!-- Mode Selector Tabs -->
    <div class="px-4 pt-3 pb-2 bg-muted/10 border-b border-border">
      <div class="flex items-center p-1 bg-muted/50 rounded-lg border border-border gap-1">
        <button
          type="button"
          @click="activeMode = 'single'"
          class="flex-1 py-1.5 px-3 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeMode === 'single' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <PackagePlus class="w-3.5 h-3.5 text-primary" />
          <span>Single Item</span>
        </button>

        <button
          type="button"
          @click="activeMode = 'batch'"
          class="flex-1 py-1.5 px-3 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeMode === 'batch' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <Layers class="w-3.5 h-3.5 text-cyan-400" />
          <span>Batch / BOM</span>
        </button>

        <button
          type="button"
          @click="activeMode = 'scanner'"
          class="flex-1 py-1.5 px-3 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeMode === 'scanner' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <QrCode class="w-3.5 h-3.5 text-emerald-400" />
          <span>Scan Code</span>
        </button>
      </div>
    </div>

    <!-- Mode Content -->
    <div class="flex-1 overflow-y-auto p-5">
      <!-- 1. Single Item Registration -->
      <div v-if="activeMode === 'single'" class="space-y-4 text-xs">
        <!-- Blueprint Template Selector -->
        <div v-if="templates && templates.length > 0" class="p-3 rounded-xl bg-muted/20 border border-border space-y-1.5">
          <label class="font-bold text-[10px] uppercase text-muted-foreground block">
            Inherit from Blueprint Template (Optional)
          </label>
          <select
            :value="singleForm.templateId"
            @change="handleTemplateSelect(($event.target as HTMLSelectElement).value)"
            class="w-full bg-background border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-primary cursor-pointer"
          >
            <option value="">-- Custom Unique Component --</option>
            <option v-for="t in templates" :key="t.id" :value="t.id">
              {{ t.name }} ({{ t.topLevelCategory }})
            </option>
          </select>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Part Identifier / Code
            </label>
            <Input
              v-model="singleForm.customIdentifier"
              placeholder="e.g. CX5140-IPC-01"
              class="font-mono text-xs"
            />
          </div>

          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Manufacturer / OEM
            </label>
            <Input
              v-model="singleForm.manufacturerName"
              placeholder="e.g. Beckhoff / Festo"
              class="text-xs"
            />
          </div>
        </div>

        <div>
          <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
            Part Name & Model Description
          </label>
          <Input
            v-model="singleForm.name"
            placeholder="e.g. Modular Embedded IPC Controller Unit"
            class="text-xs font-medium"
          />
        </div>

        <div class="grid grid-cols-3 gap-3">
          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Tracking Paradigm
            </label>
            <select
              v-model="singleForm.trackingType"
              class="w-full bg-background border border-border rounded-lg px-2.5 py-2 font-mono text-xs text-foreground focus:outline-none focus:border-primary cursor-pointer"
            >
              <option value="serialized">Serialized (UID)</option>
              <option value="bulk">Bulk Crib Stock</option>
            </select>
          </div>

          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Intake Quantity
            </label>
            <input
              v-model.number="singleForm.quantity"
              type="number"
              min="1"
              class="w-full bg-background border border-border rounded-lg px-2.5 py-2 font-mono font-bold text-xs text-foreground focus:outline-none focus:border-primary"
            />
          </div>

          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Min Threshold
            </label>
            <input
              v-model.number="singleForm.minQuantity"
              type="number"
              min="0"
              class="w-full bg-background border border-border rounded-lg px-2.5 py-2 font-mono font-bold text-xs text-foreground focus:outline-none focus:border-primary"
            />
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Unit Purchase Price (EUR)
            </label>
            <input
              v-model.number="singleForm.priceEur"
              type="number"
              min="0"
              step="0.01"
              class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono font-bold text-xs text-foreground focus:outline-none focus:border-primary"
            />
          </div>

          <div>
            <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
              Storage Bay / Bin
            </label>
            <Input
              v-model="singleForm.location"
              placeholder="e.g. Central Crib / Rack 2B"
              class="font-mono text-xs"
            />
          </div>
        </div>

        <div v-if="singleForm.trackingType === 'serialized'">
          <label class="font-bold text-[10px] uppercase text-muted-foreground block mb-1">
            Factory Serial Number (Optional)
          </label>
          <Input
            v-model="singleForm.serialNumber"
            placeholder="e.g. SN-892147-2026"
            class="font-mono text-xs"
          />
        </div>

        <div class="pt-4">
          <Button
            @click="submitSinglePart"
            :disabled="!singleForm.name.trim() || isSingleSubmitting"
            class="w-full bg-primary text-primary-foreground font-bold text-xs gap-2 cursor-pointer shadow-xs"
          >
            <CheckCircle2 class="w-4 h-4" />
            <span>Complete Component Intake</span>
          </Button>
        </div>
      </div>

      <!-- 2. Batch / Document AI Intake -->
      <div v-else-if="activeMode === 'batch'" class="space-y-4 text-xs">
        <div class="p-3 rounded-xl bg-cyan-950/20 border border-cyan-500/30 text-cyan-400 text-xs flex items-start gap-2">
          <Layers class="w-4 h-4 shrink-0 mt-0.5 text-cyan-400" />
          <div>
            <strong class="font-bold">Automated Batch Ingestion</strong>
            <p class="text-[11px] text-cyan-300/80 mt-0.5">
              Paste BOM component list or OEM JSON documentation. Multiple assets are created and linked in one atomic operation.
            </p>
          </div>
        </div>

        <div>
          <div class="flex items-center justify-between mb-1">
            <label class="font-bold text-[10px] uppercase text-muted-foreground">
              JSON Specification Array
            </label>
            <span class="font-mono text-[10px]" :class="batchItemsCount > 0 ? 'text-emerald-400 font-bold' : 'text-muted-foreground'">
              Detected: {{ batchItemsCount }} item(s)
            </span>
          </div>
          <textarea
            v-model="rawBatchInput"
            rows="10"
            class="w-full bg-background border border-border rounded-xl p-3 font-mono text-xs text-foreground focus:outline-none focus:border-cyan-500"
          />
        </div>

        <div>
          <Button
            @click="submitBatchIntake"
            :disabled="batchItemsCount === 0"
            class="w-full bg-cyan-600 hover:bg-cyan-500 text-white font-bold text-xs gap-2 cursor-pointer shadow-md shadow-cyan-950/40"
          >
            <Upload class="w-4 h-4" />
            <span>Import {{ batchItemsCount }} Assets to Registry</span>
          </Button>
        </div>
      </div>

      <!-- 3. Optical Barcode Scanner -->
      <div v-else-if="activeMode === 'scanner'" class="space-y-4 text-xs">
        <div class="p-6 rounded-2xl bg-muted/20 border-2 border-dashed border-border text-center space-y-3">
          <div class="w-12 h-12 rounded-full bg-emerald-500/10 text-emerald-400 mx-auto flex items-center justify-center">
            <Camera class="w-6 h-6" />
          </div>
          <div>
            <h4 class="font-bold text-foreground">Optical Camera Ready</h4>
            <p class="text-[11px] text-muted-foreground mt-0.5">Point camera at QR matrix or standard 1D barcode on component housing</p>
          </div>

          <div class="pt-2">
            <Badge variant="outline" class="font-mono text-xs px-2.5 py-1 text-emerald-400 border-emerald-500/40 bg-emerald-500/5">
              Listening for Optical Stream...
            </Badge>
          </div>
        </div>

        <!-- Simulation Quick-Select Chips -->
        <div class="p-3 rounded-xl bg-muted/20 border border-border space-y-2">
          <span class="text-[10px] font-mono uppercase text-muted-foreground block font-bold">
            Simulate Hardware Scan (Test Tags)
          </span>
          <div class="flex flex-wrap gap-2">
            <button
              v-for="code in scanSimulateList"
              :key="code"
              type="button"
              @click="handleSimulatedScan(code)"
              class="px-2.5 py-1 rounded-md bg-muted border border-border text-xs font-mono text-foreground hover:border-primary hover:text-primary transition-colors cursor-pointer"
            >
              {{ code }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
