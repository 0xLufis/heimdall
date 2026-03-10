<template>
  <div class="space-y-6">
    <!-- Breadcrumbs -->
    <nav class="flex items-center gap-2 text-xs font-bold text-gray-400 uppercase tracking-widest">
      <NuxtLink to="/dashboard/users" class="hover:text-indigo-600 transition-colors">Users</NuxtLink>
      <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
      </svg>
      <span class="text-gray-900">User Details</span>
    </nav>

    <div v-if="loading" class="flex flex-col items-center justify-center py-24 gap-4">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-600"></div>
        <p class="text-xs font-bold text-gray-400 uppercase tracking-widest">Loading Account Data...</p>
    </div>

    <div v-else-if="error" class="bg-red-50 border border-red-100 p-6 rounded-xl text-center">
        <div class="w-12 h-12 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
        </div>
        <h3 class="text-lg font-bold text-red-900">Failed to load user</h3>
        <p class="text-sm text-red-700 mt-1">{{ error }}</p>
        <button @click="fetchUserDetails" class="mt-4 px-4 py-2 bg-red-600 text-white rounded-lg text-sm font-bold">Try Again</button>
    </div>

    <div v-else-if="user" class="grid grid-cols-1 lg:grid-cols-3 gap-8">
      <!-- Left Column: Profile Card -->
      <div class="lg:col-span-1 space-y-6">
        <div class="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden">
           <div class="h-24 bg-gradient-to-r from-indigo-500 to-purple-600"></div>
           <div class="px-6 pb-6">
              <div class="-mt-12 mb-4">
                 <div class="w-24 h-24 rounded-2xl bg-white p-1 shadow-lg mx-auto">
                    <div class="w-full h-full rounded-xl bg-indigo-100 flex items-center justify-center text-3xl font-black text-indigo-600">
                        {{ user.name.charAt(0).toUpperCase() }}
                    </div>
                 </div>
              </div>
              <div class="text-center">
                 <h4 class="text-xl font-black text-gray-900">{{ user.name }}</h4>
                 <p class="text-sm text-gray-500">@{{ user.username || 'no-username' }}</p>
                 <div class="mt-3 inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider shadow-sm"
                   :class="user.role === 'system_admin' ? 'bg-indigo-600 text-white' : 'bg-gray-100 text-gray-700'"
                 >
                    {{ user.role || 'user' }}
                 </div>
              </div>

              <div class="mt-8 space-y-4 border-t border-gray-50 pt-6">
                 <div class="flex items-center justify-between">
                    <span class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Email</span>
                    <span class="text-sm font-medium text-gray-900">{{ user.email }}</span>
                 </div>
                 <div class="flex items-center justify-between">
                    <span class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Status</span>
                    <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-bold uppercase"
                      :class="user.banned ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'"
                    >
                        {{ user.banned ? 'Banned' : 'Active' }}
                    </span>
                 </div>
                 <div class="flex items-center justify-between">
                    <span class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Joined</span>
                    <span class="text-sm font-medium text-gray-900">{{ formatDate(user.createdAt) }}</span>
                 </div>
              </div>
           </div>
           <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex gap-2">
              <button @click="handleImpersonate" class="flex-grow py-2 px-4 bg-white border border-gray-200 rounded-lg text-xs font-bold text-gray-700 hover:bg-gray-100 transition-all shadow-sm">
                Impersonate
              </button>
              <button v-if="!user.banned" @click="handleBan" class="flex-grow py-2 px-4 bg-red-50 border border-red-100 rounded-lg text-xs font-bold text-red-600 hover:bg-red-100 transition-all shadow-sm">
                Ban
              </button>
           </div>
        </div>
      </div>

      <!-- Right Column: Details & Sessions -->
      <div class="lg:col-span-2 space-y-8">
        <!-- Account Statistics -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
                <p class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Active Sessions</p>
                <p class="text-2xl font-black text-gray-900 mt-1">{{ sessions.length }}</p>
            </div>
            <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
                <p class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Last Activity</p>
                <p class="text-sm font-bold text-gray-900 mt-2">{{ sessions.length > 0 ? formatDate(sessions[0].updatedAt) : 'Never' }}</p>
            </div>
            <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
                <p class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Organizations</p>
                <p class="text-2xl font-black text-gray-900 mt-1">0</p>
            </div>
        </div>

        <!-- Sessions List -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden">
            <div class="px-6 py-4 border-b border-gray-100 flex justify-between items-center bg-gray-50/50">
                <h4 class="text-sm font-black text-gray-900 uppercase tracking-wider">Active Sessions</h4>
                <button @click="fetchUserSessions" class="text-xs font-bold text-indigo-600 hover:text-indigo-800">Refresh</button>
            </div>
            <div class="divide-y divide-gray-100">
                <div v-for="sess in sessions" :key="sess.id" class="px-6 py-4 flex items-center justify-between hover:bg-gray-50 transition-colors">
                    <div class="flex items-center gap-4">
                        <div class="w-10 h-10 rounded-xl bg-gray-100 flex items-center justify-center text-gray-400">
                            <svg v-if="sess.userAgent.toLowerCase().includes('windows')" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 24 24" fill="currentColor"><path d="M0 3.449L9.75 2.1V11.7h-9.75V3.449zm0 9.15h9.75v9.6L0 20.551V12.599zm10.65-10.8L24 0v11.7h-13.35V1.799zM24 12.599V24l-13.35-1.95V12.599H24z"/></svg>
                            <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg>
                        </div>
                        <div>
                            <div class="text-sm font-bold text-gray-900">{{ parseUserAgent(sess.userAgent) }}</div>
                            <div class="text-[10px] text-gray-400 font-mono mt-0.5">{{ sess.ipAddress || 'Unknown IP' }} • Last seen {{ formatDate(sess.updatedAt) }}</div>
                        </div>
                    </div>
                    <button @click="handleRevokeSession(sess.id)" class="px-3 py-1.5 border border-red-100 bg-red-50 text-red-600 rounded-lg text-[10px] font-black uppercase tracking-wider hover:bg-red-100 transition-all">
                        Revoke
                    </button>
                </div>
                <div v-if="sessions.length === 0" class="px-6 py-12 text-center">
                    <p class="text-sm text-gray-400">No active sessions found for this user.</p>
                </div>
            </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"

