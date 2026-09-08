<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  SlidersHorizontal,
  Send,
  ShieldCheck,
  Cpu,
  RefreshCw,
  HardDrive,
  Activity,
  CheckCircle2,
  Clock,
  AlertTriangle,
  Layers,
  Lock,
  Radio,
  Server,
  Search,
  X,
  Plus,
  Trash2,
  Edit2,
  ArrowUp,
  ArrowDown,
  Tag,
  Network,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Undo2,
  Filter,
  Sparkles
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table'
import { useControllers } from '~/composables/useControllers'
import { useTelemetryTemplates } from '~/composables/useTelemetryTemplates'
import type { FleetAgentPolicy, OuTagRecipeRule } from '~/types/telemetry'
import {
  extractHostDna,
  evaluateFleetAssignments,
  filterAndPaginateControllers,
  type HostAssignmentEvaluation,
  type PaginationResult
} from '~/utils/ouTagRuleEngine'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { controllers, isLoading: isControllersLoading, fetchControllers } = useControllers()
const { allTemplates, fetchTemplates } = useTelemetryTemplates()

const isSavingPolicy = ref(false)
const saveSuccess = ref(false)
const dispatchingHostId = ref<string | null>(null)
const dispatchSuccessMessage = ref<string | null>(null)
const isPushingBulk = ref(false)
const bulkProgress = ref<{ current: number; total: number }>({ current: 0, total: 0 })

// Default Pre-loaded Industrial Rules
const defaultIndustrialRules: OuTagRecipeRule[] = [
  {
    id: 'rule-beckhoff-motion',
    name: 'Beckhoff IPC & Motion Controllers',
    description: 'High-frequency ADS and EtherCAT telemetry for Beckhoff automation PCs',
    priority: 1,
    enabled: true,
    targetTemplateId: 'high-freq-motion',
    ouPatterns: [],
    ouMatchMode: 'ANY',
    tags: ['Beckhoff', 'IPC', 'Controller'],
    tagMatchMode: 'ALL'
  },
  {
    id: 'rule-vision-inspection',
    name: 'Vision & Quality Inspection Cells',
    description: 'High-throughput optical inspection and defect capture',
    priority: 2,
    enabled: true,
    targetTemplateId: 'vision-inspection',
    ouPatterns: [],
    ouMatchMode: 'ANY',
    tags: ['Vision', 'Cognex', 'Inspector', 'Camera'],
    tagMatchMode: 'ANY'
  },
  {
    id: 'rule-scada-gateway',
    name: 'SCADA & Fieldbus Gateways',
    description: 'Edge polling for industrial fieldbus couplers and OPC UA / Modbus gateways',
    priority: 3,
    enabled: true,
    targetTemplateId: 'scada-gateway',
    ouPatterns: [],
    ouMatchMode: 'ANY',
    tags: ['SCADA', 'OPC', 'Modbus', 'Gateway'],
    tagMatchMode: 'ANY'
  },
  {
    id: 'rule-line-a',
    name: 'Line-A Assembly Cells',
    description: 'Standard baseline recipe for workstations located in Line-A OU',
    priority: 4,
    enabled: true,
    targetTemplateId: 'standard-factory-baseline',
    ouPatterns: ['*LINE-A*', '*Line-A*'],
    ouMatchMode: 'ANY',
    tags: [],
    tagMatchMode: 'ALL'
  }
]

// Master fleet policy
const fleetPolicy = ref<FleetAgentPolicy>({
  configSchemaVersion: '1.0.0',
  backendUrl: 'http://localhost:5001',
  authType: 'NoAuth',
  enforceHardwareBinding: true,
  spoolEncryptionMode: 'AES_256_GCM',
  telemetryPayloadEncryption: false,
  allowRemoteExecution: true,
  allowUnsignedCommands: false,
  piiScrubberStrictLevel: 'Strict',
  maxNetworkEgressBytesPerSec: 1048576,
  deltaEvaluationAlgorithm: 'xxHash64',
  deadbandTolerancePercentage: 1.0,
  maxSpoolDiskMb: 500,
  heartbeatIntervalSeconds: 10,
  assignedTemplateId: 'standard-factory-baseline',
  ouTagRules: [...defaultIndustrialRules]
})

// Manual Host Specific Overrides (Host ID -> Template Recipe ID)
const manualOverrides = ref<Record<string, string>>({})
const dispatchedLogs = ref<Array<{ id: string; hostname: string; time: string; status: string; template: string }>>([])

// Search & Filter State
const searchQuery = ref('')
const filterMode = ref<'all' | 'rule' | 'manual' | 'default'>('all')

// Configurable Pagination State
const currentPage = ref(1)
const selectedPageSizeOption = ref<'5' | '10' | '100' | '1000' | 'custom'>('10')
const customPageSizeInput = ref(25)
const effectivePageSize = computed(() => {
  if (selectedPageSizeOption.value === 'custom') {
    return Math.max(1, Math.min(10000, Number(customPageSizeInput.value) || 25))
  }
  return Number(selectedPageSizeOption.value) || 10
})

// Rule Builder / Editor State
const isRuleModalOpen = ref(false)
const editingRuleId = ref<string | null>(null)
const newRuleForm = ref<OuTagRecipeRule>({
  id: '',
  name: '',
  description: '',
  priority: 1,
  enabled: true,
  targetTemplateId: 'standard-factory-baseline',
  ouPatterns: [],
  ouMatchMode: 'ANY',
  tags: [],
  tagMatchMode: 'ALL'
})
const newTagInput = ref('')
const newOuPatternInput = ref('')

// Template Names lookup
const templateNamesById = computed(() => {
  const map: Record<string, string> = {}
  allTemplates.value.forEach(t => {
    map[t.recipeId] = t.name
  })
  return map
})

// Live Evaluated Assignments across the entire Fleet
const fleetEvaluations = computed<Record<string, HostAssignmentEvaluation>>(() => {
  return evaluateFleetAssignments(
    controllers.value,
    fleetPolicy.value.ouTagRules || [],
    manualOverrides.value,
    fleetPolicy.value.assignedTemplateId || 'standard-factory-baseline'
  )
})

// 3-State Column Sorting State: asc -> desc -> null
const sortKey = ref<string | null>(null)
const sortOrder = ref<'asc' | 'desc' | null>(null)

function handleSort(key: string, direction: 'asc' | 'desc' | null) {
  sortKey.value = direction ? key : null
  sortOrder.value = direction
}

