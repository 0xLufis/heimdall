<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { toast } from 'vue-sonner'
import { authClient } from '~/utils/auth-client'
import {
  ChevronRight,
  AlertTriangle,
  Monitor,
  Smartphone,
  RefreshCw,
  Ban,
  UserCheck,
  ShieldCheck,
  Calendar,
  Mail,
  Activity,
  Building2,
  Lock,
  Cpu,
  Layers,
  Sparkles,
  Trash2,
  Plus
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import DashboardRoleBadge from '~/components/dashboard/RoleBadge.vue'
import { useDedications } from '~/composables/useDedications'
import type { UserDedications } from '~/types/maintenance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const userId = route.params.id as string

const loading = ref(true)
const error = ref('')
const user = ref<any>(null)
const sessions = ref<any[]>([])

async function fetchUserDetails() {
  loading.value = true
  error.value = ''
  try {
    let foundUser: any = null

    // 1. Try resilient BFF endpoint /api/users
    try {
      const res = await $fetch<{ success: boolean; users: any[] }>('/api/users')
      if (res?.users) {
        foundUser = res.users.find((u: any) => u.id === userId || u.username === userId || u.email === userId)
      }
    } catch {}

    // 2. Try authClient admin listUsers if not found yet
    if (!foundUser) {
      try {
        const { data: allData, error: allErr } = await authClient.admin.listUsers({
          query: { limit: 100 }
        })
        if (!allErr && allData?.users) {
          foundUser = allData.users.find((u: any) => u.id === userId || u.username === userId)
        }
      } catch {}
    }

    if (!foundUser) {
      error.value = 'User profile was not found in the directory.'
      return
    }

    user.value = foundUser
    await fetchUserSessions()
  } catch (e: any) {
    error.value = e.message || 'An unexpected error occurred.'
  } finally {
    loading.value = false
  }
}

async function fetchUserSessions() {
  try {
    const { data, error: sessError } = await authClient.admin.listUserSessions({
      userId
    })
    if (sessError) throw sessError
    sessions.value = data.sessions || []
  } catch (e) {
    console.error('Failed to fetch sessions', e)
  }
}

async function handleRevokeSession(id: string) {
  try {
    const { error: revokeError } = await authClient.admin.revokeUserSession({
      userId,
      sessionId: id
    })
    if (revokeError) throw revokeError
    toast.success('User session revoked successfully')
    await fetchUserSessions()
  } catch (e: any) {
    toast.error(e.message || 'Failed to revoke session')
  }
}

async function handleImpersonate() {
  try {
    const { error: impError } = await authClient.admin.impersonateUser({
      userId
    })
    if (impError) throw impError
    toast.info('Starting user impersonation session...')
    navigateTo('/dashboard')
  } catch (e: any) {
    toast.error(e.message || 'Failed to impersonate user')
  }
}

async function handleBan() {
  const reason = 'Administrative suspension via User Profile Console'
  try {
    const { error: banError } = await authClient.admin.banUser({
      userId,
      banReason: reason
    })
    if (banError) throw banError
    toast.success('Account suspended successfully')
    await fetchUserDetails()
  } catch (e: any) {
    toast.error(e.message || 'Failed to suspend account')
  }
}

function formatDate(date: string | Date) {
  return new Date(date).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function parseUserAgent(ua: string) {
  if (!ua) return 'Unknown Device'
  if (ua.includes('Windows')) return 'Windows Desktop'
  if (ua.includes('Macintosh')) return 'macOS Desktop'
  if (ua.includes('iPhone')) return 'iPhone Mobile'
  if (ua.includes('Android')) return 'Android Device'
  return 'Browser Session'
}

// User Dedications state
const { fetchUserDedications, dedicateUserToMachine, removeUserDedicationFromMachine } = useDedications()
const userDedications = ref<UserDedications | null>(null)
const dedicationsLoading = ref(false)
const availableMachines = ref<any[]>([])
const selectedMachineToDedicate = ref('')
const selectedDedicationTechnology = ref('General')
const isDedicatingMachine = ref(false)

async function fetchUserDedicationsData() {
  dedicationsLoading.value = true
  try {
    const [deds, machines] = await Promise.all([
      fetchUserDedications(userId),
      $fetch<any[]>('/api/machines').catch(() => [])
    ])
    userDedications.value = deds
    availableMachines.value = machines || []
  } catch (err: any) {
    console.error('Failed to load user dedications', err)
  } finally {
    dedicationsLoading.value = false
  }
}

async function handleDedicateToMachine() {
  if (!selectedMachineToDedicate.value) return
  isDedicatingMachine.value = true
  try {
    const success = await dedicateUserToMachine(userId, selectedMachineToDedicate.value, selectedDedicationTechnology.value)
    if (success) {
      toast.success(`User dedicated to machine for ${selectedDedicationTechnology.value} successfully`)
      selectedMachineToDedicate.value = ''
      await fetchUserDedicationsData()
    } else {
      toast.error('Failed to dedicate user to machine')
    }
  } catch (err: any) {
    toast.error(err?.message || 'Failed to dedicate user')
  } finally {
    isDedicatingMachine.value = false
  }
}

async function handleRemoveUserDedication(machineId: string, technology?: string) {
  dedicationsLoading.value = true
  try {
    const success = await removeUserDedicationFromMachine(userId, machineId, technology)
    if (success) {
      toast.success('Machine dedication removed')
      await fetchUserDedicationsData()
    } else {
      toast.error('Failed to remove dedication')
    }
  } catch (err: any) {
    toast.error(err?.message || 'Failed to remove dedication')
  } finally {
    dedicationsLoading.value = false
  }
}

onMounted(() => {
  fetchUserDetails()
  fetchUserDedicationsData()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Breadcrumbs -->
    <nav class="flex items-center gap-2 text-xs font-medium text-muted-foreground">
      <NuxtLink to="/dashboard/users" class="hover:text-indigo-600 dark:hover:text-indigo-400 transition-colors">Users</NuxtLink>
      <ChevronRight class="size-3 text-muted-foreground/60" />
      <span class="text-foreground">User Profile</span>
    </nav>

    <!-- Loading State -->
    <div v-if="loading" class="flex flex-col items-center justify-center py-24 gap-3">
      <div class="animate-spin rounded-full size-8 border-b-2 border-indigo-500"></div>
      <p class="text-xs font-medium text-muted-foreground">Loading user profile...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="bg-rose-500/10 border border-rose-500/20 p-6 rounded-xl text-center max-w-lg mx-auto">
      <div class="w-10 h-10 bg-rose-500/20 rounded-xl flex items-center justify-center mx-auto mb-3 text-rose-600 dark:text-rose-400">
        <AlertTriangle class="size-5" />
      </div>
      <h3 class="text-base font-semibold text-rose-700 dark:text-rose-300">User Profile Unavailable</h3>
      <p class="text-xs text-rose-600/80 dark:text-rose-400/80 mt-1">{{ error }}</p>
      <div class="mt-4 flex items-center justify-center gap-2">
        <Button @click="fetchUserDetails" size="sm" class="bg-rose-600 hover:bg-rose-700 text-white text-xs font-medium rounded-lg h-8 px-3 cursor-pointer">
          Try Again
        </Button>
        <NuxtLink to="/dashboard/users">
          <Button variant="outline" size="sm" class="border-border text-foreground hover:bg-muted text-xs font-medium rounded-lg h-8 px-3 cursor-pointer">
            Back to User Directory
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- User Content -->
    <div v-else-if="user" class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Left Column: Profile Card -->
      <div class="lg:col-span-1 space-y-6">
        <div class="bg-card rounded-xl shadow-sm border border-border overflow-hidden">
          <div class="h-20 bg-muted/40 border-b border-border flex items-center justify-end px-4">
            <Badge v-if="user.banned" class="bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/30 text-xs font-medium px-2 py-0.5 rounded-md">
              Suspended
            </Badge>
            <Badge v-else class="bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30 text-xs font-medium px-2 py-0.5 rounded-md">
              Active
            </Badge>
          </div>
          <div class="px-5 pb-5">
            <div class="-mt-10 mb-3 flex justify-center">
              <div class="size-20 rounded-xl bg-card p-1 border-2 border-border shadow-md">
                <div class="w-full h-full rounded-lg bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-2xl font-bold text-indigo-600 dark:text-indigo-400">
                  {{ (user.name || 'U').charAt(0).toUpperCase() }}
                </div>
              </div>
            </div>

            <div class="text-center">
              <h4 class="text-lg font-bold text-foreground">{{ user.name }}</h4>
              <p class="text-xs text-muted-foreground mt-0.5">@{{ user.username || user.email?.split('@')[0] }}</p>
              <div class="mt-2.5 flex justify-center">
                <DashboardRoleBadge :role="user.role" />
              </div>
            </div>

            <div class="mt-5 space-y-3 border-t border-border pt-4 text-xs">
              <div class="flex items-center justify-between">
                <span class="text-muted-foreground font-medium flex items-center gap-1.5">
                  <Mail class="size-3.5 text-muted-foreground" />
                  Email
                </span>
                <span class="text-foreground font-mono text-xs">{{ user.email }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-muted-foreground font-medium flex items-center gap-1.5">
                  <Calendar class="size-3.5 text-muted-foreground" />
                  Joined
                </span>
                <span class="text-foreground font-medium">{{ formatDate(user.createdAt) }}</span>
              </div>
            </div>
          </div>

          <div class="bg-muted/40 px-5 py-3 border-t border-border flex gap-2">
            <Button
              variant="outline"
              size="sm"
              @click="handleImpersonate"
              class="flex-1 border-border bg-card hover:bg-muted text-foreground text-xs font-medium rounded-lg h-8 transition-colors"
            >
              <UserCheck class="size-3.5 mr-1.5" />
              Impersonate
            </Button>
            <Button
              v-if="!user.banned"
              variant="outline"
              size="sm"
              @click="handleBan"
              class="flex-1 border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 text-rose-600 dark:text-rose-400 text-xs font-medium rounded-lg h-8 transition-colors"
            >
              <Ban class="size-3.5 mr-1.5" />
              Suspend
            </Button>
          </div>
        </div>
      </div>

      <!-- Right Column: Details & Sessions -->
      <div class="lg:col-span-2 space-y-6">
        <!-- Account Statistics -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div class="bg-card p-4 rounded-xl border border-border shadow-sm flex items-center justify-between">
            <div>
              <p class="text-xs font-medium text-muted-foreground">Active Sessions</p>
              <p class="text-xl font-bold text-foreground mt-0.5">{{ sessions.length }}</p>
            </div>
            <div class="p-2 rounded-lg bg-muted text-foreground">
              <Monitor class="size-4" />
            </div>
          </div>
          <div class="bg-card p-4 rounded-xl border border-border shadow-sm flex items-center justify-between">
            <div>
              <p class="text-xs font-medium text-muted-foreground">Last Activity</p>
              <p class="text-xs font-medium text-foreground mt-1">{{ sessions.length > 0 ? formatDate(sessions[0].updatedAt) : 'Never' }}</p>
            </div>
            <div class="p-2 rounded-lg bg-muted text-foreground">
              <Activity class="size-4" />
            </div>
          </div>
          <div class="bg-card p-4 rounded-xl border border-border shadow-sm flex items-center justify-between">
            <div>
              <p class="text-xs font-medium text-muted-foreground">Organizations</p>
              <p class="text-xl font-bold text-foreground mt-0.5">0</p>
            </div>
            <div class="p-2 rounded-lg bg-muted text-foreground">
              <Building2 class="size-4" />
            </div>
          </div>
        </div>

        <!-- Machine Dedications & Technology Coverage Card -->
        <div class="bg-card rounded-xl shadow-sm border border-border overflow-hidden space-y-4 p-5">
          <div class="flex items-center justify-between border-b border-border pb-3.5">
            <div class="flex items-center gap-2">
              <Cpu class="size-4 text-indigo-600 dark:text-indigo-400" />
              <h4 class="text-xs font-semibold text-foreground uppercase tracking-wider">
                Machine Dedications & Coverage
              </h4>
            </div>
            <Badge variant="outline" class="text-[10px] uppercase font-mono text-indigo-600 dark:text-indigo-400 border-indigo-500/30 bg-indigo-500/10">
              {{ (userDedications?.directlyDedicatedMachines?.length || 0) + (userDedications?.dedicatedMachineGroups?.length || 0) }} Dedicated Targets
            </Badge>
          </div>

          <!-- Directly Dedicated Machines List -->
          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground">Directly Dedicated Machines</span>
              <span class="text-[11px] text-muted-foreground">{{ userDedications?.directlyDedicatedMachines?.length || 0 }} machines</span>
            </div>

            <div v-if="userDedications?.directlyDedicatedMachines?.length" class="space-y-2">
              <div
                v-for="mach in userDedications.directlyDedicatedMachines"
                :key="`${mach.machineId}_${mach.technology || 'General'}`"
                class="p-3 rounded-lg bg-muted/40 border border-border flex items-center justify-between"
              >
                <div class="space-y-1">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="text-xs font-semibold text-foreground">{{ mach.machineName }}</span>
                    <Badge variant="outline" class="text-[9px] font-mono border-border">
                      {{ mach.customIdentifier || mach.machineType || 'Machine' }}
                    </Badge>
                    <Badge
                      variant="outline"
                      class="text-[9px] font-mono px-1.5 py-0 uppercase"
                      :class="mach.technology === 'Vision' ? 'border-violet-500/40 text-violet-500 bg-violet-500/10' :
                              mach.technology === 'Controls' ? 'border-sky-500/40 text-sky-500 bg-sky-500/10' :
                              mach.technology === 'Mechanical' ? 'border-amber-500/40 text-amber-500 bg-amber-500/10' :
                              'border-emerald-500/30 text-emerald-500 bg-emerald-500/10'"
                    >
                      {{ mach.technology || 'General' }}
                    </Badge>
                    <Badge
                      v-if="mach.supersedesTechnologicalGroup"
                      class="text-[9px] bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/30 px-1.5 py-0"
                    >
                      ⚡ Supersedes {{ mach.supersededGroupName }}
                    </Badge>
                  </div>
                  <p class="text-[11px] text-muted-foreground">
                    Physical Line: <span class="text-foreground font-medium">{{ mach.groupName || 'Primary Line' }}</span>
                  </p>
                </div>

                <Button
                  variant="outline"
                  size="sm"
                  @click="handleRemoveUserDedication(mach.machineId, mach.technology)"
                  class="h-7 px-2.5 text-xs text-rose-600 dark:text-rose-400 border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 rounded-lg cursor-pointer"
                >
                  <Trash2 class="size-3 mr-1" />
                  Unassign
                </Button>
              </div>
            </div>

            <div v-else class="p-4 text-center rounded-lg bg-muted/20 border border-dashed border-border text-xs text-muted-foreground">
              This user is not directly dedicated to any individual machines.
            </div>
          </div>

          <!-- Vice-Versa Selector: Dedicate User to Additional Machine -->
          <div class="p-3.5 rounded-lg bg-muted/20 border border-border space-y-2.5">
            <span class="text-[11px] font-semibold text-foreground uppercase tracking-wider flex items-center gap-1.5">
              <Plus class="size-3.5 text-indigo-500" />
              Dedicate User to a Machine
            </span>
            <div class="flex flex-wrap items-center gap-2">
              <select
                v-model="selectedMachineToDedicate"
                class="flex-1 min-w-[180px] h-8 px-2.5 text-xs rounded-md bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary cursor-pointer"
              >
                <option value="" disabled>Select machine to dedicate this user to...</option>
                <option v-for="m in availableMachines" :key="m.id" :value="m.id">
                  {{ m.name }} ({{ m.customIdentifier || m.machineType || 'Station' }})
                </option>
              </select>
              <select
                v-model="selectedDedicationTechnology"
                class="w-36 h-8 px-2.5 text-xs rounded-md bg-background border border-border text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary cursor-pointer"
              >
                <option value="General">General Support</option>
                <option value="Controls">Controls</option>
                <option value="Mechanical">Mechanical</option>
                <option value="Vision">Vision</option>
                <option value="Robotics">Robotics</option>
                <option value="Laser">Laser</option>
                <option value="Welding">Welding</option>
              </select>
              <Button
                size="sm"
                @click="handleDedicateToMachine"
                :disabled="!selectedMachineToDedicate || isDedicatingMachine"
                class="h-8 px-3 text-xs font-semibold bg-primary text-primary-foreground hover:bg-primary/90 cursor-pointer"
              >
                <RefreshCw v-if="isDedicatingMachine" class="size-3 mr-1 animate-spin" />
                <Plus v-else class="size-3 mr-1" />
                Dedicate ({{ selectedDedicationTechnology }})
              </Button>
            </div>
          </div>

          <!-- Dedicated Machine Groups (if any) -->
          <div v-if="userDedications?.dedicatedMachineGroups?.length" class="space-y-2 pt-2 border-t border-border">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground flex items-center gap-1.5">
                <Layers class="size-3.5 text-primary" />
                Assigned Machine Groups (Lead Engineer)
              </span>
            </div>
            <div class="space-y-1.5">
              <div
                v-for="grp in userDedications.dedicatedMachineGroups"
                :key="grp.groupId"
                class="p-2.5 rounded-lg bg-muted/30 border border-border flex items-center justify-between text-xs"
              >
                <div>
                  <span class="font-semibold text-foreground">{{ grp.groupName }}</span>
                  <Badge variant="outline" class="ml-2 text-[9px] uppercase font-mono">
                    {{ grp.groupType }}
                  </Badge>
                </div>
                <span class="text-[11px] text-muted-foreground">{{ grp.machineCount }} child machines</span>
              </div>
            </div>
          </div>

          <!-- Technological Group Coverage -->
          <div v-if="userDedications?.technologicalCoverage?.length" class="space-y-2.5 pt-2 border-t border-border">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground flex items-center gap-1.5">
                <Sparkles class="size-3.5 text-indigo-500" />
                Technological Team Coverage
              </span>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Machines possessing technology maintained by this user's technological groups.
            </p>

            <div
              v-for="cov in userDedications.technologicalCoverage"
              :key="cov.groupId"
              class="p-3 rounded-lg bg-indigo-500/5 border border-indigo-500/20 space-y-2 text-xs"
            >
              <div class="flex items-center justify-between">
                <span class="font-bold text-foreground">{{ cov.groupName }}</span>
                <Badge class="text-[9px] font-mono bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border-indigo-500/30">
                  {{ cov.technology }}
                </Badge>
              </div>

              <div v-if="cov.coveredMachines.length" class="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                <div
                  v-for="cm in cov.coveredMachines"
                  :key="cm.machineId"
                  class="p-2 rounded-md bg-card border border-border flex items-center justify-between text-[11px]"
                >
                  <span class="font-medium text-foreground truncate mr-2">{{ cm.machineName }}</span>
                  <Badge
                    v-if="cm.supersedesTechnologicalGroup"
                    class="text-[8px] bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30 shrink-0"
                  >
                    ⚡ Superseded by You
                  </Badge>
                  <span v-else class="text-[10px] text-emerald-600 dark:text-emerald-400 shrink-0">
                    Auto-Covered
                  </span>
                </div>
              </div>
              <div v-else class="text-[11px] text-muted-foreground italic">
                No active stations found requiring this technology.
              </div>
            </div>
          </div>
        </div>

        <!-- Sessions List -->
        <div class="bg-card rounded-xl shadow-sm border border-border overflow-hidden">
          <div class="px-5 py-3.5 border-b border-border flex justify-between items-center bg-muted/40">
            <h4 class="text-xs font-semibold text-foreground uppercase tracking-wider">Active Device Sessions</h4>
            <Button
              variant="ghost"
              size="sm"
              @click="fetchUserSessions"
              class="text-xs text-indigo-600 dark:text-indigo-400 hover:text-indigo-700 dark:hover:text-indigo-300 hover:bg-muted h-7 px-2.5 rounded-md"
            >
              <RefreshCw class="size-3 mr-1" />
              Refresh
            </Button>
          </div>
          <div class="divide-y divide-border">
            <div
              v-for="sess in sessions"
              :key="sess.id"
              class="px-5 py-3 flex items-center justify-between hover:bg-muted/50 transition-colors"
            >
              <div class="flex items-center gap-3">
                <div class="size-8 rounded-lg bg-muted flex items-center justify-center text-muted-foreground">
                  <Smartphone v-if="sess.userAgent && (sess.userAgent.includes('iPhone') || sess.userAgent.includes('Android'))" class="size-4" />
                  <Monitor v-else class="size-4" />
                </div>
                <div>
                  <div class="text-xs font-semibold text-foreground">{{ parseUserAgent(sess.userAgent) }}</div>
                  <div class="text-[11px] text-muted-foreground font-mono mt-0.5">{{ sess.ipAddress || 'Unknown IP' }} • Last seen {{ formatDate(sess.updatedAt) }}</div>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                @click="handleRevokeSession(sess.id)"
                class="border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 text-rose-600 dark:text-rose-400 rounded-lg text-xs font-medium h-7 px-2.5 transition-colors"
              >
                Revoke
              </Button>
            </div>
            <div v-if="sessions.length === 0" class="px-5 py-10 text-center">
              <p class="text-xs text-muted-foreground font-medium">No active sessions found for this user.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
