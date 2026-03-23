<script setup lang="ts">
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  role: string | null | undefined
}>()

const roleConfig = computed(() => {
  switch (props.role?.toLowerCase()) {
    case 'system_admin':
      return { class: 'bg-indigo-600/20 text-indigo-400 border-indigo-700/50', dot: 'bg-indigo-400 animate-pulse' }
    case 'admin':
      return { class: 'bg-purple-600/20 text-purple-400 border-purple-700/50', dot: 'bg-purple-500' }
    case 'manager':
      return { class: 'bg-blue-600/20 text-blue-400 border-blue-700/50', dot: 'bg-blue-500' }
    case 'engineer':
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
    class="gap-1.5 px-2.5 py-1 text-[10px] font-black uppercase tracking-wider transition-all"
    :class="roleConfig.class"
  >
    <div class="w-1.5 h-1.5 rounded-full" :class="roleConfig.dot"></div>
    {{ role || 'user' }}
  </Badge>
</template>
