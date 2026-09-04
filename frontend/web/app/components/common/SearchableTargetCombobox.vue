<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { Search, X, Check, ChevronsUpDown, User, Cpu, GitBranch, Layers, Lock, PlusCircle } from 'lucide-vue-next'

export interface TargetItem {
  id: string
  label: string
  sublabel?: string
  badge?: string
  badgeColor?: string
  category?: string
  isOutOfOffice?: boolean
  role?: string
  raw?: any
}

const props = withDefaults(defineProps<{
  modelValue?: string
  placeholder?: string
  disabled?: boolean
  disabledReason?: string
  options?: TargetItem[]
  queryFn?: (query: string) => Promise<TargetItem[]>
  categoryLabel?: string
  iconType?: 'user' | 'machine' | 'group' | 'technology' | 'default'
  allowCustom?: boolean
  customNotice?: string
}>(), {
  modelValue: '',
  placeholder: 'Type or search target...',
  disabled: false,
  disabledReason: '',
  options: () => [],
  categoryLabel: 'Targets',
  iconType: 'default',
  allowCustom: true,
  customNotice: 'Undefined / Custom Target'
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: string): void
  (e: 'select', item: TargetItem | { id: string; label: string; isCustom: true; raw?: any }): void
  (e: 'clear'): void
}>()

const isOpen = ref(false)
const highlightedIndex = ref(-1)
const rootRef = ref<HTMLElement | null>(null)
const internalOptions = ref<TargetItem[]>([])
const isLoading = ref(false)

// Sync external options
watch(() => props.options, (newOpts) => {
  if (newOpts && newOpts.length > 0) {
    internalOptions.value = newOpts
  }
}, { immediate: true })

async function runQuery(search: string) {
  if (props.queryFn) {
    isLoading.value = true
    try {
      internalOptions.value = await props.queryFn(search)
    } catch (err) {
      console.warn('Error querying target options:', err)
    } finally {
      isLoading.value = false
    }
  }
}

onMounted(() => {
  if (props.queryFn && internalOptions.value.length === 0) {
    runQuery('')
  }
  if (typeof window !== 'undefined') {
    window.addEventListener('click', handleClickOutside)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('click', handleClickOutside)
  }
})

function handleClickOutside(event: MouseEvent) {
  if (rootRef.value && !rootRef.value.contains(event.target as Node)) {
    isOpen.value = false
  }
}

const inputValue = computed({
  get: () => props.modelValue || '',
  set: (val: string) => {
    emit('update:modelValue', val)
    if (!isOpen.value) isOpen.value = true
    highlightedIndex.value = 0
    if (props.queryFn) {
      runQuery(val)
    }
  }
})

