<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue'
import {
  Clock, UserCheck, GitBranch, X, Plus, Trash2, Check,
  ChevronDown, ChevronUp, AlertTriangle, RefreshCw, Shield,
  Users, Edit2, CheckSquare, Square, Lock, Sparkles, User, Layers, Cpu
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription
} from '~/components/ui/dialog'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '~/components/ui/select'
import SearchableTargetCombobox, { type TargetItem } from '~/components/common/SearchableTargetCombobox.vue'
import { useAuthSession, DEMO_PERSONAS, type DemoPersona } from '~/composables/useAuthSession'

// ─── Constants ───────────────────────────────────────────────────────────────
const MACHINE_TYPES = [
  'Automatic Optical Inspection',
  'Gap Filler',
  'Screwing Station',
  'Soldering',
  'Milling',
  'Fitting',
  'Pressing',
  'Manipulator',
  'Tester Cell',
  'Painting',
]

const STANDARD_CATEGORIES = [
  'Mechanical',
  'Electrical',
  'Controls & PLC',
  'Robotics & Automation',
  'Vision & Optics',
  'Dispensing & Fluidics',
  'Thermal & Soldering',
  'Pneumatics & Hydraulics',
  'MES & SAP Sync'
]

const ABSENCE_REASONS = ['Sick', 'Emergency', 'Vacation', 'Training', 'Unplanned']

// ─── Props & Emits ───────────────────────────────────────────────────────────
const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ (e: 'close'): void }>()

// ─── Session & Governance State ──────────────────────────────────────────────
const {
  user,
  userRole,
  dedicationTier,
  simulatedPersona,
  setSimulatedPersona,
  clearSimulatedPersona
} = useAuthSession()

// ─── Tab state ───────────────────────────────────────────────────────────────
type TabId = 'attendance' | 'dedication' | 'clusters'
const activeTab = ref<TabId>('attendance')

const tabs: { id: TabId; label: string; icon: any }[] = [
  { id: 'attendance',  label: 'Shift Attendance',       icon: Clock },
  { id: 'dedication',  label: 'Technician Dedication',  icon: UserCheck },
  { id: 'clusters',    label: 'Machine Group Clusters', icon: GitBranch },
]

// ─────────────────────────────────────────────────────────────────────────────
// TAB 1 — Shift Attendance
// ─────────────────────────────────────────────────────────────────────────────
interface AbsenceRecord {
  id: string
  technicianName: string
  reason: string
  endDate: string
  backupTechnician?: string
}
interface TeamsOooRecord {
  id: string
  displayName: string
}

const absences = ref<AbsenceRecord[]>([])
const teamsOoo  = ref<TeamsOooRecord[]>([])
const attendanceLoading = ref(false)

const knownTechnicians = ref<string[]>([
  'István Kovács', 'Gábor Varga', 'Zoltán Németh', 'Bence Horváth',
  'Engineer Sally', 'Engineer Orwell', 'Katalin Nagy', 'Shift Leader Ferenc'
])

function technicianStatus(name: string): 'Available' | 'Absent' | 'Teams OOO' {
  if (absences.value.some(a => a.technicianName === name)) return 'Absent'
  if (teamsOoo.value.some(t => t.displayName === name))   return 'Teams OOO'
  return 'Available'
}

const absenceFormTarget = ref<string | null>(null)
const absenceForm = reactive({ reason: '', endDate: '', backupTechnician: '' })
const absenceSubmitting = ref(false)
const absenceError = ref<string | null>(null)

function openAbsenceForm(name: string) {
  absenceFormTarget.value = name
  absenceForm.reason = ''
  absenceForm.endDate = ''
  absenceForm.backupTechnician = ''
  absenceError.value = null
}

function closeAbsenceForm() { absenceFormTarget.value = null }

async function submitAbsence() {
  if (!absenceForm.reason || !absenceForm.endDate) {
    absenceError.value = 'Reason and end date are required.'
    return
  }
  absenceSubmitting.value = true
  absenceError.value = null
  try {
    await $fetch('/api/technicians/absences', {
      method: 'POST',
      body: {
        technicianName: absenceFormTarget.value,
        reason: absenceForm.reason,
        endDate: absenceForm.endDate,
        backupTechnician: absenceForm.backupTechnician || undefined,
        markedBy: user.value?.name || 'Shift Leader'
      },
    })
    closeAbsenceForm()
    await loadAttendanceData()
  } catch (err: any) {
    absenceError.value = err?.data?.message || 'Failed to mark absence.'
  } finally {
    absenceSubmitting.value = false
  }
}

async function resolveAbsence(absence: AbsenceRecord) {
  try {
    await $fetch(`/api/technicians/absences/${absence.id}`, { method: 'DELETE' })
    await loadAttendanceData()
  } catch (err) {
    console.error('Failed to resolve absence', err)
  }
}

const oooToggles = reactive<Record<string, boolean>>({})

async function toggleTeamsOoo(name: string) {
  try {
    await $fetch('/api/integrations/teams/ooo', {
      method: 'POST',
      body: { userId: name, displayName: name, isOutOfOffice: !oooToggles[name] },
    })
    oooToggles[name] = !oooToggles[name]
    await loadAttendanceData()
  } catch (err) {
    console.error('Teams OOO toggle failed', err)
  }
}

async function loadAttendanceData() {
  attendanceLoading.value = true
  try {
    const [absData, oooData, candData] = await Promise.all([
      $fetch<AbsenceRecord[]>('/api/technicians/absences').catch(() => []),
      $fetch<any>('/api/integrations/teams/ooo').catch(() => []),
      $fetch<any[]>('/api/technicians/candidates').catch(() => [])
    ])
    absences.value = Array.isArray(absData) ? absData : []
    const statusList = Array.isArray(oooData) ? oooData : (oooData?.statuses ?? [])
    teamsOoo.value = statusList
    for (const t of statusList) {
      if (t && (t.displayName || t.userId)) {
        const key = t.displayName || t.userId
        oooToggles[key] = !!t.isOutOfOffice
      }
    }
    if (candData && candData.length > 0) {
      const names = candData.map(c => c.name)
      knownTechnicians.value = [...new Set([...names, ...knownTechnicians.value])]
    }
  } finally {
    attendanceLoading.value = false
  }
}

