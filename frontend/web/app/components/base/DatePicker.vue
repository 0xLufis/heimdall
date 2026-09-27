<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { CalendarDate, parseDate, today, getLocalTimeZone, DateFormatter, type DateValue } from '@internationalized/date'
import { Calendar as CalendarIcon, X } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '~/components/ui/popover'
import { Calendar } from '~/components/ui/calendar'
import { cn } from '~/lib/utils'

const props = withDefaults(defineProps<{
  modelValue?: string | null
  placeholder?: string
  disabled?: boolean
  minValue?: DateValue
  maxValue?: DateValue
  class?: string
}>(), {
  modelValue: '',
  placeholder: 'Select date...',
  disabled: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

const isOpen = ref(false)

const df = new DateFormatter('en-US', {
  dateStyle: 'medium',
})

const calendarValue = ref<DateValue | undefined>(undefined)

function parseModelDate(val?: string | null): DateValue | undefined {
  if (!val || typeof val !== 'string') return undefined
  try {
    const trimmed = val.trim()
    if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) {
      return parseDate(trimmed)
    }
  } catch {}
  return undefined
}

watch(() => props.modelValue, (newVal) => {
  calendarValue.value = parseModelDate(newVal)
}, { immediate: true })

const formattedDate = computed(() => {
  if (!calendarValue.value) return ''
  try {
    return df.format(calendarValue.value.toDate(getLocalTimeZone()))
  } catch {
    return props.modelValue || ''
  }
})

function handleDateSelect(val: DateValue | undefined) {
  if (val) {
    const str = val.toString() // YYYY-MM-DD
    emit('update:modelValue', str)
  } else {
    emit('update:modelValue', '')
  }
  isOpen.value = false
}

function clearDate(e: Event) {
  e.stopPropagation()
  calendarValue.value = undefined
  emit('update:modelValue', '')
}

function selectPresetDays(daysToAdd: number) {
  try {
    const target = today(getLocalTimeZone()).add({ days: daysToAdd })
    calendarValue.value = target
    emit('update:modelValue', target.toString())
    isOpen.value = false
  } catch (err) {
    console.error('Failed to set date preset', err)
  }
}
</script>

<template>
  <div :class="cn('relative', props.class)">
    <Popover v-model:open="isOpen">
      <PopoverTrigger as-child>
        <button
          type="button"
          :disabled="disabled"
          :class="cn(
            'flex items-center justify-between w-full h-8 px-3 rounded-lg border text-xs font-normal transition-colors text-left',
            'bg-background border-border text-foreground hover:bg-muted/50 focus:outline-none focus:ring-1 focus:ring-ring',
            disabled && 'opacity-50 cursor-not-allowed pointer-events-none',
            !calendarValue && 'text-muted-foreground'
          )"
        >
          <span class="flex items-center gap-2 truncate">
            <CalendarIcon class="w-3.5 h-3.5 text-muted-foreground shrink-0" />
            <span class="truncate">{{ formattedDate || placeholder }}</span>
          </span>
          <span v-if="calendarValue && !disabled" class="ml-1 text-muted-foreground hover:text-foreground p-0.5 rounded cursor-pointer" @click="clearDate">
            <X class="w-3 h-3" />
          </span>
        </button>
      </PopoverTrigger>

      <PopoverContent class="w-auto p-0 bg-popover text-popover-foreground border-border shadow-xl rounded-2xl overflow-hidden" align="start">
        <div class="p-3 border-b border-border/70 bg-muted/20 flex flex-wrap items-center gap-1.5">
          <span class="text-[10px] font-black uppercase tracking-wider text-muted-foreground mr-1">Quick:</span>
          <button
            type="button"
            @click="selectPresetDays(0)"
            class="px-2 py-0.5 rounded text-[10px] font-bold bg-background hover:bg-accent border border-border text-foreground transition-colors"
          >
            Today
          </button>
          <button
            type="button"
            @click="selectPresetDays(1)"
            class="px-2 py-0.5 rounded text-[10px] font-bold bg-background hover:bg-accent border border-border text-foreground transition-colors"
          >
            +1 Day
          </button>
          <button
            type="button"
            @click="selectPresetDays(3)"
            class="px-2 py-0.5 rounded text-[10px] font-bold bg-background hover:bg-accent border border-border text-foreground transition-colors"
          >
            +3 Days
          </button>
          <button
            type="button"
            @click="selectPresetDays(7)"
            class="px-2 py-0.5 rounded text-[10px] font-bold bg-background hover:bg-accent border border-border text-foreground transition-colors"
          >
            +1 Week
          </button>
        </div>

        <Calendar
          :model-value="calendarValue"
          :min-value="minValue"
          :max-value="maxValue"
          initial-focus
          @update:model-value="handleDateSelect"
        />
      </PopoverContent>
    </Popover>
  </div>
</template>
