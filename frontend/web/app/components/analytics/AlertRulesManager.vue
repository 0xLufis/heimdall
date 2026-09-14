<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { useAlertRules } from '~/composables/useAlertRules'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import type { AlertRule, AlertEvent, RuleCategory, RuleOperator, AlertSeverity } from '~/types/alertRules'
import type { UserTelemetryMetric, CreateTelemetryMetricInput } from '~/types/telemetryMetrics'
import {
  Bell,
  AlertTriangle,
  ShieldAlert,
  CheckCircle2,
  Sliders,
  Play,
  Ticket,
  ExternalLink,
  Plus,
  Trash2,
  Edit2,
  RefreshCw,
  Clock,
  Activity,
  Cpu,
  Flame,
  Wind,
  Zap,
  Layers,
  ChevronRight,
  Filter,
  Check,
  RotateCcw
} from 'lucide-vue-next'

const router = useRouter()
const {
  rules,
  alerts,
  isEvaluating,
  lastEvaluationTime,
  activeAlertsCount,
  criticalAlertsCount,
  enabledRulesCount,
  totalAutomatedTicketsCount,
  acknowledgeAlert,
  resolveAlert,
  toggleRule,
  deleteRule,
  addRule,
  runSimulatedEvaluation
} = useAlertRules()

const {
  metrics: telemetryMetrics,
  userDefinedMetrics,
  createCustomMetric,
  deleteCustomMetric,
  fetchMetrics: fetchTelemetryMetrics
} = useTelemetryMetrics()

onMounted(() => {
  fetchTelemetryMetrics()
})

type SubTab = 'alerts' | 'rules' | 'metrics' | 'sandbox' | 'ticket-log'
const activeSubTab = ref<SubTab>('alerts')

// Filters
const alertStatusFilter = ref<'all' | 'active' | 'acknowledged'>('active')
const ruleCategoryFilter = ref<string>('all')
const metricOriginFilter = ref<'all' | 'builtin' | 'custom'>('all')

const filteredAlerts = computed(() => {
  if (alertStatusFilter.value === 'all') return alerts.value
  return alerts.value.filter(a => a.status === alertStatusFilter.value)
})

const filteredRules = computed(() => {
  if (ruleCategoryFilter.value === 'all') return rules.value
  return rules.value.filter(r => r.category === ruleCategoryFilter.value)
})

const filteredMetricsList = computed(() => {
  if (metricOriginFilter.value === 'builtin') return telemetryMetrics.value.filter(m => !m.isUserDefined)
  if (metricOriginFilter.value === 'custom') return telemetryMetrics.value.filter(m => m.isUserDefined)
  return telemetryMetrics.value
})

// Auto-ticket list
const automatedTicketsList = computed(() => {
  return alerts.value.filter(a => a.ticketNumber)
})

// Dynamic Metric Options (Built-in + User Defined)
const metricOptions = computed(() => {
  return telemetryMetrics.value.map(m => ({
    key: m.key,
    label: m.name,
    unit: m.unit,
    category: m.category,
    defaultThreshold: m.upperTolerance !== undefined ? m.upperTolerance : m.nominalValue,
    isUserDefined: m.isUserDefined
  }))
})

// New Rule Modal State
const isAddRuleOpen = ref(false)
const newRuleForm = ref({
  name: '',
  description: '',
  category: 'thermal' as RuleCategory,
  targetType: 'all' as 'all' | 'machine' | 'controller',
  targetId: '',
  targetName: '',
  metricKey: 'motor_temp_c',
  metricLabel: 'Core Bearing Temp',
  condition: '>' as RuleOperator,
  threshold: 72.0,
  unit: '°C',
  severity: 'High' as AlertSeverity,
  enabled: true,
  autoCreateTicket: true,
  errorCode: 'W-THERM-WARN',
  ticketPriority: 'High' as const,
  assignedTechnicianName: 'Plant Shift Technician',
  cooldownMinutes: 30
})

// User-Defined Metric Modal State
const isAddMetricOpen = ref(false)
const newMetricForm = ref<CreateTelemetryMetricInput>({
  key: '',
  name: '',
  description: '',
  unit: '°C',
  category: 'thermal',
  sourceType: 'BeckhoffAds',
  pathOrSymbol: '',
  nominalValue: 50.0,
  upperTolerance: 75.0,
  lowerTolerance: 20.0
})

const handleMetricSelect = (e: Event) => {
  const targetKey = (e.target as HTMLSelectElement).value
  const found = metricOptions.value.find(m => m.key === targetKey)
  if (found) {
    newRuleForm.value.metricKey = found.key
    newRuleForm.value.metricLabel = found.label
    newRuleForm.value.unit = found.unit
    newRuleForm.value.category = found.category as RuleCategory
    newRuleForm.value.threshold = found.defaultThreshold
  }
}

const handleCreateCustomMetric = async () => {
  if (!newMetricForm.value.key || !newMetricForm.value.name) return
  await createCustomMetric({ ...newMetricForm.value })
  isAddMetricOpen.value = false
  newMetricForm.value = {
    key: '',
    name: '',
    description: '',
    unit: '°C',
    category: 'thermal',
    sourceType: 'BeckhoffAds',
    pathOrSymbol: '',
    nominalValue: 50.0,
    upperTolerance: 75.0,
    lowerTolerance: 20.0
  }
}

const handleDeleteCustomMetric = async (key: string) => {
  await deleteCustomMetric(key)
}

