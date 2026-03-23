<script setup lang="ts">
import { Card, CardContent } from '~/components/ui/card'
import { cn } from '~/utils/cn'

defineProps<{
  title: string
  value: string | number
  bgColor: string
  trend?: string
}>()
</script>

<template>
  <Card class="bg-slate-900/50 border-slate-800 hover:bg-slate-900 transition-all group overflow-hidden">
    <CardContent class="p-6">
      <div class="flex items-center gap-4">
        <div :class="cn('p-3 rounded-xl transition-transform text-white shadow-sm group-hover:scale-110 opacity-80', bgColor)">
          <slot name="icon" />
        </div>
        <div>
          <p class="text-[10px] font-black text-slate-500 uppercase tracking-widest">{{ title }}</p>
          <p class="text-2xl font-black text-slate-100 mt-0.5">{{ value }}</p>
        </div>
      </div>
      <div v-if="trend" class="mt-4 flex items-center gap-1.5">
         <span :class="trend.startsWith('+') ? 'text-emerald-500' : 'text-rose-500'" class="text-[10px] font-bold px-1.5 py-0.5 rounded bg-slate-800 border border-slate-700">
           {{ trend }}
         </span>
         <span class="text-[10px] text-slate-500 font-medium tracking-tight">from last month</span>
      </div>
    </CardContent>
  </Card>
</template>
