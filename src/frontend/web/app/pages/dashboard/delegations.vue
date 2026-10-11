<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  Users,
  Shield,
  Award,
  Clock,
  Sparkles,
  Plus,
  Trash2,
  CheckCircle2,
  AlertTriangle,
  Play,
  RotateCcw,
  Search,
  Filter,
  Layers,
  Cpu,
  BookOpen,
  ArrowRight,
  PhoneCall,
  Briefcase,
  Calendar,
  Zap,
  Info,
  UserCheck
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import DelegationTestResolverModal from '~/components/delegations/DelegationTestResolverModal.vue'
import { DEMO_PERSONAS } from '~/composables/useAuthSession'

definePageMeta({
  layout: 'shadcn-dashboard'
})

// ─── Active Tab State ────────────────────────────────────────────────────────
type TabId = 'recipes' | 'skills' | 'roster' | 'groups'
const activeTab = ref<TabId>('recipes')

const tabs: { id: TabId; label: string; icon: any }[] = [
  { id: 'recipes', label: 'Delegation Recipes', icon: Briefcase },
  { id: 'skills', label: 'Skills & Certifications', icon: Award },
  { id: 'roster', label: 'Personnel Qualifications', icon: UserCheck },
  { id: 'groups', label: 'Shift Crews & Groups', icon: Users }
]

// ─── Loading & Search ────────────────────────────────────────────────────────
const isLoading = ref(false)
const searchQuery = ref('')

// ─── Data State ──────────────────────────────────────────────────────────────
const recipes = ref<any[]>([])
const skillsCatalog = ref<any[]>([])
const personnelSkills = ref<any[]>([])
const personnelGroups = ref<any[]>([])
const machines = ref<any[]>([])

// ─── Resolver Simulator Modal ────────────────────────────────────────────────
const isResolverModalOpen = ref(false)

// ─── Create Recipe Modal State ───────────────────────────────────────────────
const isCreateRecipeModalOpen = ref(false)
const newRecipe = ref({
  name: '',
  description: '',
  discipline: 'Mechanical',
  triggerEventCategory: '',
  allowedSchedules: ['Shift'] as string[],
  requiredSkillIds: [] as string[],
  primaryPersonnelGroupId: '',
  primaryUserId: '',
  onCallFallbackUserId: '',
  escalationTimeoutMinutes: 30
})

// ─── Create Skill Modal State ────────────────────────────────────────────────
const isCreateSkillModalOpen = ref(false)
const newSkill = ref({
  code: '',
  name: '',
  description: '',
  category: 'Certification',
  issuer: '',
  validityMonths: 24
})

// ─── Assign Skill to User Modal State ────────────────────────────────────────
const isAssignSkillModalOpen = ref(false)
const newAssignment = ref({
  userId: '',
  skillId: '',
  certificateNumber: '',
  verifiedBy: '',
  validityMonths: 24
})

// ─── Create Personnel Group Modal State ──────────────────────────────────────
const isCreateGroupModalOpen = ref(false)
const newGroup = ref({
  name: '',
  code: '',
  groupType: 'ShiftCrew',
  discipline: 'Controls',
  shiftPattern: 'Morning',
  leadUserId: '',
  memberUserIds: [] as string[],
  description: ''
})

// ─── Disciplines & Categories ────────────────────────────────────────────────
const DISCIPLINES = [
  'Mechanical',
  'Controls',
  'Electrical',
  'Safety',
  'Quality',
  'Process',
  'Tooling',
  'General'
]

const SKILL_CATEGORIES = [
  'Certification',
  'TechnologyKnowHow',
  'MachineAuthorization',
  'SafetyClearance'
]

const GROUP_TYPES = [
  { value: 'ShiftCrew', label: 'Shift Crew' },
  { value: 'OfficeTimeStaff', label: 'Office-Time Engineering' },
  { value: 'OnCallRoster', label: 'On-Call Standby Roster' },
  { value: 'SpecialistTeam', label: 'Specialist Domain Team' }
]

const SHIFT_PATTERNS = [
  { value: 'Morning', label: 'Morning Shift (06:00 – 14:00)' },
  { value: 'Afternoon', label: 'Afternoon Shift (14:00 – 22:00)' },
  { value: 'Night', label: 'Night Shift (22:00 – 06:00)' },
  { value: 'Office', label: 'Standard Office Time (08:00 – 17:00)' },
  { value: 'Weekend', label: 'Weekend / 24-7 Standby' }
]

// ─── Fetch All Data ──────────────────────────────────────────────────────────
const fetchData = async () => {
  isLoading.value = true
  try {
    const [recRes, skillsRes, psRes, grpRes, machRes] = await Promise.allSettled([
      $fetch<any[]>('/api/delegations/recipes'),
      $fetch<any[]>('/api/skills/catalog'),
      $fetch<any[]>('/api/skills/user'),
      $fetch<any[]>('/api/personnel/groups'),
      $fetch<any[]>('/api/machines', { params: { includeDecommissioned: true } })
    ])

    if (recRes.status === 'fulfilled') recipes.value = recRes.value
    if (skillsRes.status === 'fulfilled') skillsCatalog.value = skillsRes.value
    if (psRes.status === 'fulfilled') personnelSkills.value = psRes.value
    if (grpRes.status === 'fulfilled') personnelGroups.value = grpRes.value
    if (machRes.status === 'fulfilled') machines.value = machRes.value
  } catch (err) {
    console.error('Failed to load delegations data:', err)
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  fetchData()
})