const openCreateRuleForMetric = (m: UserTelemetryMetric) => {
  newRuleForm.value.metricKey = m.key
  newRuleForm.value.metricLabel = m.name
  newRuleForm.value.unit = m.unit
  newRuleForm.value.category = m.category as RuleCategory
  newRuleForm.value.threshold = m.upperTolerance !== undefined ? m.upperTolerance : m.nominalValue
  newRuleForm.value.name = `${m.name} Outlier Alert`
  isAddRuleOpen.value = true
}

const handleCreateRule = () => {
  if (!newRuleForm.value.name) return
  addRule({
    name: newRuleForm.value.name,
    description: newRuleForm.value.description,
    category: newRuleForm.value.category,
    targetType: newRuleForm.value.targetType,
    targetId: newRuleForm.value.targetId || undefined,
    targetName: newRuleForm.value.targetName || undefined,
    metricKey: newRuleForm.value.metricKey,
    metricLabel: newRuleForm.value.metricLabel,
    condition: newRuleForm.value.condition,
    threshold: Number(newRuleForm.value.threshold),
    unit: newRuleForm.value.unit,
    severity: newRuleForm.value.severity,
    enabled: newRuleForm.value.enabled,
    autoCreateTicket: newRuleForm.value.autoCreateTicket,
    errorCode: newRuleForm.value.errorCode,
    ticketPriority: newRuleForm.value.ticketPriority,
    assignedTechnicianName: newRuleForm.value.assignedTechnicianName,
    cooldownMinutes: Number(newRuleForm.value.cooldownMinutes)
  })
  isAddRuleOpen.value = false
  // Reset form
  newRuleForm.value.name = ''
  newRuleForm.value.description = ''
}

// Sandbox test log
const sandboxLogs = ref<Array<{ timestamp: string; message: string; type: 'info' | 'alert' | 'ticket' }>>([])

const handleRunSandbox = async (preset?: 'thermal' | 'jitter' | 'pressure' | 'vibration') => {
  sandboxLogs.value.unshift({
    timestamp: new Date().toLocaleTimeString(),
    message: `Running telemetry evaluation for preset: ${preset || 'thermal'}...`,
    type: 'info'
  })

  const res = await runSimulatedEvaluation(preset)
  if (res && res.generated && res.generated.length > 0) {
    for (const evt of res.generated) {
      sandboxLogs.value.unshift({
        timestamp: new Date().toLocaleTimeString(),
        message: `🚨 Rule Breached: "${evt.ruleName}" on ${evt.targetName} (${evt.measuredValue} ${evt.unit} ${evt.condition} ${evt.threshold} ${evt.unit})`,
        type: 'alert'
      })
      if (evt.ticketNumber) {
        sandboxLogs.value.unshift({
          timestamp: new Date().toLocaleTimeString(),
          message: `🎫 Maintenance Ticket Auto-Dispatched: ${evt.ticketNumber} [Priority: ${evt.severity}]`,
          type: 'ticket'
        })
      }
    }
  } else {
    sandboxLogs.value.unshift({
      timestamp: new Date().toLocaleTimeString(),
      message: `Evaluation completed: Rule cooldown active or metric within bounds. No tickets generated.`,
      type: 'info'
    })
  }
}

const navigateToTickets = (ticketNumber?: string) => {
  if (ticketNumber) {
    router.push({ path: '/dashboard/tickets', query: { search: ticketNumber } })
  } else {
    router.push('/dashboard/tickets')
  }
}

const getCategoryIcon = (cat: string) => {
  switch (cat) {
    case 'thermal': return Flame
    case 'vibration': return Activity
    case 'jitter': return Zap
    case 'pneumatics': return Wind
    case 'fieldbus': return Layers
    case 'resources': return Cpu
    default: return Sliders
  }
}

