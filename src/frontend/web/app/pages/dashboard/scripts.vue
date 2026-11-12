<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { useLuaExtensions } from '~/composables/useLuaExtensions'
import type { LuaScriptRecord, LuaScriptTriggerType } from '~/types/lua'
import LuaKpiWizard from '~/components/lua/LuaKpiWizard.vue'
import LuaApiReferencePanel from '~/components/lua/LuaApiReferencePanel.vue'
import { asKpiResult, explainLuaError } from '~/utils/lua/kpiTemplates'
import { LUA_API_REFERENCE } from '~/utils/lua/luaApiReference'
import {
  CodeIcon,
  PlayIcon,
  SaveIcon,
  Trash2Icon,
  PlusIcon,
  SparklesIcon,
  ActivityIcon,
  ClockIcon,
  CheckCircle2Icon,
  AlertTriangleIcon,
  TerminalIcon,
  RotateCcwIcon,
  FolderTreeIcon,
  GaugeIcon
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard',
  alias: ['/dashboard/automation']
})

const {
  scripts,
  loading,
  executing,
  error,
  lastExecutionResult,
  fetchScripts,
  saveScript,
  deleteScript,
  testExecute
} = useLuaExtensions()

const selectedScriptId = ref<string | null>(null)
const filterType = ref<string>('all')
const editorName = ref('')
const editorDesc = ref('')
const editorTriggerType = ref<LuaScriptTriggerType>('Manual')
const editorCode = ref('')
const editorIsActive = ref(true)
const statusMessage = ref('')
const validationMessage = ref('')
const showKpiWizard = ref(false)
const editorRef = ref<HTMLTextAreaElement | null>(null)

// Stations for the KPI wizard and the test run ("run against this station's live data").
const stations = ref<Array<{ id: string; label: string }>>([])
const testStationId = ref('')
async function loadStations() {
  try {
    const rows = await $fetch<any[]>('/api/proxy/v1/inventory/machines')
    stations.value = (Array.isArray(rows) ? rows : [])
      .map(m => ({ id: String(m.id), label: String(m.customIdentifier || m.displayName || m.name || m.id) }))
      .sort((a, b) => a.label.localeCompare(b.label))
  } catch {
    stations.value = []
  }
}

onMounted(async () => {
  await Promise.all([fetchScripts(), loadStations()])
  if (scripts.value.length > 0) {
    selectScript(scripts.value[0])
  }
})

// A test run executes the script for real: name the calls that change plant data.
const MUTATING_CALLS = LUA_API_REFERENCE.flatMap(g => g.functions.filter(f => f.mutates).map(f => `${g.global}.${f.name}`))
const mutatingCalls = computed(() => MUTATING_CALLS.filter(call => editorCode.value.includes(`${call}(`)))

const kpiResult = computed(() => asKpiResult(lastExecutionResult.value?.returnValue))
const errorHint = computed(() => explainLuaError(lastExecutionResult.value?.error))
const formatDetail = (value: unknown) => (typeof value === 'number' ? String(value) : JSON.stringify(value))

function handleKpiCreate(payload: { name: string; description: string; code: string }) {
  selectedScriptId.value = null
  editorName.value = payload.name
  editorDesc.value = payload.description
  editorTriggerType.value = 'Manual'
  editorCode.value = payload.code
  editorIsActive.value = true
  showKpiWizard.value = false
  validationMessage.value = ''
  statusMessage.value = 'KPI script created. Test it against a station below, then Save.'
}

function insertSnippet(snippet: string) {
  const el = editorRef.value
  const code = editorCode.value
  const start = el ? el.selectionStart : code.length
  const end = el ? el.selectionEnd : code.length
  editorCode.value = code.slice(0, start) + snippet + code.slice(end)
  if (el) {
    const caret = start + snippet.length
    requestAnimationFrame(() => { el.focus(); el.setSelectionRange(caret, caret) })
  }
}

