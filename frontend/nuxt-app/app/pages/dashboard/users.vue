<script setup lang="ts">
import { authClient } from "~/utils/auth-client"
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { RefreshCcwIcon, UserPlusIcon, SearchIcon, ShieldAlertIcon } from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const loading = ref(false)
const error = ref('')
const users = ref<any[]>([])
const searchQuery = ref('')
const roleFilter = ref('')
const statusFilter = ref('')

const availableRoles = [
  "system_admin",
  "admin",
  "manager",
  "team_lead",
  "engineer",
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
  
  if (roleFilter.value) {
    result = result.filter(u => u.role === roleFilter.value)
  }
  
  if (statusFilter.value) {
    result = result.filter(u => statusFilter.value === 'banned' ? u.banned : !u.banned)
  }
  
  return result
})

async function fetchUsers() {
  loading.value = true
  error.value = ''
  try {
    const { data, error: fetchError } = await authClient.admin.listUsers({
        query: {
            limit: 100
        }
    })
    if (fetchError) {
      error.value = fetchError.message || 'Failed to fetch users. Ensure you have admin privileges.'
    } else {
      users.value = data.users
    }
  } catch (e: any) {
    error.value = 'An unexpected error occurred while fetching users.'
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
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">User Management</h3>
        <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">System Access Control & Audit</p>
      </div>
      <div class="flex items-center gap-3">
        <Button variant="outline" @click="fetchUsers" class="gap-2 border-slate-800 bg-slate-900 text-slate-400 hover:bg-slate-800 hover:text-slate-100 rounded-xl h-11">
          <RefreshCcwIcon class="h-4 w-4" :class="{ 'animate-spin': loading }" />
          <span class="text-[10px] font-black uppercase tracking-widest">Refresh</span>
        </Button>
        <Button class="bg-indigo-600 hover:bg-indigo-700 text-white gap-2 shadow-lg shadow-indigo-500/20 rounded-xl h-11 px-6">
          <UserPlusIcon class="h-4 w-4" />
          <span class="text-[10px] font-black uppercase tracking-widest">Invite User</span>
        </Button>
      </div>
    </div>

    <!-- Filters & Search -->
    <Card class="border-slate-800 bg-slate-900/50 shadow-sm overflow-hidden">
       <CardContent class="p-6 flex flex-col md:flex-row gap-4 items-center">
          <div class="relative flex-grow w-full md:w-auto group">
              <SearchIcon class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500 group-focus-within:text-indigo-400 transition-colors z-10" />
              <Input 
                v-model="searchQuery" 
                placeholder="Search by name, email, or identifier..." 
                class="w-full pl-10 pr-3 h-12 bg-slate-950 border-slate-800 rounded-xl focus:ring-4 focus:ring-indigo-500/10 transition-all font-bold text-xs text-slate-200" 
              />
          </div>
          <div class="flex items-center gap-2 w-full md:w-auto">
              <Select v-model="roleFilter">
                <SelectTrigger class="w-full md:w-40 h-12 bg-slate-950 border-slate-800 rounded-xl font-bold text-xs uppercase tracking-widest text-slate-400">
                  <SelectValue placeholder="All Roles" />
                </SelectTrigger>
                <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                  <SelectItem value="">All Roles</SelectItem>
                  <SelectItem v-for="role in availableRoles" :key="role" :value="role" class="text-[10px] uppercase font-black tracking-widest">
                    {{ role }}
                  </SelectItem>
                </SelectContent>
              </Select>

              <Select v-model="statusFilter">
                <SelectTrigger class="w-full md:w-40 h-12 bg-slate-950 border-slate-800 rounded-xl font-bold text-xs uppercase tracking-widest text-slate-400">
                  <SelectValue placeholder="All Statuses" />
                </SelectTrigger>
                <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                  <SelectItem value="">All Statuses</SelectItem>
                  <SelectItem value="active">Active</SelectItem>
                  <SelectItem value="banned">Banned</SelectItem>
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