// ─── Helpers ─────────────────────────────────────────────────────────────────
const getDisciplineColor = (disc: string) => {
  switch (disc) {
    case 'Safety':
      return 'bg-rose-500/15 text-rose-600 dark:text-rose-400 border-rose-500/30'
    case 'Controls':
      return 'bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 border-indigo-500/30'
    case 'Mechanical':
      return 'bg-blue-500/15 text-blue-600 dark:text-blue-400 border-blue-500/30'
    case 'Electrical':
      return 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30'
    case 'Quality':
      return 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30'
    case 'Process':
      return 'bg-purple-500/15 text-purple-600 dark:text-purple-400 border-purple-500/30'
    default:
      return 'bg-muted text-muted-foreground border-border'
  }
}

const getCategoryColor = (cat: string) => {
  switch (cat) {
    case 'SafetyClearance':
      return 'bg-red-500/15 text-red-600 dark:text-red-400 border-red-500/30'
    case 'Certification':
      return 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30'
    case 'TechnologyKnowHow':
      return 'bg-cyan-500/15 text-cyan-600 dark:text-cyan-400 border-cyan-500/30'
    case 'MachineAuthorization':
      return 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30'
    default:
      return 'bg-muted text-muted-foreground border-border'
  }
}

const getExpiryStatus = (expiresAt?: string) => {
  if (!expiresAt) return { label: 'Perpetual', class: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30' }
  const expiryDate = new Date(expiresAt).getTime()
  const now = Date.now()
  const oneMonthMs = 30 * 24 * 60 * 60 * 1000

  if (expiryDate < now) {
    return { label: 'Expired', class: 'bg-rose-500/15 text-rose-600 dark:text-rose-400 border-rose-500/30' }
  } else if (expiryDate - now < oneMonthMs) {
    return { label: 'Expiring Soon', class: 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30' }
  }
  return { label: 'Valid', class: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border-emerald-500/30' }
}

const getUserName = (id: string) => {
  const p = DEMO_PERSONAS.find(x => x.id === id)
  return p ? p.name : id
}

// ─── Filtered Views ──────────────────────────────────────────────────────────
const filteredRecipes = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return recipes.value
  return recipes.value.filter(r =>
    r.name?.toLowerCase().includes(q) ||
    r.discipline?.toLowerCase().includes(q) ||
    r.primaryPersonnelGroupName?.toLowerCase().includes(q) ||
    r.primaryUserName?.toLowerCase().includes(q)
  )
})

const filteredSkills = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return skillsCatalog.value
  return skillsCatalog.value.filter(s =>
    s.name?.toLowerCase().includes(q) ||
    s.code?.toLowerCase().includes(q) ||
    s.category?.toLowerCase().includes(q) ||
    s.issuer?.toLowerCase().includes(q)
  )
})

const filteredRoster = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return personnelSkills.value
  return personnelSkills.value.filter(ps =>
    ps.userName?.toLowerCase().includes(q) ||
    ps.skillName?.toLowerCase().includes(q) ||
    ps.skillCode?.toLowerCase().includes(q) ||
    ps.category?.toLowerCase().includes(q)
  )
})

const filteredGroups = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return personnelGroups.value
  return personnelGroups.value.filter(g =>
    g.name?.toLowerCase().includes(q) ||
    g.code?.toLowerCase().includes(q) ||
    g.discipline?.toLowerCase().includes(q) ||
    g.groupType?.toLowerCase().includes(q)
  )
})

// ─── Mutation Actions ────────────────────────────────────────────────────────
const handleCreateRecipe = async () => {
  try {
    const primaryGroup = personnelGroups.value.find(g => g.id === newRecipe.value.primaryPersonnelGroupId)
    const primaryUser = DEMO_PERSONAS.find(p => p.id === newRecipe.value.primaryUserId)
    const onCallUser = DEMO_PERSONAS.find(p => p.id === newRecipe.value.onCallFallbackUserId)

    const skillCodes = skillsCatalog.value
      .filter(s => newRecipe.value.requiredSkillIds.includes(s.id))
      .map(s => s.code)

    await $fetch('/api/delegations/recipes', {
      method: 'POST',
      body: {
        name: newRecipe.value.name,
        description: newRecipe.value.description,
        discipline: newRecipe.value.discipline,
        triggerEventCategory: newRecipe.value.triggerEventCategory || undefined,
        allowedSchedules: newRecipe.value.allowedSchedules,
        requiredSkillIds: newRecipe.value.requiredSkillIds,
        requiredSkillCodes: skillCodes,
        primaryPersonnelGroupId: primaryGroup?.id,
        primaryPersonnelGroupName: primaryGroup?.name,
        primaryUserId: primaryUser?.id,
        primaryUserName: primaryUser?.name,
        onCallFallbackUserId: onCallUser?.id,
        onCallFallbackUserName: onCallUser?.name,
        escalationTimeoutMinutes: Number(newRecipe.value.escalationTimeoutMinutes) || 30,
        isActive: true
      }
    })

    isCreateRecipeModalOpen.value = false
    fetchData()
  } catch (err: any) {
    alert(err?.data?.message || 'Failed to create delegation recipe')
  }
}

