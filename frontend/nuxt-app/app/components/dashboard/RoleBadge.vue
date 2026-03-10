<template>
  <div 
    class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-black uppercase tracking-wider shadow-sm border transition-all"
    :class="roleConfig.class"
  >
    <div class="w-1.5 h-1.5 rounded-full" :class="roleConfig.dot"></div>
    {{ role || 'user' }}
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  role: string | null | undefined
}>()

const roleConfig = computed(() => {
  switch (props.role?.toLowerCase()) {
    case 'system_admin':
      return { class: 'bg-indigo-600 text-white border-indigo-700', dot: 'bg-indigo-200 animate-pulse' }
    case 'admin':
      return { class: 'bg-purple-100 text-purple-700 border-purple-200', dot: 'bg-purple-500' }
    case 'manager':
      return { class: 'bg-blue-100 text-blue-700 border-blue-200', dot: 'bg-blue-500' }
    case 'engineer':
      return { class: 'bg-emerald-100 text-emerald-700 border-emerald-200', dot: 'bg-emerald-500' }
    case 'technician':
      return { class: 'bg-amber-100 text-orange-700 border-orange-200', dot: 'bg-orange-500' }
    default:
      return { class: 'bg-gray-100 text-gray-600 border-gray-200', dot: 'bg-gray-400' }
  }
})
</script>