const allTechnicians = computed(() => {
  const extra = absences.value
    .map(a => a.technicianName)
    .filter(n => !knownTechnicians.value.includes(n))
  return [...knownTechnicians.value, ...extra]
})

// ─────────────────────────────────────────────────────────────────────────────
// TAB 2 — Technician Dedication
// ─────────────────────────────────────────────────────────────────────────────
interface DedicationRule {
  id: string
  scopeType: 'Technology' | 'Line/Group' | 'Machine' | 'technology' | 'group' | 'machine'
  target: string
  targetId?: string
  categoryFilter?: string
  technicianName: string
  technicianEmail?: string
  backupTechnician?: string
  backupTechnicianName?: string
  role?: string
  assignedByRole?: string
  assignedByUserName?: string
}

const rules = ref<DedicationRule[]>([])
const rulesLoading = ref(false)
const showAddRule = ref(false)
const ruleSubmitting = ref(false)
const ruleError = ref<string | null>(null)

const ruleForm = reactive({
  scopeType: 'Technology' as 'Technology' | 'Line/Group' | 'Machine',
  target: '',
  categoryFilter: '',
  technicianName: '',
  technicianEmail: '',
  backupTechnician: '',
  role: 'Group Leader' as 'Shift Leader' | 'Group Leader' | 'Manager' | 'Engineer' | 'Technician',
})

// Auto sync technicianName if user is restricted to self dedication
watch([() => user.value, dedicationTier], () => {
  if (dedicationTier.value === 'self') {
    ruleForm.technicianName = user.value?.name || ''
    ruleForm.technicianEmail = (user.value as any)?.email || ''
    ruleForm.role = userRole.value === 'technician' ? 'Technician' : 'Engineer'
  } else if (dedicationTier.value === 'shift') {
    if (ruleForm.scopeType === 'Technology') {
      ruleForm.scopeType = 'Machine'
    }
    ruleForm.role = 'Shift Leader'
  } else if (dedicationTier.value === 'group') {
    ruleForm.role = 'Group Leader'
  } else {
    ruleForm.role = 'Manager'
  }
}, { immediate: true })

function openAddRule() {
  ruleError.value = null
  showAddRule.value = true

  if (dedicationTier.value === 'self') {
    ruleForm.technicianName = user.value?.name || ''
    ruleForm.technicianEmail = (user.value as any)?.email || ''
    ruleForm.role = userRole.value === 'technician' ? 'Technician' : 'Engineer'
    ruleForm.target = ''
    ruleForm.categoryFilter = ''
    ruleForm.backupTechnician = ''
  } else if (dedicationTier.value === 'shift') {
    ruleForm.scopeType = 'Machine'
    ruleForm.technicianName = ''
    ruleForm.technicianEmail = ''
    ruleForm.role = 'Shift Leader'
    ruleForm.target = ''
    ruleForm.categoryFilter = ''
    ruleForm.backupTechnician = ''
  } else {
    ruleForm.technicianName = ''
    ruleForm.technicianEmail = ''
    ruleForm.role = dedicationTier.value === 'group' ? 'Group Leader' : 'Manager'
    ruleForm.target = ''
    ruleForm.categoryFilter = ''
    ruleForm.backupTechnician = ''
  }
}

// Combobox Query Functions
async function queryTechnicians(q: string): Promise<TargetItem[]> {
  try {
    let url = '/api/technicians/candidates'
    if (dedicationTier.value === 'shift') {
      url += '?role=technician'
    } else if (dedicationTier.value === 'group') {
      url += '?role=engineer_technician'
    }
    const cands = await $fetch<any[]>(url)
    return cands.map(c => ({
      id: c.id,
      label: c.name,
      sublabel: `${c.department} • ${c.specialization || ''}`,
      badge: c.role.replace('_', ' '),
      badgeColor: c.role === 'manager'
        ? 'border-emerald-500/30 bg-emerald-500/10 text-emerald-400'
        : c.role === 'group_leader'
          ? 'border-violet-500/30 bg-violet-500/10 text-violet-400'
          : c.role === 'shift_leader'
            ? 'border-cyan-500/30 bg-cyan-500/10 text-cyan-400'
            : 'border-indigo-500/30 bg-indigo-500/10 text-indigo-400',
      role: c.role,
      isOutOfOffice: c.isOutOfOffice,
      raw: c
    }))
  } catch {
    return []
  }
}

async function queryBackupTechnicians(q: string): Promise<TargetItem[]> {
  try {
    const cands = await $fetch<any[]>('/api/technicians/candidates')
    return cands.map(c => ({
      id: c.id,
      label: c.name,
      sublabel: `${c.department} • Availability: ${c.isOutOfOffice ? 'OOO' : 'On-Duty'}`,
      badge: c.role.replace('_', ' '),
      isOutOfOffice: c.isOutOfOffice,
      raw: c
    }))
  } catch {
    return []
  }
}

// Scope Target options based on current scopeType
const technologyOptions = computed<TargetItem[]>(() => {
  return MACHINE_TYPES.map(mt => ({
    id: mt,
    label: mt,
    sublabel: 'Standard Machine Technology Group',
    category: 'Technology',
    badge: 'Tech'
  }))
})

