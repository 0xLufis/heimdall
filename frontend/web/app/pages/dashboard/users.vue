<script setup lang="ts">
import { authClient } from "~/utils/auth-client"
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { RefreshCcwIcon, UserPlusIcon, SearchIcon, ShieldAlertIcon, FingerprintIcon, Users } from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const loading = ref(false)
const error = ref('')
const users = ref<any[]>([])
const searchQuery = ref('')
const roleFilter = ref('all')
const statusFilter = ref('all')

const { canManageUsers } = useAuthSession()

const availableRoles = [
  "system_admin",
  "heimdall_admin",
  "it_admin",
  "engineering_admin",
  "admin",
  "manager",
  "group_leader",
  "shift_leader",
  "team_lead",
  "engineer",
  "controls_engineer",
  "lead_engineer",
  "technician",
  "generic",
  "user"
]

const filteredUsers = computed(() => {
  let result = users.value
  
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    result = result.filter(u => 
      u.email.toLowerCase().includes(q) || 
      u.name.toLowerCase().includes(q) ||
      (u.username && u.username.toLowerCase().includes(q))
    )
  }
  
  if (roleFilter.value && roleFilter.value !== 'all') {
    result = result.filter(u => u.role === roleFilter.value)
  }
  
  if (statusFilter.value && statusFilter.value !== 'all') {
    result = result.filter(u => statusFilter.value === 'banned' ? u.banned : !u.banned)
  }
  
  return result
})

async function fetchUsers() {
  loading.value = true
  error.value = ''
  try {
    const res = await $fetch<{ success: boolean; users: any[] }>('/api/users')
    if (res && res.success && res.users && res.users.length > 0) {
      users.value = res.users
    } else {
      // Auto-seed in dev mode if empty
      await $fetch('/api/dev/seed-admin')
      const seededRes = await $fetch<{ success: boolean; users: any[] }>('/api/users')
      if (seededRes && seededRes.users) {
        users.value = seededRes.users
      }
    }
  } catch (e: any) {
    error.value = 'Failed to load user directory.'
  } finally {
    loading.value = false
  }
}

async function handleRoleChange(userId: string, newRole: string) {
  try {
    const { error: roleError } = await authClient.admin.setRole({
      userId,
      role: newRole
    })
    if (roleError) {
      alert(roleError.message)
    } else {
      await fetchUsers() 
    }
  } catch (e) {
    alert('Failed to update role')
  }
}

async function handleBanUser(userId: string) {
  const reason = prompt('Please provide a reason for the ban:')
  if (reason === null) return
  
  try {
    const { error: banError } = await authClient.admin.banUser({
      userId,
      banReason: reason
    })
    if (banError) {
      alert(banError.message)
    } else {
      await fetchUsers()
    }
  } catch (e) {
    alert('Failed to ban user')
  }
}

async function handleUnbanUser(userId: string) {
  if (!confirm('Are you sure you want to unban this user?')) return
  try {
    const { error: unbanError } = await authClient.admin.unbanUser({
      userId
    })
    if (unbanError) {
      alert(unbanError.message)
    } else {
      await fetchUsers()
    }
  } catch (e) {
    alert('Failed to unban user')
  }
}

onMounted(() => {
  fetchUsers()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header with Actions -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
          <Users class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-100">User Directory & Access Governance</h1>
          <p class="text-sm text-slate-400 mt-0.5">System access control, RBAC policy assignments, and directory audit</p>
        </div>
      </div>
      <div class="flex items-center gap-2.5">
        <Button variant="outline" size="sm" @click="fetchUsers" class="gap-1.5 border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-lg h-8 text-xs font-medium transition-colors">
          <RefreshCcwIcon class="size-3.5" :class="{ 'animate-spin': loading }" />
          <span>Refresh</span>
        </Button>
        <NuxtLink to="/admin/studio" target="_blank">
          <Button variant="outline" size="sm" class="gap-1.5 border-slate-800 bg-slate-900 hover:bg-slate-800 text-indigo-400 hover:text-indigo-300 rounded-lg h-8 text-xs font-medium transition-colors">
            <FingerprintIcon class="size-3.5" />
            <span>Identity Studio</span>
          </Button>
        </NuxtLink>
        <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white gap-1.5 rounded-lg h-8 px-3.5 text-xs font-medium shadow-sm transition-colors">
          <UserPlusIcon class="size-3.5" />
          <span>Invite User</span>
        </Button>
      </div>
    </div>

    <!-- Filters & Search -->
    <Card class="border-slate-800 bg-slate-900 rounded-xl shadow-sm overflow-hidden">
       <CardContent class="p-4 flex flex-col md:flex-row gap-3 items-center">
          <div class="relative flex-grow w-full md:w-auto group">
              <SearchIcon class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-500 group-focus-within:text-indigo-400 transition-colors z-10" />
              <Input 
                v-model="searchQuery" 
                placeholder="Search by name, email, or identifier..." 
                class="w-full pl-9 pr-3 h-8 bg-slate-950 border-slate-800 rounded-lg text-xs text-slate-200" 
              />
          </div>
          <div class="flex items-center gap-2 w-full md:w-auto">
              <Select v-model="roleFilter">
                <SelectTrigger class="w-full md:w-40 h-8 bg-slate-950 border-slate-800 rounded-lg font-medium text-xs text-slate-300">
                  <SelectValue placeholder="All Roles" />
                </SelectTrigger>
                <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                  <SelectItem value="all" class="text-xs">All Roles</SelectItem>
                  <SelectItem v-for="role in availableRoles" :key="role" :value="role" class="text-xs">
                    {{ role }}
                  </SelectItem>
                </SelectContent>
              </Select>

              <Select v-model="statusFilter">
                <SelectTrigger class="w-full md:w-40 h-8 bg-slate-950 border-slate-800 rounded-lg font-medium text-xs text-slate-300">
                  <SelectValue placeholder="All Statuses" />
                </SelectTrigger>
                <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                  <SelectItem value="all" class="text-xs">All Statuses</SelectItem>
                  <SelectItem value="active" class="text-xs">Active</SelectItem>
                  <SelectItem value="banned" class="text-xs">Banned</SelectItem>
                </SelectContent>
              </Select>
          </div>
       </CardContent>
    </Card>

    <!-- Error State -->
    <div v-if="error" class="bg-rose-500/10 border border-rose-500/50 p-4 rounded-xl flex items-start gap-3 shadow-lg">
      <ShieldAlertIcon class="h-5 w-5 text-rose-500 mt-0.5" />
      <div>
        <p class="text-[10px] font-black text-rose-500 uppercase tracking-widest leading-none">Authorization Error</p>
        <p class="text-xs text-rose-200 mt-1 font-medium opacity-80">{{ error }}</p>
      </div>
    </div>

    <!-- User Table Component -->
    <DashboardUserTable 
      :users="filteredUsers" 
      :roles="availableRoles" 
      :loading="loading"
      @update-role="handleRoleChange"
      @ban-user="handleBanUser"
      @unban-user="handleUnbanUser"
    />
  </div>
</template>
