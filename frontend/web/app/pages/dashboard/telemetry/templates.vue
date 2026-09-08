<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  FileCode2,
  Plus,
  Play,
  Copy,
  Check,
  Download,
  Upload,
  Trash2,
  Layers,
  Sparkles,
  Gauge,
  Sliders,
  Radio,
  Cpu,
  RefreshCw,
  HardDrive,
  Eye,
  CheckCircle2,
  Activity,
  Code
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useTelemetryTemplates } from '~/composables/useTelemetryTemplates'
import type { TelemetryTemplate, DataPointDefinition, RecipeSourceType, DataCategory, EgressPriority, DeadbandType, PollingStrategyType } from '~/types/telemetry'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const {
  allTemplates,
  customTemplates,
  isLoading,
  isSaving,
  fetchTemplates,
  addTemplate,
  updateTemplate,
  deleteTemplate,
  duplicateTemplate,
  simulateEvaluation
} = useTelemetryTemplates()

const selectedTemplateId = ref<string>('tpl-motion-beckhoff-01')
const isEditing = ref(false)
const copied = ref(false)
const activeTab = ref<'designer' | 'json' | 'simulator'>('designer')
const simulationOutput = ref<any | null>(null)
const isSimulating = ref(false)
const saveSuccess = ref(false)

// Active working template in editor
const editorForm = ref<TelemetryTemplate>({
  recipeId: '',
  version: '1.0.0',
  name: '',
  description: '',
  targetSelector: {
    osPlatform: 'All',
    controllerRoles: ['GeneralIPC'],
    tags: {}
  },
  security: {
    keyId: 'pki-key-01',
    algorithm: 'RSA_PSS_SHA256',
    signPayload: true
  },
  dataPoints: []
})

const currentSelected = computed(() => {
  return allTemplates.value.find(t => t.recipeId === selectedTemplateId.value) || allTemplates.value[0]
})

function loadTemplateIntoEditor(template: TelemetryTemplate) {
  selectedTemplateId.value = template.recipeId
  editorForm.value = JSON.parse(JSON.stringify(template))
  simulationOutput.value = null
}

onMounted(async () => {
  await fetchTemplates()
  if (allTemplates.value.length > 0) {
    loadTemplateIntoEditor(allTemplates.value[0])
  }
})

const availableSourceTypes: { label: string; value: RecipeSourceType }[] = [
  { label: 'Beckhoff ADS (TwinCAT Real-Time)', value: 'BeckhoffAds' },
  { label: 'Beckhoff EtherCAT Diagnostics', value: 'BeckhoffEtherCat' },
  { label: 'System CIM / WMI Performance', value: 'SystemCim' },
  { label: 'System Process Deep Inspection', value: 'SystemProcess' },
  { label: 'System Disk & Drive Geometry', value: 'SystemDisk' },
  { label: 'OPC UA Northbound Subscription', value: 'OpcUaSubscription' },
  { label: 'Modbus TCP Register Block', value: 'ModbusTcp' },
  { label: 'Raw TCP Streaming Socket', value: 'TcpSocket' }
]

const availableCategories: { label: string; value: DataCategory }[] = [
  { label: 'Scalar Value', value: 'Scalar' },
  { label: 'Metric Series', value: 'Metric' },
  { label: 'Device State Enum', value: 'DeviceState' },
  { label: 'Structured Map', value: 'Map' },
  { label: 'Array / List', value: 'List' },
  { label: 'Nested Object Tree', value: 'NestedObject' }
]

const availablePriorities: { label: string; value: EgressPriority }[] = [
  { label: 'P0 - Critical Alarm (Immediate)', value: 'P0_CriticalAlarm' },
  { label: 'P1 - High Operational (Bandwidth Guaranteed)', value: 'P1_HighOperational' },
  { label: 'P2 - Medium Metrics (Token Bucket Throttle)', value: 'P2_MediumMetrics' },
  { label: 'P3 - Low Inventory (Opportunistic)', value: 'P3_LowInventory' }
]

const availableDeadbands: { label: string; value: DeadbandType }[] = [
  { label: 'None (Stream All Values)', value: 'None' },
  { label: 'Percentage Change (% Delta)', value: 'Percentage' },
  { label: 'Absolute Difference (Delta Units)', value: 'Absolute' },
  { label: 'State Change Only (Boolean / Enum)', value: 'StateChangeOnly' }
]

