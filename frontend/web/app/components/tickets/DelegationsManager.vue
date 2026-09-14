<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue'
import {
  Clock, UserCheck, GitBranch, X, Plus, Trash2, Check,
  ChevronDown, ChevronUp, AlertTriangle, RefreshCw, Shield,
  Users, Edit2, CheckSquare, Square, Lock, Sparkles, User, Layers, Cpu,
  Search, CheckCircle2, Calendar, PhoneCall, ArrowRight
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '~/components/ui/select'
import SearchableTargetCombobox, { type TargetItem } from '~/components/common/SearchableTargetCombobox.vue'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useAuthSession, DEMO_PERSONAS, type DemoPersona } from '~/composables/useAuthSession'

const props = withDefaults(defineProps<{
  isModal?: boolean
}>(), {
  isModal: false
})

const emit = defineEmits<{
  (e: 'close'): void
}>()

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

const tabs: { id: TabId; label: string; icon: any; count?: () => number }[] = [
  { id: 'attendance',  label: 'Shift Attendance',       icon: Clock, count: () => allTechnicians.value.length },
  { id: 'dedication',  label: 'Technician Dedication',  icon: UserCheck, count: () => rules.value.length },
  { id: 'clusters',    label: 'Machine Group Clusters', icon: GitBranch, count: () => groups.value.length },
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
const techSearch = ref('')

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
      const names = candData.map(c => c.name).filter(Boolean)
      if (names.length > 0) {
        knownTechnicians.value = [...new Set(names)]
      }
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

const filteredTechnicians = computed(() => {
  if (!techSearch.value.trim()) return allTechnicians.value
  const q = techSearch.value.toLowerCase().trim()
  return allTechnicians.value.filter(n => n.toLowerCase().includes(q))
})

const availableCount = computed(() => {
  return allTechnicians.value.filter(n => technicianStatus(n) === 'Available').length
})

const absentCount = computed(() => {
  return allTechnicians.value.filter(n => technicianStatus(n) === 'Absent').length
})

const oooCount = computed(() => {
  return allTechnicians.value.filter(n => technicianStatus(n) === 'Teams OOO').length
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
const ruleScopeFilter = ref<string>('all')

const ruleForm = reactive({
  scopeType: 'Technology' as 'Technology' | 'Line/Group' | 'Machine',
  target: '',
  categoryFilter: '',
  technicianName: '',
  technicianEmail: '',
  backupTechnician: '',
  role: 'Group Leader' as 'Shift Leader' | 'Group Leader' | 'Manager' | 'Engineer' | 'Technician',
})

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

const filteredRules = computed(() => {
  if (ruleScopeFilter.value === 'all') return rules.value
  return rules.value.filter(r => r.scopeType.toLowerCase() === ruleScopeFilter.value.toLowerCase())
})

// ─────────────────────────────────────────────────────────────────────────────
// TAB 3 — Machine Group Clusters
// ─────────────────────────────────────────────────────────────────────────────
interface MachineGroupItem {
  id: string
  name: string
  description?: string
  parentGroupId?: string
  machineTypes: string[]
  leadEngineer?: string
}

const groups = ref<MachineGroupItem[]>([])
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

function openEditGroup(group: MachineGroupItem) {
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
    const data = await $fetch<MachineGroupItem[]>('/api/machine-groups').catch(() => [])
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
  <div class="space-y-4">
    <!-- Top Hero & Stats (Page Mode) -->
    <div v-if="!isModal" class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-violet-500/10 text-violet-400 border border-violet-500/20">
          <Users class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Total Technicians</div>
          <div class="text-lg font-black text-white">{{ allTechnicians.length }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
          <CheckCircle2 class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Available On Duty</div>
          <div class="text-lg font-black text-emerald-400">{{ availableCount }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20">
          <Clock class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Absences Marked</div>
          <div class="text-lg font-black text-rose-400">{{ absentCount }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-amber-500/10 text-amber-400 border border-amber-500/20">
          <UserCheck class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-slate-400">Teams OOO Status</div>
          <div class="text-lg font-black text-amber-400">{{ oooCount }}</div>
        </div>
      </div>
    </div>

    <!-- Main Card Container -->
    <div class="bg-slate-900/90 border border-slate-800 rounded-3xl shadow-xl overflow-hidden flex flex-col">
      <!-- Card Header -->
      <div class="p-6 border-b border-slate-800 bg-slate-900/95 backdrop-blur-md">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-2xl bg-violet-600/10 text-violet-400 border border-violet-500/20">
              <Shield class="h-6 w-6" />
            </div>
            <div>
              <h3 class="text-lg font-black uppercase tracking-tight text-slate-100 flex items-center gap-2">
                <span>Preferred Technicians & Governance</span>
                <Badge variant="outline" class="text-[9px] uppercase tracking-wider font-bold border-indigo-500/40 text-indigo-300 bg-indigo-500/10">
                  Multi-Tier Role Access
                </Badge>
              </h3>
              <p class="text-[10px] font-bold text-slate-500 uppercase tracking-widest mt-0.5">
                Shift Attendance · Dedication Rules · Machine Group Clusters
              </p>
            </div>
          </div>
          <Button v-if="isModal" variant="ghost" size="icon" @click="emit('close')" class="text-slate-400 hover:text-white rounded-xl">
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
            <span>{{ tab.label }}</span>
            <span v-if="tab.count" class="ml-1 text-[9px] font-mono px-1.5 py-0.2 bg-slate-900 rounded-full text-slate-400">
              {{ tab.count() }}
            </span>
          </button>
        </div>
      </div>

      <!-- Tab body -->
      <div class="p-6 space-y-4">
        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- TAB 1: Shift Attendance                                       -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <div v-if="activeTab === 'attendance'" class="space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div class="relative w-full sm:w-72">
              <Search class="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
              <Input
                v-model="techSearch"
                placeholder="Search technicians..."
                class="pl-8 h-8 bg-slate-950 border-slate-800 text-xs rounded-xl"
              />
            </div>

            <div class="flex items-center gap-2">
              <Button variant="ghost" size="sm" @click="loadAttendanceData" :disabled="attendanceLoading"
                class="h-8 text-slate-400 hover:text-slate-200 text-xs font-bold rounded-xl border border-slate-800">
                <RefreshCw class="h-3.5 w-3.5 mr-1.5" :class="attendanceLoading && 'animate-spin'" />
                Refresh Attendance
              </Button>
            </div>
          </div>

          <!-- Technicians Grid / Table -->
          <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-950/60">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950/80 border-b border-slate-800 text-slate-400 uppercase text-[10px] font-black tracking-wider">
                <tr>
                  <th class="py-3 px-4">Technician</th>
                  <th class="py-3 px-4">Status</th>
                  <th class="py-3 px-4">Absence Notes</th>
                  <th class="py-3 px-4">Backup Support</th>
                  <th class="py-3 px-4 text-center">Teams Sync</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                <tr v-for="tech in filteredTechnicians" :key="tech" class="hover:bg-slate-800/30 transition-colors">
                  <td class="py-3 px-4 font-bold text-slate-200 flex items-center gap-2">
                    <div class="w-7 h-7 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-slate-300 font-bold text-[10px]">
                      {{ tech.split(' ').map(n => n[0]).join('').slice(0, 2) }}
                    </div>
                    <span>{{ tech }}</span>
                  </td>
                  <td class="py-3 px-4">
                    <Badge variant="outline" class="text-[10px] font-bold" :class="statusColor[technicianStatus(tech)]">
                      {{ technicianStatus(tech) }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 text-slate-400">
                    <span v-if="absences.find(a => a.technicianName === tech)">
                      {{ absences.find(a => a.technicianName === tech)?.reason }} (until {{ absences.find(a => a.technicianName === tech)?.endDate }})
                    </span>
                    <span v-else class="text-slate-600 font-mono text-[11px]">—</span>
                  </td>
                  <td class="py-3 px-4 text-slate-400">
                    <span v-if="absences.find(a => a.technicianName === tech)?.backupTechnician" class="text-indigo-400 font-semibold">
                      {{ absences.find(a => a.technicianName === tech)?.backupTechnician }}
                    </span>
                    <span v-else class="text-slate-600 font-mono text-[11px]">—</span>
                  </td>
                  <td class="py-3 px-4 text-center">
                    <button
                      type="button"
                      class="px-2 py-0.5 rounded text-[10px] font-bold border transition-colors"
                      :class="oooToggles[tech] ? 'bg-amber-500/20 text-amber-300 border-amber-500/40' : 'bg-slate-900 text-slate-500 border-slate-800 hover:text-slate-300'"
                      @click="toggleTeamsOoo(tech)"
                    >
                      {{ oooToggles[tech] ? 'Teams OOO' : 'In Office' }}
                    </button>
                  </td>
                  <td class="py-3 px-4 text-right">
                    <div class="flex items-center justify-end gap-1.5">
                      <Button
                        v-if="technicianStatus(tech) === 'Absent'"
                        size="sm"
                        variant="ghost"
                        class="h-7 text-xs text-emerald-400 hover:text-emerald-300 hover:bg-emerald-950/30 font-bold"
                        @click="resolveAbsence(absences.find(a => a.technicianName === tech)!)"
                      >
                        Return to Work
                      </Button>
                      <Button
                        v-else
                        size="sm"
                        variant="outline"
                        class="h-7 text-xs border-slate-800 text-slate-300 hover:bg-slate-800 rounded-lg"
                        @click="openAbsenceForm(tech)"
                      >
                        Mark Absent
                      </Button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Inline Absence Form Modal / Dialog -->
          <div v-if="absenceFormTarget" class="p-4 rounded-2xl bg-slate-950 border border-slate-800 space-y-3 mt-3">
            <div class="flex items-center justify-between pb-2 border-b border-slate-800">
              <div class="text-xs font-black uppercase tracking-wider text-slate-300">
                Mark Absence: <span class="text-indigo-400">{{ absenceFormTarget }}</span>
              </div>
              <Button variant="ghost" size="sm" class="h-6 w-6 p-0 text-slate-400" @click="closeAbsenceForm">
                <X class="w-4 h-4" />
              </Button>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Reason</label>
                <select
                  v-model="absenceForm.reason"
                  class="w-full h-8 rounded-lg bg-slate-900 border border-slate-800 text-slate-200 text-xs px-2"
                >
                  <option value="" disabled>Select reason</option>
                  <option v-for="r in ABSENCE_REASONS" :key="r" :value="r">{{ r }}</option>
                </select>
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">End Date</label>
                <Input
                  v-model="absenceForm.endDate"
                  type="date"
                  class="h-8 bg-slate-900 border-slate-800 text-xs rounded-lg"
                />
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Designated Backup</label>
                <Input
                  v-model="absenceForm.backupTechnician"
                  placeholder="e.g. Engineer Orwell"
                  class="h-8 bg-slate-900 border-slate-800 text-xs rounded-lg"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-2">
              <Button size="sm" variant="ghost" class="h-7 text-xs text-slate-400" @click="closeAbsenceForm">Cancel</Button>
              <Button size="sm" class="h-7 text-xs bg-rose-600 hover:bg-rose-500 font-bold" :disabled="absenceSubmitting" @click="submitAbsence">
                Confirm Absence
              </Button>
            </div>
          </div>
        </div>

        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- TAB 2: Technician Dedication                                  -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <div v-else-if="activeTab === 'dedication'" class="space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div class="flex items-center gap-2">
              <Button
                v-for="sc in ['all', 'technology', 'group', 'machine']"
                :key="sc"
                size="sm"
                variant="ghost"
                class="h-8 text-xs font-bold capitalize rounded-xl"
                :class="ruleScopeFilter === sc ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:bg-slate-800'"
                @click="ruleScopeFilter = sc"
              >
                {{ sc === 'all' ? 'All Scopes' : sc }}
              </Button>
            </div>

            <Button size="sm" class="h-8 bg-indigo-600 hover:bg-indigo-500 text-white font-bold text-xs gap-1.5 rounded-xl" @click="openAddRule">
              <Plus class="w-3.5 h-3.5" />
              Add Dedication Rule
            </Button>
          </div>

          <!-- Add Rule Form -->
          <div v-if="showAddRule" class="p-5 rounded-2xl bg-slate-950 border border-indigo-500/30 space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="text-xs font-black uppercase tracking-wider text-indigo-400 flex items-center gap-2">
                <UserCheck class="w-4 h-4" />
                <span>Add Dedicated Technician Rule</span>
              </div>
              <Button variant="ghost" size="sm" class="h-6 w-6 p-0 text-slate-400" @click="showAddRule = false">
                <X class="w-4 h-4" />
              </Button>
            </div>

            <div v-if="ruleError" class="p-3 bg-rose-950/40 border border-rose-800/60 rounded-xl text-rose-400 text-xs flex items-center gap-2">
              <AlertTriangle class="w-4 h-4" />
              <span>{{ ruleError }}</span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Scope</label>
                <select
                  v-model="ruleForm.scopeType"
                  class="w-full h-9 rounded-xl bg-slate-900 border border-slate-800 text-slate-200 text-xs px-3"
                >
                  <option value="Technology">Technology Cluster</option>
                  <option value="Line/Group">Line / Envelope Group</option>
                  <option value="Machine">Specific Machine</option>
                </select>
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Target</label>
                <SearchableTargetCombobox
                  v-model="ruleForm.target"
                  placeholder="Select target..."
                  category-label="Target"
                  :query-fn="queryScopeTargets"
                />
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Technician</label>
                <SearchableTargetCombobox
                  v-model="ruleForm.technicianName"
                  placeholder="Select technician..."
                  category-label="Technicians"
                  :query-fn="queryTechnicians"
                />
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Backup Support</label>
                <SearchableTargetCombobox
                  v-model="ruleForm.backupTechnician"
                  placeholder="Select backup..."
                  category-label="Backup"
                  :query-fn="queryBackupTechnicians"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-2">
              <Button size="sm" variant="ghost" class="text-xs text-slate-400" @click="showAddRule = false">Cancel</Button>
              <Button size="sm" class="text-xs bg-indigo-600 hover:bg-indigo-500 font-bold" :disabled="ruleSubmitting" @click="saveRule">
                Save Rule
              </Button>
            </div>
          </div>

          <!-- Rules Table -->
          <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-950/60">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950/80 border-b border-slate-800 text-slate-400 uppercase text-[10px] font-black tracking-wider">
                <tr>
                  <th class="py-3 px-4">Scope</th>
                  <th class="py-3 px-4">Target</th>
                  <th class="py-3 px-4">Dedicated Tech</th>
                  <th class="py-3 px-4">Backup Tech</th>
                  <th class="py-3 px-4">Assigned Authority</th>
                  <th class="py-3 px-4 text-right">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                <tr v-if="filteredRules.length === 0">
                  <td colspan="6" class="py-10 text-center text-slate-500 text-xs">
                    No dedication rules found for this scope.
                  </td>
                </tr>
                <tr v-for="rule in filteredRules" :key="rule.id" class="hover:bg-slate-800/30 transition-colors">
                  <td class="py-3 px-4">
                    <Badge variant="outline" class="text-[10px] font-bold" :class="scopeColor[rule.scopeType]">
                      {{ rule.scopeType }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 font-bold text-slate-200">
                    {{ rule.target }}
                  </td>
                  <td class="py-3 px-4 text-indigo-400 font-semibold">
                    {{ rule.technicianName }}
                  </td>
                  <td class="py-3 px-4 text-slate-400">
                    {{ rule.backupTechnician || rule.backupTechnicianName || '—' }}
                  </td>
                  <td class="py-3 px-4">
                    <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider border-violet-500/30 text-violet-400 bg-violet-500/10">
                      {{ (rule.assignedByRole || rule.role || 'System').replace('_', ' ') }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 text-right">
                    <Button variant="ghost" size="icon" @click="deleteRule(rule)" class="h-7 w-7 text-slate-500 hover:text-rose-400 rounded-lg">
                      <Trash2 class="w-3.5 h-3.5" />
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
        <div v-else-if="activeTab === 'clusters'" class="space-y-4">
          <div class="flex items-center justify-between pb-2 border-b border-slate-800">
            <div>
              <div class="text-xs font-black uppercase tracking-wider text-slate-200">Factory Envelope Clusters</div>
              <p class="text-xs text-slate-400">Cluster machine types and assign dedicated engineering leadership.</p>
            </div>
            <Button size="sm" class="h-8 bg-cyan-600 hover:bg-cyan-500 text-white font-bold text-xs gap-1.5 rounded-xl" @click="openCreateGroup">
              <Plus class="w-3.5 h-3.5" />
              Create Group
            </Button>
          </div>

          <!-- Create Group Form -->
          <div v-if="showCreateGroup" class="p-5 rounded-2xl bg-slate-950 border border-cyan-500/30 space-y-4">
            <h4 class="text-xs font-black uppercase tracking-wider text-cyan-400">Create Machine Group Cluster</h4>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Name</label>
                <Input v-model="createForm.name" placeholder="e.g. SMT Line 01" class="h-9 bg-slate-900 border-slate-800 text-xs rounded-xl" />
              </div>
              <div>
                <label class="text-[10px] font-black uppercase text-slate-400 block mb-1">Parent</label>
                <select v-model="createForm.parentGroupId" class="w-full h-9 rounded-xl bg-slate-900 border border-slate-800 text-slate-200 text-xs px-3">
                  <option value="">None (Top-Level Plant)</option>
                  <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
                </select>
              </div>
            </div>

            <div>
              <label class="text-[10px] font-black uppercase text-slate-400 block mb-2">Machine Types</label>
              <div class="grid grid-cols-2 sm:grid-cols-4 gap-2">
                <button
                  v-for="mt in MACHINE_TYPES"
                  :key="mt"
                  type="button"
                  @click="toggleCreateMachineType(mt)"
                  class="p-2 rounded-lg text-left text-xs border transition-colors flex items-center gap-1.5"
                  :class="createForm.machineTypes.includes(mt) ? 'bg-cyan-500/20 text-cyan-300 border-cyan-500/40 font-bold' : 'bg-slate-900 text-slate-400 border-slate-800'"
                >
                  <Check v-if="createForm.machineTypes.includes(mt)" class="w-3.5 h-3.5" />
                  <span class="truncate">{{ mt }}</span>
                </button>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-2">
              <Button size="sm" variant="ghost" class="text-xs text-slate-400" @click="showCreateGroup = false">Cancel</Button>
              <Button size="sm" class="text-xs bg-cyan-600 hover:bg-cyan-500 font-bold" :disabled="groupSubmitting" @click="createGroup">
                Save Cluster
              </Button>
            </div>
          </div>

          <!-- Groups Grid -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div v-for="group in groups" :key="group.id" class="p-4 rounded-2xl bg-slate-950 border border-slate-800 space-y-3">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2.5">
                  <GitBranch class="w-4 h-4 text-cyan-400" />
                  <span class="font-bold text-sm text-slate-100">{{ group.name }}</span>
                </div>
                <Button size="sm" variant="outline" class="h-7 text-xs border-slate-800 text-slate-300" @click="openEditGroup(group)">
                  Edit
                </Button>
              </div>

              <div class="flex flex-wrap gap-1.5">
                <span v-for="mt in group.machineTypes" :key="mt" class="text-[9px] font-mono px-2 py-0.5 rounded bg-slate-900 border border-slate-800 text-slate-300">
                  {{ mt }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