const handleDeleteRecipe = async (id: string) => {
  if (!confirm('Are you sure you want to delete this delegation recipe?')) return
  try {
    await $fetch(`/api/delegations/recipes/${id}`, { method: 'DELETE' })
    fetchData()
  } catch (err) {
    console.error(err)
  }
}

const handleCreateSkill = async () => {
  try {
    await $fetch('/api/skills/catalog', {
      method: 'POST',
      body: {
        code: newSkill.value.code,
        name: newSkill.value.name,
        description: newSkill.value.description,
        category: newSkill.value.category,
        issuer: newSkill.value.issuer,
        validityMonths: Number(newSkill.value.validityMonths) || 24
      }
    })
    isCreateSkillModalOpen.value = false
    fetchData()
  } catch (err: any) {
    alert(err?.data?.message || 'Failed to register skill')
  }
}

const handleDeleteSkill = async (id: string) => {
  if (!confirm('Are you sure you want to delete this skill definition?')) return
  try {
    await $fetch(`/api/skills/catalog/${id}`, { method: 'DELETE' })
    fetchData()
  } catch (err) {
    console.error(err)
  }
}

const handleAssignSkill = async () => {
  try {
    const selectedUser = DEMO_PERSONAS.find(p => p.id === newAssignment.value.userId)
    const expires = new Date()
    expires.setMonth(expires.getMonth() + (Number(newAssignment.value.validityMonths) || 24))

    await $fetch('/api/skills/user', {
      method: 'POST',
      body: {
        userId: newAssignment.value.userId,
        userName: selectedUser?.name,
        skillId: newAssignment.value.skillId,
        certificateNumber: newAssignment.value.certificateNumber,
        verifiedBy: newAssignment.value.verifiedBy || 'EHS & Plant Engineering',
        expiresAt: expires.toISOString()
      }
    })
    isAssignSkillModalOpen.value = false
    fetchData()
  } catch (err: any) {
    alert(err?.data?.message || 'Failed to certify personnel')
  }
}

const handleRevokeSkill = async (id: string) => {
  if (!confirm('Revoke this qualification from the user?')) return
  try {
    await $fetch(`/api/skills/user/${id}`, { method: 'DELETE' })
    fetchData()
  } catch (err) {
    console.error(err)
  }
}

const handleCreateGroup = async () => {
  try {
    const lead = DEMO_PERSONAS.find(p => p.id === newGroup.value.leadUserId)
    const members = DEMO_PERSONAS.filter(p => newGroup.value.memberUserIds.includes(p.id))

    await $fetch('/api/personnel/groups', {
      method: 'POST',
      body: {
        name: newGroup.value.name,
        code: newGroup.value.code,
        groupType: newGroup.value.groupType,
        discipline: newGroup.value.discipline,
        shiftPattern: newGroup.value.shiftPattern,
        leadUserId: lead?.id,
        leadUserName: lead?.name,
        memberUserIds: members.map(m => m.id),
        memberUserNames: members.map(m => m.name),
        description: newGroup.value.description
      }
    })
    isCreateGroupModalOpen.value = false
    fetchData()
  } catch (err: any) {
    alert(err?.data?.message || 'Failed to create personnel group')
  }
}

const handleDeleteGroup = async (id: string) => {
  if (!confirm('Are you sure you want to delete this personnel group?')) return
  try {
    await $fetch(`/api/personnel/groups/${id}`, { method: 'DELETE' })
    fetchData()
  } catch (err) {
    console.error(err)
  }
}
</script>

