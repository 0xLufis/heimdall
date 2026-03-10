<template>
  <div class="space-y-6">
    <!-- Header with Actions -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h3 class="text-2xl font-bold text-gray-900 tracking-tight font-sans">User Management</h3>
        <p class="text-sm text-gray-500 mt-1 font-sans">Manage user roles, permissions, and account status.</p>
      </div>
      <div class="flex items-center gap-3">
        <button @click="fetchUsers" class="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-200 rounded-lg text-sm font-semibold text-gray-700 hover:bg-gray-50 transition-all shadow-sm">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Refresh
        </button>
        <button class="inline-flex items-center gap-2 px-4 py-2 bg-indigo-600 rounded-lg text-sm font-semibold text-white hover:bg-indigo-700 transition-all shadow-md">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Invite User
        </button>
      </div>
    </div>

    <!-- Filters & Search -->
    <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm flex flex-col md:flex-row gap-4 items-center">
       <div class="relative flex-grow w-full md:w-auto">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>
          <input 
            v-model="searchQuery" 
            type="text" 
            placeholder="Search by name, email, or username..." 
            class="block w-full pl-10 pr-3 py-2 border border-gray-200 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 text-sm transition-all" 
          />
       </div>
       <div class="flex items-center gap-2 w-full md:w-auto">
          <select v-model="roleFilter" class="block w-full md:w-40 py-2 pl-3 pr-10 border border-gray-200 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 text-sm transition-all">
            <option value="">All Roles</option>
            <option v-for="role in availableRoles" :key="role" :value="role">{{ role }}</option>
          </select>
          <select v-model="statusFilter" class="block w-full md:w-40 py-2 pl-3 pr-10 border border-gray-200 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 text-sm transition-all">
            <option value="">All Statuses</option>
            <option value="active">Active</option>
            <option value="banned">Banned</option>
          </select>
       </div>
    </div>

    <!-- Error State -->
    <div v-if="error" class="bg-red-50 border-l-4 border-red-500 p-4 rounded-lg flex items-start gap-3 shadow-sm">
      <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-red-500 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
      <div>
        <p class="text-sm font-bold text-red-800">Authorization Error</p>
        <p class="text-xs text-red-700 mt-1">{{ error }}</p>
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

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"

definePageMeta({
  layout: 'dashboard'
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
