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
import RbacButton from '~/components/common/RbacButton.vue'
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
  <div class="space-y-6 animate-in fade-in duration-300">
    <!-- Header Section -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div>
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <FileCode2 class="size-6" />
          </div>
          <div>
            <h1 class="text-2xl font-bold tracking-tight text-slate-100">
              Agent Telemetry Templating Studio
            </h1>
            <p class="text-sm text-slate-400 mt-0.5">
              Declarative data point recipes, sampling schedules, deadband filters, and live JSON payload preview
            </p>
          </div>
        </div>
      </div>

      <!-- Action Buttons -->
      <div class="flex flex-wrap items-center gap-2.5 shrink-0">
        <label class="cursor-pointer">
          <input type="file" accept=".json" class="hidden" @change="handleImportJson" />
          <Button variant="outline" size="sm" class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 rounded-lg text-xs font-medium h-8 px-3">
            <Upload class="size-3.5 mr-1.5 text-slate-400" />
            Import JSON
          </Button>
        </label>

        <Button variant="outline" size="sm" @click="exportJson" class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 rounded-lg text-xs font-medium h-8 px-3">
          <Download class="size-3.5 mr-1.5 text-slate-400" />
          Export
        </Button>

        <RbacButton capability="canManageEndpoints" size="sm" @click="handleCreateNew" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium h-8 px-3.5 shadow-sm transition-all border-0">
          <Plus class="size-3.5 mr-1.5" />
          New Template
        </RbacButton>
      </div>
    </div>

    <!-- Template Catalog Grid / Selector -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="tpl in allTemplates"
        :key="tpl.recipeId"
        @click="loadTemplateIntoEditor(tpl)"
        class="cursor-pointer border rounded-xl transition-all duration-200"
        :class="selectedTemplateId === tpl.recipeId 
          ? 'bg-indigo-950/20 border-indigo-500/50 shadow-sm' 
          : 'bg-slate-900 border-slate-800 hover:border-slate-700 hover:bg-slate-850/60'"
      >
        <CardHeader class="p-4 sm:p-5 pb-2">
          <div class="flex items-start justify-between gap-3">
            <div>
              <div class="flex items-center gap-2">
                <Badge
                  variant="outline"
                  class="text-xs font-medium px-2 py-0.5 rounded-md"
                  :class="tpl.isBuiltin ? 'border-indigo-500/30 text-indigo-400 bg-indigo-500/10' : 'border-purple-500/30 text-purple-400 bg-purple-500/10'"
                >
                  {{ tpl.isBuiltin ? 'Built-in Standard' : 'Custom Recipe' }}
                </Badge>
                <span class="text-xs font-mono text-slate-500">v{{ tpl.version }}</span>
              </div>
              <CardTitle class="text-sm font-semibold text-slate-200 mt-1.5 line-clamp-1">
                {{ tpl.name }}
              </CardTitle>
            </div>
            <div class="size-7 rounded-lg bg-slate-950 border border-slate-800 flex items-center justify-center shrink-0">
              <Radio class="size-3.5" :class="selectedTemplateId === tpl.recipeId ? 'text-indigo-400' : 'text-slate-600'" />
            </div>
          </div>
          <CardDescription class="text-xs text-slate-400 mt-1 line-clamp-2">
            {{ tpl.description || 'No description provided' }}
          </CardDescription>
        </CardHeader>
        <CardContent class="p-4 sm:p-5 pt-0">
          <div class="flex items-center justify-between text-xs font-mono text-slate-400 pt-2.5 border-t border-slate-800/80 mt-2">
            <span class="flex items-center gap-1.5 text-slate-400">
              <Layers class="size-3 text-indigo-400" />
              {{ tpl.dataPoints.length }} Probes
            </span>
            <span class="text-slate-400">
              OS: {{ tpl.targetSelector.osPlatform }}
            </span>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Main Workspace: Split Tabs for Designer, JSON Recipe, and Live Simulator -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardHeader class="p-4 sm:p-5 border-b border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="bg-slate-950 p-1 rounded-lg border border-slate-800 flex gap-1">
            <Button
              variant="ghost"
              size="sm"
              @click="activeTab = 'designer'"
              :class="activeTab === 'designer' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
              class="rounded-md text-xs font-medium px-3 h-8"
            >
              <Sliders class="size-3.5 mr-1.5" />
              Recipe Designer
            </Button>
            <Button
              variant="ghost"
              size="sm"
              @click="activeTab = 'json'"
              :class="activeTab === 'json' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
              class="rounded-md text-xs font-medium px-3 h-8"
            >
              <Code class="size-3.5 mr-1.5" />
              JSON Schema
            </Button>
            <Button
              variant="ghost"
              size="sm"
              @click="activeTab = 'simulator'; if (!simulationOutput) runSimulation()"
              :class="activeTab === 'simulator' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
              class="rounded-md text-xs font-medium px-3 h-8"
            >
              <Play class="size-3.5 mr-1.5" />
              Live Simulator
            </Button>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <span v-if="saveSuccess" class="text-xs text-emerald-400 font-medium flex items-center gap-1">
            <CheckCircle2 class="size-4" /> Saved
          </span>
          <RbacButton
            capability="canManageEndpoints"
            size="sm"
            @click="handleSave"
            :disabled="isSaving"
            class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-medium h-8 px-3.5 border-0 shadow-sm"
          >
            <Check class="size-3.5 mr-1.5" />
            Save Recipe
          </RbacButton>
        </div>
      </CardHeader>

      <CardContent class="p-5 sm:p-6">
        <!-- TAB 1: DESIGNER -->
        <div v-if="activeTab === 'designer'" class="space-y-6">
          <!-- Metadata Form -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 p-4 rounded-xl bg-slate-950/60 border border-slate-800">
            <div class="space-y-1.5 md:col-span-2">
              <Label class="text-xs font-medium text-slate-400">Recipe Name</Label>
              <Input v-model="editorForm.name" class="bg-slate-900 border-slate-800 text-slate-100 text-xs font-semibold h-9 rounded-lg" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs font-medium text-slate-400">Recipe Version</Label>
              <Input v-model="editorForm.version" class="bg-slate-900 border-slate-800 text-slate-100 font-mono text-xs h-9 rounded-lg" />
            </div>
            <div class="space-y-1.5 md:col-span-2">
              <Label class="text-xs font-medium text-slate-400">Description</Label>
              <Input v-model="editorForm.description" class="bg-slate-900 border-slate-800 text-slate-300 text-xs h-9 rounded-lg" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs font-medium text-slate-400">Target OS Platform</Label>
              <select v-model="editorForm.targetSelector.osPlatform" class="w-full h-9 px-3 rounded-lg bg-slate-900 border border-slate-800 text-slate-200 text-xs font-medium">
                <option value="All">All Platforms (Cross-Platform)</option>
                <option value="Windows">Windows (TwinCAT & WMI)</option>
                <option value="Linux">Linux (Embedded IPC / ARM64)</option>
              </select>
            </div>
          </div>

          <!-- Data Points Probes Table -->
          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-slate-200">
                  Configured Probes ({{ editorForm.dataPoints.length }})
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">
                  Signal drivers, sampling strategies, and bandwidth deadbands
                </p>
              </div>
              <RbacButton capability="canManageEndpoints" size="sm" @click="addProbe" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium h-8 px-3 shadow-sm border-0">
                <Plus class="size-3.5 mr-1" />
                Add Probe
              </RbacButton>
            </div>

            <div class="space-y-3">
              <div
                v-for="(dp, idx) in editorForm.dataPoints"
                :key="dp.pointId || idx"
                class="p-3.5 rounded-lg bg-slate-950/60 border border-slate-800 hover:border-slate-700 transition-colors space-y-3"
              >
                <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div class="flex flex-wrap items-center gap-2.5 flex-1">
                    <span class="size-6 rounded-md bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 flex items-center justify-center text-xs font-mono font-medium">
                      {{ idx + 1 }}
                    </span>
                    <Input v-model="dp.name" placeholder="Probe Name" class="bg-slate-900 border-slate-800 text-slate-200 font-medium h-8 w-56 text-xs rounded-md" />
                    <Input v-model="dp.pathOrSymbol" placeholder="Path / Symbol / WMI Query" class="bg-slate-900 border-slate-800 text-slate-300 font-mono text-xs h-8 flex-1 min-w-[200px] rounded-md" />
                  </div>
                  <Button variant="ghost" size="icon" @click="removeProbe(idx)" class="text-slate-400 hover:text-rose-400 hover:bg-rose-500/10 h-7 w-7 rounded-md">
                    <Trash2 class="size-3.5" />
                  </Button>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-2.5 pt-2.5 border-t border-slate-800/80 text-xs">
                  <div>
                    <label class="text-xs font-medium text-slate-400">Driver / Source</label>
                    <select v-model="dp.sourceType" class="mt-1 w-full h-8 px-2 rounded-md bg-slate-900 border border-slate-800 text-slate-200 text-xs">
                      <option v-for="opt in availableSourceTypes" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-xs font-medium text-slate-400">Data Category</label>
                    <select v-model="dp.dataCategory" class="mt-1 w-full h-8 px-2 rounded-md bg-slate-900 border border-slate-800 text-slate-200 text-xs">
                      <option v-for="opt in availableCategories" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-xs font-medium text-slate-400">Deadband Filtering</label>
                    <div class="flex items-center gap-1.5 mt-1">
                      <select v-model="dp.deadband.deadbandType" class="h-8 px-2 rounded-md bg-slate-900 border border-slate-800 text-slate-200 text-xs flex-1">
                        <option v-for="opt in availableDeadbands" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                      </select>
                      <Input 
                        v-if="dp.deadband.deadbandType === 'Percentage' || dp.deadband.deadbandType === 'Absolute'"
                        v-model.number="dp.deadband.deadbandValue" 
                        type="number" 
                        step="0.1" 
                        class="h-8 w-16 bg-slate-900 border-slate-800 text-slate-200 text-xs text-center rounded-md" 
                      />
                    </div>
                  </div>
                  <div>
                    <label class="text-xs font-medium text-slate-400">Egress Priority</label>
                    <select v-model="dp.egressPriority" class="mt-1 w-full h-8 px-2 rounded-md bg-slate-900 border border-slate-800 text-slate-200 text-xs">
                      <option v-for="opt in availablePriorities" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 2: JSON SCHEMA -->
        <div v-if="activeTab === 'json'" class="space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-xs text-slate-400 font-mono">Declarative RecipeDocument Model Representation</span>
            <Button size="sm" variant="outline" @click="copyJson" class="h-8 border-slate-800 text-xs font-medium text-slate-300 rounded-lg">
              <Check v-if="copied" class="size-3.5 mr-1 text-emerald-400" />
              <Copy v-else class="size-3.5 mr-1 text-slate-400" />
              {{ copied ? 'Copied' : 'Copy JSON' }}
            </Button>
          </div>
          <pre class="p-4 rounded-lg bg-slate-950 border border-slate-800 font-mono text-xs text-indigo-300 overflow-x-auto max-h-[550px]">{{ JSON.stringify(editorForm, null, 2) }}</pre>
        </div>

        <!-- TAB 3: LIVE SIMULATOR -->
        <div v-if="activeTab === 'simulator'" class="space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between p-3.5 rounded-lg bg-slate-950 border border-slate-800 gap-3">
            <div>
              <h4 class="text-sm font-semibold text-slate-200">Telemetry Simulation Engine</h4>
              <p class="text-xs text-slate-400 mt-0.5">Executes recipe probes against mock edge environment and evaluates deadbands</p>
            </div>
            <Button size="sm" @click="runSimulation" :disabled="isSimulating" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium h-8 px-3.5 shadow-sm border-0">
              <RefreshCw class="size-3.5 mr-1.5" :class="{'animate-spin': isSimulating}" />
              Run Probe Simulation
            </Button>
          </div>

          <div v-if="simulationOutput" class="space-y-4">
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800">
                <span class="text-xs font-medium text-slate-400">Probes Evaluated</span>
                <div class="text-xl font-mono font-semibold text-slate-100 mt-0.5">{{ simulationOutput.probesExecuted }}</div>
              </div>
              <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800">
                <span class="text-xs font-medium text-slate-400">Target Platform</span>
                <div class="text-xl font-mono font-semibold text-indigo-400 mt-0.5">{{ simulationOutput.targetPlatform }}</div>
              </div>
              <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800">
                <span class="text-xs font-medium text-slate-400">Timestamp</span>
                <div class="text-xs font-mono font-medium text-slate-300 mt-1.5">{{ simulationOutput.evaluatedAt }}</div>
              </div>
            </div>

            <div class="p-4 rounded-lg bg-slate-950 border border-slate-800">
              <span class="text-xs font-medium text-slate-400">Simulated Telemetry Ingestion Payload:</span>
              <pre class="mt-2 font-mono text-xs text-emerald-400 overflow-x-auto max-h-[400px]">{{ JSON.stringify(simulationOutput.payload, null, 2) }}</pre>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
