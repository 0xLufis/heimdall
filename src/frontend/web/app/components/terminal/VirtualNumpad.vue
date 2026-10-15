<script setup lang="ts">
import { Delete, Check } from 'lucide-vue-next'

const props = defineProps<{
  modelValue: string
  maxLength?: number
}>()

const emit = defineEmits<{
  'update:modelValue': [val: string]
  'submit': []
}>()

const handleKey = (digit: string) => {
  if (props.maxLength && props.modelValue.length >= props.maxLength) return
  emit('update:modelValue', props.modelValue + digit)
}

const handleBackspace = () => {
  if (props.modelValue.length > 0) {
    emit('update:modelValue', props.modelValue.slice(0, -1))
  }
}

const handleClear = () => {
  emit('update:modelValue', '')
}
</script>

<template>
  <div class="grid grid-cols-3 gap-2.5 max-w-xs mx-auto select-none">
    <button
      v-for="n in [1, 2, 3, 4, 5, 6, 7, 8, 9]"
      :key="n"
      type="button"
      @click="handleKey(String(n))"
      class="h-14 rounded-xl border border-slate-700 bg-slate-800 text-xl font-bold font-mono text-slate-100 hover:bg-slate-700 active:scale-95 transition-all shadow-md flex items-center justify-center cursor-pointer"
    >
      {{ n }}
    </button>

    <button
      type="button"
      @click="handleBackspace"
      class="h-14 rounded-xl border border-slate-700 bg-slate-800/80 text-amber-400 hover:bg-slate-700 active:scale-95 transition-all shadow-md flex items-center justify-center cursor-pointer"
      title="Backspace"
    >
      <Delete class="w-6 h-6" />
    </button>

    <button
      type="button"
      @click="handleKey('0')"
      class="h-14 rounded-xl border border-slate-700 bg-slate-800 text-xl font-bold font-mono text-slate-100 hover:bg-slate-700 active:scale-95 transition-all shadow-md flex items-center justify-center cursor-pointer"
    >
      0
    </button>

    <button
      type="button"
      @click="emit('submit')"
      class="h-14 rounded-xl border border-emerald-600 bg-emerald-600 text-white font-bold text-base hover:bg-emerald-500 active:scale-95 transition-all shadow-md shadow-emerald-950/40 flex items-center justify-center gap-1 cursor-pointer"
      title="Enter"
    >
      <Check class="w-6 h-6" />
    </button>
  </div>
</template>