// Filter and Paginate Result
const paginationResult = computed<PaginationResult<any>>(() => {
  return filterAndPaginateControllers(
    controllers.value,
    fleetEvaluations.value,
    templateNamesById.value,
    searchQuery.value,
    filterMode.value,
    currentPage.value,
    effectivePageSize.value,
    sortKey.value,
    sortOrder.value
  )
})

// Filtered All Controllers (for bulk push across all matching pages)
const filteredAllControllers = computed(() => {
  const normalizedQuery = searchQuery.value.trim().toLowerCase()
  return controllers.value.filter(ctrl => {
    const evalData = fleetEvaluations.value[ctrl.id]
    if (!evalData) return false
    if (filterMode.value !== 'all' && evalData.assignmentMode !== filterMode.value) return false
    if (!normalizedQuery) return true

    const host = ctrl.hostname || ''
    const ip = ctrl.ipAddress || ''
    const mac = ctrl.macAddress || ''
    const ou = evalData.adOuPath || ''
    const ruleName = evalData.matchedRule?.name || ''
    const templateName = templateNamesById.value[evalData.recipeId] || ''
    const tagsJoined = (evalData.detectedTags || []).join(' ')
    const tokensJoined = Array.from(evalData.hostDna?.tokens || []).join(' ')

    const searchableText = `${host} ${ip} ${mac} ${ou} ${ruleName} ${templateName} ${tagsJoined} ${tokensJoined}`.toLowerCase()
    return searchableText.includes(normalizedQuery)
  })
})

// Counts for filter chips
const countsByMode = computed(() => {
  let ruleCount = 0
  let manualCount = 0
  let defaultCount = 0
  Object.values(fleetEvaluations.value).forEach(ev => {
    if (ev.assignmentMode === 'rule') ruleCount++
    else if (ev.assignmentMode === 'manual') manualCount++
    else defaultCount++
  })
  return {
    all: controllers.value.length,
    rule: ruleCount,
    manual: manualCount,
    default: defaultCount
  }
})

// Reset page to 1 when search or page size changes
watch([searchQuery, filterMode, effectivePageSize], () => {
  currentPage.value = 1
})

onMounted(async () => {
  await Promise.all([fetchControllers(), fetchTemplates()])

  // Load saved fleet policy and rules from BFF
  try {
    const saved = await $fetch<any>('/api/telemetry/config')
    if (saved && typeof saved === 'object') {
      fleetPolicy.value = {
        ...fleetPolicy.value,
        ...saved,
        ouTagRules: saved.ouTagRules && saved.ouTagRules.length > 0
          ? saved.ouTagRules
          : [...defaultIndustrialRules]
      }
    }
  } catch {
    // Keep defaults
  }
})

async function saveFleetPolicy() {
  isSavingPolicy.value = true
  saveSuccess.value = false
  try {
    await $fetch('/api/telemetry/config', {
      method: 'PUT',
      body: fleetPolicy.value
    })
    saveSuccess.value = true
    setTimeout(() => { saveSuccess.value = false }, 3000)
  } catch (err: any) {
    alert(err?.message || 'Failed to save policy')
  } finally {
    isSavingPolicy.value = false
  }
}

function handleHostRecipeChange(controllerId: string, newRecipeId: string) {
  manualOverrides.value[controllerId] = newRecipeId
}

function revertToRule(controllerId: string) {
  delete manualOverrides.value[controllerId]
}

// Rule Management Functions
function openAddRuleModal() {
  editingRuleId.value = null
  newRuleForm.value = {
    id: `rule-${Date.now()}`,
    name: '',
    description: '',
    priority: (fleetPolicy.value.ouTagRules?.length || 0) + 1,
    enabled: true,
    targetTemplateId: allTemplates.value[0]?.recipeId || 'standard-factory-baseline',
    ouPatterns: [],
    ouMatchMode: 'ANY',
    tags: [],
    tagMatchMode: 'ALL'
  }
  newTagInput.value = ''
  newOuPatternInput.value = ''
  isRuleModalOpen.value = true
}

function openEditRuleModal(rule: OuTagRecipeRule) {
  editingRuleId.value = rule.id
  newRuleForm.value = JSON.parse(JSON.stringify(rule))
  newTagInput.value = ''
  newOuPatternInput.value = ''
  isRuleModalOpen.value = true
}

function addTagToForm() {
  const val = newTagInput.value.trim()
  if (val && !newRuleForm.value.tags.includes(val)) {
    newRuleForm.value.tags.push(val)
  }
  newTagInput.value = ''
}

function removeTagFromForm(idx: number) {
  newRuleForm.value.tags.splice(idx, 1)
}

function addOuPatternToForm() {
  const val = newOuPatternInput.value.trim()
  if (val && !newRuleForm.value.ouPatterns.includes(val)) {
    newRuleForm.value.ouPatterns.push(val)
  }
  newOuPatternInput.value = ''
}

function removeOuPatternFromForm(idx: number) {
  newRuleForm.value.ouPatterns.splice(idx, 1)
}

function saveRuleModal() {
  if (!newRuleForm.value.name.trim()) {
    alert('Please enter a rule name.')
    return
  }

  if (!fleetPolicy.value.ouTagRules) {
    fleetPolicy.value.ouTagRules = []
  }

  if (editingRuleId.value) {
    const idx = fleetPolicy.value.ouTagRules.findIndex(r => r.id === editingRuleId.value)
    if (idx !== -1) {
      fleetPolicy.value.ouTagRules[idx] = { ...newRuleForm.value }
    }
  } else {
    fleetPolicy.value.ouTagRules.push({ ...newRuleForm.value })
  }

  // Re-sort priority
  fleetPolicy.value.ouTagRules.sort((a, b) => a.priority - b.priority)
  isRuleModalOpen.value = false
  saveFleetPolicy()
}

function deleteRule(ruleId: string) {
  if (confirm('Are you sure you want to delete this rule?')) {
    fleetPolicy.value.ouTagRules = (fleetPolicy.value.ouTagRules || []).filter(r => r.id !== ruleId)
    saveFleetPolicy()
  }
}

function moveRulePriority(idx: number, direction: 'up' | 'down') {
  const rules = fleetPolicy.value.ouTagRules || []
  const targetIdx = direction === 'up' ? idx - 1 : idx + 1
  if (targetIdx < 0 || targetIdx >= rules.length) return

  const temp = rules[idx]
  rules[idx] = rules[targetIdx]
  rules[targetIdx] = temp

  // Reassign priorities sequentially
  rules.forEach((r, i) => {
    r.priority = i + 1
  })

  fleetPolicy.value.ouTagRules = [...rules]
  saveFleetPolicy()
}

