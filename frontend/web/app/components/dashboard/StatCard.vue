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
  <Card class="bg-card border-border hover:border-border/80 transition-colors shadow-sm rounded-xl">
    <CardContent class="p-5">
      <div class="flex items-center gap-4">
        <div :class="cn('p-2.5 rounded-lg text-foreground bg-muted border border-border', bgColor)">
          <slot name="icon" />
        </div>
        <div>
          <p class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">{{ title }}</p>
          <p class="text-2xl font-bold text-foreground mt-0.5">{{ value }}</p>
        </div>
      </div>
      <div v-if="trend" class="mt-3 flex items-center gap-1.5">
         <span :class="trend.startsWith('+') ? 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20' : 'text-destructive bg-destructive/10 border-destructive/20'" class="text-[10px] font-mono font-medium px-1.5 py-0.5 rounded border">
           {{ trend }}
         </span>
         <span class="text-[10px] text-muted-foreground font-medium">from last cycle</span>
      </div>
    </CardContent>
  </Card>
</template>
