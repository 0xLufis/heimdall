<script setup lang="ts">
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '~/components/ui/tooltip'
import { Lock } from 'lucide-vue-next'

const props = withDefaults(defineProps<{
  disabled?: boolean
  tooltip?: string
  side?: 'top' | 'right' | 'bottom' | 'left'
  align?: 'start' | 'center' | 'end'
}>(), {
  disabled: false,
  tooltip: '',
  side: 'top',
  align: 'center',
})
</script>

<template>
  <TooltipProvider v-if="disabled && tooltip">
    <Tooltip>
      <TooltipTrigger as-child>
        <span class="inline-flex items-center cursor-not-allowed select-none">
          <slot />
        </span>
      </TooltipTrigger>
      <TooltipContent :side="side" :align="align" class="bg-slate-900 border-slate-700 text-slate-100 text-xs shadow-xl max-w-xs z-50">
        <div class="flex items-center gap-1.5 font-medium">
          <Lock class="w-3.5 h-3.5 text-amber-400 shrink-0" />
          <span>{{ tooltip }}</span>
        </div>
      </TooltipContent>
    </Tooltip>
  </TooltipProvider>
  <slot v-else />
</template>