definePageMeta({
  layout: 'dashboard'
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
      // Note: admin.getUser is the method for admin plugin
      const { data, error: userError } = await authClient.admin.listUsers({
          query: {
              limit: 1,
              // Better Auth V1 doesn't have direct getUserById in admin yet, 
              // we can filter listUsers or use base API if we have permissions
          }
      })
      
      // Since listUsers with limit 1 is just a workaround, let's try to find 
      // the user in a larger list or hope for a dedicated endpoint.
      // Better way: listUsers with a filter if supported, or fetch all and find.
      // For now, let's fetch all (up to 100) and find the specific one.
      const { data: allData, error: allErr } = await authClient.admin.listUsers({
          query: { limit: 100 }
      })
      
      if (allErr) throw allErr
      
      const foundUser = allData.users.find((u: any) => u.id === userId)
      if (!foundUser) {
          error.value = "User not found."
          return
      }
      
      user.value = foundUser
      await fetchUserSessions()
  } catch (e: any) {
      error.value = e.message || "An unexpected error occurred."
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
        sessions.value = data.sessions
    } catch (e) {
        console.error("Failed to fetch sessions", e)
    }
}

async function handleRevokeSession(id: string) {
    if (!confirm("Revoke this session? The user will be signed out on that device.")) return
    try {
        const { error: revokeError } = await authClient.admin.revokeUserSession({
            userId,
            sessionId: id
        })
        if (revokeError) throw revokeError
        await fetchUserSessions()
    } catch (e: any) {
        alert(e.message || "Failed to revoke session")
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
        alert(e.message || "Failed to impersonate")
    }
}

async function handleBan() {
    const reason = prompt("Reason for ban:")
    if (reason === null) return
    try {
        const { error: banError } = await authClient.admin.banUser({
            userId,
            banReason: reason
        })
        if (banError) throw banError
        await fetchUserDetails()
    } catch (e: any) {
        alert(e.message || "Failed to ban")
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
    if (!ua) return "Unknown Device"
    if (ua.includes("Windows")) return "Windows Desktop"
    if (ua.includes("Macintosh")) return "macOS Desktop"
    if (ua.includes("iPhone")) return "iPhone"
    if (ua.includes("Android")) return "Android Device"
    return "Browser Session"
}

onMounted(() => {
  fetchUserDetails()
})
</script>