async function queryScopeTargets(q: string): Promise<TargetItem[]> {
  if (ruleForm.scopeType === 'Technology') {
    return technologyOptions.value
  }

  if (ruleForm.scopeType === 'Line/Group') {
    try {
      const grps = await $fetch<any[]>('/api/machine-groups').catch(() => [])
      if (grps && grps.length > 0) {
        return grps.map(g => ({
          id: g.id,
          label: g.name,
          sublabel: g.description || `Machine Types: ${(g.machineTypes || []).join(', ')}`,
          category: 'Group / Line',
          badge: g.parentId ? 'Sub-Cell' : 'Line',
          badgeColor: 'border-cyan-500/30 bg-cyan-500/10 text-cyan-400',
          raw: g
        }))
      }
    } catch {}
    return [
      { id: 'grp-line06', label: 'Line 06 — Module Assembly', sublabel: 'Battery module assembly line', category: 'Line' },
      { id: 'grp-cell-a', label: 'Cell A — Dispensing & Fastening', sublabel: 'Dispensing and screwing cell', category: 'Cell' },
      { id: 'grp-line09', label: 'Line 09 — Pack Assembly', sublabel: 'Battery pack assembly line', category: 'Line' }
    ]
  }

  // scopeType === 'Machine'
  try {
    const stns = await $fetch<any[]>('/api/proxy/v1/Machine').catch(() => [])
    if (stns && stns.length > 0) {
      return stns.map(s => ({
        id: s.customIdentifier || s.name || s.id,
        label: s.displayName || s.name || s.customIdentifier,
        sublabel: `${s.organizationId || 'Floor'} • ${s.machineType || 'Machining'}`,
        badge: s.machineType || 'Station',
        raw: s
      }))
    }
  } catch {}
  return [
    { id: 'STATION-OP10-01', label: 'OP10 Machining Cell', sublabel: 'Battery Assembly Plant • Milling', badge: 'Milling' },
    { id: 'L06-OP150', label: 'Line 06 - Automated Battery Station 150', sublabel: 'Line 06 • Screwing Station', badge: 'Screwing' },
    { id: 'L09-OP270', label: 'Line 09 - AOI Optical Inspection 270', sublabel: 'Line 09 • AOI', badge: 'AOI' }
  ]
}

const categoryOptions = computed<TargetItem[]>(() => {
  return STANDARD_CATEGORIES.map(cat => ({
    id: cat,
    label: cat,
    sublabel: 'Standard Technical Discipline',
    badge: 'Category'
  }))
})

async function saveRule() {
  if (!ruleForm.target || !ruleForm.technicianName) {
    ruleError.value = 'Target and technician name are required.'
    return
  }
  ruleSubmitting.value = true
  ruleError.value = null
  try {
    await $fetch('/api/technicians/rules', {
      method: 'POST',
      body: {
        scopeType: ruleForm.scopeType,
        target: ruleForm.target,
        targetId: ruleForm.target,
        categoryFilter: ruleForm.categoryFilter || undefined,
        technicianName: ruleForm.technicianName,
        technicianEmail: ruleForm.technicianEmail || undefined,
        backupTechnician: ruleForm.backupTechnician || undefined,
        role: ruleForm.role,
        callerRole: userRole.value,
        callerUserName: user.value?.name,
        callerUserId: user.value?.id
      },
    })
    showAddRule.value = false
    await loadRules()
  } catch (err: any) {
    ruleError.value = err?.data?.message || err?.message || 'Failed to save rule.'
  } finally {
    ruleSubmitting.value = false
  }
}

async function deleteRule(rule: DedicationRule) {
  try {
    await $fetch(`/api/technicians/rules/${rule.id}`, { method: 'DELETE' })
    await loadRules()
  } catch (err) { console.error('Failed to delete rule', err) }
}

async function loadRules() {
  rulesLoading.value = true
  try {
    const data = await $fetch<DedicationRule[]>('/api/technicians/rules').catch(() => [])
    rules.value = data ?? []
  } finally { rulesLoading.value = false }
}

// ─────────────────────────────────────────────────────────────────────────────
// TAB 3 — Machine Group Clusters
// ─────────────────────────────────────────────────────────────────────────────
interface MachineGroup {
  id: string
  name: string
  description?: string
  parentGroupId?: string
  machineTypes: string[]
  leadEngineer?: string
}

const groups = ref<MachineGroup[]>([])
const groupsLoading = ref(false)
const editingGroupId = ref<string | null>(null)
const showCreateGroup = ref(false)
const groupSubmitting = ref(false)
const groupError = ref<string | null>(null)

const editForm = reactive<{ machineTypes: string[]; leadEngineer: string }>(
  { machineTypes: [], leadEngineer: '' }
)

const createForm = reactive<{
  name: string; description: string; parentGroupId: string; machineTypes: string[]
}>({ name: '', description: '', parentGroupId: '', machineTypes: [] })

function openEditGroup(group: MachineGroup) {
  editingGroupId.value = group.id
  editForm.machineTypes = [...(group.machineTypes ?? [])]
  editForm.leadEngineer = group.leadEngineer ?? ''
  groupError.value = null
}
function closeEditGroup() { editingGroupId.value = null }

function toggleEditMachineType(mt: string) {
  const idx = editForm.machineTypes.indexOf(mt)
  idx >= 0 ? editForm.machineTypes.splice(idx, 1) : editForm.machineTypes.push(mt)
}

function toggleCreateMachineType(mt: string) {
  const idx = createForm.machineTypes.indexOf(mt)
  idx >= 0 ? createForm.machineTypes.splice(idx, 1) : createForm.machineTypes.push(mt)
}

async function saveGroupCluster(groupId: string) {
  groupSubmitting.value = true
  groupError.value = null
  try {
    await $fetch(`/api/machine-groups/${groupId}`, {
      method: 'PATCH',
      body: { machineTypes: editForm.machineTypes, leadEngineer: editForm.leadEngineer || undefined },
    })
    closeEditGroup()
    await loadGroups()
  } catch (err: any) {
    groupError.value = err?.data?.message || 'Failed to save cluster.'
  } finally { groupSubmitting.value = false }
}

function openCreateGroup() {
  Object.assign(createForm, { name: '', description: '', parentGroupId: '', machineTypes: [] })
  groupError.value = null
  showCreateGroup.value = true
}

async function createGroup() {
  if (!createForm.name.trim()) { groupError.value = 'Group name is required.'; return }
  groupSubmitting.value = true
  groupError.value = null
  try {
    await $fetch('/api/machine-groups', {
      method: 'POST',
      body: {
        name: createForm.name.trim(),
        description: createForm.description || undefined,
        parentGroupId: createForm.parentGroupId || undefined,
        machineTypes: createForm.machineTypes,
      },
    })
    showCreateGroup.value = false
    await loadGroups()
  } catch (err: any) {
    groupError.value = err?.data?.message || 'Failed to create group.'
  } finally { groupSubmitting.value = false }
}

async function loadGroups() {
  groupsLoading.value = true
  try {
    const data = await $fetch<MachineGroup[]>('/api/machine-groups').catch(() => [])
    groups.value = data ?? []
  } finally { groupsLoading.value = false }
}