const availableStrategies: { label: string; value: PollingStrategyType }[] = [
  { label: 'Periodic Fixed Interval', value: 'Periodic' },
  { label: 'Change of Value (Interrupt)', value: 'ChangeOfValue' },
  { label: 'Cron Expression Schedule', value: 'Cron' },
  { label: 'On Demand RPC Trigger', value: 'OnDemand' }
]

function addProbe() {
  const newProbe: DataPointDefinition = {
    pointId: `dp-probe-${Date.now()}`,
    name: 'New Telemetry Probe',
    description: 'Monitors target runtime signal',
    sourceType: 'SystemCim',
    dataCategory: 'Metric',
    egressPriority: 'P2_MediumMetrics',
    schedule: { strategy: 'Periodic', intervalMs: 5000 },
    deadband: { deadbandType: 'Percentage', deadbandValue: 1.0 },
    pathOrSymbol: 'Custom.Signal'
  }
  editorForm.value.dataPoints.push(newProbe)
}

function removeProbe(index: number) {
  editorForm.value.dataPoints.splice(index, 1)
}

async function handleSave() {
  saveSuccess.value = false
  if (editorForm.value.isBuiltin) {
    // Clone built-in into custom
    const created = await addTemplate({
      ...editorForm.value,
      name: `${editorForm.value.name} (Custom)`
    })
    selectedTemplateId.value = created.recipeId
    editorForm.value = JSON.parse(JSON.stringify(created))
  } else if (editorForm.value.recipeId) {
    await updateTemplate(editorForm.value.recipeId, editorForm.value)
  } else {
    const created = await addTemplate(editorForm.value)
    selectedTemplateId.value = created.recipeId
    editorForm.value = JSON.parse(JSON.stringify(created))
  }
  saveSuccess.value = true
  setTimeout(() => { saveSuccess.value = false }, 3000)
}

function handleCreateNew() {
  const fresh: TelemetryTemplate = {
    recipeId: '',
    version: '1.0.0',
    name: 'New Custom Telemetry Template',
    description: 'Custom industrial telemetry profile',
    targetSelector: {
      osPlatform: 'All',
      controllerRoles: ['GeneralIPC'],
      tags: { Line: 'Line-1' }
    },
    security: {
      keyId: 'pki-custom-key',
      algorithm: 'RSA_PSS_SHA256',
      signPayload: true
    },
    dataPoints: [
      {
        pointId: 'dp-cpu-metric',
        name: 'CPU Utilization',
        sourceType: 'SystemCim',
        dataCategory: 'Metric',
        egressPriority: 'P2_MediumMetrics',
        schedule: { strategy: 'Periodic', intervalMs: 5000 },
        deadband: { deadbandType: 'Percentage', deadbandValue: 2.0 },
        pathOrSymbol: 'Processor.Load'
      }
    ]
  }
  editorForm.value = fresh
  selectedTemplateId.value = ''
  simulationOutput.value = null
  activeTab.value = 'designer'
}

function runSimulation() {
  isSimulating.value = true
  setTimeout(() => {
    simulationOutput.value = simulateEvaluation(editorForm.value)
    isSimulating.value = false
  }, 400)
}

function copyJson() {
  const jsonStr = JSON.stringify(editorForm.value, null, 2)
  navigator.clipboard.writeText(jsonStr)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}

