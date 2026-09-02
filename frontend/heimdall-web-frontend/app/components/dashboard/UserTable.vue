<script setup lang="ts">
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '~/components/ui/table'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '~/components/ui/select'
import { Button } from '~/components/ui/button'

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

<template>
  <div class="bg-slate-900 rounded-2xl shadow-sm border border-slate-800 overflow-hidden relative font-sans">
    <!-- Loading Overlay -->
    <div v-if="loading" class="absolute inset-0 bg-slate-900/60 backdrop-blur-[1px] z-10 flex items-center justify-center">
      <div class="flex flex-col items-center gap-3">
        <div class="animate-spin rounded-full h-10 w-10 border-b-2 border-slate-600"></div>
        <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Loading Records...</span>
      </div>
    </div>

    <div class="overflow-x-auto">
      <Table>
        <TableHeader class="bg-slate-950/40">
          <TableRow class="border-b border-slate-800 hover:bg-transparent">
            <TableHead class="px-6 py-5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Identity</TableHead>
            <TableHead class="px-6 py-5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Access Level</TableHead>
            <TableHead class="px-6 py-5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Account Health</TableHead>
            <TableHead class="px-6 py-5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto">Metadata</TableHead>
            <TableHead class="px-6 py-5 text-[10px] text-slate-500 uppercase tracking-widest font-black h-auto text-right">Commands</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody class="divide-y divide-slate-800 border-0">
          <TableRow v-for="user in users" :key="user.id" class="group hover:bg-slate-800/30 transition-all duration-200 border-slate-800">
            <TableCell class="px-6 py-4">
              <DashboardUserAvatar :name="user.name" :email="user.email" />
            </TableCell>
            <TableCell class="px-6 py-4">
              <div class="flex flex-col gap-2">
                <DashboardRoleBadge :role="user.role" />
                <Select 
                  :model-value="user.role || 'user'" 
                  @update:model-value="(val) => $emit('update-role', user.id, val)"
                >
                  <SelectTrigger class="h-7 w-full max-w-[140px] text-[9px] bg-slate-950 border-slate-800 font-bold text-slate-400 uppercase tracking-tighter hover:bg-slate-900 transition-all shadow-sm">
                    <SelectValue placeholder="Select Role" />
                  </SelectTrigger>
                  <SelectContent class="bg-slate-950 border-slate-800 text-slate-300">
                    <SelectItem v-for="role in roles" :key="role" :value="role" class="text-[10px] uppercase font-black tracking-widest focus:bg-slate-800 focus:text-white">
                      {{ role }}
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </TableCell>
            <TableCell class="px-6 py-4 text-[10px] font-black uppercase tracking-wider">
              <div v-if="user.banned" class="text-rose-500 flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full bg-rose-600 animate-pulse"></span>
                Suspended
              </div>
              <div v-else class="text-emerald-500 flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-600"></span>
                Verified
              </div>
            </TableCell>
            <TableCell class="px-6 py-4">
              <div class="text-[10px] text-slate-600 font-bold uppercase tracking-tight">Active since</div>
              <div class="text-[11px] text-slate-300 font-medium">{{ formatDate(user.createdAt) }}</div>
            </TableCell>
            <TableCell class="px-6 py-4 text-right">
              <div class="flex items-center justify-end gap-1">
                <Button 
                  variant="ghost" 
                  size="icon"
                  @click="navigateTo(`/dashboard/users/${user.id}`)" 
                  class="h-8 w-8 text-slate-500 hover:text-slate-100 hover:bg-slate-800 rounded-xl"
                  title="System Forensic"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                </Button>
                <Button 
                  v-if="!user.banned" 
                  variant="ghost"
                  size="icon"
                  @click="$emit('ban-user', user.id)" 
                  class="h-8 w-8 text-slate-500 hover:text-rose-500 hover:bg-rose-950/30 rounded-xl"
                  title="Invoke Ban"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728L5.636 5.636" />
                  </svg>
                </Button>
                <Button 
                  v-else 
                  variant="ghost"
                  size="icon"
                  @click="$emit('unban-user', user.id)" 
                  class="h-8 w-8 text-slate-500 hover:text-emerald-500 hover:bg-emerald-950/30 rounded-xl"
                  title="Lift Suspension"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
    
    <!-- Empty State -->
    <div v-if="!loading && users.length === 0" class="flex flex-col items-center justify-center py-24 px-4">
      <div class="w-16 h-16 bg-slate-800 rounded-2xl flex items-center justify-center mb-4 text-slate-700">
         <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" /></svg>
      </div>
      <p class="text-[10px] font-black text-slate-600 uppercase tracking-widest">No matching records found.</p>
    </div>
  </div>
</template>