// ─── Lifecycle ───────────────────────────────────────────────────────────────
onMounted(async () => {
  await Promise.all([loadAttendanceData(), loadRules(), loadGroups()])
})

// ─── Style helpers ───────────────────────────────────────────────────────────
const statusColor: Record<string, string> = {
  'Available': 'border-emerald-500/30 text-emerald-400 bg-emerald-500/10',
  'Absent':    'border-rose-500/30 text-rose-400 bg-rose-500/10',
  'Teams OOO': 'border-amber-500/30 text-amber-400 bg-amber-500/10',
}

const scopeColor: Record<string, string> = {
  'Technology': 'text-indigo-400 bg-indigo-500/10 border-indigo-500/30',
  'technology': 'text-indigo-400 bg-indigo-500/10 border-indigo-500/30',
  'Line/Group': 'text-cyan-400 bg-cyan-500/10 border-cyan-500/30',
  'group':      'text-cyan-400 bg-cyan-500/10 border-cyan-500/30',
  'Machine':    'text-violet-400 bg-violet-500/10 border-violet-500/30',
  'machine':    'text-violet-400 bg-violet-500/10 border-violet-500/30',
}
</script>

<template>
  <Dialog :open="open" @update:open="(v) => { if (!v) emit('close') }">
    <DialogContent class="max-w-4xl w-full bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl p-0 overflow-hidden max-h-[92vh] flex flex-col">

      <!-- ── Header ─────────────────────────────────────────────────────── -->
      <DialogHeader class="p-6 border-b border-slate-800 bg-slate-900/80 flex-shrink-0">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-2xl bg-violet-600/10 text-violet-400 border border-violet-500/20">
              <Shield class="h-6 w-6" />
            </div>
            <div>
              <DialogTitle class="text-lg font-black uppercase tracking-tight text-slate-100 flex items-center gap-2">
                <span>Preferred Technicians & Governance</span>
                <Badge variant="outline" class="text-[9px] uppercase tracking-wider font-bold border-indigo-500/40 text-indigo-300 bg-indigo-500/10">
                  Multi-Tier Role Access
                </Badge>
              </DialogTitle>
              <DialogDescription class="text-[10px] font-bold text-slate-500 uppercase tracking-widest mt-0.5">
                Shift Attendance · Dedication Rules · Machine Group Clusters
              </DialogDescription>
            </div>
          </div>
          <Button variant="ghost" size="icon" @click="emit('close')" class="text-slate-400 hover:text-white rounded-xl">
            <X class="h-5 w-5" />
          </Button>
        </div>

        <!-- Role Simulator Bar (Allows switching test role live to test governance) -->
        <div class="mt-4 p-2.5 bg-slate-950/80 rounded-2xl border border-slate-800/80 flex flex-wrap items-center justify-between gap-2">
          <div class="flex items-center gap-2">
            <span class="text-[10px] font-black uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
              <Sparkles class="w-3.5 h-3.5 text-amber-400" />
              <span>Simulate Role Persona:</span>
            </span>
            <div class="flex flex-wrap gap-1">
              <button
                v-for="p in DEMO_PERSONAS"
                :key="p.id"
                type="button"
                @click="setSimulatedPersona(p)"
                class="px-2 py-1 rounded-lg text-[10px] font-bold transition-all flex items-center gap-1"
                :class="[
                  user?.name === p.name
                    ? 'bg-indigo-600 text-white shadow-sm ring-1 ring-indigo-400'
                    : 'bg-slate-900 border border-slate-800 text-slate-400 hover:text-slate-200 hover:bg-slate-800'
                ]"
              >
                <span>{{ p.name.replace(' (Plant Manager)', '') }}</span>
                <span class="text-[8px] opacity-70">({{ p.role.replace('_', ' ') }})</span>
              </button>
            </div>
          </div>

          <!-- Active Tier Badge -->
          <div class="flex items-center gap-1.5">
            <span class="text-[9px] font-mono text-slate-500">Tier:</span>
            <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider"
              :class="[
                dedicationTier === 'self' ? 'border-amber-500/40 text-amber-300 bg-amber-500/10' :
                dedicationTier === 'shift' ? 'border-cyan-500/40 text-cyan-300 bg-cyan-500/10' :
                dedicationTier === 'group' ? 'border-violet-500/40 text-violet-300 bg-violet-500/10' :
                'border-emerald-500/40 text-emerald-300 bg-emerald-500/10'
              ]"
            >
              {{ dedicationTier === 'self' ? 'Self-Dedication Only' : dedicationTier === 'shift' ? 'Shift Leader Authority' : dedicationTier === 'group' ? 'Group Leader Authority' : 'Manager / Full Governance' }}
            </Badge>
          </div>
        </div>

        <!-- Tab row -->
        <div class="flex gap-1 mt-4 p-1 bg-slate-950/60 rounded-xl border border-slate-800 w-fit">
          <button
            v-for="tab in tabs"
            :key="tab.id"
            @click="activeTab = tab.id"
            :class="[
              'flex items-center gap-2 px-4 py-2 rounded-lg text-[11px] font-black uppercase tracking-wider transition-all',
              activeTab === tab.id
                ? 'bg-slate-800 text-slate-100 shadow'
                : 'text-slate-500 hover:text-slate-300 hover:bg-slate-800/50'
            ]"
          >
            <component :is="tab.icon" class="h-3.5 w-3.5" />
            {{ tab.label }}
          </button>
        </div>
      </DialogHeader>

      <!-- ── Tab body ───────────────────────────────────────────────────── -->
      <div class="flex-1 overflow-y-auto">

        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- TAB 1: Shift Attendance                                       -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'attendance'" class="p-6 space-y-3">
          <div class="flex items-center justify-between mb-4">
            <span class="text-[10px] font-black uppercase tracking-widest text-slate-500">Current Shift Roster</span>
            <Button variant="ghost" size="sm" @click="loadAttendanceData" :disabled="attendanceLoading"
              class="h-7 text-slate-400 hover:text-slate-200 text-[10px] uppercase font-black tracking-wider">
              <RefreshCw class="h-3 w-3 mr-1.5" :class="attendanceLoading && 'animate-spin'" />
              Refresh
            </Button>
          </div>

          <div v-if="attendanceLoading" class="flex items-center justify-center py-12 text-slate-500">
            <RefreshCw class="h-5 w-5 animate-spin mr-2" /> Loading…
          </div>

          <div v-else class="space-y-2">
            <div v-for="name in allTechnicians" :key="name"
              class="bg-slate-950 border border-slate-800 rounded-xl">

              <!-- Row -->
              <div class="flex items-center justify-between px-4 py-3">
                <div class="flex items-center gap-3">
                  <div class="h-8 w-8 rounded-lg bg-slate-800 border border-slate-700 flex items-center justify-center text-xs font-black text-slate-300">
                    {{ name.split(' ').map((n: string) => n[0]).join('').slice(0, 2) }}
                  </div>
                  <div>
                    <p class="text-sm font-bold text-slate-200">{{ name }}</p>
                    <Badge variant="outline"
                      :class="statusColor[technicianStatus(name)]"
                      class="text-[9px] font-black uppercase tracking-widest mt-0.5">
                      {{ technicianStatus(name) }}
                    </Badge>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <!-- Teams OOO toggle (dev) -->
                  <button
                    @click="toggleTeamsOoo(name)"
                    :class="[
                      'flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[9px] font-black uppercase tracking-wider border transition-all',
                      oooToggles[name]
                        ? 'bg-amber-500/10 border-amber-500/30 text-amber-400'
                        : 'bg-slate-900 border-slate-700 text-slate-500 hover:text-slate-300'
                    ]"
                    title="Simulate Teams OOO (dev mode)"
                  >Teams OOO</button>

                  <!-- Resolve / Mark absent -->
                  <template v-if="technicianStatus(name) === 'Absent'">
                    <Button size="sm"
                      @click="resolveAbsence(absences.find(a => a.technicianName === name)!)"
                      class="h-7 bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 border border-emerald-500/30 rounded-lg text-[10px] font-black uppercase tracking-wider">
                      <Check class="h-3 w-3 mr-1" /> Resolve
                    </Button>
                  </template>
                  <template v-else>
                    <Button size="sm"
                      @click="absenceFormTarget === name ? closeAbsenceForm() : openAbsenceForm(name)"
                      class="h-7 bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 rounded-lg text-[10px] font-black uppercase tracking-wider">
                      Mark Absent
                      <component :is="absenceFormTarget === name ? ChevronUp : ChevronDown" class="h-3 w-3 ml-1" />
                    </Button>
                  </template>
                </div>
              </div>

              <!-- Absence sub-form -->
              <div v-if="absenceFormTarget === name"
                class="border-t border-slate-800 px-4 py-4 bg-slate-900/60 space-y-3 rounded-b-xl">
                <div v-if="absenceError" class="flex items-center gap-2 text-xs text-rose-400 bg-rose-950/30 border border-rose-900/50 rounded-lg px-3 py-2">
                  <AlertTriangle class="h-3.5 w-3.5 shrink-0" /> {{ absenceError }}
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Reason</label>
                    <Select v-model="absenceForm.reason">
                      <SelectTrigger class="h-8 bg-slate-950 border-slate-700 text-slate-200 text-xs rounded-lg">
                        <SelectValue placeholder="Select reason…" />
                      </SelectTrigger>
                      <SelectContent class="bg-slate-900 border-slate-700">
                        <SelectItem v-for="r in ABSENCE_REASONS" :key="r" :value="r" class="text-xs text-slate-200">{{ r }}</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Return Date</label>
                    <input v-model="absenceForm.endDate" type="date"
                      class="w-full h-8 bg-slate-950 border border-slate-700 rounded-lg px-2.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>
                <div>
                  <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Backup Technician (Query or Free-Text)</label>
                  <SearchableTargetCombobox
                    v-model="absenceForm.backupTechnician"
                    placeholder="Search technician or enter custom backup name..."
                    category-label="Shift Technicians & Engineers"
                    icon-type="user"
                    :query-fn="queryBackupTechnicians"
                  />
                </div>
                <div class="flex justify-end gap-2 pt-1">
                  <Button variant="ghost" size="sm" @click="closeAbsenceForm" class="h-7 text-slate-400 text-[10px] uppercase font-black">Cancel</Button>
                  <Button size="sm" @click="submitAbsence" :disabled="absenceSubmitting"
                    class="h-7 bg-rose-600 hover:bg-rose-700 text-white text-[10px] font-black uppercase tracking-wider rounded-lg">
                    {{ absenceSubmitting ? 'Submitting…' : 'Confirm Absence' }}
                  </Button>
                </div>
              </div>

              <!-- Active absence detail row -->
              <div v-else-if="technicianStatus(name) === 'Absent'"
                class="border-t border-slate-800 px-4 py-2 flex flex-wrap items-center gap-4 text-[11px] text-slate-400 bg-rose-950/10 rounded-b-xl">
                <span><span class="text-slate-500 font-bold">Reason:</span> {{ absences.find(a => a.technicianName === name)?.reason }}</span>
                <span><span class="text-slate-500 font-bold">Until:</span> {{ absences.find(a => a.technicianName === name)?.endDate }}</span>
                <span v-if="absences.find(a => a.technicianName === name)?.backupTechnician">
                  <span class="text-slate-500 font-bold">Backup:</span> {{ absences.find(a => a.technicianName === name)?.backupTechnician }}
                </span>
              </div>

            </div>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- TAB 2: Technician Dedication                                  -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <div v-else-if="activeTab === 'dedication'" class="p-6 space-y-4">
          <div class="flex items-center justify-between mb-4">
            <div>
              <span class="text-[10px] font-black uppercase tracking-widest text-slate-500 block">Dedication Rules</span>
              <p class="text-[11px] text-slate-400 mt-0.5">
                Governed scope assignment for stations, lines, and technology domains.
              </p>
            </div>
            <Button size="sm" @click="openAddRule"
              class="h-7 bg-indigo-600 hover:bg-indigo-700 text-white text-[10px] font-black uppercase tracking-wider rounded-lg">
              <Plus class="h-3 w-3 mr-1.5" /> Add Rule
            </Button>
          </div>

          <!-- Add rule form with Governance & Free-Text Resolution -->
          <div v-if="showAddRule" class="bg-slate-950 border border-indigo-500/30 rounded-2xl p-5 space-y-4 mb-4">
            <div class="flex items-center justify-between">
              <h4 class="text-xs font-black uppercase tracking-widest text-indigo-400 flex items-center gap-2">
                <span>New Dedication Rule</span>
                <Badge variant="outline" class="text-[8px] uppercase tracking-wider border-indigo-500/30 text-indigo-300">
                  {{ dedicationTier === 'self' ? 'Tier 1 (Self)' : dedicationTier === 'shift' ? 'Tier 2 (Shift)' : dedicationTier === 'group' ? 'Tier 3 (Group)' : 'Tier 4 (Manager)' }}
                </Badge>
              </h4>
            </div>

            <div v-if="ruleError" class="flex items-center gap-2 text-xs text-rose-400 bg-rose-950/40 border border-rose-900/60 rounded-xl px-3 py-2.5">
              <AlertTriangle class="h-4 w-4 shrink-0 text-rose-400" />
              <span>{{ ruleError }}</span>
            </div>

            <!-- Scope type -->
            <div>
              <div class="flex items-center justify-between mb-2">
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400">
                  Scope Type
                </label>
                <span v-if="dedicationTier === 'shift'" class="text-[9px] text-amber-400">
                  (Technology scope restricted to Group Leaders & Managers)
                </span>
              </div>

              <div class="flex gap-2">
                <button
                  type="button"
                  @click="ruleForm.scopeType = 'Technology'; ruleForm.target = ''"
                  :disabled="dedicationTier === 'shift'"
                  :class="[
                    'px-3 py-1.5 rounded-lg text-[10px] font-black uppercase tracking-wider border transition-all flex items-center gap-1.5',
                    dedicationTier === 'shift' ? 'opacity-40 cursor-not-allowed border-slate-800 bg-slate-950 text-slate-600' :
                    ruleForm.scopeType === 'Technology' ? scopeColor['Technology'] : 'border-slate-700 bg-slate-900 text-slate-500 hover:text-slate-300'
                  ]"
                >
                  <Layers class="w-3 h-3" />
                  <span>Technology</span>
                </button>

                <button
                  type="button"
                  @click="ruleForm.scopeType = 'Line/Group'; ruleForm.target = ''"
                  :class="[
                    'px-3 py-1.5 rounded-lg text-[10px] font-black uppercase tracking-wider border transition-all flex items-center gap-1.5',
                    ruleForm.scopeType === 'Line/Group' ? scopeColor['Line/Group'] : 'border-slate-700 bg-slate-900 text-slate-500 hover:text-slate-300'
                  ]"
                >
                  <GitBranch class="w-3 h-3" />
                  <span>Line / Group</span>
                </button>

                <button
                  type="button"
                  @click="ruleForm.scopeType = 'Machine'; ruleForm.target = ''"
                  :class="[
                    'px-3 py-1.5 rounded-lg text-[10px] font-black uppercase tracking-wider border transition-all flex items-center gap-1.5',
                    ruleForm.scopeType === 'Machine' ? scopeColor['Machine'] : 'border-slate-700 bg-slate-900 text-slate-500 hover:text-slate-300'
                  ]"
                >
                  <Cpu class="w-3 h-3" />
                  <span>Machine / Station</span>
                </button>
              </div>
            </div>

            <!-- Target with Free-text & Queried Resolution -->
            <div>
              <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
                Scope Target (Select Queried Target or Type Free-Text for Undefined Type)
              </label>
              <SearchableTargetCombobox
                v-model="ruleForm.target"
                :placeholder="ruleForm.scopeType === 'Technology' ? 'Search technology (e.g. Milling) or type undefined technology...' : ruleForm.scopeType === 'Line/Group' ? 'Search line/group or type undefined group...' : 'Search station ID or type undefined station...'"
                :category-label="ruleForm.scopeType === 'Technology' ? 'Technologies' : ruleForm.scopeType === 'Line/Group' ? 'Lines & Cell Groups' : 'Stations & Machines'"
                :icon-type="ruleForm.scopeType === 'Technology' ? 'technology' : ruleForm.scopeType === 'Line/Group' ? 'group' : 'machine'"
                :query-fn="queryScopeTargets"
              />
            </div>

            <!-- Technician Name Input (Locked to self if Tier 1, or queried combobox + free text if higher tiers) -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5 flex items-center justify-between">
                  <span>Assigned Person / Lead</span>
                  <span v-if="dedicationTier === 'self'" class="text-[9px] text-amber-400 font-bold uppercase">
                    Self-Assignment Only
                  </span>
                </label>
                <SearchableTargetCombobox
                  v-model="ruleForm.technicianName"
                  :disabled="dedicationTier === 'self'"
                  disabled-reason="Engineers & Technicians can only dedicate themselves."
                  placeholder="Search technician or enter free-text..."
                  category-label="Technicians & Engineers"
                  icon-type="user"
                  :query-fn="queryTechnicians"
                />
              </div>

              <div>
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
                  Technical Discipline / Category (Query or Free-Text)
                </label>
                <SearchableTargetCombobox
                  v-model="ruleForm.categoryFilter"
                  placeholder="e.g. Mechanical, Vision, Controls or free-text..."
                  category-label="Standard Disciplines"
                  icon-type="technology"
                  :options="categoryOptions"
                />
              </div>
            </div>

            <!-- Backup Technician & Contact -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
                  Backup Technician (Optional Free-Text or Query)
                </label>
                <SearchableTargetCombobox
                  v-model="ruleForm.backupTechnician"
                  placeholder="e.g. Gábor Varga or enter custom backup..."
                  category-label="Available Candidates"
                  icon-type="user"
                  :query-fn="queryBackupTechnicians"
                />
              </div>

              <div>
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
                  Technician Email / Contact (Optional)
                </label>
                <Input
                  v-model="ruleForm.technicianEmail"
                  placeholder="e.g. sally.milling@heimdall.dev"
                  type="email"
                  class="h-9 bg-slate-950 border-slate-800 text-xs rounded-xl"
                />
              </div>
            </div>

            <!-- Buttons -->
            <div class="flex justify-end gap-2 pt-2 border-t border-slate-800/80">
              <Button variant="ghost" size="sm" @click="showAddRule = false" class="h-8 text-slate-400 text-xs font-bold">
                Cancel
              </Button>
              <Button size="sm" @click="saveRule" :disabled="ruleSubmitting"
                class="h-8 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold uppercase tracking-wider rounded-xl px-5">
                {{ ruleSubmitting ? 'Saving…' : 'Save Rule' }}
              </Button>
            </div>
          </div>

          <!-- Rules table -->
          <div v-if="rulesLoading" class="flex items-center justify-center py-12 text-slate-500">
            <RefreshCw class="h-5 w-5 animate-spin mr-2" /> Loading rules…
          </div>
          <div v-else-if="rules.length === 0 && !showAddRule" class="flex flex-col items-center justify-center py-12 text-slate-500">
            <Users class="h-8 w-8 mb-2 opacity-30" />
            <p class="text-xs font-bold uppercase tracking-widest">No dedication rules configured</p>
            <p class="text-[11px] mt-1 text-slate-600">Click "Add Rule" to assign technicians to technologies or lines.</p>
          </div>
          <div v-else class="overflow-x-auto rounded-xl border border-slate-800">
            <table class="w-full text-xs">
              <thead>
                <tr class="bg-slate-950 border-b border-slate-800">
                  <th class="text-left text-[10px] font-black uppercase tracking-widest text-slate-500 px-4 py-2.5">Scope / Target</th>
                  <th class="text-left text-[10px] font-black uppercase tracking-widest text-slate-500 px-4 py-2.5">Technician / Lead</th>
                  <th class="text-left text-[10px] font-black uppercase tracking-widest text-slate-500 px-4 py-2.5">Category</th>
                  <th class="text-left text-[10px] font-black uppercase tracking-widest text-slate-500 px-4 py-2.5">Backup</th>
                  <th class="text-left text-[10px] font-black uppercase tracking-widest text-slate-500 px-4 py-2.5">Governance Tier</th>
                  <th class="px-4 py-2.5"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="rule in rules" :key="rule.id"
                  class="border-b border-slate-800/50 hover:bg-slate-950/60 transition-colors">
                  <td class="px-4 py-3">
                    <Badge variant="outline" :class="scopeColor[rule.scopeType] || 'border-slate-700 text-slate-300'"
                      class="text-[9px] font-black uppercase tracking-wider mr-1.5">
                      {{ rule.scopeType }}
                    </Badge>
                    <span class="text-slate-200 font-mono text-[11px] font-semibold">{{ rule.target || rule.targetId }}</span>
                  </td>
                  <td class="px-4 py-3">
                    <p class="text-slate-200 font-bold">{{ rule.technicianName }}</p>
                    <p v-if="rule.technicianEmail" class="text-slate-500 font-mono text-[10px]">{{ rule.technicianEmail }}</p>
                  </td>
                  <td class="px-4 py-3 text-slate-400">
                    <span v-if="rule.categoryFilter" class="px-2 py-0.5 rounded bg-slate-900 border border-slate-800 text-[10px] text-indigo-300">
                      {{ rule.categoryFilter }}
                    </span>
                    <span v-else class="text-slate-600">—</span>
                  </td>
                  <td class="px-4 py-3 text-slate-400">{{ rule.backupTechnician || rule.backupTechnicianName || '—' }}</td>
                  <td class="px-4 py-3">
                    <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider border-violet-500/30 text-violet-400 bg-violet-500/10">
                      {{ (rule.assignedByRole || rule.role || 'System').replace('_', ' ') }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <Button variant="ghost" size="icon" @click="deleteRule(rule)"
                      class="h-6 w-6 text-slate-600 hover:text-rose-400 hover:bg-rose-950/20 rounded-lg">
                      <Trash2 class="h-3.5 w-3.5" />
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- TAB 3: Machine Group Clusters                                 -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <div v-else-if="activeTab === 'clusters'" class="p-6 space-y-4">
          <div class="flex items-center justify-between mb-4">
            <div>
              <span class="text-[10px] font-black uppercase tracking-widest text-slate-500 block">Group Hierarchy & Clusters</span>
              <p class="text-[11px] text-slate-400 mt-0.5">Managers can cluster machine types and line cells recursively.</p>
            </div>
            <Button size="sm" @click="openCreateGroup"
              class="h-7 bg-cyan-600 hover:bg-cyan-700 text-white text-[10px] font-black uppercase tracking-wider rounded-lg">
              <Plus class="h-3 w-3 mr-1.5" /> Create Group
            </Button>
          </div>

          <!-- Create group form -->
          <div v-if="showCreateGroup" class="bg-slate-950 border border-cyan-500/30 rounded-xl p-5 space-y-4 mb-4">
            <h4 class="text-xs font-black uppercase tracking-widest text-cyan-400">New Machine Group</h4>
            <div v-if="groupError" class="flex items-center gap-2 text-xs text-rose-400 bg-rose-950/30 border border-rose-900/50 rounded-lg px-3 py-2">
              <AlertTriangle class="h-3.5 w-3.5 shrink-0" /> {{ groupError }}
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Group Name</label>
                <Input v-model="createForm.name" placeholder="e.g. SMT Line A"
                  class="h-9 bg-slate-900 border-slate-700 text-xs rounded-lg" />
              </div>
              <div>
                <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Parent Group (optional)</label>
                <Select v-model="createForm.parentGroupId">
                  <SelectTrigger class="h-9 bg-slate-900 border-slate-700 text-slate-200 text-xs rounded-lg">
                    <SelectValue placeholder="None (top-level)" />
                  </SelectTrigger>
                  <SelectContent class="bg-slate-900 border-slate-700 max-h-48 overflow-y-auto">
                    <SelectItem value="" class="text-xs text-slate-400">None (top-level)</SelectItem>
                    <SelectItem v-for="g in groups" :key="g.id" :value="g.id" class="text-xs text-slate-200">{{ g.name }}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Description (optional)</label>
              <Input v-model="createForm.description" placeholder="Short description of this group…"
                class="h-9 bg-slate-900 border-slate-700 text-xs rounded-lg" />
            </div>
            <div>
              <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-2">Machine Types</label>
              <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
                <button v-for="mt in MACHINE_TYPES" :key="mt" @click="toggleCreateMachineType(mt)"
                  :class="['flex items-center gap-2 px-3 py-1.5 rounded-lg border text-[10px] font-bold text-left transition-all',
                    createForm.machineTypes.includes(mt)
                      ? 'bg-cyan-500/10 border-cyan-500/30 text-cyan-400'
                      : 'bg-slate-900 border-slate-700 text-slate-500 hover:text-slate-300']">
                  <component :is="createForm.machineTypes.includes(mt) ? CheckSquare : Square" class="h-3.5 w-3.5 shrink-0" />
                  {{ mt }}
                </button>
              </div>
            </div>
            <div class="flex justify-end gap-2 pt-2">
              <Button variant="ghost" size="sm" @click="showCreateGroup = false" class="h-7 text-slate-400 text-[10px] uppercase font-black">Cancel</Button>
              <Button size="sm" @click="createGroup" :disabled="groupSubmitting"
                class="h-7 bg-cyan-600 hover:bg-cyan-700 text-white text-[10px] font-black uppercase tracking-wider rounded-lg">
                {{ groupSubmitting ? 'Creating…' : 'Create Group' }}
              </Button>
            </div>
          </div>

          <!-- Groups list -->
          <div v-if="groupsLoading" class="flex items-center justify-center py-12 text-slate-500">
            <RefreshCw class="h-5 w-5 animate-spin mr-2" /> Loading groups…
          </div>
          <div v-else-if="groups.length === 0 && !showCreateGroup" class="flex flex-col items-center justify-center py-12 text-slate-500">
            <GitBranch class="h-8 w-8 mb-2 opacity-30" />
            <p class="text-xs font-bold uppercase tracking-widest">No machine groups found</p>
          </div>
          <div v-else class="space-y-3">
            <div v-for="group in groups" :key="group.id" class="bg-slate-950 border border-slate-800 rounded-xl overflow-hidden">
              <!-- Group header -->
              <div class="flex items-center justify-between px-4 py-3">
                <div class="flex items-center gap-3">
                  <div class="p-2 rounded-lg bg-cyan-500/10 border border-cyan-500/20 text-cyan-400">
                    <GitBranch class="h-4 w-4" />
                  </div>
                  <div>
                    <p class="text-sm font-bold text-slate-200">{{ group.name }}</p>
                    <p v-if="group.description" class="text-[11px] text-slate-500 mt-0.5">{{ group.description }}</p>
                    <p v-if="group.parentGroupId" class="text-[10px] text-slate-600 mt-0.5">
                      Parent: {{ groups.find(g => g.id === group.parentGroupId)?.name ?? group.parentGroupId }}
                    </p>
                  </div>
                </div>
                <Button size="sm"
                  @click="editingGroupId === group.id ? closeEditGroup() : openEditGroup(group)"
                  class="h-7 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 text-[10px] font-black uppercase tracking-wider rounded-lg">
                  <Edit2 class="h-3 w-3 mr-1.5" /> Edit Cluster
                </Button>
              </div>

              <!-- Machine type badges -->
              <div class="px-4 pb-3 flex flex-wrap gap-1.5 items-center" v-if="group.machineTypes?.length">
                <Badge v-for="mt in group.machineTypes" :key="mt" variant="outline"
                  class="text-[9px] font-black uppercase tracking-wider border-slate-700 text-slate-400 bg-slate-900">
                  {{ mt }}
                </Badge>
                <span v-if="group.leadEngineer" class="ml-auto text-[10px] text-slate-500">
                  Lead: <span class="text-slate-300 font-bold">{{ group.leadEngineer }}</span>
                </span>
              </div>
              <div v-else class="px-4 pb-3 text-[11px] text-slate-600 italic">No machine types assigned</div>

              <!-- Edit cluster inline form -->
              <div v-if="editingGroupId === group.id"
                class="border-t border-slate-800 px-4 py-4 bg-slate-900/60 space-y-4">
                <div v-if="groupError" class="flex items-center gap-2 text-xs text-rose-400 bg-rose-950/30 border border-rose-900/50 rounded-lg px-3 py-2">
                  <AlertTriangle class="h-3.5 w-3.5 shrink-0" /> {{ groupError }}
                </div>
                <div>
                  <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-2">Machine Types</label>
                  <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
                    <button v-for="mt in MACHINE_TYPES" :key="mt" @click="toggleEditMachineType(mt)"
                      :class="['flex items-center gap-2 px-3 py-1.5 rounded-lg border text-[10px] font-bold text-left transition-all',
                        editForm.machineTypes.includes(mt)
                          ? 'bg-cyan-500/10 border-cyan-500/30 text-cyan-400'
                          : 'bg-slate-950 border-slate-700 text-slate-500 hover:text-slate-300']">
                      <component :is="editForm.machineTypes.includes(mt) ? CheckSquare : Square" class="h-3.5 w-3.5 shrink-0" />
                      {{ mt }}
                    </button>
                  </div>
                </div>
                <div>
                  <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1">Lead Engineer (Query or Free-Text)</label>
                  <SearchableTargetCombobox
                    v-model="editForm.leadEngineer"
                    placeholder="Search candidate engineer or enter custom name..."
                    category-label="Engineers & Leads"
                    icon-type="user"
                    :query-fn="queryBackupTechnicians"
                  />
                </div>
                <div class="flex justify-end gap-2 pt-1">
                  <Button variant="ghost" size="sm" @click="closeEditGroup" class="h-7 text-slate-400 text-[10px] uppercase font-black">Cancel</Button>
                  <Button size="sm" @click="saveGroupCluster(group.id)" :disabled="groupSubmitting"
                    class="h-7 bg-cyan-600 hover:bg-cyan-700 text-white text-[10px] font-black uppercase tracking-wider rounded-lg">
                    {{ groupSubmitting ? 'Saving…' : 'Save Cluster' }}
                  </Button>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>
    </DialogContent>
  </Dialog>
</template>