function exportJson() {
  const jsonStr = JSON.stringify(editorForm.value, null, 2)
  const blob = new Blob([jsonStr], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${editorForm.value.name.toLowerCase().replace(/\s+/g, '_')}_recipe.json`
  a.click()
  URL.revokeObjectURL(url)
}

function handleImportJson(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = async (e) => {
    try {
      const parsed = JSON.parse(e.target?.result as string)
      if (parsed && parsed.name && Array.isArray(parsed.dataPoints)) {
        const created = await addTemplate({
          ...parsed,
          name: `${parsed.name} (Imported)`
        })
        loadTemplateIntoEditor(created)
      }
    } catch (err) {
      alert('Invalid JSON file')
    }
  }
  reader.readAsText(file)
}
</script>

<template>
  <div class="space-y-8 animate-in fade-in duration-300">
    <!-- Header Section -->
    <div class="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-2 border-b border-slate-900">
      <div>
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-xl bg-purple-500/10 border border-purple-500/20 text-purple-400">
            <FileCode2 class="size-6" />
          </div>
          <div>
            <h1 class="text-2xl font-black text-slate-100 tracking-tight uppercase">
              Agent Telemetry Templating Studio
            </h1>
            <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">
              Declarative data point recipes, sampling schedules, deadband filters, and live JSON payload preview
            </p>
          </div>
        </div>
      </div>

      <!-- Action Buttons -->
      <div class="flex flex-wrap items-center gap-3 shrink-0">
        <label class="cursor-pointer">
          <input type="file" accept=".json" class="hidden" @change="handleImportJson" />
          <Button variant="outline" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl text-xs font-bold uppercase tracking-widest h-10 px-4">
            <Upload class="size-4 mr-2 text-slate-400" />
            Import JSON
          </Button>
        </label>

        <Button variant="outline" @click="exportJson" class="border-slate-800 bg-slate-900 text-slate-300 rounded-xl text-xs font-bold uppercase tracking-widest h-10 px-4">
          <Download class="size-4 mr-2 text-slate-400" />
          Export
        </Button>

        <Button @click="handleCreateNew" class="bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold uppercase tracking-widest h-10 px-5 shadow-lg shadow-purple-600/20 transition-all border-0">
          <Plus class="size-4 mr-2" />
          New Template
        </Button>
      </div>
    </div>

    <!-- Template Catalog Grid / Selector -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="tpl in allTemplates"
        :key="tpl.recipeId"
        @click="loadTemplateIntoEditor(tpl)"
        class="cursor-pointer border transition-all duration-200"
        :class="selectedTemplateId === tpl.recipeId 
          ? 'bg-purple-950/20 border-purple-500/50 shadow-lg shadow-purple-500/10' 
          : 'bg-slate-900/60 border-slate-800 hover:border-slate-700 hover:bg-slate-900'"
      >
        <CardHeader class="p-5 pb-3">
          <div class="flex items-start justify-between gap-3">
            <div>
              <div class="flex items-center gap-2">
                <Badge
                  variant="outline"
                  class="text-[9px] uppercase font-bold tracking-widest"
                  :class="tpl.isBuiltin ? 'border-indigo-500/30 text-indigo-400 bg-indigo-500/10' : 'border-purple-500/30 text-purple-400 bg-purple-500/10'"
                >
                  {{ tpl.isBuiltin ? 'Built-in Standard' : 'Custom Recipe' }}
                </Badge>
                <span class="text-[10px] font-mono text-slate-500">v{{ tpl.version }}</span>
              </div>
              <CardTitle class="text-sm font-black text-slate-200 mt-2 line-clamp-1">
                {{ tpl.name }}
              </CardTitle>
            </div>
            <div class="size-8 rounded-lg bg-slate-800/80 border border-slate-700/50 flex items-center justify-center shrink-0">
              <Radio class="size-4" :class="selectedTemplateId === tpl.recipeId ? 'text-purple-400' : 'text-slate-500'" />
            </div>
          </div>
          <CardDescription class="text-xs text-slate-400 mt-1 line-clamp-2">
            {{ tpl.description || 'No description provided' }}
          </CardDescription>
        </CardHeader>
        <CardContent class="p-5 pt-0">
          <div class="flex items-center justify-between text-[11px] font-mono text-slate-500 pt-3 border-t border-slate-800/60 mt-2">
            <span class="flex items-center gap-1.5 text-slate-400">
              <Layers class="size-3 text-purple-400" />
              {{ tpl.dataPoints.length }} Probes
            </span>
            <span class="text-slate-400 uppercase">
              OS: {{ tpl.targetSelector.osPlatform }}
            </span>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Main Workspace: Split Tabs for Designer, JSON Recipe, and Live Simulator -->
    <Card class="bg-slate-900/60 border-slate-800">
      <CardHeader class="p-6 border-b border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="bg-slate-950 p-1 rounded-xl border border-slate-800 flex gap-1">
            <Button
              variant="ghost"
              size="sm"
              @click="activeTab = 'designer'"
              :class="activeTab === 'designer' ? 'bg-purple-600 text-white font-bold' : 'text-slate-400 hover:text-slate-200'"
              class="rounded-lg text-xs uppercase px-4 h-8"
            >
              <Sliders class="size-3.5 mr-1.5" />
              Recipe Designer
            </Button>
            <Button
              variant="ghost"
              size="sm"
              @click="activeTab = 'json'"
              :class="activeTab === 'json' ? 'bg-purple-600 text-white font-bold' : 'text-slate-400 hover:text-slate-200'"
              class="rounded-lg text-xs uppercase px-4 h-8"
            >
              <Code class="size-3.5 mr-1.5" />
              JSON Schema
            </Button>
            <Button
              variant="ghost"
              size="sm"
              @click="activeTab = 'simulator'; if (!simulationOutput) runSimulation()"
              :class="activeTab === 'simulator' ? 'bg-purple-600 text-white font-bold' : 'text-slate-400 hover:text-slate-200'"
              class="rounded-lg text-xs uppercase px-4 h-8"
            >
              <Play class="size-3.5 mr-1.5" />
              Live Simulator
            </Button>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <span v-if="saveSuccess" class="text-xs text-emerald-400 font-bold flex items-center gap-1">
            <CheckCircle2 class="size-4" /> Saved!
          </span>
          <Button
            @click="handleSave"
            :disabled="isSaving"
            class="bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-bold uppercase tracking-widest h-9 px-5 border-0"
          >
            <Check class="size-4 mr-1.5" />
            Save Recipe
          </Button>
        </div>
      </CardHeader>

      <CardContent class="p-6">
        <!-- TAB 1: DESIGNER -->
        <div v-if="activeTab === 'designer'" class="space-y-8">
          <!-- Metadata Form -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-6 p-5 rounded-2xl bg-slate-950/60 border border-slate-800">
            <div class="space-y-2 md:col-span-2">
              <Label class="text-xs uppercase font-bold text-slate-400">Recipe Name</Label>
              <Input v-model="editorForm.name" class="bg-slate-900 border-slate-800 text-slate-100 font-bold" />
            </div>
            <div class="space-y-2">
              <Label class="text-xs uppercase font-bold text-slate-400">Recipe Version</Label>
              <Input v-model="editorForm.version" class="bg-slate-900 border-slate-800 text-slate-100 font-mono" />
            </div>
            <div class="space-y-2 md:col-span-2">
              <Label class="text-xs uppercase font-bold text-slate-400">Description</Label>
              <Input v-model="editorForm.description" class="bg-slate-900 border-slate-800 text-slate-300" />
            </div>
            <div class="space-y-2">
              <Label class="text-xs uppercase font-bold text-slate-400">Target OS Platform</Label>
              <select v-model="editorForm.targetSelector.osPlatform" class="w-full h-10 px-3 rounded-md bg-slate-900 border border-slate-800 text-slate-200 text-sm font-bold">
                <option value="All">All Platforms (Cross-Platform)</option>
                <option value="Windows">Windows (TwinCAT & WMI)</option>
                <option value="Linux">Linux (Embedded IPC / ARM64)</option>
              </select>
            </div>
          </div>

          <!-- Data Points Probes Table -->
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-black text-slate-200 uppercase tracking-wide">
                  Configured Probes ({{ editorForm.dataPoints.length }})
                </h3>
                <p class="text-xs text-slate-500 uppercase tracking-widest">
                  Signal drivers, sampling strategies, and bandwidth deadbands
                </p>
              </div>
              <Button size="sm" @click="addProbe" class="bg-purple-600/20 hover:bg-purple-600/30 text-purple-300 border border-purple-500/30 rounded-lg text-xs font-bold">
                <Plus class="size-3.5 mr-1" />
                Add Probe
              </Button>
            </div>

            <div class="space-y-3">
              <div
                v-for="(dp, idx) in editorForm.dataPoints"
                :key="dp.pointId || idx"
                class="p-4 rounded-xl bg-slate-950/80 border border-slate-800 hover:border-slate-700 transition-colors space-y-4"
              >
                <div class="flex items-center justify-between gap-4">
                  <div class="flex items-center gap-3">
                    <span class="size-6 rounded-md bg-purple-500/10 border border-purple-500/20 text-purple-400 flex items-center justify-center text-xs font-mono font-bold">
                      {{ idx + 1 }}
                    </span>
                    <Input v-model="dp.name" placeholder="Probe Name" class="bg-slate-900 border-slate-800 text-slate-200 font-bold h-9 w-64" />
                    <Input v-model="dp.pathOrSymbol" placeholder="Path / Symbol / WMI Query" class="bg-slate-900 border-slate-800 text-slate-300 font-mono text-xs h-9 w-72" />
                  </div>
                  <Button variant="ghost" size="icon" @click="removeProbe(idx)" class="text-slate-500 hover:text-red-400 hover:bg-red-500/10 h-8 w-8">
                    <Trash2 class="size-4" />
                  </Button>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 pt-2 border-t border-slate-900 text-xs">
                  <div>
                    <label class="text-[10px] font-bold text-slate-500 uppercase">Driver / Source</label>
                    <select v-model="dp.sourceType" class="mt-1 w-full h-8 px-2 rounded bg-slate-900 border border-slate-800 text-slate-200 text-xs">
                      <option v-for="opt in availableSourceTypes" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[10px] font-bold text-slate-500 uppercase">Data Category</label>
                    <select v-model="dp.dataCategory" class="mt-1 w-full h-8 px-2 rounded bg-slate-900 border border-slate-800 text-slate-200 text-xs">
                      <option v-for="opt in availableCategories" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[10px] font-bold text-slate-500 uppercase">Deadband Filtering</label>
                    <div class="flex items-center gap-1.5 mt-1">
                      <select v-model="dp.deadband.deadbandType" class="h-8 px-2 rounded bg-slate-900 border border-slate-800 text-slate-200 text-xs flex-1">
                        <option v-for="opt in availableDeadbands" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                      </select>
                      <Input 
                        v-if="dp.deadband.deadbandType === 'Percentage' || dp.deadband.deadbandType === 'Absolute'"
                        v-model.number="dp.deadband.deadbandValue" 
                        type="number" 
                        step="0.1" 
                        class="h-8 w-16 bg-slate-900 border-slate-800 text-slate-200 text-xs text-center" 
                      />
                    </div>
                  </div>
                  <div>
                    <label class="text-[10px] font-bold text-slate-500 uppercase">Egress Priority</label>
                    <select v-model="dp.egressPriority" class="mt-1 w-full h-8 px-2 rounded bg-slate-900 border border-slate-800 text-slate-200 text-xs">
                      <option v-for="opt in availablePriorities" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 2: JSON SCHEMA -->
        <div v-if="activeTab === 'json'" class="space-y-4">
          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-500 font-mono">Declarative RecipeDocument Model Representation</span>
            <Button size="sm" variant="outline" @click="copyJson" class="h-8 border-slate-800 text-xs font-bold text-slate-300">
              <Check v-if="copied" class="size-3.5 mr-1 text-emerald-400" />
              <Copy v-else class="size-3.5 mr-1 text-slate-400" />
              {{ copied ? 'Copied' : 'Copy JSON' }}
            </Button>
          </div>
          <pre class="p-5 rounded-xl bg-slate-950 border border-slate-800 font-mono text-xs text-purple-300 overflow-x-auto max-h-[600px]">{{ JSON.stringify(editorForm, null, 2) }}</pre>
        </div>

        <!-- TAB 3: LIVE SIMULATOR -->
        <div v-if="activeTab === 'simulator'" class="space-y-6">
          <div class="flex items-center justify-between p-4 rounded-xl bg-slate-950 border border-slate-800">
            <div>
              <h4 class="text-sm font-bold text-slate-200 uppercase">Telemetry Simulation Engine</h4>
              <p class="text-xs text-slate-500 mt-0.5">Executes recipe probes against mock edge environment and evaluates deadbands</p>
            </div>
            <Button @click="runSimulation" :disabled="isSimulating" class="bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold uppercase tracking-wider h-9 px-4">
              <RefreshCw class="size-3.5 mr-1.5" :class="{'animate-spin': isSimulating}" />
              Run Probe Simulation
            </Button>
          </div>

          <div v-if="simulationOutput" class="space-y-4">
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div class="p-4 rounded-xl bg-slate-950 border border-slate-800">
                <span class="text-[10px] font-bold text-slate-500 uppercase">Probes Evaluated</span>
                <div class="text-xl font-mono font-black text-slate-100 mt-1">{{ simulationOutput.probesExecuted }}</div>
              </div>
              <div class="p-4 rounded-xl bg-slate-950 border border-slate-800">
                <span class="text-[10px] font-bold text-slate-500 uppercase">Target Platform</span>
                <div class="text-xl font-mono font-black text-purple-400 mt-1">{{ simulationOutput.targetPlatform }}</div>
              </div>
              <div class="p-4 rounded-xl bg-slate-950 border border-slate-800">
                <span class="text-[10px] font-bold text-slate-500 uppercase">Timestamp</span>
                <div class="text-xs font-mono font-black text-slate-300 mt-2">{{ simulationOutput.evaluatedAt }}</div>
              </div>
            </div>

            <div class="p-5 rounded-xl bg-slate-950 border border-slate-800">
              <span class="text-xs font-bold text-slate-400 uppercase tracking-wider">Simulated Telemetry Ingestion Payload:</span>
              <pre class="mt-3 font-mono text-xs text-emerald-400 overflow-x-auto max-h-[450px]">{{ JSON.stringify(simulationOutput.payload, null, 2) }}</pre>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