<template>
  <div class="space-y-6 pb-16">
    <!-- Header & Breadcrumbs -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-border pb-5">
      <div>
        <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-violet-600 dark:text-violet-400 mb-1">
          <NuxtLink to="/dashboard" class="hover:underline">Dashboard</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <NuxtLink to="/dashboard/tickets" class="hover:underline">Operations</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <span class="text-muted-foreground">Delegations & Skills Hub</span>
        </div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-3">
          <div class="p-2.5 rounded-2xl bg-violet-500/10 text-violet-600 dark:text-violet-400 border border-violet-500/20 shadow-xs">
            <Users class="h-6 w-6" />
          </div>
          <span>Personnel Delegations, Skills & Dispatch Matrix</span>
        </h1>
        <p class="text-sm text-muted-foreground mt-1 max-w-3xl">
          Configure responsibility recipes, certified skill tagging, shift rosters, and on-call fallback routing decoupled from auth credentials.
        </p>
      </div>

      <!-- Quick Action Controls -->
      <div class="flex flex-wrap items-center gap-2.5">
        <Button
          variant="outline"
          size="sm"
          class="border-border bg-card hover:bg-muted text-foreground rounded-xl text-xs font-bold cursor-pointer"
          @click="isResolverModalOpen = true"
        >
          <Play class="w-3.5 h-3.5 mr-1.5 text-violet-500" />
          Test Dispatch Resolver
        </Button>

        <Button
          v-if="activeTab === 'recipes'"
          size="sm"
          class="bg-violet-600 hover:bg-violet-700 text-white rounded-xl text-xs font-bold cursor-pointer shadow-sm"
          @click="isCreateRecipeModalOpen = true"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          New Delegation Recipe
        </Button>

        <Button
          v-else-if="activeTab === 'skills'"
          size="sm"
          class="bg-amber-600 hover:bg-amber-700 text-white rounded-xl text-xs font-bold cursor-pointer shadow-sm"
          @click="isCreateSkillModalOpen = true"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          Register Skill Definition
        </Button>

        <Button
          v-else-if="activeTab === 'roster'"
          size="sm"
          class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-bold cursor-pointer shadow-sm"
          @click="isAssignSkillModalOpen = true"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          Certify Personnel
        </Button>

        <Button
          v-else-if="activeTab === 'groups'"
          size="sm"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-bold cursor-pointer shadow-sm"
          @click="isCreateGroupModalOpen = true"
        >
          <Plus class="w-3.5 h-3.5 mr-1.5" />
          New Personnel Group
        </Button>
      </div>
    </div>

    <!-- Navigation Tabs & Search Bar -->
    <div class="flex flex-col sm:flex-row items-center justify-between gap-4">
      <div class="flex items-center gap-1.5 p-1 bg-muted/30 border border-border rounded-2xl w-full sm:w-auto">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          @click="activeTab = tab.id"
          :class="[
            'flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer flex-1 sm:flex-initial justify-center',
            activeTab === tab.id
              ? 'bg-card text-foreground shadow-xs border border-border/80'
              : 'text-muted-foreground hover:text-foreground hover:bg-card/40'
          ]"
        >
          <component :is="tab.icon" class="w-4 h-4" />
          <span>{{ tab.label }}</span>
          <span
            v-if="tab.id === 'recipes'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-violet-500/15 text-violet-600 dark:text-violet-400 font-mono"
          >
            {{ recipes.length }}
          </span>
          <span
            v-else-if="tab.id === 'skills'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-amber-500/15 text-amber-600 dark:text-amber-400 font-mono"
          >
            {{ skillsCatalog.length }}
          </span>
          <span
            v-else-if="tab.id === 'roster'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 font-mono"
          >
            {{ personnelSkills.length }}
          </span>
          <span
            v-else-if="tab.id === 'groups'"
            class="ml-1 text-[10px] px-1.5 py-0.2 rounded-full bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 font-mono"
          >
            {{ personnelGroups.length }}
          </span>
        </button>
      </div>

      <div class="relative w-full sm:w-72">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
        <Input
          v-model="searchQuery"
          placeholder="Filter current view..."
          class="pl-9 h-9 text-xs rounded-xl bg-card border-border"
        />
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 1: DELEGATION RECIPES                                               -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'recipes'" class="space-y-4">
      <div v-if="filteredRecipes.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <Briefcase class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Delegation Recipes Found</h3>
        <p class="text-xs text-muted-foreground mt-1">Create responsibility recipes specifying required skills and dispatch escalation.</p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        <div
          v-for="r in filteredRecipes"
          :key="r.id"
          class="bg-card border border-border rounded-2xl p-5 shadow-xs hover:border-violet-500/40 transition-all flex flex-col justify-between"
        >
          <div class="space-y-3">
            <div class="flex items-start justify-between gap-3">
              <div>
                <Badge variant="outline" :class="['text-[10px] font-bold uppercase tracking-wider', getDisciplineColor(r.discipline)]">
                  {{ r.discipline }}
                </Badge>
                <h3 class="text-base font-black text-foreground mt-1.5 leading-snug">{{ r.name }}</h3>
              </div>
              <Button
                variant="ghost"
                size="sm"
                class="size-8 p-0 text-muted-foreground hover:text-rose-600 hover:bg-rose-500/10 rounded-lg cursor-pointer"
                @click="handleDeleteRecipe(r.id)"
              >
                <Trash2 class="size-4" />
              </Button>
            </div>

            <p class="text-xs text-muted-foreground leading-relaxed line-clamp-2">
              {{ r.description || 'Standard incident handling and engineering response protocol.' }}
            </p>

            <!-- Schedule Coverage -->
            <div class="flex flex-wrap items-center gap-1.5 pt-1">
              <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest block w-full mb-0.5">Coverage:</span>
              <Badge
                v-for="sch in r.allowedSchedules"
                :key="sch"
                variant="outline"
                class="text-[10px] font-semibold bg-muted/40 border-border text-foreground"
              >
                <Clock class="size-2.5 mr-1 text-muted-foreground" />
                {{ sch }}
              </Badge>
            </div>

            <!-- Required Skills Gate -->
            <div class="space-y-1.5 pt-1">
              <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest block">Required Qualifications:</span>
              <div v-if="r.requiredSkillCodes && r.requiredSkillCodes.length > 0" class="flex flex-wrap gap-1">
                <Badge
                  v-for="code in r.requiredSkillCodes"
                  :key="code"
                  variant="outline"
                  class="text-[10px] font-mono font-bold bg-amber-500/10 text-amber-700 dark:text-amber-400 border-amber-500/30"
                >
                  <Award class="size-2.5 mr-1" />
                  {{ code }}
                </Badge>
              </div>
              <span v-else class="text-[11px] text-muted-foreground italic">None (Open to all roster personnel)</span>
            </div>

            <!-- Dispatch Target Info -->
            <div class="p-3 bg-muted/20 border border-border/80 rounded-xl space-y-2 text-xs">
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Primary Group:</span>
                <span class="font-bold text-foreground truncate max-w-[170px]">{{ r.primaryPersonnelGroupName || 'Standard Crew' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Primary Lead:</span>
                <span class="font-semibold text-foreground">{{ r.primaryUserName || 'Auto-Dispatched' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">On-Call Standby:</span>
                <span class="font-semibold text-violet-600 dark:text-violet-400 flex items-center gap-1">
                  <PhoneCall class="size-3" />
                  {{ r.onCallFallbackUserName || 'Dr. Standby' }}
                </span>
              </div>
              <div class="flex items-center justify-between pt-1 border-t border-border/50 text-[11px]">
                <span class="text-muted-foreground">Escalation Timer:</span>
                <span class="font-mono font-bold text-foreground">{{ r.escalationTimeoutMinutes }} min</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 2: SKILLS & CERTIFICATIONS CATALOG                                  -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-else-if="activeTab === 'skills'" class="space-y-4">
      <div v-if="filteredSkills.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <Award class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Skills Registered</h3>
        <p class="text-xs text-muted-foreground mt-1">Register technical certifications and authorizations to gate delegation dispatch.</p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <div
          v-for="s in filteredSkills"
          :key="s.id"
          class="bg-card border border-border rounded-2xl p-5 shadow-xs hover:border-amber-500/40 transition-all flex flex-col justify-between"
        >
          <div class="space-y-3">
            <div class="flex items-start justify-between gap-3">
              <div>
                <Badge variant="outline" :class="['text-[10px] font-bold uppercase tracking-wider', getCategoryColor(s.category)]">
                  {{ s.category }}
                </Badge>
                <div class="font-mono text-xs font-bold text-muted-foreground mt-1">{{ s.code }}</div>
                <h3 class="text-base font-black text-foreground mt-0.5 leading-snug">{{ s.name }}</h3>
              </div>
              <Button
                variant="ghost"
                size="sm"
                class="size-8 p-0 text-muted-foreground hover:text-rose-600 hover:bg-rose-500/10 rounded-lg cursor-pointer"
                @click="handleDeleteSkill(s.id)"
              >
                <Trash2 class="size-4" />
              </Button>
            </div>

            <p class="text-xs text-muted-foreground leading-relaxed">
              {{ s.description || 'Verified industrial qualification required for equipment service.' }}
            </p>

            <div class="p-3 bg-muted/20 border border-border/80 rounded-xl space-y-1.5 text-xs">
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Certifying Body / Issuer:</span>
                <span class="font-semibold text-foreground truncate max-w-[160px]">{{ s.issuer || 'Internal EHS' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Standard Validity:</span>
                <span class="font-mono font-bold text-foreground">{{ s.validityMonths }} Months</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 3: PERSONNEL QUALIFICATIONS ROSTER                                  -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-else-if="activeTab === 'roster'" class="space-y-4">
      <div v-if="filteredRoster.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <UserCheck class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Certified Personnel Found</h3>
        <p class="text-xs text-muted-foreground mt-1">Assign certifications to engineering personnel to unlock recipe dispatch eligibility.</p>
      </div>

      <div v-else class="bg-card border border-border rounded-2xl overflow-hidden shadow-xs">
        <div class="overflow-x-auto">
          <table class="w-full text-xs text-left">
            <thead class="bg-muted/40 border-b border-border text-muted-foreground uppercase text-[10px] font-black tracking-wider">
              <tr>
                <th class="px-4 py-3">Technician / Engineer</th>
                <th class="px-4 py-3">Skill / Qualification</th>
                <th class="px-4 py-3">Category</th>
                <th class="px-4 py-3">Certificate Ref</th>
                <th class="px-4 py-3">Expiry / Status</th>
                <th class="px-4 py-3">Certified By</th>
                <th class="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border/60">
              <tr v-for="ps in filteredRoster" :key="ps.id" class="hover:bg-muted/20 transition-colors">
                <td class="px-4 py-3 font-bold text-foreground">
                  <div class="flex items-center gap-2">
                    <div class="size-7 rounded-lg bg-violet-500/10 text-violet-600 dark:text-violet-400 flex items-center justify-center font-bold text-xs uppercase">
                      {{ (ps.userName || ps.userId).substring(0, 2) }}
                    </div>
                    <div>
                      <div>{{ ps.userName || ps.userId }}</div>
                      <div class="text-[10px] text-muted-foreground font-mono font-normal">{{ ps.userId }}</div>
                    </div>
                  </div>
                </td>
                <td class="px-4 py-3">
                  <div class="font-bold text-foreground">{{ ps.skillName }}</div>
                  <div class="font-mono text-[10px] text-muted-foreground">{{ ps.skillCode }}</div>
                </td>
                <td class="px-4 py-3">
                  <Badge variant="outline" :class="['text-[10px] font-semibold', getCategoryColor(ps.category)]">
                    {{ ps.category }}
                  </Badge>
                </td>
                <td class="px-4 py-3 font-mono text-muted-foreground">
                  {{ ps.certificateNumber || '—' }}
                </td>
                <td class="px-4 py-3">
                  <Badge variant="outline" :class="['text-[10px] font-bold', getExpiryStatus(ps.expiresAt).class]">
                    {{ getExpiryStatus(ps.expiresAt).label }}
                  </Badge>
                  <div v-if="ps.expiresAt" class="text-[10px] text-muted-foreground font-mono mt-0.5">
                    Exp: {{ new Date(ps.expiresAt).toLocaleDateString() }}
                  </div>
                </td>
                <td class="px-4 py-3 text-muted-foreground">
                  {{ ps.verifiedBy || 'EHS Authority' }}
                </td>
                <td class="px-4 py-3 text-right">
                  <Button
                    variant="ghost"
                    size="sm"
                    class="size-7 p-0 text-muted-foreground hover:text-rose-600 hover:bg-rose-500/10 rounded-lg cursor-pointer"
                    @click="handleRevokeSkill(ps.id)"
                  >
                    <Trash2 class="size-3.5" />
                  </Button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- TAB 4: SHIFT CREWS & PERSONNEL GROUPS                                   -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <div v-else-if="activeTab === 'groups'" class="space-y-4">
      <div v-if="filteredGroups.length === 0" class="text-center py-16 bg-muted/10 border border-dashed border-border rounded-2xl">
        <Users class="size-10 text-muted-foreground mx-auto mb-2 opacity-50" />
        <h3 class="text-sm font-bold text-foreground">No Personnel Groups Found</h3>
        <p class="text-xs text-muted-foreground mt-1">Configure shift crews, office-time engineering teams, and on-call rosters.</p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <div
          v-for="g in filteredGroups"
          :key="g.id"
          class="bg-card border border-border rounded-2xl p-5 shadow-xs hover:border-indigo-500/40 transition-all flex flex-col justify-between"
        >
          <div class="space-y-3">
            <div class="flex items-start justify-between gap-3">
              <div>
                <Badge variant="outline" :class="['text-[10px] font-bold uppercase tracking-wider', getDisciplineColor(g.discipline)]">
                  {{ g.discipline }} • {{ g.groupType }}
                </Badge>
                <div class="font-mono text-xs font-bold text-muted-foreground mt-1">{{ g.code }}</div>
                <h3 class="text-base font-black text-foreground mt-0.5 leading-snug">{{ g.name }}</h3>
              </div>
              <Button
                variant="ghost"
                size="sm"
                class="size-8 p-0 text-muted-foreground hover:text-rose-600 hover:bg-rose-500/10 rounded-lg cursor-pointer"
                @click="handleDeleteGroup(g.id)"
              >
                <Trash2 class="size-4" />
              </Button>
            </div>

            <p class="text-xs text-muted-foreground leading-relaxed line-clamp-2">
              {{ g.description || 'Specialized personnel group bound to industrial machines via recipes.' }}
            </p>

            <div class="p-3 bg-muted/20 border border-border/80 rounded-xl space-y-1.5 text-xs">
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Shift Pattern:</span>
                <span class="font-semibold text-foreground">{{ g.shiftPattern || 'Standard Shift' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[11px] text-muted-foreground">Group Lead:</span>
                <span class="font-bold text-foreground">{{ g.leadUserName || 'Designated Lead' }}</span>
              </div>
            </div>

            <!-- Members Roster -->
            <div class="space-y-1 pt-1">
              <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest block">Active Members ({{ g.memberUserNames?.length || 0 }}):</span>
              <div class="flex flex-wrap gap-1">
                <Badge
                  v-for="mName in g.memberUserNames"
                  :key="mName"
                  variant="outline"
                  class="text-[10px] font-medium bg-card border-border text-foreground"
                >
                  {{ mName }}
                </Badge>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- MODAL: CREATE DELEGATION RECIPE                                         -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <Dialog v-model:open="isCreateRecipeModalOpen">
      <DialogContent class="sm:max-w-xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-base font-black">
            <Briefcase class="size-5 text-violet-600 dark:text-violet-400" />
            <span>Create Delegation Responsibility Recipe</span>
          </DialogTitle>
        </DialogHeader>

        <form @submit.prevent="handleCreateRecipe" class="space-y-4 pt-2 text-xs">
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Recipe Name</label>
            <Input v-model="newRecipe.name" required placeholder="e.g. Laser Safety & Optical Response Recipe" class="h-9" />
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Description & Dispatch Scope</label>
            <Input v-model="newRecipe.description" placeholder="e.g. Immediate intervention for beam degradation and interlock faults" class="h-9" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Engineering Discipline</label>
              <select v-model="newRecipe.discipline" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option v-for="d in DISCIPLINES" :key="d" :value="d">{{ d }}</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Trigger Event Category (Optional)</label>
              <Input v-model="newRecipe.triggerEventCategory" placeholder="e.g. OpticalAlarm, PlcError" class="h-9" />
            </div>
          </div>

          <!-- Allowed Schedules -->
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Allowed Schedule Profiles</label>
            <div class="flex gap-4 pt-1">
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input type="checkbox" value="Shift" v-model="newRecipe.allowedSchedules" class="rounded" />
                <span>Shift Worker</span>
              </label>
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input type="checkbox" value="OfficeTime" v-model="newRecipe.allowedSchedules" class="rounded" />
                <span>Office Time Staff</span>
              </label>
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input type="checkbox" value="OnCall" v-model="newRecipe.allowedSchedules" class="rounded" />
                <span>On-Call Standby</span>
              </label>
            </div>
          </div>

          <!-- Required Skills -->
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Required Skill Tags (Multi-Select)</label>
            <div class="grid grid-cols-2 gap-2 p-3 bg-muted/20 border border-border rounded-xl max-h-36 overflow-y-auto">
              <label
                v-for="sk in skillsCatalog"
                :key="sk.id"
                class="flex items-start gap-2 p-1.5 rounded-lg hover:bg-card cursor-pointer"
              >
                <input
                  type="checkbox"
                  :value="sk.id"
                  v-model="newRecipe.requiredSkillIds"
                  class="rounded mt-0.5"
                />
                <div class="leading-tight">
                  <div class="font-bold text-foreground text-[11px]">{{ sk.name }}</div>
                  <div class="font-mono text-[9px] text-muted-foreground">{{ sk.code }}</div>
                </div>
              </label>
            </div>
          </div>

          <!-- Responders -->
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Primary Personnel Group</label>
              <select v-model="newRecipe.primaryPersonnelGroupId" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option value="">-- None / Direct User --</option>
                <option v-for="g in personnelGroups" :key="g.id" :value="g.id">{{ g.name }} ({{ g.discipline }})</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Primary Lead Responder</label>
              <select v-model="newRecipe.primaryUserId" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option value="">-- Auto Dispatch from Group --</option>
                <option v-for="u in DEMO_PERSONAS" :key="u.id" :value="u.id">{{ u.name }}</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">On-Call Fallback Specialist</label>
              <select v-model="newRecipe.onCallFallbackUserId" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option value="">-- None --</option>
                <option v-for="u in DEMO_PERSONAS" :key="u.id" :value="u.id">{{ u.name }}</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Escalation Timeout (Minutes)</label>
              <Input type="number" v-model="newRecipe.escalationTimeoutMinutes" min="5" max="180" class="h-9" />
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-border">
            <Button type="button" variant="outline" size="sm" @click="isCreateRecipeModalOpen = false">Cancel</Button>
            <Button type="submit" size="sm" class="bg-violet-600 hover:bg-violet-700 text-white font-bold">Create Recipe</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- MODAL: REGISTER SKILL DEFINITION                                        -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <Dialog v-model:open="isCreateSkillModalOpen">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-base font-black">
            <Award class="size-5 text-amber-600 dark:text-amber-400" />
            <span>Register New Skill Definition</span>
          </DialogTitle>
        </DialogHeader>

        <form @submit.prevent="handleCreateSkill" class="space-y-4 pt-2 text-xs">
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Skill / Qualification Code</label>
            <Input v-model="newSkill.code" required placeholder="e.g. CERT-LASER-4, AUTH-HIGH-VOLT" class="h-9 font-mono" />
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Skill Title</label>
            <Input v-model="newSkill.name" required placeholder="e.g. Laser Class 4 Safety Officer Certification" class="h-9" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Qualification Category</label>
              <select v-model="newSkill.category" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option v-for="c in SKILL_CATEGORIES" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Standard Validity (Months)</label>
              <Input type="number" v-model="newSkill.validityMonths" min="1" max="120" class="h-9" />
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Certifying Body / Issuer</label>
            <Input v-model="newSkill.issuer" placeholder="e.g. TÜV Rheinland / DGUV / Internal" class="h-9" />
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Scope / Description</label>
            <Input v-model="newSkill.description" placeholder="Description of authorized work and capabilities" class="h-9" />
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-border">
            <Button type="button" variant="outline" size="sm" @click="isCreateSkillModalOpen = false">Cancel</Button>
            <Button type="submit" size="sm" class="bg-amber-600 hover:bg-amber-700 text-white font-bold">Register Skill</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- MODAL: CERTIFY PERSONNEL / ASSIGN SKILL                                -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <Dialog v-model:open="isAssignSkillModalOpen">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-base font-black">
            <UserCheck class="size-5 text-emerald-600 dark:text-emerald-400" />
            <span>Certify Personnel Qualification</span>
          </DialogTitle>
        </DialogHeader>

        <form @submit.prevent="handleAssignSkill" class="space-y-4 pt-2 text-xs">
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Technician / Engineer</label>
            <select v-model="newAssignment.userId" required class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
              <option value="" disabled>-- Select Personnel --</option>
              <option v-for="u in DEMO_PERSONAS" :key="u.id" :value="u.id">{{ u.name }} ({{ u.role }})</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Skill / Qualification</label>
            <select v-model="newAssignment.skillId" required class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
              <option value="" disabled>-- Select Skill --</option>
              <option v-for="s in skillsCatalog" :key="s.id" :value="s.id">{{ s.name }} ({{ s.code }})</option>
            </select>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Certificate Number</label>
              <Input v-model="newAssignment.certificateNumber" placeholder="e.g. TUV-LAS-2026-0042" class="h-9 font-mono" />
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Validity (Months)</label>
              <Input type="number" v-model="newAssignment.validityMonths" min="1" max="120" class="h-9" />
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Verified By (Auditor / Safety Lead)</label>
            <Input v-model="newAssignment.verifiedBy" placeholder="e.g. Plant Safety Manager" class="h-9" />
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-border">
            <Button type="button" variant="outline" size="sm" @click="isAssignSkillModalOpen = false">Cancel</Button>
            <Button type="submit" size="sm" class="bg-emerald-600 hover:bg-emerald-700 text-white font-bold">Certify Personnel</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- MODAL: CREATE PERSONNEL GROUP                                           -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <Dialog v-model:open="isCreateGroupModalOpen">
      <DialogContent class="sm:max-w-md max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-base font-black">
            <Users class="size-5 text-indigo-600 dark:text-indigo-400" />
            <span>Create Personnel Group / Shift Crew</span>
          </DialogTitle>
        </DialogHeader>

        <form @submit.prevent="handleCreateGroup" class="space-y-4 pt-2 text-xs">
          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Group Name</label>
            <Input v-model="newGroup.name" required placeholder="e.g. Morning Shift Fastening Crew" class="h-9" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Group Code</label>
              <Input v-model="newGroup.code" placeholder="e.g. GRP-MORN-FAST" class="h-9 font-mono" />
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Engineering Discipline</label>
              <select v-model="newGroup.discipline" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option v-for="d in DISCIPLINES" :key="d" :value="d">{{ d }}</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Group Type</label>
              <select v-model="newGroup.groupType" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option v-for="gt in GROUP_TYPES" :key="gt.value" :value="gt.value">{{ gt.label }}</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="font-bold text-foreground">Shift Pattern</label>
              <select v-model="newGroup.shiftPattern" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
                <option v-for="sp in SHIFT_PATTERNS" :key="sp.value" :value="sp.value">{{ sp.label }}</option>
              </select>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Group Lead</label>
            <select v-model="newGroup.leadUserId" class="w-full h-9 rounded-md border border-input bg-card px-3 text-xs">
              <option value="">-- Select Lead --</option>
              <option v-for="u in DEMO_PERSONAS" :key="u.id" :value="u.id">{{ u.name }}</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Group Members (Multi-Select)</label>
            <div class="grid grid-cols-1 gap-1.5 p-3 bg-muted/20 border border-border rounded-xl max-h-36 overflow-y-auto">
              <label
                v-for="u in DEMO_PERSONAS"
                :key="u.id"
                class="flex items-center gap-2 p-1 rounded hover:bg-card cursor-pointer"
              >
                <input
                  type="checkbox"
                  :value="u.id"
                  v-model="newGroup.memberUserIds"
                  class="rounded"
                />
                <span class="font-medium text-foreground text-xs">{{ u.name }}</span>
              </label>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="font-bold text-foreground">Description</label>
            <Input v-model="newGroup.description" placeholder="Purpose and line responsibilities" class="h-9" />
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-border">
            <Button type="button" variant="outline" size="sm" @click="isCreateGroupModalOpen = false">Cancel</Button>
            <Button type="submit" size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white font-bold">Create Group</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>

    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <!-- MODAL: DELEGATION DISPATCH TEST RESOLVER SIMULATOR                      -->
    <!-- ═══════════════════════════════════════════════════════════════════════ -->
    <DelegationTestResolverModal
      :open="isResolverModalOpen"
      :machines="machines"
      @update:open="isResolverModalOpen = $event"
    />
  </div>
</template>