function normalizeText(text: string): string {
  return (text || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
}

// Filtered options based on query
const filteredOptions = computed(() => {
  const rawQuery = (props.modelValue || '').trim()
  if (!rawQuery) {
    return internalOptions.value.slice(0, 12)
  }
  const query = normalizeText(rawQuery)
  return internalOptions.value.filter(item => {
    const matchLabel = normalizeText(item.label).includes(query)
    const matchSublabel = item.sublabel ? normalizeText(item.sublabel).includes(query) : false
    const matchId = normalizeText(item.id).includes(query)
    const matchCategory = item.category ? normalizeText(item.category).includes(query) : false
    const matchRole = item.role ? normalizeText(item.role).includes(query) : false
    return matchLabel || matchSublabel || matchId || matchCategory || matchRole
  }).slice(0, 15)
})

// Can show custom option if user typed something
const showCustomOption = computed(() => {
  if (!props.allowCustom) return false
  const query = (props.modelValue || '').trim()
  return query.length > 0
})

// Total items in dropdown including custom option if shown
const totalSelectableCount = computed(() => {
  return filteredOptions.value.length + (showCustomOption.value ? 1 : 0)
})

function selectItem(item: TargetItem) {
  emit('update:modelValue', item.label)
  emit('select', item)
  isOpen.value = false
}

function selectCustomText() {
  const customStr = (props.modelValue || '').trim()
  if (customStr) {
    emit('select', {
      id: customStr,
      label: customStr,
      isCustom: true
    })
  }
  isOpen.value = false
}

function handleInputFocus() {
  if (!props.disabled) {
    isOpen.value = true
    if (props.queryFn && internalOptions.value.length === 0) {
      runQuery(props.modelValue || '')
    }
  }
}

function onKeyDown(e: KeyboardEvent) {
  if (props.disabled) return

  if (!isOpen.value) {
    if (e.key === 'ArrowDown' || e.key === 'Enter') {
      isOpen.value = true
      e.preventDefault()
    }
    return
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    if (highlightedIndex.value < totalSelectableCount.value - 1) {
      highlightedIndex.value++
    }
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    if (highlightedIndex.value > 0) {
      highlightedIndex.value--
    }
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (highlightedIndex.value >= 0 && highlightedIndex.value < filteredOptions.value.length) {
      selectItem(filteredOptions.value[highlightedIndex.value])
    } else if (highlightedIndex.value === filteredOptions.value.length && showCustomOption.value) {
      selectCustomText()
    } else {
      // Free text is kept
      isOpen.value = false
    }
  } else if (e.key === 'Escape') {
    isOpen.value = false
  }
}

function clear() {
  emit('update:modelValue', '')
  emit('clear')
  if (props.queryFn) runQuery('')
}
</script>

<template>
  <div ref="rootRef" class="relative w-full">
    <!-- Input Bar -->
    <div class="relative flex items-center">
      <!-- Left Icon -->
      <div class="absolute left-3 flex items-center pointer-events-none text-slate-500">
        <Lock v-if="disabled" class="h-3.5 w-3.5 text-amber-500" />
        <User v-else-if="iconType === 'user'" class="h-3.5 w-3.5 text-indigo-400" />
        <Cpu v-else-if="iconType === 'machine'" class="h-3.5 w-3.5 text-indigo-400" />
        <GitBranch v-else-if="iconType === 'group'" class="h-3.5 w-3.5 text-cyan-400" />
        <Layers v-else-if="iconType === 'technology'" class="h-3.5 w-3.5 text-violet-400" />
        <Search v-else class="h-3.5 w-3.5 text-slate-500" />
      </div>

      <!-- Free-text Editable Input -->
      <input
        v-model="inputValue"
        type="text"
        :placeholder="placeholder"
        :disabled="disabled"
        @focus="handleInputFocus"
        @keydown="onKeyDown"
        class="w-full bg-slate-950 border rounded-xl pl-9 pr-14 py-2 text-xs text-slate-200 placeholder-slate-500 focus:outline-none transition-colors"
        :class="[
          disabled
            ? 'border-amber-500/30 bg-amber-950/10 text-slate-300 cursor-not-allowed opacity-90'
            : 'border-slate-800 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500'
        ]"
      />

      <!-- Right Controls -->
      <div class="absolute right-2 flex items-center gap-1">
        <!-- Clear Button -->
        <button
          v-if="modelValue && !disabled"
          type="button"
          @click="clear"
          class="p-1 rounded-md text-slate-500 hover:text-slate-300 hover:bg-slate-800/60 transition-colors"
          tabindex="-1"
          title="Clear"
        >
          <X class="h-3 w-3" />
        </button>

        <!-- Dropdown Toggle -->
        <button
          v-if="!disabled"
          type="button"
          @click="isOpen = !isOpen; if (isOpen) handleInputFocus()"
          class="p-1 rounded-md text-slate-500 hover:text-slate-300 transition-colors"
          tabindex="-1"
          title="Toggle Suggestions"
        >
          <ChevronsUpDown class="h-3.5 w-3.5" />
        </button>
      </div>
    </div>

    <!-- Disabled Helper / Policy Notification -->
    <div v-if="disabled && disabledReason" class="mt-1 flex items-center gap-1.5 text-[10px] text-amber-400">
      <Lock class="w-3 h-3 shrink-0" />
      <span>{{ disabledReason }}</span>
    </div>

    <!-- Floating Suggestions Dropdown -->
    <div
      v-if="isOpen && !disabled"
      class="absolute z-50 left-0 right-0 mt-1.5 bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden max-h-64 overflow-y-auto animate-in fade-in zoom-in-95 duration-150"
    >
      <!-- Dropdown Header -->
      <div class="p-2 border-b border-slate-800/60 flex items-center justify-between text-[10px] font-black uppercase tracking-wider text-slate-400 bg-slate-950/60">
        <span class="flex items-center gap-1.5">
          <span>{{ categoryLabel }}</span>
          <span v-if="isLoading" class="text-[9px] text-indigo-400 animate-pulse">(Loading...)</span>
        </span>
        <span v-if="filteredOptions.length > 0" class="text-indigo-400 font-mono">
          {{ filteredOptions.length }} suggested
        </span>
      </div>

      <!-- No options & free text prompt -->
      <div v-if="filteredOptions.length === 0 && !showCustomOption" class="p-4 text-center text-xs text-slate-500">
        No suggestions available. Type free-text directly.
      </div>

      <!-- Options List -->
      <ul v-if="filteredOptions.length > 0" class="p-1 space-y-0.5">
        <li
          v-for="(item, idx) in filteredOptions"
          :key="item.id"
          @click="selectItem(item)"
          @mouseenter="highlightedIndex = idx"
          class="px-3 py-2 rounded-xl text-xs flex items-center justify-between cursor-pointer transition-colors"
          :class="[
            highlightedIndex === idx ? 'bg-indigo-600/20 text-indigo-200' : 'hover:bg-slate-800/60 text-slate-300',
            (modelValue && (item.label.toLowerCase() === modelValue.toLowerCase() || item.id.toLowerCase() === modelValue.toLowerCase()))
              ? 'border border-indigo-500/30 bg-indigo-950/20 font-semibold'
              : ''
          ]"
        >
          <!-- Item Left: Icon & Label/Sublabel -->
          <div class="flex items-center gap-2.5 min-w-0 flex-1 pr-2">
            <div class="p-1.5 rounded-lg bg-slate-800 text-indigo-400 shrink-0">
              <User v-if="item.role || iconType === 'user'" class="h-3.5 w-3.5" />
              <Cpu v-else-if="iconType === 'machine'" class="h-3.5 w-3.5" />
              <GitBranch v-else-if="iconType === 'group'" class="h-3.5 w-3.5" />
              <Layers v-else class="h-3.5 w-3.5" />
            </div>

            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-1.5 flex-wrap">
                <span class="font-bold text-slate-100 truncate">
                  {{ item.label }}
                </span>

                <!-- Badge / Role -->
                <span
                  v-if="item.badge || item.role"
                  class="px-1.5 py-0.5 text-[9px] font-black uppercase tracking-wider rounded border"
                  :class="item.badgeColor || 'border-indigo-500/30 bg-indigo-500/10 text-indigo-300'"
                >
                  {{ item.badge || item.role }}
                </span>

                <!-- Out of Office Badge -->
                <span
                  v-if="item.isOutOfOffice"
                  class="px-1.5 py-0.5 text-[8px] font-black uppercase tracking-wider rounded border border-amber-500/30 bg-amber-500/10 text-amber-300"
                >
                  Out of Office
                </span>
              </div>

              <!-- Sublabel / Department / Tech detail -->
              <p v-if="item.sublabel" class="text-[10px] text-slate-400 truncate mt-0.5">
                {{ item.sublabel }}
              </p>
            </div>
          </div>

          <!-- Item Right: ID & Checkmark -->
          <div class="flex items-center gap-1.5 shrink-0 text-[10px] font-mono text-slate-500">
            <span v-if="item.id && item.id !== item.label" class="text-slate-500 bg-slate-950/60 px-1.5 py-0.5 rounded border border-slate-800">
              {{ item.id }}
            </span>
            <Check
              v-if="modelValue && (item.label.toLowerCase() === modelValue.toLowerCase() || item.id.toLowerCase() === modelValue.toLowerCase())"
              class="h-3.5 w-3.5 text-indigo-400"
            />
          </div>
        </li>
      </ul>

      <!-- Custom Free-Text Option -->
      <div
        v-if="showCustomOption"
        @click="selectCustomText"
        @mouseenter="highlightedIndex = filteredOptions.length"
        class="border-t border-slate-800 p-2.5 bg-slate-950/80 cursor-pointer flex items-center justify-between transition-colors"
        :class="[
          highlightedIndex === filteredOptions.length ? 'bg-indigo-950/40 text-indigo-200' : 'text-slate-400 hover:text-slate-200'
        ]"
      >
        <div class="flex items-center gap-2 min-w-0">
          <PlusCircle class="h-3.5 w-3.5 text-cyan-400 shrink-0" />
          <span class="text-xs truncate">
            Use custom: <strong class="text-cyan-300 font-bold">"{{ modelValue }}"</strong>
          </span>
        </div>
        <span class="text-[9px] uppercase tracking-wider font-bold text-slate-500 border border-slate-800 bg-slate-900 px-2 py-0.5 rounded-md shrink-0">
          {{ customNotice }}
        </span>
      </div>
    </div>
  </div>
</template>