const filteredScripts = computed(() => {
  if (filterType.value === 'all') return scripts.value
  return scripts.value.filter(s => s.triggerType === filterType.value)
})

const selectedScript = computed(() => {
  return scripts.value.find(s => s.id === selectedScriptId.value) || null
})

function selectScript(script: LuaScriptRecord) {
  selectedScriptId.value = script.id
  editorName.value = script.name
  editorDesc.value = script.description
  editorTriggerType.value = script.triggerType
  editorCode.value = script.code
  editorIsActive.value = script.isActive
  statusMessage.value = ''
}

function handleNewScript() {
  selectedScriptId.value = null
  editorName.value = 'New Plant Automation Rule'
  editorDesc.value = 'Custom plant-level rule for machines, telemetry, groupings, or ticketing.'
  editorTriggerType.value = 'Manual'
  editorCode.value = `-- Heimdall Sandboxed Lua Script
print('Executing custom plant automation rule...')

-- 1. Query machines
local active_machines = machines.list({ status = 'active' })
print('Active production stations: ' .. #active_machines)

-- 2. Inspect telemetry
for i = 1, #active_machines do
    local m = active_machines[i]
    local temp = telemetry.get_latest(m.id, 'temperature')
    if temp ~= nil and temp > 75.0 then
        print('Alert: Overheating detected on ' .. m.custom_identifier .. ': ' .. temp .. ' °C')
    end
end

return { status = 'Evaluation completed', evaluated_count = #active_machines }
`
  editorIsActive.value = true
}

async function handleSave() {
  validationMessage.value = !editorName.value.trim()
    ? 'Give the script a name before saving.'
    : !editorCode.value.trim()
      ? 'The script has no code yet.'
      : ''
  if (validationMessage.value) return
  try {
    const payload: Partial<LuaScriptRecord> = {
      id: selectedScriptId.value || undefined,
      name: editorName.value,
      description: editorDesc.value,
      triggerType: editorTriggerType.value,
      code: editorCode.value,
      isActive: editorIsActive.value
    }

    const saved = await saveScript(payload)
    selectedScriptId.value = saved.id
    statusMessage.value = 'Script saved and deployed to sandbox successfully.'
    setTimeout(() => { statusMessage.value = '' }, 4000)
  } catch (err: any) {
    // Error is set in composable
  }
}

async function handleDelete() {
  if (!selectedScriptId.value) return
  if (confirm(`Delete script "${editorName.value}"?`)) {
    await deleteScript(selectedScriptId.value)
    if (scripts.value.length > 0) {
      selectScript(scripts.value[0])
    } else {
      handleNewScript()
    }
  }
}

async function handleRunTest() {
  statusMessage.value = ''
  await testExecute(editorCode.value, {
    context_test_mode: true,
    context_simulated_time: new Date().toISOString(),
    ...(testStationId.value ? { context_machine_id: testStationId.value } : {})
  })
}

