<script setup lang="ts">
import { ref, onMounted } from 'vue'
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
  Lock
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import DashboardRoleBadge from '~/components/dashboard/RoleBadge.vue'

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
    const { data: allData, error: allErr } = await authClient.admin.listUsers({
      query: { limit: 100 }
    })

    if (allErr) throw allErr

    const foundUser = allData.users.find((u: any) => u.id === userId)
    if (!foundUser) {
      error.value = 'User not found.'
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
  if (!confirm('Revoke this session? The user will be signed out on that device.')) return
  try {
    const { error: revokeError } = await authClient.admin.revokeUserSession({
      userId,
      sessionId: id
    })
    if (revokeError) throw revokeError
    await fetchUserSessions()
  } catch (e: any) {
    alert(e.message || 'Failed to revoke session')
  }
}

async function handleImpersonate() {
  try {
    const { error: impError } = await authClient.admin.impersonateUser({
      userId
    })
    if (impError) throw impError
    navigateTo('/dashboard')
  } catch (e: any) {
    alert(e.message || 'Failed to impersonate')
  }
}

async function handleBan() {
  const reason = prompt('Reason for account suspension:')
  if (reason === null) return
  try {
    const { error: banError } = await authClient.admin.banUser({
      userId,
      banReason: reason
    })
    if (banError) throw banError
    await fetchUserDetails()
  } catch (e: any) {
    alert(e.message || 'Failed to suspend account')
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

onMounted(() => {
  fetchUserDetails()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Breadcrumbs -->
    <nav class="flex items-center gap-2 text-xs font-medium text-slate-400">
      <NuxtLink to="/dashboard/users" class="hover:text-indigo-400 transition-colors">Users</NuxtLink>
      <ChevronRight class="size-3 text-slate-600" />
      <span class="text-slate-200">User Profile</span>
    </nav>

    <!-- Loading State -->
    <div v-if="loading" class="flex flex-col items-center justify-center py-24 gap-3">
      <div class="animate-spin rounded-full size-8 border-b-2 border-indigo-500"></div>
      <p class="text-xs font-medium text-slate-400">Loading user profile...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="bg-rose-500/10 border border-rose-500/20 p-6 rounded-xl text-center">
      <div class="w-10 h-10 bg-rose-500/20 rounded-xl flex items-center justify-center mx-auto mb-3 text-rose-400">
        <AlertTriangle class="size-5" />
      </div>
      <h3 class="text-base font-semibold text-rose-300">Failed to Load User</h3>
      <p class="text-xs text-rose-400/80 mt-1">{{ error }}</p>
      <Button @click="fetchUserDetails" size="sm" class="mt-4 bg-rose-600 hover:bg-rose-700 text-white text-xs font-medium rounded-lg h-8 px-3">
        Try Again
      </Button>
    </div>

    <!-- User Content -->
    <div v-else-if="user" class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Left Column: Profile Card -->
      <div class="lg:col-span-1 space-y-6">
        <div class="bg-slate-900 rounded-xl shadow-sm border border-slate-800 overflow-hidden">
          <div class="h-20 bg-slate-950/80 border-b border-slate-800 flex items-center justify-end px-4">
            <Badge v-if="user.banned" class="bg-rose-500/10 text-rose-400 border border-rose-500/30 text-xs font-medium px-2 py-0.5 rounded-md">
              Suspended
            </Badge>
            <Badge v-else class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-xs font-medium px-2 py-0.5 rounded-md">
              Active
            </Badge>
          </div>
          <div class="px-5 pb-5">
            <div class="-mt-10 mb-3 flex justify-center">
              <div class="size-20 rounded-xl bg-slate-900 p-1 border-2 border-slate-800 shadow-md">
                <div class="w-full h-full rounded-lg bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-2xl font-bold text-indigo-400">
                  {{ (user.name || 'U').charAt(0).toUpperCase() }}
                </div>
              </div>
            </div>

            <div class="text-center">
              <h4 class="text-lg font-bold text-slate-100">{{ user.name }}</h4>
              <p class="text-xs text-slate-400 mt-0.5">@{{ user.username || user.email?.split('@')[0] }}</p>
              <div class="mt-2.5 flex justify-center">
                <DashboardRoleBadge :role="user.role" />
              </div>
            </div>

            <div class="mt-5 space-y-3 border-t border-slate-800 pt-4 text-xs">
              <div class="flex items-center justify-between">
                <span class="text-slate-400 font-medium flex items-center gap-1.5">
                  <Mail class="size-3.5 text-slate-500" />
                  Email
                </span>
                <span class="text-slate-200 font-mono text-xs">{{ user.email }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-slate-400 font-medium flex items-center gap-1.5">
                  <Calendar class="size-3.5 text-slate-500" />
                  Joined
                </span>
                <span class="text-slate-300 font-medium">{{ formatDate(user.createdAt) }}</span>
              </div>
            </div>
          </div>

          <div class="bg-slate-950/60 px-5 py-3 border-t border-slate-800 flex gap-2">
            <Button
              variant="outline"
              size="sm"
              @click="handleImpersonate"
              class="flex-1 border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 text-xs font-medium rounded-lg h-8 transition-colors"
            >
              <UserCheck class="size-3.5 mr-1.5" />
              Impersonate
            </Button>
            <Button
              v-if="!user.banned"
              variant="outline"
              size="sm"
              @click="handleBan"
              class="flex-1 border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 text-xs font-medium rounded-lg h-8 transition-colors"
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
          <div class="bg-slate-900 p-4 rounded-xl border border-slate-800 shadow-sm flex items-center justify-between">
            <div>
              <p class="text-xs font-medium text-slate-400">Active Sessions</p>
              <p class="text-xl font-bold text-slate-100 mt-0.5">{{ sessions.length }}</p>
            </div>
            <div class="p-2 rounded-lg bg-slate-800 text-slate-300">
              <Monitor class="size-4" />
            </div>
          </div>
          <div class="bg-slate-900 p-4 rounded-xl border border-slate-800 shadow-sm flex items-center justify-between">
            <div>
              <p class="text-xs font-medium text-slate-400">Last Activity</p>
              <p class="text-xs font-medium text-slate-200 mt-1">{{ sessions.length > 0 ? formatDate(sessions[0].updatedAt) : 'Never' }}</p>
            </div>
            <div class="p-2 rounded-lg bg-slate-800 text-slate-300">
              <Activity class="size-4" />
            </div>
          </div>
          <div class="bg-slate-900 p-4 rounded-xl border border-slate-800 shadow-sm flex items-center justify-between">
            <div>
              <p class="text-xs font-medium text-slate-400">Organizations</p>
              <p class="text-xl font-bold text-slate-100 mt-0.5">0</p>
            </div>
            <div class="p-2 rounded-lg bg-slate-800 text-slate-300">
              <Building2 class="size-4" />
            </div>
          </div>
        </div>

        <!-- Sessions List -->
        <div class="bg-slate-900 rounded-xl shadow-sm border border-slate-800 overflow-hidden">
          <div class="px-5 py-3.5 border-b border-slate-800 flex justify-between items-center bg-slate-950/60">
            <h4 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Active Device Sessions</h4>
            <Button
              variant="ghost"
              size="sm"
              @click="fetchUserSessions"
              class="text-xs text-indigo-400 hover:text-indigo-300 hover:bg-slate-800 h-7 px-2.5 rounded-md"
            >
              <RefreshCw class="size-3 mr-1" />
              Refresh
            </Button>
          </div>
          <div class="divide-y divide-slate-800/60">
            <div
              v-for="sess in sessions"
              :key="sess.id"
              class="px-5 py-3 flex items-center justify-between hover:bg-slate-800/40 transition-colors"
            >
              <div class="flex items-center gap-3">
                <div class="size-8 rounded-lg bg-slate-800 flex items-center justify-center text-slate-400">
                  <Smartphone v-if="sess.userAgent && (sess.userAgent.includes('iPhone') || sess.userAgent.includes('Android'))" class="size-4" />
                  <Monitor v-else class="size-4" />
                </div>
                <div>
                  <div class="text-xs font-semibold text-slate-200">{{ parseUserAgent(sess.userAgent) }}</div>
                  <div class="text-[11px] text-slate-500 font-mono mt-0.5">{{ sess.ipAddress || 'Unknown IP' }} • Last seen {{ formatDate(sess.updatedAt) }}</div>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                @click="handleRevokeSession(sess.id)"
                class="border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 rounded-lg text-xs font-medium h-7 px-2.5 transition-colors"
              >
                Revoke
              </Button>
            </div>
            <div v-if="sessions.length === 0" class="px-5 py-10 text-center">
              <p class="text-xs text-slate-500 font-medium">No active sessions found for this user.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
