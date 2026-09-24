<script setup lang="ts">
import { computed } from 'vue'
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  role: string | null | undefined
}>()

const roleConfig = computed(() => {
  switch (props.role?.toLowerCase()) {
    case 'system_admin':
      return { class: 'bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/40', dot: 'bg-rose-500 animate-pulse' }
    case 'plant_director':
      return { class: 'bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/40', dot: 'bg-violet-500' }
    case 'plant_engineering_manager':
      return { class: 'bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/40', dot: 'bg-rose-500' }
    case 'senior_engineering_manager':
      return { class: 'bg-amber-500/15 text-amber-800 dark:text-amber-300 border-amber-500/40', dot: 'bg-amber-500' }
    case 'heimdall_admin':
      return { class: 'bg-purple-500/15 text-purple-700 dark:text-purple-300 border-purple-500/40', dot: 'bg-purple-500' }
    case 'it_admin':
    case 'it_site_admin':
      return { class: 'bg-cyan-500/15 text-cyan-800 dark:text-cyan-300 border-cyan-500/40', dot: 'bg-cyan-500' }
    case 'engineering_admin':
      return { class: 'bg-amber-500/15 text-amber-800 dark:text-amber-300 border-amber-500/40', dot: 'bg-amber-500' }
    case 'admin':
      return { class: 'bg-purple-500/15 text-purple-700 dark:text-purple-300 border-purple-500/40', dot: 'bg-purple-500' }
    case 'operative_planner':
    case 'manager':
      return { class: 'bg-blue-500/15 text-blue-700 dark:text-blue-300 border-blue-500/40', dot: 'bg-blue-500' }
    case 'group_leader':
    case 'team_lead':
      return { class: 'bg-teal-500/15 text-teal-800 dark:text-teal-300 border-teal-500/40', dot: 'bg-teal-500' }
    case 'shift_leader':
      return { class: 'bg-indigo-500/15 text-indigo-700 dark:text-indigo-300 border-indigo-500/40', dot: 'bg-indigo-500' }
    case 'engineer':
    case 'controls_engineer':
    case 'lead_engineer':
      return { class: 'bg-emerald-500/15 text-emerald-800 dark:text-emerald-300 border-emerald-500/40', dot: 'bg-emerald-500' }
    case 'technician':
      return { class: 'bg-orange-500/15 text-orange-800 dark:text-orange-300 border-orange-500/40', dot: 'bg-orange-500' }
    default:
      return { class: 'bg-muted text-muted-foreground border-border', dot: 'bg-muted-foreground/60' }
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