function loadTemplate(templateType: string) {
  if (templateType === 'telemetry') {
    editorName.value = 'Spindle Vibration & Temperature Watchdog'
    editorTriggerType.value = 'TelemetryChange'
    editorCode.value = `local machine_id = context_machine_id or '11111111-1111-1111-1111-111111111111'
local vib = telemetry.get_latest(machine_id, 'spindle_vibration')
local temp = telemetry.get_latest(machine_id, 'temperature')

print('Evaluating telemetry for machine: ' .. machine_id)
print('Current Vibration: ' .. tostring(vib) .. ' G, Temperature: ' .. tostring(temp) .. ' °C')

local history = telemetry.get_history(machine_id, 'spindle_vibration', 10)
local avg_vib = telemetry.moving_average(history, 5)

if vib ~= nil and avg_vib ~= nil and vib > (avg_vib * 1.5) then
    print('WARNING: Spindle vibration spike detected!')
    local spike_res = telemetry.detect_spike(vib, history, 2.0)
    if spike_res.is_spike then
        local t = tickets.create({
            title = '[AUTO-TELEMETRY] Excessive Spindle Vibration Spike',
            machine_id = machine_id,
            priority = 'High',
            sla_hours = 4.0
        })
        print('Created ticket: ' .. t.id)
        return t.id
    end
end
return 'Telemetry baseline normal'
`
  } else if (templateType === 'sla') {
    editorName.value = 'SLA Preventive Maintenance Scheduler'
    editorTriggerType.value = 'SlaPreventiveCheck'
    editorCode.value = `print('Running Plant SLA Preventive Maintenance Scan...')
local machines_list = machines.list({ status = 'active' })

for i = 1, #machines_list do
    local m = machines_list[i]
    local hours = telemetry.get_latest(m.id, 'runtime_hours') or 0.0
    if hours >= 1400.0 then
        local existing = tickets.list({ machine_id = m.id, status = 'Open' })
        if #existing == 0 then
            local pm = tickets.trigger_preventive_maintenance(m.id, '1500h Periodic Overhaul', {
                priority = 'Medium',
                sla_hours = 24.0
            })
            print('Preventive maintenance ticket created: ' .. pm.id)
        end
    end
end
return 'SLA evaluation complete'
`
  } else if (templateType === 'ui') {
    editorName.value = 'Plant Dynamic UI Extension'
    editorTriggerType.value = 'UiExtension'
    editorCode.value = `print('Registering Dynamic UI Extensions...')

ui.register_action({
    id = 'action_recalibrate_spindle',
    title = 'Recalibrate Spindle Drive',
    description = 'Executes closed-loop zero-offset calibration on the station axis drive.',
    target = 'machine',
    icon = 'i-lucide-wrench',
    color = 'indigo',
    handler = function(ctx)
        print('Recalibration triggered for machine ' .. tostring(ctx and ctx.machine_id))
        return { status = 'Calibration routine dispatched successfully' }
    end
})

ui.register_badge({
    id = 'badge_sla_risk',
    title = 'SLA Critical',
    target = 'ticket',
    color = 'red'
})

return true
`
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-foreground flex items-center gap-2">
          <CodeIcon class="h-6 w-6 text-primary" />
          Plant Automation Scripting & Rules
        </h1>
        <p class="text-sm text-muted-foreground">
          Safe sandboxed Lua scripting platform to mold plant-specific machine rules, telemetry analytics, groupings, and SLA preventive ticketing.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button size="sm" variant="outline" class="gap-1.5" data-testid="new-kpi-btn" @click="showKpiWizard = true">
          <GaugeIcon class="h-4 w-4" />
          New KPI
        </Button>
        <Button size="sm" class="gap-1.5" @click="handleNewScript">
          <PlusIcon class="h-4 w-4" />
          New Script
        </Button>
      </div>
    </div>

    <LuaKpiWizard
      v-if="showKpiWizard"
      :stations="stations"
      :default-station-id="testStationId || null"
      @create="handleKpiCreate"
      @close="showKpiWizard = false"
    />

    <!-- Feedback alerts -->
    <div v-if="validationMessage" class="p-3 bg-amber-500/10 border border-amber-500/20 text-amber-500 rounded-lg text-sm flex items-center gap-2" role="alert">
      <AlertTriangleIcon class="h-4 w-4 shrink-0" />
      <span>{{ validationMessage }}</span>
    </div>
    <div v-if="statusMessage" class="p-3 bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 rounded-lg text-sm flex items-center gap-2">
      <CheckCircle2Icon class="h-4 w-4 shrink-0" />
      <span>{{ statusMessage }}</span>
    </div>
    <div v-if="error" class="p-3 bg-red-500/10 border border-red-500/20 text-red-400 rounded-lg text-sm flex items-center gap-2">
      <AlertTriangleIcon class="h-4 w-4 shrink-0" />
      <span>{{ error }}</span>
    </div>

    <!-- Main Grid: Sidebar + Editor -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
      <!-- Script List Sidebar -->
      <div class="lg:col-span-4 space-y-4">
        <Card>
          <CardHeader class="pb-3">
            <div class="flex items-center justify-between">
              <CardTitle class="text-base font-semibold">Automation Scripts</CardTitle>
              <Badge variant="secondary">{{ filteredScripts.length }}</Badge>
            </div>
            <!-- Category Filter Tabs -->
            <div class="flex flex-wrap gap-1 pt-2">
              <button
                class="px-2 py-1 text-xs rounded transition-colors"
                :class="filterType === 'all' ? 'bg-primary text-primary-foreground font-medium' : 'bg-muted text-muted-foreground hover:bg-muted/80'"
                @click="filterType = 'all'"
              >
                All
              </button>
              <button
                class="px-2 py-1 text-xs rounded transition-colors"
                :class="filterType === 'TelemetryChange' ? 'bg-primary text-primary-foreground font-medium' : 'bg-muted text-muted-foreground hover:bg-muted/80'"
                @click="filterType = 'TelemetryChange'"
              >
                Telemetry
              </button>
              <button
                class="px-2 py-1 text-xs rounded transition-colors"
                :class="filterType === 'SlaPreventiveCheck' ? 'bg-primary text-primary-foreground font-medium' : 'bg-muted text-muted-foreground hover:bg-muted/80'"
                @click="filterType = 'SlaPreventiveCheck'"
              >
                SLA / Tickets
              </button>
              <button
                class="px-2 py-1 text-xs rounded transition-colors"
                :class="filterType === 'UiExtension' ? 'bg-primary text-primary-foreground font-medium' : 'bg-muted text-muted-foreground hover:bg-muted/80'"
                @click="filterType = 'UiExtension'"
              >
                UI Ext
              </button>
            </div>
          </CardHeader>

          <CardContent class="p-2 space-y-1 max-h-[620px] overflow-y-auto">
            <div
              v-for="s in filteredScripts"
              :key="s.id"
              class="p-3 rounded-lg border transition-all cursor-pointer text-left"
              :class="selectedScriptId === s.id ? 'border-primary bg-primary/5 shadow-sm' : 'border-border/60 hover:border-border hover:bg-muted/40'"
              @click="selectScript(s)"
            >
              <div class="flex items-start justify-between gap-2">
                <span class="font-medium text-sm text-foreground line-clamp-1">{{ s.name }}</span>
                <Badge
                  :variant="s.isActive ? 'default' : 'secondary'"
                  class="text-[10px] px-1.5 py-0 h-4 shrink-0"
                >
                  {{ s.triggerType }}
                </Badge>
              </div>
              <p class="text-xs text-muted-foreground mt-1 line-clamp-2">
                {{ s.description || 'No description provided' }}
              </p>
              <div class="flex items-center justify-between text-[11px] text-muted-foreground/70 mt-2">
                <span>{{ s.isActive ? 'Active' : 'Disabled' }}</span>
                <span v-if="s.lastExecutedAt">Ran {{ new Date(s.lastExecutedAt).toLocaleTimeString() }}</span>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Starter Template Quick Buttons -->
        <Card>
          <CardHeader class="pb-2">
            <CardTitle class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Starter Templates (Spec Presets)
            </CardTitle>
          </CardHeader>
          <CardContent class="space-y-1.5">
            <Button
              variant="outline"
              size="sm"
              class="w-full justify-start text-xs h-8 gap-2"
              @click="loadTemplate('telemetry')"
            >
              <ActivityIcon class="h-3.5 w-3.5 text-blue-400" />
              Telemetry Anomaly Spike Watchdog
            </Button>
            <Button
              variant="outline"
              size="sm"
              class="w-full justify-start text-xs h-8 gap-2"
              @click="loadTemplate('sla')"
            >
              <ClockIcon class="h-3.5 w-3.5 text-amber-400" />
              SLA Preventive Maintenance Scheduler
            </Button>
            <Button
              variant="outline"
              size="sm"
              class="w-full justify-start text-xs h-8 gap-2"
              @click="loadTemplate('ui')"
            >
              <SparklesIcon class="h-3.5 w-3.5 text-indigo-400" />
              Dynamic UI Extension Actions & Badges
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardContent class="pt-4">
            <LuaApiReferencePanel @insert="insertSnippet" />
          </CardContent>
        </Card>
      </div>

      <!-- Editor & Playground Workspace -->
      <div class="lg:col-span-8 space-y-4">
        <Card class="border-border">
          <CardHeader class="pb-3 border-b">
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
              <div class="space-y-1 flex-1">
                <Input
                  v-model="editorName"
                  placeholder="Script Name"
                  class="text-base font-semibold h-9 bg-transparent border-transparent hover:border-border focus:border-primary"
                />
                <Input
                  v-model="editorDesc"
                  placeholder="Brief description of rule or workflow"
                  class="text-xs text-muted-foreground h-7 bg-transparent border-transparent hover:border-border"
                />
              </div>

              <!-- Controls -->
              <div class="flex items-center gap-2 shrink-0">
                <select
                  v-model="editorTriggerType"
                  class="h-8 text-xs rounded-md border border-input bg-background px-2"
                >
                  <option value="Manual">Manual</option>
                  <option value="TelemetryChange">Telemetry Change</option>
                  <option value="MachineStateChange">Machine State Change</option>
                  <option value="SlaPreventiveCheck">SLA Preventive Check</option>
                  <option value="UiExtension">UI Extension</option>
                  <option value="Scheduled">Scheduled</option>
                </select>

                <select
                  v-model="testStationId"
                  class="h-8 max-w-40 text-xs rounded-md border border-input bg-background px-2"
                  aria-label="Station for the test run"
                  title="The test run passes this station as context_machine_id"
                >
                  <option value="">No station</option>
                  <option v-for="st in stations" :key="st.id" :value="st.id">{{ st.label }}</option>
                </select>

                <Button
                  size="sm"
                  variant="outline"
                  class="gap-1.5 h-8 text-xs"
                  :disabled="executing"
                  data-testid="test-run-btn"
                  @click="handleRunTest"
                >
                  <PlayIcon class="h-3.5 w-3.5 text-emerald-400 fill-emerald-400/20" />
                  Test run
                </Button>

                <Button
                  size="sm"
                  class="gap-1.5 h-8 text-xs"
                  :disabled="loading"
                  @click="handleSave"
                >
                  <SaveIcon class="h-3.5 w-3.5" />
                  Save
                </Button>

                <Button
                  v-if="selectedScriptId"
                  size="sm"
                  variant="ghost"
                  class="h-8 px-2 text-destructive hover:bg-destructive/10"
                  @click="handleDelete"
                >
                  <Trash2Icon class="h-4 w-4" />
                </Button>
              </div>
            </div>
          </CardHeader>

          <!-- Code Editor Body -->
          <CardContent class="p-0">
            <div class="relative font-mono text-xs">
              <textarea
                ref="editorRef"
                v-model="editorCode"
                rows="18"
                class="w-full p-4 bg-muted/20 font-mono text-xs leading-relaxed focus:outline-none resize-y border-none text-foreground placeholder:text-muted-foreground"
                placeholder="-- Write sandboxed Lua code here..."
                spellcheck="false"
              ></textarea>
            </div>
          </CardContent>
          <div v-if="mutatingCalls.length" class="border-t px-4 py-2 text-xs text-amber-500 flex items-center gap-2">
            <AlertTriangleIcon class="h-3.5 w-3.5 shrink-0" />
            <span>A test run executes for real: {{ mutatingCalls.join(', ') }} will change plant data.</span>
          </div>
        </Card>

        <!-- Live Sandbox Terminal & Console Output -->
        <Card class="bg-card border-border">
          <CardHeader class="py-2.5 px-4 border-b flex flex-row items-center justify-between">
            <div class="flex items-center gap-2">
              <TerminalIcon class="h-4 w-4 text-primary" />
              <CardTitle class="text-xs font-mono uppercase tracking-wider">
                Sandbox Console Output
              </CardTitle>
            </div>
            <div v-if="lastExecutionResult" class="flex items-center gap-3 text-xs">
              <span v-if="lastExecutionResult.duration" class="text-muted-foreground">
                Time: {{ lastExecutionResult.duration }}
              </span>
              <Badge
                :variant="lastExecutionResult.success ? 'default' : 'destructive'"
                class="text-[10px]"
              >
                {{ lastExecutionResult.success ? 'Success' : 'Error' }}
              </Badge>
            </div>
          </CardHeader>

          <CardContent class="p-4 font-mono text-xs max-h-56 overflow-y-auto space-y-1 bg-black/40 text-emerald-300">
            <div v-if="executing" class="text-muted-foreground animate-pulse">
              Running script in isolated MoonSharp sandbox...
            </div>
            <div v-else-if="!lastExecutionResult" class="text-muted-foreground/60 italic">
              No execution output yet. Pick a station and click "Test run" above to run this script against its live data.
            </div>
            <template v-else>
              <div v-if="kpiResult" class="mb-2 rounded-lg border border-border/60 bg-background/60 p-3 font-sans text-foreground" data-testid="kpi-result">
                <div class="flex items-baseline justify-between gap-3">
                  <span class="text-xs text-muted-foreground">{{ kpiResult.kpi }}<template v-if="kpiResult.station"> · {{ kpiResult.station }}</template></span>
                  <span
                    v-if="kpiResult.status"
                    class="rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase"
                    :class="kpiResult.status === 'ok' ? 'bg-emerald-500/15 text-emerald-500' : 'bg-amber-500/15 text-amber-500'"
                  >{{ kpiResult.status.replace('_', ' ') }}</span>
                </div>
                <div class="mt-1 text-2xl font-bold tabular-nums">
                  {{ kpiResult.value }}<span class="ml-1 text-sm font-normal text-muted-foreground">{{ kpiResult.unit }}</span>
                  <span v-if="kpiResult.target !== undefined" class="ml-2 text-xs font-normal text-muted-foreground">target {{ kpiResult.target }}{{ kpiResult.unit }}</span>
                </div>
                <div v-if="kpiResult.details" class="mt-1 flex flex-wrap gap-x-3 text-[11px] text-muted-foreground">
                  <span v-for="(v, k) in kpiResult.details" :key="String(k)">{{ k }}: {{ formatDetail(v) }}</span>
                </div>
              </div>
              <div v-if="lastExecutionResult.output" class="whitespace-pre-wrap">
                {{ lastExecutionResult.output }}
              </div>
              <div v-if="!kpiResult && lastExecutionResult.returnValue !== undefined && lastExecutionResult.returnValue !== null" class="text-cyan-400 mt-2 pt-2 border-t border-border/40">
                <span class="text-muted-foreground">Return: </span>
                {{ typeof lastExecutionResult.returnValue === 'object' ? JSON.stringify(lastExecutionResult.returnValue, null, 2) : lastExecutionResult.returnValue }}
              </div>
              <div v-if="lastExecutionResult.error" class="text-red-400 bg-red-950/40 p-2 rounded border border-red-800/40 mt-2">
                {{ lastExecutionResult.error }}
                <div v-if="errorHint" class="mt-1 font-sans text-amber-300" data-testid="lua-error-hint">{{ errorHint }}</div>
              </div>
            </template>
          </CardContent>
        </Card>
      </div>
    </div>
  </div>
</template>
