<script setup lang="ts">
import { Card, CardContent } from '@/components/ui/card'
import { cn } from '@/utils/cn'

defineProps<{
  title: string
  value: string | number
  bgColor: string
  trend?: string
}>()
</script>

<template>
  <Card class="hover:shadow-md transition-all group overflow-hidden border-gray-100">
    <CardContent class="p-6">
      <div class="flex items-center gap-4">
        <div :class="cn('p-3 rounded-xl transition-transform text-white shadow-sm group-hover:scale-110', bgColor)">
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
    </CardContent>
  </Card>
</template>
