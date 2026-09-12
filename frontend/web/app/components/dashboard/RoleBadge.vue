<script setup lang="ts">
import { computed } from 'vue'
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  role: string | null | undefined
}>()

const roleConfig = computed(() => {
  switch (props.role?.toLowerCase()) {
    case 'system_admin':
      return { class: 'bg-rose-500/20 text-rose-300 border-rose-500/50', dot: 'bg-rose-400 animate-pulse' }
    case 'plant_director':
      return { class: 'bg-violet-950/40 text-violet-200 border-violet-500/50 shadow-violet-500/20', dot: 'bg-violet-400' }
    case 'plant_engineering_manager':
      return { class: 'bg-rose-950/40 text-rose-300 border-rose-600/50', dot: 'bg-rose-400' }
    case 'senior_engineering_manager':
      return { class: 'bg-amber-950/40 text-amber-200 border-amber-600/50', dot: 'bg-amber-400' }
    case 'heimdall_admin':
      return { class: 'bg-purple-600/20 text-purple-300 border-purple-600/50', dot: 'bg-purple-400' }
    case 'it_admin':
    case 'it_site_admin':
      return { class: 'bg-cyan-600/20 text-cyan-300 border-cyan-600/50', dot: 'bg-cyan-400' }
    case 'engineering_admin':
      return { class: 'bg-amber-600/20 text-amber-300 border-amber-600/50', dot: 'bg-amber-400' }
    case 'admin':
      return { class: 'bg-purple-600/20 text-purple-400 border-purple-700/50', dot: 'bg-purple-500' }
    case 'operative_planner':
    case 'manager':
      return { class: 'bg-blue-600/20 text-blue-400 border-blue-700/50', dot: 'bg-blue-500' }
    case 'group_leader':
    case 'team_lead':
      return { class: 'bg-teal-600/20 text-teal-400 border-teal-700/50', dot: 'bg-teal-500' }
    case 'shift_leader':
      return { class: 'bg-indigo-600/20 text-indigo-400 border-indigo-700/50', dot: 'bg-indigo-500' }
    case 'engineer':
    case 'controls_engineer':
    case 'lead_engineer':
      return { class: 'bg-emerald-600/20 text-emerald-400 border-emerald-700/50', dot: 'bg-emerald-500' }
    case 'technician':
      return { class: 'bg-amber-600/20 text-orange-400 border-orange-700/50', dot: 'bg-orange-500' }
    default:
      return { class: 'bg-slate-800 text-slate-400 border-slate-700', dot: 'bg-slate-500' }
  }
})
</script>

<template>
  <Badge 
    variant="outline"
    class="gap-2 px-3 py-1.5 text-[9px] font-black uppercase tracking-widest rounded-full transition-all shadow-sm"
    :class="roleConfig.class"
  >
    <div class="w-1.5 h-1.5 rounded-full shrink-0" :class="roleConfig.dot"></div>
    {{ role || 'user' }}
  </Badge>
</template>
