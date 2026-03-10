<template>
  <div class="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden relative font-sans">
    <!-- Loading Overlay -->
    <div v-if="loading" class="absolute inset-0 bg-white/60 backdrop-blur-[1px] z-10 flex items-center justify-center">
      <div class="flex flex-col items-center gap-3">
        <div class="animate-spin rounded-full h-10 w-10 border-b-2 border-indigo-600"></div>
        <span class="text-[10px] font-black text-gray-500 uppercase tracking-widest">Loading Records...</span>
      </div>
    </div>

    <div class="overflow-x-auto">
      <table class="w-full text-left">
        <thead class="bg-gray-50/50 text-[10px] text-gray-400 uppercase tracking-widest font-black">
          <tr>
            <th class="px-6 py-5 border-b border-gray-100">Identity</th>
            <th class="px-6 py-5 border-b border-gray-100">Access Level</th>
            <th class="px-6 py-5 border-b border-gray-100">Account Health</th>
            <th class="px-6 py-5 border-b border-gray-100">Metadata</th>
            <th class="px-6 py-5 border-b border-gray-100 text-right">Commands</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-50">
          <tr v-for="user in users" :key="user.id" class="group hover:bg-indigo-50/20 transition-all duration-200">
            <td class="px-6 py-4">
              <DashboardUserAvatar :name="user.name" :email="user.email" />
            </td>
            <td class="px-6 py-4">
              <div class="flex flex-col gap-2">
                <DashboardRoleBadge :role="user.role" />
                <div class="relative w-full max-w-[140px]">
                  <select 
                    :value="user.role || 'user'" 
                    @change="(e) => $emit('update-role', user.id, (e.target as HTMLSelectElement).value)"
                    class="appearance-none block w-full text-[9px] bg-gray-50 border border-gray-200 rounded-lg py-1 pl-2 pr-6 focus:outline-none focus:ring-2 focus:ring-indigo-500 font-bold text-gray-500 cursor-pointer hover:bg-white transition-all shadow-sm uppercase tracking-tighter"
                  >
                    <option v-for="role in roles" :key="role" :value="role">{{ role }}</option>
                  </select>
                  <div class="pointer-events-none absolute inset-y-0 right-0 flex items-center px-1.5 text-gray-300">
                    <svg class="h-3 w-3 fill-current" viewBox="0 0 20 20"><path d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" /></svg>
                  </div>
                </div>
              </div>
            </td>
            <td class="px-6 py-4 text-[10px] font-black uppercase tracking-wider">
              <div v-if="user.banned" class="text-red-600 flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full bg-red-600 animate-pulse"></span>
                Suspended
              </div>
              <div v-else class="text-emerald-600 flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-600"></span>
                Verified
              </div>
            </td>
            <td class="px-6 py-4">
              <div class="text-[10px] text-gray-500 font-bold uppercase tracking-tight">Active since</div>
              <div class="text-[11px] text-gray-900 font-medium">{{ formatDate(user.createdAt) }}</div>
            </td>
            <td class="px-6 py-4 text-right">
              <div class="flex items-center justify-end gap-1">
                <button 
                  @click="navigateTo(`/dashboard/users/${user.id}`)" 
                  class="p-2 text-gray-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-xl transition-all shadow-sm hover:shadow-md"
                  title="System Forensic"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                </button>
                <button 
                  v-if="!user.banned" 
                  @click="$emit('ban-user', user.id)" 
                  class="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-all shadow-sm hover:shadow-md"
                  title="Invoke Ban"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728L5.636 5.636" />
                  </svg>
                </button>
                <button 
                  v-else 
                  @click="$emit('unban-user', user.id)" 
                  class="p-2 text-indigo-400 hover:text-emerald-600 hover:bg-emerald-50 rounded-xl transition-all shadow-sm hover:shadow-md"
                  title="Lift Suspension"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    
    <!-- Empty State -->
    <div v-if="!loading && users.length === 0" class="flex flex-col items-center justify-center py-24 px-4">
      <div class="w-16 h-16 bg-gray-50 rounded-2xl flex items-center justify-center mb-4 text-gray-200">
         <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" /></svg>
      </div>
      <p class="text-sm font-bold text-gray-400 uppercase tracking-widest">No matching records found.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  users: any[],
  roles: string[],
  loading: boolean
}>()

defineEmits(['update-role', 'ban-user', 'unban-user'])

function formatDate(date: string | Date) {
  return new Date(date).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
  })
}
</script>
