<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue'
import {
  Clock, UserCheck, GitBranch, X, Plus, Trash2, Check,
  ChevronDown, ChevronUp, AlertTriangle, RefreshCw, Shield,
  Users, Edit2, CheckSquare, Square, Lock, Sparkles, User, Layers, Cpu,
  Search, CheckCircle2, Calendar as CalendarIcon, PhoneCall, ArrowRight
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '~/components/ui/select'
import { Calendar as CalendarWidget } from '~/components/ui/calendar'
import DatePicker from '~/components/base/DatePicker.vue'
import { today, getLocalTimeZone, type DateValue } from '@internationalized/date'
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
  clearSimulatedPersona,
  isPersonaSimulationAllowed
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

function formatDisplayDate(dateStr?: string): string {
  if (!dateStr) return '—'
  try {
    const parts = dateStr.split('-')
    if (parts.length === 3) {
      const d = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]))
      return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
    }
  } catch {}
  return dateStr
}

const attendanceViewMode = ref<'table' | 'calendar'>('table')
const selectedCalendarDate = ref<DateValue>(today(getLocalTimeZone()))

const selectedDateString = computed(() => {
  return selectedCalendarDate.value ? selectedCalendarDate.value.toString() : ''
})

const techniciansOnSelectedDate = computed(() => {
  const dateStr = selectedDateString.value
  return allTechnicians.value.map(tech => {
    const abs = absences.value.find(a => a.technicianName === tech)
    const isAbsent = abs && (!abs.endDate || abs.endDate >= dateStr)
    const isOoo = !!oooToggles[tech]
    const status: 'Available' | 'Absent' | 'Teams OOO' = isAbsent ? 'Absent' : (isOoo ? 'Teams OOO' : 'Available')
    return {
      name: tech,
      status,
      absence: abs
    }
  })
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
  parentId?: string | null
  machineTypes: string[]
  leadEngineer?: string
  leadEngineerName?: string
}

const groups = ref<MachineGroupItem[]>([])
const groupsLoading = ref(false)
const editingGroupId = ref<string | null>(null)
const showCreateGroup = ref(false)
const groupSubmitting = ref(false)
const groupError = ref<string | null>(null)

const editForm = reactive<{
  name: string
  description: string
  parentGroupId: string
  machineTypes: string[]
  leadEngineer: string
}>({
  name: '',
  description: '',
  parentGroupId: '',
  machineTypes: [],
  leadEngineer: ''
})

const createForm = reactive<{
  name: string; description: string; parentGroupId: string; machineTypes: string[]
}>({ name: '', description: '', parentGroupId: '', machineTypes: [] })

function openEditGroup(group: MachineGroupItem) {
  if (editingGroupId.value === group.id) {
    closeEditGroup()
    return
  }
  showCreateGroup.value = false
  editingGroupId.value = group.id
  editForm.name = group.name || ''
  editForm.description = group.description || ''
  editForm.parentGroupId = group.parentGroupId || group.parentId || ''
  editForm.machineTypes = [...(group.machineTypes ?? [])]
  editForm.leadEngineer = group.leadEngineer || group.leadEngineerName || ''
  groupError.value = null
}

function closeEditGroup() {
  editingGroupId.value = null
  groupError.value = null
}

function toggleEditMachineType(mt: string) {
  const idx = editForm.machineTypes.indexOf(mt)
  idx >= 0 ? editForm.machineTypes.splice(idx, 1) : editForm.machineTypes.push(mt)
}

function toggleCreateMachineType(mt: string) {
  const idx = createForm.machineTypes.indexOf(mt)
  idx >= 0 ? createForm.machineTypes.splice(idx, 1) : createForm.machineTypes.push(mt)
}

async function saveGroupCluster(groupId: string) {
  if (!editForm.name.trim()) {
    groupError.value = 'Cluster name is required.'
    return
  }
  groupSubmitting.value = true
  groupError.value = null
  try {
    await $fetch(`/api/machine-groups/${groupId}`, {
      method: 'PATCH',
      body: {
        name: editForm.name.trim(),
        description: editForm.description || undefined,
        parentId: editForm.parentGroupId || null,
        parentGroupId: editForm.parentGroupId || undefined,
        machineTypes: editForm.machineTypes,
        leadEngineer: editForm.leadEngineer || undefined,
        leadEngineerName: editForm.leadEngineer || undefined
      },
    })
    closeEditGroup()
    await loadGroups()
  } catch (err: any) {
    groupError.value = err?.data?.message || 'Failed to save cluster.'
  } finally {
    groupSubmitting.value = false
  }
}

async function deleteGroupCluster(groupId: string) {
  if (!confirm('Are you sure you want to delete this machine group cluster?')) return
  groupSubmitting.value = true
  groupError.value = null
  try {
    await $fetch(`/api/machine-groups/${groupId}`, {
      method: 'DELETE',
    })
    closeEditGroup()
    await loadGroups()
  } catch (err: any) {
    groupError.value = err?.data?.message || 'Failed to delete cluster.'
  } finally {
    groupSubmitting.value = false
  }
}

function openCreateGroup() {
  Object.assign(createForm, { name: '', description: '', parentGroupId: '', machineTypes: [] })
  editingGroupId.value = null
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
  'Available': 'border-emerald-500/30 text-emerald-700 dark:text-emerald-400 bg-emerald-500/10',
  'Absent':    'border-rose-500/30 text-rose-700 dark:text-rose-400 bg-rose-500/10',
  'Teams OOO': 'border-amber-500/30 text-amber-800 dark:text-amber-400 bg-amber-500/10',
}

const scopeColor: Record<string, string> = {
  'Technology': 'text-indigo-700 dark:text-indigo-400 bg-indigo-500/10 border-indigo-500/30',
  'technology': 'text-indigo-700 dark:text-indigo-400 bg-indigo-500/10 border-indigo-500/30',
  'Line/Group': 'text-cyan-700 dark:text-cyan-400 bg-cyan-500/10 border-cyan-500/30',
  'group':      'text-cyan-700 dark:text-cyan-400 bg-cyan-500/10 border-cyan-500/30',
  'Machine':    'text-violet-700 dark:text-violet-400 bg-violet-500/10 border-violet-500/30',
  'machine':    'text-violet-700 dark:text-violet-400 bg-violet-500/10 border-violet-500/30',
}
</script>

<template>
  <div class="space-y-4">
    <!-- Top Hero & Stats (Page Mode) -->
    <div v-if="!isModal" class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <div class="p-3.5 rounded-2xl bg-card border border-border flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-violet-500/10 text-violet-600 dark:text-violet-400 border border-violet-500/20">
          <Users class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-muted-foreground">Total Technicians</div>
          <div class="text-lg font-black text-foreground">{{ allTechnicians.length }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-card border border-border flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20">
          <CheckCircle2 class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-muted-foreground">Available On Duty</div>
          <div class="text-lg font-black text-emerald-600 dark:text-emerald-400">{{ availableCount }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-card border border-border flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/20">
          <Clock class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-muted-foreground">Absences Marked</div>
          <div class="text-lg font-black text-rose-600 dark:text-rose-400">{{ absentCount }}</div>
        </div>
      </div>

      <div class="p-3.5 rounded-2xl bg-card border border-border flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20">
          <UserCheck class="h-5 w-5" />
        </div>
        <div>
          <div class="text-[10px] font-black uppercase tracking-wider text-muted-foreground">Teams OOO Status</div>
          <div class="text-lg font-black text-amber-600 dark:text-amber-400">{{ oooCount }}</div>
        </div>
      </div>
    </div>

    <!-- Main Card Container -->
    <div class="bg-card border border-border rounded-3xl shadow-xl overflow-hidden flex flex-col text-foreground">
      <!-- Card Header -->
      <div class="p-6 border-b border-border bg-card/95 backdrop-blur-md">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-2xl bg-violet-600/10 text-violet-600 dark:text-violet-400 border border-violet-500/20">
              <Shield class="h-6 w-6" />
            </div>
            <div>
              <h3 class="text-lg font-black uppercase tracking-tight text-foreground flex items-center gap-2">
                <span>Preferred Technicians & Governance</span>
                <Badge variant="outline" class="text-[9px] uppercase tracking-wider font-bold border-indigo-500/40 text-indigo-700 dark:text-indigo-300 bg-indigo-500/10">
                  Multi-Tier Role Access
                </Badge>
              </h3>
              <p class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest mt-0.5">
                Shift Attendance · Dedication Rules · Machine Group Clusters
              </p>
            </div>
          </div>
          <Button v-if="isModal" variant="ghost" size="icon" @click="emit('close')" class="text-muted-foreground hover:text-foreground rounded-xl">
            <X class="h-5 w-5" />
          </Button>
        </div>

        <!-- Role Simulator Bar (Allows switching test role live to test governance) -->
        <div v-if="isPersonaSimulationAllowed" class="mt-4 p-2.5 bg-muted/40 rounded-2xl border border-border flex flex-wrap items-center justify-between gap-2">
          <div class="flex items-center gap-2">
            <span class="text-[10px] font-black uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
              <Sparkles class="w-3.5 h-3.5 text-amber-500" />
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
                    : 'bg-card border border-border text-muted-foreground hover:text-foreground hover:bg-muted'
                ]"
              >
                <span>{{ p.name.replace(' (Plant Manager)', '') }}</span>
                <span class="text-[8px] opacity-70">({{ p.role.replace('_', ' ') }})</span>
              </button>
            </div>
          </div>

          <!-- Active Tier Badge -->
          <div class="flex items-center gap-1.5">
            <span class="text-[9px] font-mono text-muted-foreground">Tier:</span>
            <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider"
              :class="[
                dedicationTier === 'self' ? 'border-amber-500/40 text-amber-800 dark:text-amber-300 bg-amber-500/10' :
                dedicationTier === 'shift' ? 'border-cyan-500/40 text-cyan-800 dark:text-cyan-300 bg-cyan-500/10' :
                dedicationTier === 'group' ? 'border-violet-500/40 text-violet-800 dark:text-violet-300 bg-violet-500/10' :
                'border-emerald-500/40 text-emerald-800 dark:text-emerald-300 bg-emerald-500/10'
              ]"
            >
              {{ dedicationTier === 'self' ? 'Self-Dedication Only' : dedicationTier === 'shift' ? 'Shift Leader Authority' : dedicationTier === 'group' ? 'Group Leader Authority' : 'Manager / Full Governance' }}
            </Badge>
          </div>
        </div>

        <!-- Tab row -->
        <div class="flex gap-1 mt-4 p-1 bg-muted/50 rounded-xl border border-border w-fit">
          <button
            v-for="tab in tabs"
            :key="tab.id"
            @click="activeTab = tab.id"
            :class="[
              'flex items-center gap-2 px-4 py-2 rounded-lg text-[11px] font-black uppercase tracking-wider transition-all',
              activeTab === tab.id
                ? 'bg-card text-foreground shadow border border-border'
                : 'text-muted-foreground hover:text-foreground hover:bg-muted/60'
            ]"
          >
            <component :is="tab.icon" class="h-3.5 w-3.5" />
            <span>{{ tab.label }}</span>
            <span v-if="tab.count" class="ml-1 text-[9px] font-mono px-1.5 py-0.2 bg-muted rounded-full text-muted-foreground">
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
              <Search class="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input
                v-model="techSearch"
                placeholder="Search technicians..."
                class="pl-8 h-8 bg-background border-border text-foreground text-xs rounded-xl"
              />
            </div>

            <div class="flex items-center gap-2">
              <div class="flex items-center p-0.5 bg-muted/60 rounded-xl border border-border">
                <button
                  type="button"
                  @click="attendanceViewMode = 'table'"
                  class="px-3 py-1 rounded-lg text-xs font-bold transition-all"
                  :class="attendanceViewMode === 'table' ? 'bg-card text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                >
                  Table
                </button>
                <button
                  type="button"
                  @click="attendanceViewMode = 'calendar'"
                  class="px-3 py-1 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5"
                  :class="attendanceViewMode === 'calendar' ? 'bg-card text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                >
                  <CalendarIcon class="w-3.5 h-3.5" />
                  Calendar
                </button>
              </div>

              <Button variant="ghost" size="sm" @click="loadAttendanceData" :disabled="attendanceLoading"
                class="h-8 text-muted-foreground hover:text-foreground text-xs font-bold rounded-xl border border-border bg-background">
                <RefreshCw class="h-3.5 w-3.5 mr-1.5" :class="attendanceLoading && 'animate-spin'" />
                Refresh Attendance
              </Button>
            </div>
          </div>

          <!-- Calendar View -->
          <div v-if="attendanceViewMode === 'calendar'" class="grid grid-cols-1 lg:grid-cols-12 gap-4">
            <div class="lg:col-span-5 p-4 rounded-2xl bg-card border border-border flex flex-col items-center justify-center">
              <div class="w-full flex items-center justify-between pb-3 mb-2 border-b border-border">
                <span class="text-xs font-black uppercase tracking-wider text-foreground flex items-center gap-2">
                  <CalendarIcon class="w-4 h-4 text-violet-500" />
                  <span>Shift Attendance Calendar</span>
                </span>
                <Badge variant="outline" class="text-[9px] font-mono border-violet-500/30 text-violet-600 dark:text-violet-400 bg-violet-500/10">
                  {{ selectedDateString }}
                </Badge>
              </div>
              <CalendarWidget
                v-model="selectedCalendarDate"
                class="rounded-xl border border-border/70 bg-background/50 shadow-xs"
              />
            </div>

            <div class="lg:col-span-7 p-5 rounded-2xl bg-card border border-border flex flex-col justify-between space-y-4">
              <div>
                <div class="flex items-center justify-between pb-3 border-b border-border">
                  <div>
                    <h4 class="text-sm font-bold text-foreground">
                      Roster for {{ formatDisplayDate(selectedDateString) }}
                    </h4>
                    <p class="text-xs text-muted-foreground">Status and coverage on the selected date</p>
                  </div>
                  <Badge variant="outline" class="text-[10px] font-bold border-border">
                    {{ techniciansOnSelectedDate.filter(t => t.status === 'Available').length }} Available
                  </Badge>
                </div>

                <div class="divide-y divide-border/60 max-h-[300px] overflow-y-auto mt-2">
                  <div
                    v-for="tech in techniciansOnSelectedDate"
                    :key="tech.name"
                    class="py-2.5 flex items-center justify-between gap-3 text-xs"
                  >
                    <div class="flex items-center gap-2">
                      <div class="w-6 h-6 rounded-full bg-muted border border-border flex items-center justify-center font-bold text-[9px]">
                        {{ tech.name.split(' ').map(n => n[0]).join('').slice(0, 2) }}
                      </div>
                      <span class="font-bold text-foreground">{{ tech.name }}</span>
                    </div>

                    <div class="flex items-center gap-2">
                      <Badge variant="outline" class="text-[9px] font-bold" :class="statusColor[tech.status]">
                        {{ tech.status }}
                      </Badge>

                      <Button
                        v-if="tech.status !== 'Absent'"
                        size="sm"
                        variant="ghost"
                        class="h-6 text-[10px] px-2 text-muted-foreground hover:text-foreground"
                        @click="openAbsenceForm(tech.name)"
                      >
                        Mark Absent
                      </Button>
                      <Button
                        v-else-if="tech.absence"
                        size="sm"
                        variant="ghost"
                        class="h-6 text-[10px] px-2 text-emerald-600 dark:text-emerald-400 hover:text-emerald-500 font-bold"
                        @click="resolveAbsence(tech.absence)"
                      >
                        Return
                      </Button>
                    </div>
                  </div>
                </div>
              </div>

              <div class="p-3 bg-muted/30 rounded-xl border border-border text-[11px] text-muted-foreground flex items-center justify-between">
                <span>Absences on this date: <strong class="text-rose-600 dark:text-rose-400 font-bold">{{ techniciansOnSelectedDate.filter(t => t.status === 'Absent').length }}</strong></span>
                <span>Teams OOO: <strong class="text-amber-600 dark:text-amber-400 font-bold">{{ techniciansOnSelectedDate.filter(t => t.status === 'Teams OOO').length }}</strong></span>
              </div>
            </div>
          </div>

          <!-- Technicians Grid / Table (Table View) -->
          <div v-if="attendanceViewMode === 'table'" class="rounded-2xl border border-border overflow-hidden bg-card">
            <table class="w-full text-left text-xs">
              <thead class="bg-muted/40 border-b border-border text-muted-foreground uppercase text-[10px] font-black tracking-wider">
                <tr>
                  <th class="py-3 px-4">Technician</th>
                  <th class="py-3 px-4">Status</th>
                  <th class="py-3 px-4">Absence Notes</th>
                  <th class="py-3 px-4">Backup Support</th>
                  <th class="py-3 px-4 text-center">Teams Sync</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border/60">
                <tr v-for="tech in filteredTechnicians" :key="tech" class="hover:bg-muted/30 transition-colors">
                  <td class="py-3 px-4 font-bold text-foreground flex items-center gap-2">
                    <div class="w-7 h-7 rounded-full bg-muted border border-border flex items-center justify-center text-foreground font-bold text-[10px]">
                      {{ tech.split(' ').map(n => n[0]).join('').slice(0, 2) }}
                    </div>
                    <span>{{ tech }}</span>
                  </td>
                  <td class="py-3 px-4">
                    <Badge variant="outline" class="text-[10px] font-bold" :class="statusColor[technicianStatus(tech)]">
                      {{ technicianStatus(tech) }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 text-muted-foreground">
                    <span v-if="absences.find(a => a.technicianName === tech)">
                      {{ absences.find(a => a.technicianName === tech)?.reason }} (until {{ formatDisplayDate(absences.find(a => a.technicianName === tech)?.endDate) }})
                    </span>
                    <span v-else class="text-muted-foreground font-mono text-[11px]">—</span>
                  </td>
                  <td class="py-3 px-4 text-muted-foreground">
                    <span v-if="absences.find(a => a.technicianName === tech)?.backupTechnician" class="text-indigo-600 dark:text-indigo-400 font-semibold">
                      {{ absences.find(a => a.technicianName === tech)?.backupTechnician }}
                    </span>
                    <span v-else class="text-muted-foreground font-mono text-[11px]">—</span>
                  </td>
                  <td class="py-3 px-4 text-center">
                    <button
                      type="button"
                      class="px-2 py-0.5 rounded text-[10px] font-bold border transition-colors"
                      :class="oooToggles[tech] ? 'bg-amber-500/20 text-amber-800 dark:text-amber-300 border-amber-500/40' : 'bg-background text-muted-foreground border-border hover:text-foreground'"
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
                        class="h-7 text-xs text-emerald-600 dark:text-emerald-400 hover:text-emerald-500 hover:bg-emerald-500/10 font-bold"
                        @click="resolveAbsence(absences.find(a => a.technicianName === tech)!)"
                      >
                        Return to Work
                      </Button>
                      <Button
                        v-else
                        size="sm"
                        variant="outline"
                        class="h-7 text-xs border-border text-foreground hover:bg-muted rounded-lg"
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
          <div v-if="absenceFormTarget" class="p-4 rounded-2xl bg-muted/30 border border-border space-y-3 mt-3">
            <div class="flex items-center justify-between pb-2 border-b border-border">
              <div class="text-xs font-black uppercase tracking-wider text-foreground">
                Mark Absence: <span class="text-indigo-600 dark:text-indigo-400">{{ absenceFormTarget }}</span>
              </div>
              <Button variant="ghost" size="sm" class="h-6 w-6 p-0 text-muted-foreground hover:text-foreground" @click="closeAbsenceForm">
                <X class="w-4 h-4" />
              </Button>
            </div>

            <div v-if="absenceError" class="p-2.5 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-700 dark:text-rose-400 text-xs flex items-center gap-2">
              <AlertTriangle class="w-4 h-4 shrink-0" />
              <span>{{ absenceError }}</span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Reason</label>
                <select
                  v-model="absenceForm.reason"
                  class="w-full h-8 rounded-lg bg-background border border-border text-foreground text-xs px-2"
                >
                  <option value="" disabled>Select reason</option>
                  <option v-for="r in ABSENCE_REASONS" :key="r" :value="r">{{ r }}</option>
                </select>
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Return Date</label>
                <DatePicker
                  v-model="absenceForm.endDate"
                  placeholder="Select return date"
                />
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Designated Backup</label>
                <SearchableTargetCombobox
                  v-model="absenceForm.backupTechnician"
                  placeholder="Search backup technician or enter name..."
                  category-label="Backup Support"
                  :query-fn="queryBackupTechnicians"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-2">
              <Button size="sm" variant="ghost" class="h-7 text-xs text-muted-foreground hover:text-foreground" @click="closeAbsenceForm">Cancel</Button>
              <Button size="sm" class="h-7 text-xs bg-rose-600 hover:bg-rose-500 text-white font-bold" :disabled="absenceSubmitting" @click="submitAbsence">
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
                :class="ruleScopeFilter === sc ? 'bg-indigo-600 text-white' : 'text-muted-foreground hover:bg-muted'"
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
          <div v-if="showAddRule" class="p-5 rounded-2xl bg-card border border-indigo-500/30 space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-border">
              <div class="text-xs font-black uppercase tracking-wider text-indigo-600 dark:text-indigo-400 flex items-center gap-2">
                <UserCheck class="w-4 h-4" />
                <span>Add Dedicated Technician Rule</span>
              </div>
              <Button variant="ghost" size="sm" class="h-6 w-6 p-0 text-muted-foreground hover:text-foreground" @click="showAddRule = false">
                <X class="w-4 h-4" />
              </Button>
            </div>

            <div v-if="ruleError" class="p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-rose-700 dark:text-rose-400 text-xs flex items-center gap-2">
              <AlertTriangle class="w-4 h-4" />
              <span>{{ ruleError }}</span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Scope</label>
                <select
                  v-model="ruleForm.scopeType"
                  class="w-full h-9 rounded-xl bg-background border border-border text-foreground text-xs px-3"
                >
                  <option value="Technology">Technology Cluster</option>
                  <option value="Line/Group">Line / Envelope Group</option>
                  <option value="Machine">Specific Machine</option>
                </select>
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Target</label>
                <SearchableTargetCombobox
                  v-model="ruleForm.target"
                  placeholder="Select target..."
                  category-label="Target"
                  :query-fn="queryScopeTargets"
                />
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Technician</label>
                <SearchableTargetCombobox
                  v-model="ruleForm.technicianName"
                  placeholder="Select technician..."
                  category-label="Technicians"
                  :query-fn="queryTechnicians"
                />
              </div>

              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Backup Support</label>
                <SearchableTargetCombobox
                  v-model="ruleForm.backupTechnician"
                  placeholder="Select backup..."
                  category-label="Backup"
                  :query-fn="queryBackupTechnicians"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-2">
              <Button size="sm" variant="ghost" class="text-xs text-muted-foreground hover:text-foreground" @click="showAddRule = false">Cancel</Button>
              <Button size="sm" class="text-xs bg-indigo-600 hover:bg-indigo-500 text-white font-bold" :disabled="ruleSubmitting" @click="saveRule">
                Save Rule
              </Button>
            </div>
          </div>

          <!-- Rules Table -->
          <div class="rounded-2xl border border-border overflow-hidden bg-card">
            <table class="w-full text-left text-xs">
              <thead class="bg-muted/40 border-b border-border text-muted-foreground uppercase text-[10px] font-black tracking-wider">
                <tr>
                  <th class="py-3 px-4">Scope</th>
                  <th class="py-3 px-4">Target</th>
                  <th class="py-3 px-4">Dedicated Tech</th>
                  <th class="py-3 px-4">Backup Tech</th>
                  <th class="py-3 px-4">Assigned Authority</th>
                  <th class="py-3 px-4 text-right">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border/60">
                <tr v-if="filteredRules.length === 0">
                  <td colspan="6" class="py-10 text-center text-muted-foreground text-xs">
                    No dedication rules found for this scope.
                  </td>
                </tr>
                <tr v-for="rule in filteredRules" :key="rule.id" class="hover:bg-muted/30 transition-colors">
                  <td class="py-3 px-4">
                    <Badge variant="outline" class="text-[10px] font-bold" :class="scopeColor[rule.scopeType]">
                      {{ rule.scopeType }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 font-bold text-foreground">
                    {{ rule.target }}
                  </td>
                  <td class="py-3 px-4 text-indigo-600 dark:text-indigo-400 font-semibold">
                    {{ rule.technicianName }}
                  </td>
                  <td class="py-3 px-4 text-muted-foreground">
                    {{ rule.backupTechnician || rule.backupTechnicianName || '—' }}
                  </td>
                  <td class="py-3 px-4">
                    <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider border-violet-500/30 text-violet-700 dark:text-violet-400 bg-violet-500/10">
                      {{ (rule.assignedByRole || rule.role || 'System').replace('_', ' ') }}
                    </Badge>
                  </td>
                  <td class="py-3 px-4 text-right">
                    <Button variant="ghost" size="icon" @click="deleteRule(rule)" class="h-7 w-7 text-muted-foreground hover:text-rose-600 rounded-lg">
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
          <div class="flex items-center justify-between pb-2 border-b border-border">
            <div>
              <div class="text-xs font-black uppercase tracking-wider text-foreground">Factory Envelope Clusters</div>
              <p class="text-xs text-muted-foreground">Cluster machine types and assign dedicated engineering leadership.</p>
            </div>
            <Button size="sm" class="h-8 bg-cyan-600 hover:bg-cyan-500 text-white font-bold text-xs gap-1.5 rounded-xl" @click="openCreateGroup">
              <Plus class="w-3.5 h-3.5" />
              Create Group
            </Button>
          </div>

          <!-- Create Group Form -->
          <div v-if="showCreateGroup" class="p-5 rounded-2xl bg-card border border-cyan-500/30 space-y-4">
            <h4 class="text-xs font-black uppercase tracking-wider text-cyan-600 dark:text-cyan-400">Create Machine Group Cluster</h4>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Name</label>
                <Input v-model="createForm.name" placeholder="e.g. SMT Line 01" class="h-9 bg-background border-border text-foreground text-xs rounded-xl" />
              </div>
              <div>
                <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Parent</label>
                <select v-model="createForm.parentGroupId" class="w-full h-9 rounded-xl bg-background border border-border text-foreground text-xs px-3">
                  <option value="">None (Top-Level Plant)</option>
                  <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
                </select>
              </div>
            </div>

            <div>
              <label class="text-[10px] font-black uppercase text-muted-foreground block mb-2">Machine Types</label>
              <div class="grid grid-cols-2 sm:grid-cols-4 gap-2">
                <button
                  v-for="mt in MACHINE_TYPES"
                  :key="mt"
                  type="button"
                  @click="toggleCreateMachineType(mt)"
                  class="p-2 rounded-lg text-left text-xs border transition-colors flex items-center gap-1.5"
                  :class="createForm.machineTypes.includes(mt) ? 'bg-cyan-500/20 text-cyan-700 dark:text-cyan-300 border-cyan-500/40 font-bold' : 'bg-background text-muted-foreground border-border'"
                >
                  <Check v-if="createForm.machineTypes.includes(mt)" class="w-3.5 h-3.5" />
                  <span class="truncate">{{ mt }}</span>
                </button>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-2">
              <Button size="sm" variant="ghost" class="text-xs text-muted-foreground hover:text-foreground" @click="showCreateGroup = false">Cancel</Button>
              <Button size="sm" class="text-xs bg-cyan-600 hover:bg-cyan-500 text-white font-bold" :disabled="groupSubmitting" @click="createGroup">
                Save Cluster
              </Button>
            </div>
          </div>

          <!-- Groups Grid -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div
              v-for="group in groups"
              :key="group.id"
              class="p-4 rounded-2xl bg-card border transition-all space-y-3"
              :class="editingGroupId === group.id ? 'border-cyan-500/60 ring-1 ring-cyan-500/30 shadow-md' : 'border-border'"
            >
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2.5">
                  <GitBranch class="w-4 h-4 text-cyan-600 dark:text-cyan-400 shrink-0" />
                  <div>
                    <span class="font-bold text-sm text-foreground">{{ group.name }}</span>
                    <span v-if="group.parentGroupId || group.parentId" class="block text-[10px] text-muted-foreground">
                      Parent: {{ groups.find(g => g.id === (group.parentGroupId || group.parentId))?.name || (group.parentGroupId || group.parentId) }}
                    </span>
                  </div>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  class="h-7 text-xs border-border text-foreground hover:bg-muted"
                  :class="editingGroupId === group.id ? 'bg-muted border-cyan-500/40 text-cyan-700 dark:text-cyan-300 font-bold' : ''"
                  @click="openEditGroup(group)"
                >
                  <Edit2 v-if="editingGroupId !== group.id" class="w-3 h-3 mr-1" />
                  {{ editingGroupId === group.id ? 'Cancel' : 'Edit' }}
                </Button>
              </div>

              <!-- View mode details -->
              <template v-if="editingGroupId !== group.id">
                <p v-if="group.description" class="text-xs text-muted-foreground">
                  {{ group.description }}
                </p>

                <div v-if="group.leadEngineer || group.leadEngineerName" class="text-xs text-muted-foreground flex items-center gap-1.5">
                  <User class="w-3.5 h-3.5 text-cyan-600 dark:text-cyan-400" />
                  <span>Lead: <span class="font-bold text-foreground">{{ group.leadEngineer || group.leadEngineerName }}</span></span>
                </div>

                <div class="flex flex-wrap gap-1.5 pt-1">
                  <span
                    v-for="mt in group.machineTypes"
                    :key="mt"
                    class="text-[9px] font-mono px-2 py-0.5 rounded bg-muted border border-border text-muted-foreground"
                  >
                    {{ mt }}
                  </span>
                  <span v-if="!group.machineTypes?.length" class="text-[10px] text-muted-foreground italic">
                    No machine types assigned
                  </span>
                </div>
              </template>

              <!-- Edit mode inline form -->
              <div v-else class="border-t border-border/80 pt-3 space-y-3">
                <div v-if="groupError" class="p-2.5 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-700 dark:text-rose-400 text-xs flex items-center gap-2">
                  <AlertTriangle class="w-4 h-4 shrink-0" />
                  <span>{{ groupError }}</span>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                  <div>
                    <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Cluster Name</label>
                    <Input
                      v-model="editForm.name"
                      placeholder="e.g. SMT Line 01"
                      class="h-8 bg-background border-border text-foreground text-xs rounded-xl"
                    />
                  </div>
                  <div>
                    <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Parent Cluster</label>
                    <select
                      v-model="editForm.parentGroupId"
                      class="w-full h-8 rounded-xl bg-background border border-border text-foreground text-xs px-2.5"
                    >
                      <option value="">None (Top-Level Plant)</option>
                      <option
                        v-for="g in groups.filter(item => item.id !== group.id)"
                        :key="g.id"
                        :value="g.id"
                      >
                        {{ g.name }}
                      </option>
                    </select>
                  </div>
                </div>

                <div>
                  <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1">Lead Engineer</label>
                  <SearchableTargetCombobox
                    v-model="editForm.leadEngineer"
                    placeholder="Search candidate engineer or enter name..."
                    category-label="Lead Engineers"
                    :query-fn="queryBackupTechnicians"
                  />
                </div>

                <div>
                  <label class="text-[10px] font-black uppercase text-muted-foreground block mb-1.5">Machine Types</label>
                  <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
                    <button
                      v-for="mt in MACHINE_TYPES"
                      :key="mt"
                      type="button"
                      @click="toggleEditMachineType(mt)"
                      class="p-1.5 rounded-lg text-left text-xs border transition-colors flex items-center gap-1.5"
                      :class="editForm.machineTypes.includes(mt) ? 'bg-cyan-500/20 text-cyan-700 dark:text-cyan-300 border-cyan-500/40 font-bold' : 'bg-background text-muted-foreground border-border hover:text-foreground'"
                    >
                      <Check v-if="editForm.machineTypes.includes(mt)" class="w-3.5 h-3.5 shrink-0" />
                      <span class="truncate">{{ mt }}</span>
                    </button>
                  </div>
                </div>

                <div class="flex items-center justify-between pt-2">
                  <Button
                    size="sm"
                    variant="ghost"
                    class="h-7 text-xs text-rose-600 hover:text-rose-500 hover:bg-rose-500/10 px-2"
                    :disabled="groupSubmitting"
                    @click="deleteGroupCluster(group.id)"
                  >
                    <Trash2 class="w-3.5 h-3.5 mr-1" />
                    Delete
                  </Button>
                  <div class="flex items-center gap-2">
                    <Button
                      size="sm"
                      variant="ghost"
                      class="h-7 text-xs text-muted-foreground hover:text-foreground"
                      @click="closeEditGroup"
                    >
                      Cancel
                    </Button>
                    <Button
                      size="sm"
                      class="h-7 text-xs bg-cyan-600 hover:bg-cyan-500 text-white font-bold"
                      :disabled="groupSubmitting"
                      @click="saveGroupCluster(group.id)"
                    >
                      <span v-if="groupSubmitting">Saving...</span>
                      <span v-else>Save Changes</span>
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