// Dispatch Commands
async function dispatchConfigToHost(controller: any, silent: boolean = false) {
  if (!silent) {
    dispatchingHostId.value = controller.id
    dispatchSuccessMessage.value = null
  }

  const evalData = fleetEvaluations.value[controller.id]
  const assignedTplId = evalData?.recipeId || fleetPolicy.value.assignedTemplateId
  const template = allTemplates.value.find(t => t.recipeId === assignedTplId)

  const configPayload = {
    ...fleetPolicy.value,
    assignedTemplate: template || null
  }

  try {
    const res = await $fetch<any>('/api/telemetry/dispatch', {
      method: 'POST',
      body: {
        clientPcId: controller.id,
        config: configPayload,
        signature: 'ED25519-SIG-APPROVED-ADMIN'
      }
    })

    if (res.success) {
      if (!silent) {
        dispatchSuccessMessage.value = `Dispatched UPDATE_CONFIG to ${controller.hostname} (${template?.name || 'Standard Baseline'})`
        setTimeout(() => { dispatchSuccessMessage.value = null }, 3500)
      }
      dispatchedLogs.value.unshift({
        id: `log-${Date.now()}-${Math.random()}`,
        hostname: controller.hostname,
        time: new Date().toLocaleTimeString(),
        status: 'Queued / Applied',
        template: template?.name || 'Standard Baseline'
      })
    } else if (!silent) {
      alert(res.error || 'Failed to dispatch configuration')
    }
  } catch (err: any) {
    if (!silent) alert(err?.message || 'Error communicating with backend')
  } finally {
    if (!silent) dispatchingHostId.value = null
  }
}

async function pushToFilteredHosts() {
  const nodes = filteredAllControllers.value
  if (nodes.length === 0) return

  isPushingBulk.value = true
  bulkProgress.value = { current: 0, total: nodes.length }

  for (const c of nodes) {
    await dispatchConfigToHost(c, true)
    bulkProgress.value.current++
  }

  isPushingBulk.value = false
  dispatchSuccessMessage.value = `Successfully dispatched configuration to all ${nodes.length} filtered controllers!`
  setTimeout(() => { dispatchSuccessMessage.value = null }, 4000)
}

async function pushToAllHosts() {
  const nodes = controllers.value
  if (nodes.length === 0) return

  isPushingBulk.value = true
  bulkProgress.value = { current: 0, total: nodes.length }

  for (const c of nodes) {
    await dispatchConfigToHost(c, true)
    bulkProgress.value.current++
  }

  isPushingBulk.value = false
  dispatchSuccessMessage.value = `Successfully dispatched configuration to all ${nodes.length} fleet controllers!`
  setTimeout(() => { dispatchSuccessMessage.value = null }, 4000)
}
</script>

