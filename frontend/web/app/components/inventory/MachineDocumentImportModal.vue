<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  X,
  Bot,
  Sparkles,
  FileCode,
  Layers,
  ArrowRight,
  CheckCircle2,
  AlertTriangle,
  FolderTree
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import type { MachineDocumentImportPayload, MachineDocumentImportResult } from '~/types/inventory'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'imported', result: MachineDocumentImportResult): void
}>()

const targetMachineId = ref('mach-op10-cnc')
const targetMachineName = ref('Line 1 - OP10 CNC Milling Cell')

const MACHINES = [
  { id: 'mach-op10-cnc', name: 'Line 1 - OP10 CNC Milling Cell' },
  { id: 'mach-op20-weld', name: 'Line 1 - OP20 Robotic Welding Station' },
  { id: 'mach-op30-vision', name: 'Line 1 - OP30 Optical Inspection Cell' },
  { id: 'mach-op40-pack', name: 'Line 2 - Battery Pack Automation Station' }
]

const SAMPLE_CHATGPT_RESPONSE = `\`\`\`json
[
  {
    "name": "Beckhoff C6030 Ultra-Compact Industrial PC",
    "category": "Hardware",
    "manufacturer": "Beckhoff Automation",
    "modelNumber": "C6030-0060",
    "serialNumber": "SN-IPC-6030-9912",
    "quantity": 1,
    "location": "Cabinet A1 - DIN Rail 3",
    "estimatedPriceEur": 2650,
    "childComponents": [
      {
        "name": "TwinCAT 3 NC PTP PLC Runtime (Level 50)",
        "category": "Software",
        "manufacturer": "Beckhoff Automation",
        "estimatedPriceEur": 1100
      },
      {
        "name": "Innodisk 240GB 3ME4 Industrial SSD",
        "category": "Hardware",
        "manufacturer": "Innodisk",
        "estimatedPriceEur": 180
      },
      {
        "name": "Beckhoff FC1100 Dual PCI-E EtherCAT Master",
        "category": "Hardware",
        "manufacturer": "Beckhoff Automation",
        "estimatedPriceEur": 340
      }
    ]
  },
  {
    "name": "Festo CPX Modular Valve Terminal 16-Station",
    "category": "Hardware",
    "manufacturer": "Festo",
    "modelNumber": "CPX-M-FB36",
    "serialNumber": "SN-FESTO-CPX-882",
    "quantity": 1,
    "location": "Pneumatics Enclosure P1",
    "estimatedPriceEur": 1250
  },
  {
    "name": "Cognex In-Sight 8402M Ultra-Fast Vision Camera",
    "category": "Hardware",
    "manufacturer": "Cognex",
    "modelNumber": "IS8402M",
    "serialNumber": "SN-CGNX-8402-55",
    "quantity": 1,
    "location": "Optical Inspection Station OP30",
    "estimatedPriceEur": 3400
  }
]
\`\`\``

const rawContent = ref(SAMPLE_CHATGPT_RESPONSE)
const isImporting = ref(false)
const errorMessage = ref<string | null>(null)

const onMachineChange = (e: Event) => {
  const sel = (e.target as HTMLSelectElement).value
  const found = MACHINES.find(m => m.id === sel)
  if (found) {
    targetMachineId.value = found.id
    targetMachineName.value = found.name
  }
}

const loadSample = () => {
  rawContent.value = SAMPLE_CHATGPT_RESPONSE
}

// Live preview parsing
const parsedPreview = computed(() => {
  errorMessage.value = null
  const trimmed = rawContent.value.trim()
  if (!trimmed) return []

  const jsonMatch = trimmed.match(/```(?:json)?\s*([\s\S]*?)\s*```/) || [null, trimmed]
  const candidate = (jsonMatch[1] || trimmed).trim()

  try {
    const parsed = JSON.parse(candidate)
    const list = Array.isArray(parsed) ? parsed : (parsed.items || parsed.components || [parsed])
    return list
  } catch (err: any) {
    return []
  }
})

const totalExtractedCount = computed(() => {
  let count = parsedPreview.value.length
  for (const item of parsedPreview.value) {
    if (item.childComponents && Array.isArray(item.childComponents)) {
      count += item.childComponents.length
    }
  }
  return count
})