const getSeverityClass = (sev: AlertSeverity) => {
  switch (sev) {
    case 'Critical': return 'bg-rose-950/60 text-rose-300 border-rose-500/40'
    case 'High': return 'bg-amber-950/60 text-amber-300 border-amber-500/40'
    case 'Medium': return 'bg-blue-950/60 text-blue-300 border-blue-500/40'
    case 'Low': return 'bg-slate-800 text-slate-300 border-slate-700'
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Summary Stat Cards -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <!-- 1. Active Alerts -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Active Rule Alerts</span>
          <Bell class="w-4 h-4 text-amber-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ activeAlertsCount }}</span>
          <Badge
            v-if="criticalAlertsCount > 0"
            variant="outline"
            class="text-[10px] bg-rose-950/50 text-rose-400 border-rose-500/30 animate-pulse"
          >
            {{ criticalAlertsCount }} Critical
          </Badge>
          <Badge v-else variant="outline" class="text-[10px] bg-emerald-950/50 text-emerald-400 border-emerald-500/30">
            Nominal
          </Badge>
        </div>
        <div class="text-[11px] text-slate-400 flex items-center gap-1">
          <span>{{ alerts.length }} total events recorded</span>
        </div>
      </Card>

      <!-- 2. Active Rules Catalog -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Configured Rules</span>
          <Sliders class="w-4 h-4 text-indigo-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ enabledRulesCount }}</span>
          <span class="text-xs text-slate-400">/ {{ rules.length }} enabled</span>
        </div>
        <div class="text-[11px] text-slate-400 flex items-center gap-1">
          <span>Covers Jitter, Thermal, Vib & Fieldbus</span>
        </div>
      </Card>

      <!-- 3. Automated Tickets -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Auto-Dispatched Tickets</span>
          <Ticket class="w-4 h-4 text-emerald-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-3xl font-bold font-mono text-white">{{ totalAutomatedTicketsCount }}</span>
          <Badge variant="outline" class="text-[10px] bg-emerald-950/50 text-emerald-400 border-emerald-500/30">
            Kanban Linked
          </Badge>
        </div>
        <div class="text-[11px] text-indigo-300 hover:text-indigo-200 cursor-pointer flex items-center gap-1" @click="navigateToTickets()">
          <span>View in Kanban Board</span>
          <ChevronRight class="w-3 h-3" />
        </div>
      </Card>

      <!-- 4. Evaluation Engine State -->
      <Card class="bg-slate-900/90 border-slate-800 text-slate-100 p-5 rounded-2xl flex flex-col justify-between shadow-sm">
        <div class="flex items-center justify-between text-xs text-slate-400">
          <span class="font-medium">Rule Engine Engine State</span>
          <Activity class="w-4 h-4 text-teal-400" />
        </div>
        <div class="flex items-baseline gap-3 my-3">
          <span class="text-sm font-semibold font-mono text-emerald-400 flex items-center gap-1.5">
            <span class="size-2 rounded-full bg-emerald-400 animate-pulse"></span>
            ACTIVE EVALUATION
          </span>
        </div>
        <div class="text-[11px] text-slate-400 font-mono flex items-center gap-1">
          <Clock class="w-3 h-3" />
          <span>Last sweep: {{ new Date(lastEvaluationTime).toLocaleTimeString() }}</span>
        </div>
      </Card>
    </div>

    <!-- Inner Sub-Tabs Navigation Bar -->
    <div class="flex items-center justify-between flex-wrap gap-3 pb-1 border-b border-slate-800">
      <div class="flex items-center bg-slate-950 p-1 rounded-xl border border-slate-800 text-xs font-medium">
        <button
          type="button"
          @click="activeSubTab = 'alerts'"
          :class="activeSubTab === 'alerts' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
          class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 cursor-pointer"
        >
          <Bell class="w-4 h-4" />
          <span>Active Alerts</span>
          <Badge v-if="activeAlertsCount > 0" class="text-[10px] px-1.5 py-0 bg-rose-500 text-white">
            {{ activeAlertsCount }}
          </Badge>
        </button>

        <button
          type="button"
          @click="activeSubTab = 'rules'"
          :class="activeSubTab === 'rules' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
          class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 cursor-pointer"
        >
          <Sliders class="w-4 h-4" />
          <span>Rule Catalog ({{ rules.length }})</span>
        </button>

        <button
          type="button"
          @click="activeSubTab = 'metrics'"
          :class="activeSubTab === 'metrics' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
          class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 cursor-pointer"
        >
          <Activity class="w-4 h-4" />
          <span>Telemetry Metrics ({{ telemetryMetrics.length }})</span>
          <Badge v-if="userDefinedMetrics.length > 0" class="text-[10px] px-1.5 py-0 bg-indigo-500 text-white">
            {{ userDefinedMetrics.length }} Custom
          </Badge>
        </button>

        <button
          type="button"
          @click="activeSubTab = 'sandbox'"
          :class="activeSubTab === 'sandbox' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
          class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 cursor-pointer"
        >
          <Play class="w-4 h-4" />
          <span>Trigger Sandbox</span>
        </button>

        <button
          type="button"
          @click="activeSubTab = 'ticket-log'"
          :class="activeSubTab === 'ticket-log' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
          class="px-4 py-2 rounded-lg transition-all flex items-center gap-2 cursor-pointer"
        >
          <Ticket class="w-4 h-4" />
          <span>Auto-Ticket Log ({{ automatedTicketsList.length }})</span>
        </button>
      </div>

      <!-- Right Action Button -->
      <div class="flex items-center gap-2">
        <Button
          v-if="activeSubTab === 'rules'"
          size="sm"
          @click="isAddRuleOpen = true"
          class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs h-9 cursor-pointer"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          <span>Define New Rule</span>
        </Button>
        <Button
          v-if="activeSubTab === 'metrics'"
          size="sm"
          @click="isAddMetricOpen = true"
          class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs h-9 cursor-pointer"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          <span>Register Custom Metric</span>
        </Button>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- SUBTAB 1: ACTIVE ALERTS FEED -->
    <!-- ========================================== -->
    <div v-if="activeSubTab === 'alerts'" class="space-y-4">
      <div class="flex items-center justify-between text-xs text-slate-400">
        <div class="flex items-center gap-2">
          <span class="font-medium">Filter Status:</span>
          <button
            type="button"
            @click="alertStatusFilter = 'active'"
            :class="alertStatusFilter === 'active' ? 'bg-slate-800 text-slate-100' : 'text-slate-400 hover:text-slate-300'"
            class="px-2.5 py-1 rounded text-[11px] border border-slate-700/50 cursor-pointer"
          >
            Active ({{ alerts.filter(a => a.status === 'active').length }})
          </button>
          <button
            type="button"
            @click="alertStatusFilter = 'acknowledged'"
            :class="alertStatusFilter === 'acknowledged' ? 'bg-slate-800 text-slate-100' : 'text-slate-400 hover:text-slate-300'"
            class="px-2.5 py-1 rounded text-[11px] border border-slate-700/50 cursor-pointer"
          >
            Acknowledged ({{ alerts.filter(a => a.status === 'acknowledged').length }})
          </button>
          <button
            type="button"
            @click="alertStatusFilter = 'all'"
            :class="alertStatusFilter === 'all' ? 'bg-slate-800 text-slate-100' : 'text-slate-400 hover:text-slate-300'"
            class="px-2.5 py-1 rounded text-[11px] border border-slate-700/50 cursor-pointer"
          >
            All Events ({{ alerts.length }})
          </button>
        </div>
      </div>

      <div v-if="filteredAlerts.length === 0" class="p-12 text-center rounded-2xl bg-slate-900/50 border border-slate-800 text-slate-400 space-y-2">
        <CheckCircle2 class="w-10 h-10 text-emerald-400 mx-auto" />
        <h4 class="text-sm font-semibold text-slate-200">No Alerts In Current View</h4>
        <p class="text-xs">All monitored industrial parameters are operating strictly within nominal bounds.</p>
      </div>

      <div v-else class="grid grid-cols-1 gap-3">
        <div
          v-for="alert in filteredAlerts"
          :key="alert.id"
          :class="alert.status === 'active' ? 'border-l-4 border-l-rose-500 bg-slate-900/80' : 'border-l-4 border-l-slate-600 bg-slate-900/40 opacity-80'"
          class="p-4 rounded-xl border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4 transition-all"
        >
          <div class="space-y-1.5">
            <div class="flex items-center gap-2 flex-wrap">
              <Badge :class="getSeverityClass(alert.severity)" class="text-[10px] font-semibold border">
                {{ alert.severity }}
              </Badge>
              <Badge variant="outline" class="text-[10px] font-mono border-slate-700 text-slate-400 uppercase">
                {{ alert.category }}
              </Badge>
              <span class="text-sm font-semibold text-slate-100">{{ alert.ruleName }}</span>
              <Badge
                :class="alert.status === 'active' ? 'bg-rose-950/60 text-rose-300 border-rose-500/30' : 'bg-slate-800 text-slate-400 border-slate-700'"
                class="text-[10px] border"
              >
                {{ alert.status === 'active' ? 'ACTIVE' : 'ACKNOWLEDGED' }}
              </Badge>
            </div>

            <div class="text-xs text-slate-300 flex items-center gap-3 flex-wrap">
              <span class="text-slate-400">Target:</span>
              <span class="font-medium text-slate-200">{{ alert.targetName }}</span>
              <span class="text-slate-500">•</span>
              <span class="text-slate-400">Observed:</span>
              <span class="font-mono font-bold text-rose-400">{{ alert.measuredValue }} {{ alert.unit }}</span>
              <span class="text-slate-500">(Threshold: {{ alert.condition }} {{ alert.threshold }} {{ alert.unit }})</span>
            </div>

            <div class="text-[11px] text-slate-500 flex items-center gap-3 flex-wrap font-mono">
              <span class="flex items-center gap-1">
                <Clock class="w-3 h-3" />
                {{ new Date(alert.triggeredAt).toLocaleString() }}
              </span>
              <span v-if="alert.acknowledgedBy" class="text-slate-400">
                Ack by: {{ alert.acknowledgedBy }}
              </span>
            </div>
          </div>

          <!-- Alert Actions & Ticket Jump -->
          <div class="flex items-center gap-2 shrink-0">
            <button
              v-if="alert.ticketNumber"
              type="button"
              @click="navigateToTickets(alert.ticketNumber)"
              class="px-3 py-1.5 rounded-lg bg-emerald-950/40 hover:bg-emerald-950/70 text-emerald-300 border border-emerald-500/30 text-xs font-mono font-medium flex items-center gap-1.5 transition-all cursor-pointer"
              title="Open Ticket in Maintenance Kanban"
            >
              <Ticket class="w-3.5 h-3.5" />
              <span>{{ alert.ticketNumber }}</span>
              <ExternalLink class="w-3 h-3 text-emerald-400" />
            </button>

            <button
              v-if="alert.status === 'active'"
              type="button"
              @click="acknowledgeAlert(alert.id, 'Shift Operator')"
              class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-xs font-medium flex items-center gap-1 transition-all cursor-pointer"
            >
              <Check class="w-3.5 h-3.5 text-emerald-400" />
              <span>Acknowledge</span>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- SUBTAB 2: CONFIGURED RULES CATALOG -->
    <!-- ========================================== -->
    <div v-else-if="activeSubTab === 'rules'" class="space-y-4">
      <!-- Category Filter Pills -->
      <div class="flex items-center gap-1.5 overflow-x-auto pb-1 text-xs">
        <span class="text-slate-400 text-xs mr-1">Category:</span>
        <button
          v-for="cat in ['all', 'thermal', 'vibration', 'jitter', 'resources', 'pneumatics', 'fieldbus']"
          :key="cat"
          type="button"
          @click="ruleCategoryFilter = cat"
          :class="ruleCategoryFilter === cat ? 'bg-indigo-600 text-white' : 'bg-slate-900 text-slate-400 hover:text-slate-200 border border-slate-800'"
          class="px-3 py-1 rounded-lg text-xs font-medium capitalize transition-all cursor-pointer shrink-0"
        >
          {{ cat }}
        </button>
      </div>

      <!-- Rules Grid -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div
          v-for="rule in filteredRules"
          :key="rule.id"
          :class="rule.enabled ? 'border-slate-800 bg-slate-900/80' : 'border-slate-800/60 bg-slate-950/60 opacity-60'"
          class="p-5 rounded-xl border flex flex-col justify-between space-y-4 transition-all"
        >
          <div class="space-y-2">
            <div class="flex items-center justify-between gap-2">
              <div class="flex items-center gap-2">
                <component :is="getCategoryIcon(rule.category)" class="w-4 h-4 text-indigo-400" />
                <span class="text-sm font-semibold text-slate-100">{{ rule.name }}</span>
              </div>
              <Badge :class="getSeverityClass(rule.severity)" class="text-[10px] font-mono border">
                {{ rule.severity }}
              </Badge>
            </div>

            <p class="text-xs text-slate-400 leading-relaxed">{{ rule.description }}</p>

            <div class="p-2.5 rounded-lg bg-slate-950 border border-slate-800 text-xs font-mono space-y-1 text-slate-300">
              <div class="flex items-center justify-between">
                <span class="text-slate-400">Trigger Threshold:</span>
                <span class="font-bold text-indigo-300">{{ rule.metricLabel }} {{ rule.condition }} {{ rule.threshold }} {{ rule.unit }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-slate-400">Target Scope:</span>
                <span class="text-slate-200">{{ rule.targetType === 'all' ? 'Entire Plant Fleet' : (rule.targetName || rule.targetId) }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-slate-400">Auto-Dispatched Ticket:</span>
                <span v-if="rule.autoCreateTicket" class="text-emerald-400 flex items-center gap-1 font-sans text-[11px]">
                  <Check class="w-3 h-3" /> Enabled ({{ rule.errorCode || 'E-GEN' }})
                </span>
                <span v-else class="text-slate-500 font-sans text-[11px]">Disabled</span>
              </div>
              <div class="flex items-center justify-between text-[11px] text-slate-500">
                <span>Cooldown & Triggers:</span>
                <span>{{ rule.cooldownMinutes }}m cooldown • {{ rule.triggerCount }} incidents</span>
              </div>
            </div>
          </div>

          <!-- Bottom rule actions -->
          <div class="flex items-center justify-between pt-2 border-t border-slate-800 text-xs">
            <div class="flex items-center gap-2">
              <button
                type="button"
                @click="toggleRule(rule.id)"
                :class="rule.enabled ? 'bg-emerald-950/60 text-emerald-300 border-emerald-500/30' : 'bg-slate-800 text-slate-400 border-slate-700'"
                class="px-2.5 py-1 rounded border text-xs font-medium cursor-pointer transition-all"
              >
                {{ rule.enabled ? 'Active' : 'Disabled' }}
              </button>
            </div>
            <div class="flex items-center gap-1">
              <button
                type="button"
                @click="deleteRule(rule.id)"
                class="p-1.5 rounded hover:bg-rose-950/40 text-slate-400 hover:text-rose-400 transition-colors cursor-pointer"
                title="Delete rule"
              >
                <Trash2 class="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- SUBTAB: TELEMETRY METRICS REGISTRY -->
    <!-- ========================================== -->
    <div v-else-if="activeSubTab === 'metrics'" class="space-y-4">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-400">
        <div>
          <h4 class="text-sm font-semibold text-slate-100 flex items-center gap-2">
            <Activity class="w-4 h-4 text-indigo-400" />
            <span>Industrial Telemetry Metrics Registry</span>
          </h4>
          <p class="text-xs text-slate-400 mt-0.5">
            Configure telemetry metrics, nominal operating baselines, and tolerances. User-defined metrics dynamically feed the rule evaluation engine and cached timeseries trend models.
          </p>
        </div>

        <div class="flex items-center gap-2 shrink-0">
          <span class="font-medium">Filter Origin:</span>
          <button
            type="button"
            @click="metricOriginFilter = 'all'"
            :class="metricOriginFilter === 'all' ? 'bg-slate-800 text-slate-100' : 'text-slate-400 hover:text-slate-300'"
            class="px-2.5 py-1 rounded text-[11px] border border-slate-700/50 cursor-pointer"
          >
            All ({{ telemetryMetrics.length }})
          </button>
          <button
            type="button"
            @click="metricOriginFilter = 'builtin'"
            :class="metricOriginFilter === 'builtin' ? 'bg-slate-800 text-slate-100' : 'text-slate-400 hover:text-slate-300'"
            class="px-2.5 py-1 rounded text-[11px] border border-slate-700/50 cursor-pointer"
          >
            Built-in ({{ telemetryMetrics.filter(m => !m.isUserDefined).length }})
          </button>
          <button
            type="button"
            @click="metricOriginFilter = 'custom'"
            :class="metricOriginFilter === 'custom' ? 'bg-slate-800 text-slate-100' : 'text-slate-400 hover:text-slate-300'"
            class="px-2.5 py-1 rounded text-[11px] border border-slate-700/50 cursor-pointer"
          >
            User Defined ({{ userDefinedMetrics.length }})
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <div
          v-for="m in filteredMetricsList"
          :key="m.key"
          class="p-4 rounded-2xl bg-slate-900/80 border border-slate-800 flex flex-col justify-between gap-4 hover:border-slate-700 transition-all group"
        >
          <div class="space-y-2">
            <div class="flex items-start justify-between gap-2">
              <div class="space-y-0.5">
                <div class="flex items-center gap-2">
                  <span class="text-sm font-semibold text-slate-100 group-hover:text-indigo-300 transition-colors">{{ m.name }}</span>
                  <Badge
                    :class="m.isUserDefined ? 'bg-indigo-950/60 text-indigo-300 border-indigo-500/40' : 'bg-slate-800 text-slate-400 border-slate-700'"
                    class="text-[10px] border"
                  >
                    {{ m.isUserDefined ? 'User Defined' : 'Built-in' }}
                  </Badge>
                </div>
                <div class="font-mono text-[11px] text-slate-400">{{ m.key }}</div>
              </div>
              <component :is="getCategoryIcon(m.category)" class="w-4 h-4 text-slate-400 shrink-0 mt-1" />
            </div>

            <p class="text-xs text-slate-400 line-clamp-2">
              {{ m.description || 'Custom industrial telemetry metric' }}
            </p>

            <div class="p-2.5 rounded-xl bg-slate-950 border border-slate-800/80 space-y-1.5 font-mono text-[11px]">
              <div class="flex items-center justify-between text-slate-400">
                <span>Protocol Source:</span>
                <span class="text-slate-200">{{ m.sourceType }}</span>
              </div>
              <div v-if="m.pathOrSymbol" class="flex items-center justify-between text-slate-400">
                <span>PLC Symbol:</span>
                <span class="text-indigo-400 truncate max-w-[170px]" :title="m.pathOrSymbol">{{ m.pathOrSymbol }}</span>
              </div>
              <div class="flex items-center justify-between text-slate-400 pt-1 border-t border-slate-900">
                <span>Nominal Value:</span>
                <span class="text-emerald-400 font-bold">{{ m.nominalValue }} {{ m.unit }}</span>
              </div>
              <div class="flex items-center justify-between text-slate-500 text-[10px]">
                <span>Tolerances:</span>
                <span>MIN {{ m.lowerTolerance }} {{ m.unit }} • MAX {{ m.upperTolerance }} {{ m.unit }}</span>
              </div>
            </div>
          </div>

          <div class="flex items-center justify-between pt-2 border-t border-slate-800 text-xs">
            <button
              type="button"
              @click="openCreateRuleForMetric(m)"
              class="px-2.5 py-1 rounded bg-indigo-950/40 hover:bg-indigo-900/60 text-indigo-300 border border-indigo-500/30 text-[11px] font-medium transition-all flex items-center gap-1.5 cursor-pointer"
            >
              <Sliders class="w-3 h-3" />
              <span>Create Rule</span>
            </button>

            <button
              v-if="m.isUserDefined"
              type="button"
              @click="handleDeleteCustomMetric(m.key)"
              class="p-1.5 rounded hover:bg-rose-950/40 text-slate-400 hover:text-rose-400 transition-colors cursor-pointer"
              title="Delete custom metric"
            >
              <Trash2 class="w-3.5 h-3.5" />
            </button>
            <span v-else class="text-[10px] text-slate-600 uppercase font-mono">Standard Catalog</span>
          </div>
        </div>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- SUBTAB 3: TRIGGER SANDBOX & EVALUATOR -->
    <!-- ========================================== -->
    <div v-else-if="activeSubTab === 'sandbox'" class="space-y-5">
      <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 space-y-4">
        <div>
          <h4 class="text-sm font-semibold text-slate-100 flex items-center gap-2">
            <Play class="w-4 h-4 text-indigo-400" />
            <span>Industrial Telemetry Sandbox & Rule Breaker</span>
          </h4>
          <p class="text-xs text-slate-400 mt-1">
            Simulate real-time hardware anomalies to test rule triggers, threshold boundaries, and automated ticket generation without touching physical PLC lines.
          </p>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <button
            type="button"
            @click="handleRunSandbox('thermal')"
            class="p-3 rounded-xl bg-slate-950 hover:bg-slate-800/80 border border-slate-800 text-left transition-all cursor-pointer group"
          >
            <div class="flex items-center justify-between text-xs text-rose-400 mb-1">
              <span class="font-semibold flex items-center gap-1">
                <Flame class="w-3.5 h-3.5" /> Thermal Spike
              </span>
              <span class="font-mono text-[10px]">84.5 °C</span>
            </div>
            <p class="text-[11px] text-slate-400 group-hover:text-slate-300">Spike Laser Cell OP20 spindle temperature above 72°C threshold.</p>
          </button>

          <button
            type="button"
            @click="handleRunSandbox('jitter')"
            class="p-3 rounded-xl bg-slate-950 hover:bg-slate-800/80 border border-slate-800 text-left transition-all cursor-pointer group"
          >
            <div class="flex items-center justify-between text-xs text-amber-400 mb-1">
              <span class="font-semibold flex items-center gap-1">
                <Zap class="w-3.5 h-3.5" /> Soft-PLC Jitter
              </span>
              <span class="font-mono text-[10px]">64.8 μs</span>
            </div>
            <p class="text-[11px] text-slate-400 group-hover:text-slate-300">Simulate TwinCAT real-time cyclic task jitter exceeding 45μs.</p>
          </button>

          <button
            type="button"
            @click="handleRunSandbox('pressure')"
            class="p-3 rounded-xl bg-slate-950 hover:bg-slate-800/80 border border-slate-800 text-left transition-all cursor-pointer group"
          >
            <div class="flex items-center justify-between text-xs text-blue-400 mb-1">
              <span class="font-semibold flex items-center gap-1">
                <Wind class="w-3.5 h-3.5" /> Pressure Sag
              </span>
              <span class="font-mono text-[10px]">4.65 bar</span>
            </div>
            <p class="text-[11px] text-slate-400 group-hover:text-slate-300">Drop pneumatic supply line pressure below 5.4 bar critical limit.</p>
          </button>

          <button
            type="button"
            @click="handleRunSandbox('vibration')"
            class="p-3 rounded-xl bg-slate-950 hover:bg-slate-800/80 border border-slate-800 text-left transition-all cursor-pointer group"
          >
            <div class="flex items-center justify-between text-xs text-teal-400 mb-1">
              <span class="font-semibold flex items-center gap-1">
                <Activity class="w-3.5 h-3.5" /> Vibration Wave
              </span>
              <span class="font-mono text-[10px]">3.45 mm/s</span>
            </div>
            <p class="text-[11px] text-slate-400 group-hover:text-slate-300">Simulate robotic fastener drive bearing wear and harmonic drift.</p>
          </button>
        </div>

        <!-- Sandbox Output Console -->
        <div class="space-y-2">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span class="font-mono">Real-Time Event Stream Log</span>
            <button
              type="button"
              @click="sandboxLogs = []"
              class="hover:text-slate-200 transition-colors text-[11px] cursor-pointer"
            >
              Clear Log
            </button>
          </div>
          <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 font-mono text-xs max-h-56 overflow-y-auto space-y-1.5">
            <div v-if="sandboxLogs.length === 0" class="text-slate-600 italic">
              Ready for simulation. Click a trigger above to evaluate telemetry and dispatch tickets.
            </div>
            <div
              v-for="(log, idx) in sandboxLogs"
              :key="idx"
              :class="log.type === 'alert' ? 'text-rose-400' : (log.type === 'ticket' ? 'text-emerald-400' : 'text-slate-400')"
              class="flex items-start gap-2 text-[11px]"
            >
              <span class="text-slate-600 shrink-0">[{{ log.timestamp }}]</span>
              <span>{{ log.message }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- SUBTAB 4: AUTO-TICKET LOG -->
    <!-- ========================================== -->
    <div v-else-if="activeSubTab === 'ticket-log'" class="space-y-4">
      <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h4 class="text-sm font-semibold text-slate-100 flex items-center gap-2">
              <Ticket class="w-4 h-4 text-emerald-400" />
              <span>Automated Maintenance Ticket Audit Log</span>
            </h4>
            <p class="text-xs text-slate-400 mt-1">
              Every ticket created autonomously by the rules engine includes cryptographic telemetry snapshots and standard error codes.
            </p>
          </div>
          <Button
            size="sm"
            @click="navigateToTickets()"
            class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs h-8 cursor-pointer"
          >
            <span>Open Kanban Board</span>
            <ExternalLink class="w-3.5 h-3.5 ml-1.5" />
          </Button>
        </div>

        <div class="divide-y divide-slate-800 border border-slate-800 rounded-xl overflow-hidden">
          <div
            v-for="item in automatedTicketsList"
            :key="item.id"
            class="p-4 bg-slate-950 flex flex-col sm:flex-row sm:items-center justify-between gap-3 hover:bg-slate-900/50 transition-colors"
          >
            <div class="space-y-1">
              <div class="flex items-center gap-2">
                <span class="font-mono text-xs font-bold text-emerald-400">{{ item.ticketNumber }}</span>
                <Badge :class="getSeverityClass(item.severity)" class="text-[10px] font-mono border">
                  {{ item.severity }}
                </Badge>
                <span class="text-xs font-semibold text-slate-200">{{ item.ruleName }}</span>
              </div>
              <div class="text-xs text-slate-400 flex items-center gap-2">
                <span>Target: <strong class="text-slate-300">{{ item.targetName }}</strong></span>
                <span>•</span>
                <span>Value: <strong class="text-rose-400 font-mono">{{ item.measuredValue }} {{ item.unit }}</strong></span>
              </div>
            </div>

            <div class="flex items-center gap-3">
              <span class="text-[11px] font-mono text-slate-500">{{ new Date(item.triggeredAt).toLocaleTimeString() }}</span>
              <button
                type="button"
                @click="navigateToTickets(item.ticketNumber)"
                class="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs rounded border border-slate-700 flex items-center gap-1 transition-all cursor-pointer"
              >
                <span>View Ticket</span>
                <ChevronRight class="w-3 h-3" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- CREATE RULE MODAL -->
    <Dialog :open="isAddRuleOpen" @update:open="isAddRuleOpen = $event">
      <DialogContent class="max-w-lg bg-slate-950 border-slate-800 text-slate-100 rounded-2xl">
        <DialogHeader>
          <DialogTitle class="text-base font-semibold text-slate-100 flex items-center gap-2">
            <Sliders class="w-4 h-4 text-indigo-400" />
            <span>Configure New Telemetry Alert Rule</span>
          </DialogTitle>
          <DialogDescription class="text-xs text-slate-400">
            Define industrial threshold limits and assign automated maintenance actions upon rule breach.
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-3.5 py-3 text-xs">
          <div>
            <label class="block text-slate-300 font-medium mb-1">Rule Name</label>
            <input
              v-model="newRuleForm.name"
              type="text"
              placeholder="e.g., High-Voltage Test Cell Temperature Warning"
              class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
            />
          </div>

          <div>
            <label class="block text-slate-300 font-medium mb-1">Description / Operational Context</label>
            <textarea
              v-model="newRuleForm.description"
              rows="2"
              placeholder="Explains what failure mode this rule protects against..."
              class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
            ></textarea>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-slate-300 font-medium mb-1">Telemetry Metric</label>
              <select
                :value="newRuleForm.metricKey"
                @change="handleMetricSelect"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500 cursor-pointer"
              >
                <option v-for="m in metricOptions" :key="m.key" :value="m.key">
                  {{ m.label }} ({{ m.unit }})
                </option>
              </select>
            </div>

            <div>
              <label class="block text-slate-300 font-medium mb-1">Severity Level</label>
              <select
                v-model="newRuleForm.severity"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500 cursor-pointer"
              >
                <option value="Critical">Critical</option>
                <option value="High">High</option>
                <option value="Medium">Medium</option>
                <option value="Low">Low</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-slate-300 font-medium mb-1">Condition</label>
              <select
                v-model="newRuleForm.condition"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500 font-mono cursor-pointer"
              >
                <option value=">">&gt; (Greater than)</option>
                <option value=">=">&gt;= (Greater or eq)</option>
                <option value="<">&lt; (Less than)</option>
                <option value="<=">&lt;= (Less or eq)</option>
              </select>
            </div>

            <div>
              <label class="block text-slate-300 font-medium mb-1">Threshold</label>
              <input
                v-model.number="newRuleForm.threshold"
                type="number"
                step="any"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>

            <div>
              <label class="block text-slate-300 font-medium mb-1">Unit</label>
              <input
                v-model="newRuleForm.unit"
                type="text"
                readonly
                class="w-full bg-slate-900/60 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-400 font-mono select-none"
              />
            </div>
          </div>

          <div class="p-3 rounded-xl bg-slate-900/90 border border-slate-800 space-y-2.5">
            <div class="flex items-center justify-between">
              <label class="font-medium text-slate-200 flex items-center gap-1.5 cursor-pointer">
                <input
                  v-model="newRuleForm.autoCreateTicket"
                  type="checkbox"
                  class="rounded bg-slate-950 border-slate-700 text-indigo-600 focus:ring-0 cursor-pointer"
                />
                <span>Auto-Dispatch Maintenance Ticket</span>
              </label>
              <Badge variant="outline" class="text-[10px] border-indigo-500/30 text-indigo-300">
                Zero-Click Repair Flow
              </Badge>
            </div>

            <div v-if="newRuleForm.autoCreateTicket" class="grid grid-cols-2 gap-3 pt-1">
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Error Code</label>
                <input
                  v-model="newRuleForm.errorCode"
                  type="text"
                  placeholder="e.g., E-MOT-01"
                  class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Cooldown (Mins)</label>
                <input
                  v-model.number="newRuleForm.cooldownMinutes"
                  type="number"
                  min="5"
                  class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
                />
              </div>
            </div>
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-800">
          <Button
            variant="outline"
            size="sm"
            @click="isAddRuleOpen = false"
            class="border-slate-800 bg-slate-900 text-slate-300 text-xs"
          >
            Cancel
          </Button>
          <Button
            size="sm"
            @click="handleCreateRule"
            :disabled="!newRuleForm.name"
            class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs disabled:opacity-40"
          >
            Save Alert Rule
          </Button>
        </div>
      </DialogContent>
    </Dialog>

    <!-- REGISTER CUSTOM METRIC MODAL -->
    <Dialog :open="isAddMetricOpen" @update:open="isAddMetricOpen = $event">
      <DialogContent class="max-w-lg bg-slate-950 border-slate-800 text-slate-100 rounded-2xl">
        <DialogHeader>
          <DialogTitle class="text-base font-semibold text-slate-100 flex items-center gap-2">
            <Activity class="w-4 h-4 text-indigo-400" />
            <span>Register User-Defined Telemetry Metric</span>
          </DialogTitle>
          <DialogDescription class="text-xs text-slate-400">
            Define a custom process signal from PLC, sensor terminal, or CIM host. This metric becomes immediately available in rules, trend charts, and cached analytics.
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-3.5 py-3 text-xs">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-slate-300 font-medium mb-1">Metric Key (Identifier)</label>
              <input
                v-model="newMetricForm.key"
                type="text"
                placeholder="e.g., coolant_flow_lpm"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>
            <div>
              <label class="block text-slate-300 font-medium mb-1">Display Name</label>
              <input
                v-model="newMetricForm.name"
                type="text"
                placeholder="e.g., Chiller Coolant Flow Rate"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              />
            </div>
          </div>

          <div>
            <label class="block text-slate-300 font-medium mb-1">Description / Functional Intent</label>
            <textarea
              v-model="newMetricForm.description"
              rows="2"
              placeholder="Explains physical instrumentation, sensor type, and process limits..."
              class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
            ></textarea>
          </div>

          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-slate-300 font-medium mb-1">Category</label>
              <select
                v-model="newMetricForm.category"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500 cursor-pointer"
              >
                <option value="thermal">Thermal</option>
                <option value="vibration">Vibration</option>
                <option value="jitter">Jitter</option>
                <option value="pneumatics">Pneumatics</option>
                <option value="fieldbus">Fieldbus</option>
                <option value="resources">Resources</option>
                <option value="cycle">Cycle</option>
                <option value="custom">Custom</option>
              </select>
            </div>

            <div>
              <label class="block text-slate-300 font-medium mb-1">Unit of Measure</label>
              <input
                v-model="newMetricForm.unit"
                type="text"
                placeholder="e.g. °C, bar, L/min"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>

            <div>
              <label class="block text-slate-300 font-medium mb-1">Source Protocol</label>
              <select
                v-model="newMetricForm.sourceType"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-indigo-500 cursor-pointer"
              >
                <option value="BeckhoffAds">Beckhoff ADS</option>
                <option value="BeckhoffEtherCat">Beckhoff EtherCAT</option>
                <option value="OpcUaSubscription">OPC-UA Sub</option>
                <option value="SystemCim">CIM / WMI Host</option>
                <option value="ModbusTcp">Modbus TCP</option>
                <option value="TcpSocket">Raw TCP Socket</option>
              </select>
            </div>
          </div>

          <div>
            <label class="block text-slate-300 font-medium mb-1">PLC Variable / Symbol Path</label>
            <input
              v-model="newMetricForm.pathOrSymbol"
              type="text"
              placeholder="e.g., MAIN.fbCoolant.fActualFlowLpm or ns=2;s=Chiller.Flow"
              class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
            />
          </div>

          <div class="grid grid-cols-3 gap-3 p-3 rounded-xl bg-slate-900/90 border border-slate-800">
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">Nominal Target</label>
              <input
                v-model.number="newMetricForm.nominalValue"
                type="number"
                step="any"
                class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">Lower Tolerance</label>
              <input
                v-model.number="newMetricForm.lowerTolerance"
                type="number"
                step="any"
                class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">Upper Tolerance</label>
              <input
                v-model.number="newMetricForm.upperTolerance"
                type="number"
                step="any"
                class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1 text-xs text-slate-100 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-800">
          <Button
            variant="outline"
            size="sm"
            @click="isAddMetricOpen = false"
            class="border-slate-800 bg-slate-900 text-slate-300 text-xs cursor-pointer"
          >
            Cancel
          </Button>
          <Button
            size="sm"
            @click="handleCreateCustomMetric"
            :disabled="!newMetricForm.key || !newMetricForm.name"
            class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs disabled:opacity-40 cursor-pointer"
          >
            Save Telemetry Metric
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
