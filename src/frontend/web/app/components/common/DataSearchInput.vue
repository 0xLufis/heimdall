<script setup lang="ts">
import { ref, watch } from 'vue'
import { Search, X } from 'lucide-vue-next'

const props = withDefaults(
  defineProps<{
    modelValue?: string
    placeholder?: string
    disabled?: boolean
    debounceMs?: number
  }>(),
  {
    modelValue: '',
    placeholder: 'Search records, identifiers, keywords...',
    disabled: false,
    debounceMs: 150
  }
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'clear'): void
}>()

const localQuery = ref(props.modelValue)
let timer: ReturnType<typeof setTimeout> | null = null

watch(
  () => props.modelValue,
  (val) => {
    if (val !== localQuery.value) {
      localQuery.value = val
    }
  }
)

function onInput(e: Event) {
  const target = e.target as HTMLInputElement
  localQuery.value = target.value
  if (timer) clearTimeout(timer)
  timer = setTimeout(() => {
    emit('update:modelValue', localQuery.value)
  }, props.debounceMs)
}

function clear() {
  localQuery.value = ''
  if (timer) clearTimeout(timer)
  emit('update:modelValue', '')
  emit('clear')
}
</script>

<template>
  <div class="relative flex-1 min-w-[200px] flex items-center">
    <Search class="absolute left-3 w-4 h-4 text-zinc-400 pointer-events-none" />
    <input
      type="text"
      :value="localQuery"
      :placeholder="placeholder"
      :disabled="disabled"
      class="w-full pl-9 pr-8 h-10 min-h-[40px] bg-zinc-900/70 hover:bg-zinc-900 border border-zinc-800 hover:border-zinc-700 focus:border-teal-500/60 focus:ring-1 focus:ring-teal-500/30 rounded-lg text-sm text-zinc-100 placeholder:text-zinc-500 transition-colors outline-none disabled:opacity-50 disabled:cursor-not-allowed"
      @input="onInput"
      @keydown.esc="clear"
    />
    <button
      v-if="localQuery"
      type="button"
      class="absolute right-2.5 p-1 rounded-md text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors"
      title="Clear search"
      aria-label="Clear search"
      @click="clear"
    >
      <X class="w-3.5 h-3.5" />
    </button>
  </div>
</template>