const handleImport = async () => {
  if (parsedPreview.value.length === 0) {
    errorMessage.value = 'Please provide valid ChatGPT JSON output or BOM text.'
    return
  }

  isImporting.value = true
  errorMessage.value = null

  try {
    const payload: MachineDocumentImportPayload = {
      machineId: targetMachineId.value,
      machineName: targetMachineName.value,
      source: 'chatgpt',
      rawContent: rawContent.value
    }

    const res = await $fetch<MachineDocumentImportResult>('/api/inventory/machine-import', {
      method: 'POST',
      body: payload
    })

    emit('imported', res)
    emit('update:open', false)
  } catch (err: any) {
    errorMessage.value = err.data?.message || err.message || 'Failed to import machine document.'
  } finally {
    isImporting.value = false
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
    <div class="relative w-full max-w-3xl bg-card border border-border rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/30">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400">
            <Bot class="size-5" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-base font-semibold text-foreground">
                Machine Document Import (AI / ChatGPT)
              </h2>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/10 text-emerald-600 border border-emerald-500/20">
                ChatGPT JSON & BOM
              </span>
            </div>
            <p class="text-xs text-muted-foreground mt-0.5">
              Automate asset extraction from vendor manuals and LLM BOM transcripts with split controller IPC support
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

      <!-- Body Content -->
      <div class="p-6 overflow-y-auto flex-1 space-y-4">
        <!-- Target Machine Assignment -->
        <div class="space-y-1.5 pb-3 border-b border-border">
          <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <Layers class="size-3.5 text-primary" />
            <span>Target Production Machine</span>
          </Label>
          <select
            :value="targetMachineId"
            @change="onMachineChange"
            class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
          >
            <option v-for="m in MACHINES" :key="m.id" :value="m.id">
              {{ m.name }}
            </option>
          </select>
          <p class="text-[11px] text-muted-foreground">
            Extracted assets will be linked to this machine and provisioned into the production asset repository.
          </p>
        </div>

        <!-- ChatGPT Input Area -->
        <div class="space-y-2">
          <div class="flex items-center justify-between">
            <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <Sparkles class="size-3.5 text-amber-500" />
              <span>ChatGPT / LLM Response Transcript</span>
            </Label>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              @click="loadSample"
              class="h-6 text-[11px] text-primary hover:text-primary/80 cursor-pointer"
            >
              Load Sample IPC & BOM
            </Button>
          </div>

          <textarea
            v-model="rawContent"
            rows="8"
            class="w-full p-3 font-mono text-xs rounded-lg bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary custom-scrollbar"
            placeholder="Paste ChatGPT output or JSON BOM here..."
          />
        </div>

        <!-- Live Parsing Feedback -->
        <div v-if="parsedPreview.length > 0" class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-2.5">
          <div class="flex items-center justify-between text-xs">
            <div class="flex items-center gap-2 font-semibold text-foreground">
              <FolderTree class="size-4 text-primary" />
              <span>Parsed Asset Tree Preview ({{ totalExtractedCount }} total items)</span>
            </div>
            <span class="text-[11px] font-mono text-emerald-600 dark:text-emerald-400 font-bold">
              Ready for Provisioning
            </span>
          </div>

          <div class="space-y-2 max-h-48 overflow-y-auto custom-scrollbar">
            <div
              v-for="(item, idx) in parsedPreview"
              :key="idx"
              class="p-2 rounded-md bg-card border border-border/80 text-xs space-y-1"
            >
              <div class="flex items-center justify-between">
                <span class="font-semibold text-foreground">{{ item.name }}</span>
                <span class="font-mono text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground">
                  {{ item.category || 'Hardware' }} · {{ item.manufacturer || 'OEM' }}
                </span>
              </div>

              <!-- Split Child Assets (e.g. IPC modular components) -->
              <div
                v-if="item.childComponents && item.childComponents.length > 0"
                class="ml-4 pl-2 border-l-2 border-primary/40 space-y-1 pt-1"
              >
                <div class="text-[10px] font-semibold text-primary">
                  ↳ Split Modular Child Components ({{ item.childComponents.length }}):
                </div>
                <div
                  v-for="(child, cIdx) in item.childComponents"
                  :key="cIdx"
                  class="flex items-center justify-between text-[11px] text-muted-foreground"
                >
                  <span>• {{ child.name }}</span>
                  <span class="font-mono text-[10px]">{{ child.category }} (€{{ child.estimatedPriceEur || 250 }})</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Error Banner -->
        <div v-if="errorMessage" class="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs flex items-center gap-2">
          <AlertTriangle class="size-4 shrink-0" />
          <span>{{ errorMessage }}</span>
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
          :disabled="parsedPreview.length === 0 || isImporting"
          @click="handleImport"
          class="bg-emerald-600 hover:bg-emerald-700 text-white text-xs h-8 gap-1.5 cursor-pointer disabled:opacity-50"
        >
          <CheckCircle2 class="size-3.5" />
          <span>Import {{ totalExtractedCount }} Assets to Machine</span>
        </Button>
      </div>
    </div>
  </div>
</template>