<template>
  <div class="space-y-8 animate-in fade-in duration-300">
    <!-- Header Section -->
    <div class="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-2 border-b border-slate-900">
      <div>
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-xl bg-purple-500/10 border border-purple-500/20 text-purple-400">
            <SlidersHorizontal class="size-6" />
          </div>
          <div>
            <h1 class="text-2xl font-black tracking-wider text-slate-100 uppercase">
              Fleet Telemetry Orchestration
            </h1>
            <p class="text-xs text-slate-400 mt-0.5">
              Active Directory OU & Asset Tag Policy Engine, Security Sealing, and High-Scale Node Configuration
            </p>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <Button
          @click="saveFleetPolicy"
          :disabled="isSavingPolicy"
          class="bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold uppercase tracking-wider px-5 shadow-lg shadow-purple-900/30"
        >
          <RefreshCw v-if="isSavingPolicy" class="size-4 mr-2 animate-spin" />
          <ShieldCheck v-else class="size-4 mr-2" />
          {{ isSavingPolicy ? 'Saving Fleet Policy...' : 'Save Fleet Policy' }}
        </Button>
      </div>
    </div>

    <!-- Alert / Status Notifications -->
    <div v-if="saveSuccess" class="p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-xs font-bold flex items-center gap-3">
      <CheckCircle2 class="size-5" />
      <span>Fleet Master Policy and OU & Asset Tag rules saved successfully.</span>
    </div>

    <div v-if="dispatchSuccessMessage" class="p-4 rounded-xl bg-purple-500/10 border border-purple-500/20 text-purple-400 text-xs font-bold flex items-center gap-3">
      <CheckCircle2 class="size-5" />
      <span>{{ dispatchSuccessMessage }}</span>
    </div>

    <div v-if="isPushingBulk" class="p-4 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 text-xs font-bold flex items-center justify-between">
      <div class="flex items-center gap-3">
        <RefreshCw class="size-5 animate-spin" />
        <span>Deploying configuration across fleet: {{ bulkProgress.current }} / {{ bulkProgress.total }} nodes completed...</span>
      </div>
      <Badge class="bg-indigo-600 text-white font-mono">
        {{ Math.round((bulkProgress.current / Math.max(1, bulkProgress.total)) * 100) }}%
      </Badge>
    </div>

    <!-- Section 1: OU & Asset Tag Recipe Rules -->
    <Card class="bg-slate-900/60 border-slate-800">
      <CardHeader class="p-6 border-b border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <Sparkles class="size-4 text-purple-400" />
            <CardTitle class="text-sm font-black text-slate-100 uppercase tracking-wide">
              Active Directory OU & Asset Tag Recipe Rules
            </CardTitle>
          </div>
          <CardDescription class="text-xs text-slate-400 mt-1">
            Rules evaluate in priority order. When a controller's Active Directory OU and asset tags match (e.g. Beckhoff + IPC + Controller), it automatically receives the designated telemetry recipe.
          </CardDescription>
        </div>
        <div class="flex items-center gap-3">
          <Button size="sm" @click="openAddRuleModal" class="bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold uppercase px-4 border-0">
            <Plus class="size-3.5 mr-1.5" />
            Add Rule
          </Button>
        </div>
      </CardHeader>

      <CardContent class="p-6">
        <div v-if="!fleetPolicy.ouTagRules || fleetPolicy.ouTagRules.length === 0" class="text-center py-8 text-slate-500 text-xs font-bold uppercase">
          No OU/Tag rules configured. Nodes will use the default fleet recipe.
        </div>

        <div v-else class="space-y-3">
          <div
            v-for="(rule, idx) in fleetPolicy.ouTagRules"
            :key="rule.id"
            class="flex flex-col lg:flex-row lg:items-center justify-between p-4 rounded-xl border transition-all"
            :class="rule.enabled ? 'bg-slate-950/80 border-slate-800/80 hover:border-slate-700' : 'bg-slate-950/40 border-slate-900 opacity-60'"
          >
            <!-- Left Info -->
            <div class="flex items-start gap-4">
              <!-- Priority Badge -->
              <div class="flex flex-col items-center gap-1 mt-1">
                <Badge variant="outline" class="bg-purple-500/10 text-purple-400 border-purple-500/30 text-xs font-mono font-bold px-2 py-0.5">
                  #{{ rule.priority }}
                </Badge>
                <div class="flex items-center gap-0.5">
                  <button
                    @click="moveRulePriority(idx, 'up')"
                    :disabled="idx === 0"
                    class="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-slate-100 disabled:opacity-30"
                    title="Increase Priority"
                  >
                    <ArrowUp class="size-3" />
                  </button>
                  <button
                    @click="moveRulePriority(idx, 'down')"
                    :disabled="idx === (fleetPolicy.ouTagRules?.length || 0) - 1"
                    class="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-slate-100 disabled:opacity-30"
                    title="Decrease Priority"
                  >
                    <ArrowDown class="size-3" />
                  </button>
                </div>
              </div>

              <!-- Rule Details -->
              <div class="space-y-1.5">
                <div class="flex items-center gap-3">
                  <span class="text-sm font-black text-slate-100">{{ rule.name }}</span>
                  <Badge class="bg-purple-600/30 text-purple-300 border border-purple-500/40 text-[10px] font-bold">
                    -> {{ templateNamesById[rule.targetTemplateId] || rule.targetTemplateId }}
                  </Badge>
                  <span v-if="!rule.enabled" class="text-[10px] text-amber-500 font-bold uppercase tracking-wider">
                    [Disabled]
                  </span>
                </div>

                <p v-if="rule.description" class="text-xs text-slate-400">
                  {{ rule.description }}
                </p>

                <!-- Criteria Chips -->
                <div class="flex flex-wrap items-center gap-2 pt-1">
                  <!-- OU Pattern -->
                  <div v-if="rule.ouPatterns && rule.ouPatterns.length > 0" class="flex items-center gap-1.5 text-xs font-mono">
                    <span class="text-[10px] text-slate-500 uppercase font-sans font-bold">OU ({{ rule.ouMatchMode }}):</span>
                    <Badge v-for="ou in rule.ouPatterns" :key="ou" variant="outline" class="bg-slate-900 border-slate-700 text-slate-300 text-[10px] font-mono">
                      {{ ou }}
                    </Badge>
                  </div>
                  <div v-else class="text-[11px] text-slate-500 font-mono">
                    <span class="text-[10px] font-sans font-bold">OU:</span> All OUs
                  </div>

                  <!-- Tag Criteria -->
                  <div v-if="rule.tags && rule.tags.length > 0" class="flex items-center gap-1.5 text-xs font-mono">
                    <span class="text-[10px] text-slate-500 uppercase font-sans font-bold">Tags ({{ rule.tagMatchMode }}):</span>
                    <Badge v-for="tag in rule.tags" :key="tag" class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px]">
                      {{ tag }}
                    </Badge>
                  </div>
                </div>
              </div>
            </div>

            <!-- Right Actions -->
            <div class="flex items-center gap-2 mt-4 lg:mt-0 pt-3 lg:pt-0 border-t lg:border-t-0 border-slate-900">
              <input
                type="checkbox"
                v-model="rule.enabled"
                @change="saveFleetPolicy"
                class="size-4 rounded border-slate-700 text-purple-600 focus:ring-purple-500 mr-2"
                title="Enable / Disable Rule"
              />
              <Button variant="ghost" size="sm" @click="openEditRuleModal(rule)" class="text-slate-400 hover:text-slate-100 hover:bg-slate-800 text-xs">
                <Edit2 class="size-3.5 mr-1" />
                Edit
              </Button>
              <Button variant="ghost" size="sm" @click="deleteRule(rule.id)" class="text-rose-400 hover:text-rose-300 hover:bg-rose-500/10 text-xs">
                <Trash2 class="size-3.5 mr-1" />
                Delete
              </Button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 2: Fleet Agent Master Policy -->
    <Card class="bg-slate-900/60 border-slate-800">
      <CardHeader class="p-6 border-b border-slate-800">
        <CardTitle class="text-sm font-black text-slate-100 uppercase tracking-wide">
          Fleet Agent Master Policy & Security Controls
        </CardTitle>
        <CardDescription class="text-xs text-slate-400 mt-1">
          Global operational baselines and cryptographic tamper protection applied to all nodes
        </CardDescription>
      </CardHeader>
      <CardContent class="p-6 space-y-6">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          <!-- Backend URL -->
          <div class="space-y-2">
            <Label class="text-xs font-bold text-slate-400 uppercase">Master Ingestion URL</Label>
            <Input v-model="fleetPolicy.backendUrl" class="bg-slate-950 border-slate-800 text-slate-100 font-mono text-xs" />
          </div>

          <!-- Delta Evaluation -->
          <div class="space-y-2">
            <Label class="text-xs font-bold text-slate-400 uppercase">Delta Evaluation Algorithm</Label>
            <select v-model="fleetPolicy.deltaEvaluationAlgorithm" class="w-full h-10 px-3 rounded-md bg-slate-950 border border-slate-800 text-slate-200 text-sm font-bold">
              <option value="xxHash64">xxHash64 (Sub-millisecond Industrial)</option>
              <option value="SHA256">SHA-256 (Cryptographic Integrity)</option>
              <option value="None">None (Stream all raw packets)</option>
            </select>
          </div>

          <!-- Encryption Mode -->
          <div class="space-y-2">
            <Label class="text-xs font-bold text-slate-400 uppercase">Spool Disk Encryption</Label>
            <select v-model="fleetPolicy.spoolEncryptionMode" class="w-full h-10 px-3 rounded-md bg-slate-950 border border-slate-800 text-slate-200 text-sm font-bold">
              <option value="AES_256_GCM">AES-256-GCM (Hardware Bound)</option>
              <option value="DPAPI">Windows DPAPI</option>
              <option value="Plaintext">Plaintext (Development only)</option>
            </select>
          </div>

          <!-- PII Scrubber -->
          <div class="space-y-2">
            <Label class="text-xs font-bold text-slate-400 uppercase">PII & IP Scrubber Level</Label>
            <select v-model="fleetPolicy.piiScrubberStrictLevel" class="w-full h-10 px-3 rounded-md bg-slate-950 border border-slate-800 text-slate-200 text-sm font-bold">
              <option value="Strict">Strict (Mask Hostnames & Subnets)</option>
              <option value="Standard">Standard (Mask Credentials)</option>
              <option value="Disabled">Disabled</option>
            </select>
          </div>

          <!-- Max Spool Size -->
          <div class="space-y-2">
            <Label class="text-xs font-bold text-slate-400 uppercase">Max Spool Disk (MB)</Label>
            <Input v-model.number="fleetPolicy.maxSpoolDiskMb" type="number" class="bg-slate-950 border-slate-800 text-slate-100 font-mono" />
          </div>

          <!-- Default Template -->
          <div class="space-y-2">
            <Label class="text-xs font-bold text-slate-400 uppercase">Default Fleet Recipe (Fallback)</Label>
            <select v-model="fleetPolicy.assignedTemplateId" class="w-full h-10 px-3 rounded-md bg-slate-950 border border-slate-800 text-slate-200 text-sm font-bold">
              <option v-for="t in allTemplates" :key="t.recipeId" :value="t.recipeId">
                {{ t.name }}
              </option>
            </select>
          </div>
        </div>

        <!-- Master Security Switches -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4 pt-4 border-t border-slate-800">
          <div class="flex items-center justify-between p-4 rounded-xl bg-slate-950/80 border border-slate-800">
            <div>
              <div class="text-xs font-bold text-slate-200">Hardware-Bound Cryptographic Sealing</div>
              <div class="text-[11px] text-slate-500">Binds secrets and agent tokens to CPU/Machine-ID and TPM</div>
            </div>
            <input type="checkbox" v-model="fleetPolicy.enforceHardwareBinding" class="size-4 rounded border-slate-700 text-purple-600 focus:ring-purple-500" />
          </div>

          <div class="flex items-center justify-between p-4 rounded-xl bg-slate-950/80 border border-slate-800">
            <div>
              <div class="text-xs font-bold text-slate-200">Allow Remote Diagnostic & File Checks</div>
              <div class="text-[11px] text-slate-500">Master kill-switch for remote commands (locked to Admin)</div>
            </div>
            <input type="checkbox" v-model="fleetPolicy.allowRemoteExecution" class="size-4 rounded border-slate-700 text-purple-600 focus:ring-purple-500" />
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 3: Fleet Node Deployment & Assignment Matrix -->
    <Card class="bg-slate-900/60 border-slate-800">
      <CardHeader class="p-6 border-b border-slate-800 flex flex-col gap-4">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <CardTitle class="text-sm font-black text-slate-100 uppercase tracking-wide flex items-center gap-2">
              <Server class="size-4 text-purple-400" />
              Fleet Node Deployment & Assignment Matrix
            </CardTitle>
            <CardDescription class="text-xs text-slate-400 mt-1">
              Real-time rule evaluation, Active Directory OU & asset tag badges, and high-scale paginated dispatch
            </CardDescription>
          </div>
          <div class="flex flex-wrap items-center gap-3">
            <Button variant="outline" size="sm" @click="fetchControllers(false)" class="border-slate-800 text-xs font-bold uppercase">
              <RefreshCw class="size-3.5 mr-1" />
              Refresh Nodes
            </Button>
            <Button
              size="sm"
              @click="pushToFilteredHosts"
              :disabled="filteredAllControllers.length === 0 || isPushingBulk"
              class="bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold uppercase px-4 border-0"
            >
              <Send class="size-3.5 mr-1.5" />
              Push Config to Filtered ({{ filteredAllControllers.length }})
            </Button>
            <Button
              size="sm"
              @click="pushToAllHosts"
              :disabled="controllers.length === 0 || isPushingBulk"
              class="bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-xl text-xs font-bold uppercase px-4 border border-slate-700"
            >
              Push Entire Fleet ({{ controllers.length }})
            </Button>
          </div>
        </div>

        <!-- Real-Time Search Bar & Filter Mode Bar -->
        <div class="flex flex-col md:flex-row items-center justify-between gap-4 pt-3 border-t border-slate-800/80">
          <!-- Omni Search Input -->
          <div class="relative w-full md:w-96">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-500" />
            <Input
              v-model="searchQuery"
              placeholder="Search hostname, IP, MAC, OU, tags, or rule..."
              class="pl-9 pr-8 bg-slate-950 border-slate-800 text-xs text-slate-200 h-9 rounded-lg"
            />
            <button
              v-if="searchQuery"
              @click="searchQuery = ''"
              class="absolute right-2.5 top-1/2 -translate-y-1/2 p-0.5 rounded text-slate-500 hover:text-slate-300"
            >
              <X class="size-3.5" />
            </button>
          </div>

          <!-- Filter Category Chips -->
          <div class="flex items-center gap-1.5 w-full md:w-auto overflow-x-auto pb-1 md:pb-0">
            <button
              @click="filterMode = 'all'"
              class="px-2.5 py-1 rounded-lg text-xs font-bold transition-all"
              :class="filterMode === 'all' ? 'bg-purple-600 text-white' : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'"
            >
              All ({{ countsByMode.all }})
            </button>
            <button
              @click="filterMode = 'rule'"
              class="px-2.5 py-1 rounded-lg text-xs font-bold transition-all flex items-center gap-1"
              :class="filterMode === 'rule' ? 'bg-purple-600 text-white' : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'"
            >
              <Sparkles class="size-3" />
              Rule Matched ({{ countsByMode.rule }})
            </button>
            <button
              @click="filterMode = 'manual'"
              class="px-2.5 py-1 rounded-lg text-xs font-bold transition-all"
              :class="filterMode === 'manual' ? 'bg-purple-600 text-white' : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'"
            >
              Manual Override ({{ countsByMode.manual }})
            </button>
            <button
              @click="filterMode = 'default'"
              class="px-2.5 py-1 rounded-lg text-xs font-bold transition-all"
              :class="filterMode === 'default' ? 'bg-purple-600 text-white' : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'"
            >
              Default ({{ countsByMode.default }})
            </button>
          </div>
        </div>
      </CardHeader>

      <CardContent class="p-0">
        <Table>
          <TableHeader class="bg-slate-950/80">
            <TableRow class="border-b border-slate-800 hover:bg-transparent">
              <TableHead
                sortable
                :sort-direction="sortKey === 'hostname' ? sortOrder : null"
                @sort="handleSort('hostname', $event)"
                class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]"
              >
                Host Node / Controller
              </TableHead>
              <TableHead
                sortable
                :sort-direction="sortKey === 'ou' ? sortOrder : null"
                @sort="handleSort('ou', $event)"
                class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]"
              >
                Active Directory OU & Tags
              </TableHead>
              <TableHead
                sortable
                :sort-direction="sortKey === 'network' ? sortOrder : null"
                @sort="handleSort('network', $event)"
                class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]"
              >
                Network Identity
              </TableHead>
              <TableHead
                sortable
                :sort-direction="sortKey === 'status' ? sortOrder : null"
                @sort="handleSort('status', $event)"
                class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px]"
              >
                Status & Heartbeat
              </TableHead>
              <TableHead
                sortable
                :sort-direction="sortKey === 'recipe' ? sortOrder : null"
                @sort="handleSort('recipe', $event)"
                class="px-6 py-4 uppercase tracking-widest font-black text-slate-500 text-[10px] w-80"
              >
                Assigned Telemetry Recipe
              </TableHead>
              <TableHead class="px-6 py-4 text-right uppercase tracking-widest font-black text-slate-500 text-[10px]">
                Deployment Action
              </TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            <template v-if="paginationResult.items.length === 0">
              <TableRow>
                <TableCell colspan="6" class="h-32 text-center text-slate-500 uppercase font-bold text-xs">
                  {{ controllers.length === 0 ? 'No controllers detected. Verify fleet agents are running.' : 'No controllers match the specified search query or filter.' }}
                </TableCell>
              </TableRow>
            </template>

            <template v-else>
              <TableRow
                v-for="c in paginationResult.items"
                :key="c.id"
                class="hover:bg-slate-850/60 border-b border-slate-800/80"
              >
                <!-- Host Identity -->
                <TableCell class="px-6 py-4 font-bold">
                  <div class="flex items-center gap-3">
                    <div class="p-2 rounded-lg bg-slate-800 border border-slate-700/60 text-slate-300">
                      <Server class="size-4" />
                    </div>
                    <div>
                      <div class="text-slate-100 font-black text-xs uppercase">{{ c.hostname }}</div>
                      <div class="text-[10px] text-slate-500 font-mono">{{ c.id }}</div>
                    </div>
                  </div>
                </TableCell>

                <!-- Active Directory OU & Tags -->
                <TableCell class="px-6 py-4">
                  <div class="space-y-1 max-w-xs">
                    <!-- AD OU Path Badge -->
                    <div v-if="fleetEvaluations[c.id]?.adOuPath" class="text-slate-400 font-mono text-[10px] truncate" :title="fleetEvaluations[c.id].adOuPath">
                      <span class="text-purple-400 font-bold">OU:</span> {{ fleetEvaluations[c.id].adOuPath }}
                    </div>
                    <!-- Detected Tags Chips -->
                    <div class="flex flex-wrap gap-1">
                      <Badge
                        v-for="tag in (fleetEvaluations[c.id]?.detectedTags || []).slice(0, 4)"
                        :key="tag"
                        variant="outline"
                        class="bg-slate-900 border-slate-800 text-slate-400 text-[9px] px-1.5 py-0"
                      >
                        {{ tag }}
                      </Badge>
                      <span v-if="(fleetEvaluations[c.id]?.detectedTags?.length || 0) > 4" class="text-[9px] text-slate-500">
                        +{{ (fleetEvaluations[c.id]?.detectedTags?.length || 0) - 4 }}
                      </span>
                    </div>
                  </div>
                </TableCell>

                <!-- Network -->
                <TableCell class="px-6 py-4 text-xs font-mono text-slate-400">
                  <div>{{ c.ipAddress || '10.10.x.x' }}</div>
                  <div class="text-[10px] text-slate-500">{{ c.macAddress }}</div>
                </TableCell>

                <!-- Heartbeat -->
                <TableCell class="px-6 py-4">
                  <div class="flex items-center gap-2">
                    <span class="size-2 rounded-full" :class="c.telemetry?.isOnline ? 'bg-emerald-400 animate-pulse' : 'bg-rose-500'" />
                    <span class="text-xs font-bold" :class="c.telemetry?.isOnline ? 'text-emerald-400' : 'text-slate-500'">
                      {{ c.telemetry?.isOnline ? 'Online' : 'Offline' }}
                    </span>
                  </div>
                  <div class="text-[10px] text-slate-500 mt-0.5">
                    CPU: {{ c.telemetry?.cpuUsagePercent || 0 }}% • RAM: {{ c.telemetry?.ramUsagePercent || 0 }}%
                  </div>
                </TableCell>

                <!-- Assigned Recipe with Rule / Manual Badges -->
                <TableCell class="px-6 py-4">
                  <div class="space-y-1.5">
                    <select
                      :value="fleetEvaluations[c.id]?.recipeId"
                      @change="(e: any) => handleHostRecipeChange(c.id, e.target.value)"
                      class="w-full h-8 px-2 rounded-lg bg-slate-950 border border-slate-800 text-slate-200 text-xs font-bold"
                    >
                      <option v-for="tpl in allTemplates" :key="tpl.recipeId" :value="tpl.recipeId">
                        {{ tpl.name }}
                      </option>
                    </select>

                    <!-- Origin Indicator Badge -->
                    <div class="flex items-center justify-between gap-1 text-[10px]">
                      <!-- Rule Mode -->
                      <Badge
                        v-if="fleetEvaluations[c.id]?.assignmentMode === 'rule'"
                        class="bg-purple-500/10 text-purple-400 border border-purple-500/30 text-[9px] px-1.5 py-0 flex items-center gap-1 font-bold"
                        :title="`Matched Rule #${fleetEvaluations[c.id].matchedRule?.priority}: ${fleetEvaluations[c.id].matchedRule?.name}`"
                      >
                        <Sparkles class="size-2.5" />
                        Rule: {{ fleetEvaluations[c.id].matchedRule?.name }}
                      </Badge>

                      <!-- Manual Override Mode -->
                      <div v-else-if="fleetEvaluations[c.id]?.assignmentMode === 'manual'" class="flex items-center gap-1.5">
                        <Badge class="bg-amber-500/10 text-amber-400 border border-amber-500/30 text-[9px] px-1.5 py-0 font-bold">
                          Manual Override
                        </Badge>
                        <button
                          @click="revertToRule(c.id)"
                          class="text-[10px] text-slate-400 hover:text-purple-300 underline flex items-center gap-0.5"
                          title="Revert to evaluated rule recipe"
                        >
                          <Undo2 class="size-2.5" />
                          Revert
                        </button>
                      </div>

                      <!-- Default Mode -->
                      <Badge v-else class="bg-slate-800 text-slate-400 border-0 text-[9px] px-1.5 py-0">
                        Fleet Default
                      </Badge>
                    </div>
                  </div>
                </TableCell>

                <!-- Action Button -->
                <TableCell class="px-6 py-4 text-right">
                  <Button
                    size="sm"
                    @click="dispatchConfigToHost(c)"
                    :disabled="dispatchingHostId === c.id"
                    class="bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-xs font-bold h-8 px-3 border-0"
                  >
                    <RefreshCw v-if="dispatchingHostId === c.id" class="size-3 mr-1 animate-spin" />
                    <Send v-else class="size-3 mr-1" />
                    Push Config
                  </Button>
                </TableCell>
              </TableRow>
            </template>
          </TableBody>
        </Table>

        <!-- Configurable Pagination Bar -->
        <div class="p-4 bg-slate-950/60 border-t border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4 text-xs text-slate-400 font-mono">
          <!-- Left: Showing items count -->
          <div>
            Showing <span class="text-slate-200 font-bold">{{ paginationResult.startItem }}</span> to
            <span class="text-slate-200 font-bold">{{ paginationResult.endItem }}</span> of
            <span class="text-slate-200 font-bold">{{ paginationResult.filteredTotal }}</span> controllers
            <span v-if="paginationResult.filteredTotal < controllers.length" class="text-slate-500">
              (filtered from {{ controllers.length }} total)
            </span>
          </div>

          <!-- Center: Page Size Selector (5, 10, 100, 1000, Custom) -->
          <div class="flex items-center gap-2">
            <span class="text-[11px] text-slate-500 uppercase font-sans font-bold">Show per page:</span>
            <div class="inline-flex rounded-lg bg-slate-900 border border-slate-800 p-0.5">
              <button
                v-for="opt in ['5', '10', '100', '1000'] as const"
                :key="opt"
                @click="selectedPageSizeOption = opt"
                class="px-2.5 py-1 rounded-md text-xs font-bold transition-colors"
                :class="selectedPageSizeOption === opt ? 'bg-purple-600 text-white' : 'text-slate-400 hover:text-slate-200'"
              >
                {{ opt }}
              </button>
              <button
                @click="selectedPageSizeOption = 'custom'"
                class="px-2.5 py-1 rounded-md text-xs font-bold transition-colors"
                :class="selectedPageSizeOption === 'custom' ? 'bg-purple-600 text-white' : 'text-slate-400 hover:text-slate-200'"
              >
                Custom
              </button>
            </div>

            <!-- Custom Page Size Input -->
            <div v-if="selectedPageSizeOption === 'custom'" class="flex items-center gap-1">
              <Input
                type="number"
                min="1"
                max="10000"
                v-model.number="customPageSizeInput"
                class="w-16 h-8 bg-slate-900 border-slate-800 text-slate-200 text-xs text-center font-mono p-1"
              />
            </div>
          </div>

          <!-- Right: Page Navigation (<< < Page X of Y > >>) -->
          <div class="flex items-center gap-1.5">
            <Button
              variant="outline"
              size="sm"
              @click="currentPage = 1"
              :disabled="paginationResult.currentPage === 1"
              class="size-8 p-0 border-slate-800 bg-slate-900 disabled:opacity-30"
              title="First Page"
            >
              <ChevronsLeft class="size-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              @click="currentPage = Math.max(1, currentPage - 1)"
              :disabled="paginationResult.currentPage === 1"
              class="size-8 p-0 border-slate-800 bg-slate-900 disabled:opacity-30"
              title="Previous Page"
            >
              <ChevronLeft class="size-4" />
            </Button>

            <span class="px-2 font-bold text-slate-300">
              Page {{ paginationResult.currentPage }} of {{ paginationResult.totalPages }}
            </span>

            <Button
              variant="outline"
              size="sm"
              @click="currentPage = Math.min(paginationResult.totalPages, currentPage + 1)"
              :disabled="paginationResult.currentPage === paginationResult.totalPages"
              class="size-8 p-0 border-slate-800 bg-slate-900 disabled:opacity-30"
              title="Next Page"
            >
              <ChevronRight class="size-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              @click="currentPage = paginationResult.totalPages"
              :disabled="paginationResult.currentPage === paginationResult.totalPages"
              class="size-8 p-0 border-slate-800 bg-slate-900 disabled:opacity-30"
              title="Last Page"
            >
              <ChevronsRight class="size-4" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Dispatched Audit History -->
    <Card v-if="dispatchedLogs.length > 0" class="bg-slate-900/60 border-slate-800">
      <CardHeader class="p-5 pb-3">
        <CardTitle class="text-xs font-black text-slate-200 uppercase tracking-widest flex items-center gap-2">
          <Clock class="size-3.5 text-purple-400" />
          Recent Command Dispatch Activity
        </CardTitle>
      </CardHeader>
      <CardContent class="p-5 pt-0 space-y-2">
        <div
          v-for="log in dispatchedLogs.slice(0, 5)"
          :key="log.id"
          class="flex items-center justify-between p-3 rounded-lg bg-slate-950/80 border border-slate-800/80 text-xs font-mono"
        >
          <div class="flex items-center gap-2">
            <CheckCircle2 class="size-3.5 text-emerald-400" />
            <span class="text-slate-200 font-bold">{{ log.hostname }}</span>
            <span class="text-slate-500">-> {{ log.template }}</span>
          </div>
          <div class="flex items-center gap-3">
            <span class="text-[10px] text-purple-400 font-bold">{{ log.status }}</span>
            <span class="text-[10px] text-slate-500">{{ log.time }}</span>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Rule Creation / Editing Modal -->
    <div
      v-if="isRuleModalOpen"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in"
    >
      <div class="w-full max-w-lg bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden space-y-6 p-6">
        <div class="flex items-center justify-between border-b border-slate-800 pb-4">
          <div class="flex items-center gap-2.5">
            <div class="p-2 rounded-xl bg-purple-500/10 text-purple-400">
              <Sparkles class="size-5" />
            </div>
            <div>
              <h3 class="text-base font-black text-slate-100 uppercase tracking-wide">
                {{ editingRuleId ? 'Edit OU & Tag Recipe Rule' : 'Add New OU & Tag Recipe Rule' }}
              </h3>
              <p class="text-xs text-slate-400 mt-0.5">
                Target recipe automatically selected when criteria match
              </p>
            </div>
          </div>
          <button @click="isRuleModalOpen = false" class="text-slate-400 hover:text-slate-200 p-1">
            <X class="size-5" />
          </button>
        </div>

        <div class="space-y-4 max-h-[70vh] overflow-y-auto pr-1">
          <!-- Rule Name -->
          <div class="space-y-1.5">
            <Label class="text-xs font-bold text-slate-400 uppercase">Rule Name</Label>
            <Input v-model="newRuleForm.name" placeholder="e.g. Beckhoff IPC & Motion Controllers" class="bg-slate-950 border-slate-800 text-slate-100 text-xs" />
          </div>

          <!-- Description -->
          <div class="space-y-1.5">
            <Label class="text-xs font-bold text-slate-400 uppercase">Description (Optional)</Label>
            <Input v-model="newRuleForm.description" placeholder="Brief description of the rule criteria and target" class="bg-slate-950 border-slate-800 text-slate-100 text-xs" />
          </div>

          <!-- Target Recipe Selector -->
          <div class="space-y-1.5">
            <Label class="text-xs font-bold text-slate-400 uppercase">Target Telemetry Recipe</Label>
            <select v-model="newRuleForm.targetTemplateId" class="w-full h-9 px-3 rounded-lg bg-slate-950 border border-slate-800 text-slate-200 text-xs font-bold">
              <option v-for="t in allTemplates" :key="t.recipeId" :value="t.recipeId">
                {{ t.name }}
              </option>
            </select>
          </div>

          <!-- Priority -->
          <div class="space-y-1.5">
            <Label class="text-xs font-bold text-slate-400 uppercase">Evaluation Priority (1 = Highest)</Label>
            <Input v-model.number="newRuleForm.priority" type="number" min="1" class="bg-slate-950 border-slate-800 text-slate-100 font-mono text-xs" />
          </div>

          <!-- Active Directory OU Pattern -->
          <div class="space-y-2 pt-2 border-t border-slate-800">
            <div class="flex items-center justify-between">
              <Label class="text-xs font-bold text-slate-400 uppercase">Active Directory OU Wildcards</Label>
              <div class="flex items-center gap-2 text-xs">
                <span class="text-slate-500 text-[10px]">Match:</span>
                <select v-model="newRuleForm.ouMatchMode" class="bg-slate-950 border border-slate-800 rounded px-2 py-0.5 text-[10px] text-slate-300 font-bold">
                  <option value="ANY">ANY Pattern</option>
                  <option value="ALL">ALL Patterns</option>
                </select>
              </div>
            </div>

            <div class="flex gap-2">
              <Input
                v-model="newOuPatternInput"
                @keyup.enter="addOuPatternToForm"
                placeholder="e.g. *LINE-A* or OU=Fastening*"
                class="bg-slate-950 border-slate-800 text-slate-100 font-mono text-xs"
              />
              <Button size="sm" @click="addOuPatternToForm" variant="outline" class="border-slate-800 text-xs">
                Add
              </Button>
            </div>

            <div class="flex flex-wrap gap-1.5 min-h-6">
              <Badge
                v-for="(ou, idx) in newRuleForm.ouPatterns"
                :key="ou"
                variant="outline"
                class="bg-slate-950 border-slate-800 text-slate-300 font-mono text-[10px] flex items-center gap-1"
              >
                {{ ou }}
                <button @click="removeOuPatternFromForm(idx)" class="text-slate-500 hover:text-rose-400">
                  <X class="size-3" />
                </button>
              </Badge>
              <span v-if="newRuleForm.ouPatterns.length === 0" class="text-[10px] text-slate-500 italic">
                No OU pattern specified (matches all OUs)
              </span>
            </div>
          </div>

          <!-- Tag Criteria & Match Mode -->
          <div class="space-y-2 pt-2 border-t border-slate-800">
            <div class="flex items-center justify-between">
              <Label class="text-xs font-bold text-slate-400 uppercase">Required Asset Tags / Criteria</Label>
              <div class="flex items-center gap-2 text-xs">
                <span class="text-slate-500 text-[10px]">Mode:</span>
                <select v-model="newRuleForm.tagMatchMode" class="bg-slate-950 border border-slate-800 rounded px-2 py-0.5 text-[10px] text-slate-300 font-bold">
                  <option value="ALL">Match ALL Tags (AND)</option>
                  <option value="ANY">Match ANY Tag (OR)</option>
                </select>
              </div>
            </div>

            <div class="flex gap-2">
              <Input
                v-model="newTagInput"
                @keyup.enter="addTagToForm"
                placeholder="Type tag (e.g. Beckhoff, IPC, Controller) and press Enter"
                class="bg-slate-950 border-slate-800 text-slate-100 text-xs"
              />
              <Button size="sm" @click="addTagToForm" variant="outline" class="border-slate-800 text-xs">
                Add
              </Button>
            </div>

            <div class="flex flex-wrap gap-1.5 min-h-6">
              <Badge
                v-for="(tag, idx) in newRuleForm.tags"
                :key="tag"
                class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px] flex items-center gap-1"
              >
                {{ tag }}
                <button @click="removeTagFromForm(idx)" class="text-emerald-500 hover:text-rose-400">
                  <X class="size-3" />
                </button>
              </Badge>
              <span v-if="newRuleForm.tags.length === 0" class="text-[10px] text-slate-500 italic">
                No tag criteria specified
              </span>
            </div>
          </div>
        </div>

        <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-800">
          <Button variant="ghost" size="sm" @click="isRuleModalOpen = false" class="text-xs font-bold uppercase text-slate-400">
            Cancel
          </Button>
          <Button size="sm" @click="saveRuleModal" class="bg-purple-600 hover:bg-purple-500 text-white text-xs font-bold uppercase px-5">
            Save Rule
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
