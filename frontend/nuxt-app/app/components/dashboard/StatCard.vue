<template>
  <div class="bg-white p-6 rounded-2xl shadow-sm border border-gray-100 hover:shadow-md transition-all group">
    <div class="flex items-center gap-4">
      <div :class="`p-3 rounded-xl ${bgColor} group-hover:scale-110 transition-transform text-white shadow-sm` ">
        <slot name="icon" />
      </div>
      <div>
        <p class="text-[10px] font-black text-gray-400 uppercase tracking-widest">{{ title }}</p>
        <p class="text-2xl font-black text-gray-900 mt-0.5">{{ value }}</p>
      </div>
    </div>
    <div v-if="trend" class="mt-4 flex items-center gap-1.5">
       <span :class="trend.startsWith('+') ? 'text-green-600' : 'text-red-600'" class="text-[10px] font-bold px-1.5 py-0.5 rounded bg-gray-50 border border-gray-100">
         {{ trend }}
       </span>
       <span class="text-[10px] text-gray-400 font-medium tracking-tight">from last month</span>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string
  value: string | number
  bgColor: string
  trend?: string
}>()
</script>
