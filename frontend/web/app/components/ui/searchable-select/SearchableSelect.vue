<template>
  <Popover v-model:open="open">
    <PopoverTrigger as-child>
      <div class="relative group w-full">
        <Button
          variant="outline"
          role="combobox"
          :aria-expanded="open"
          class="w-full justify-between rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 font-bold hover:bg-slate-800 hover:border-slate-700 transition-all text-left px-3 pr-10"
        >
          <span class="truncate text-xs">{{ selectedLabel || placeholder }}</span>
          <ChevronsUpDown class="ml-2 h-4 w-4 shrink-0 opacity-50 text-slate-400" />
        </Button>
        
        <!-- Clear Button -->
        <button 
          v-if="modelValue"
          type="button"
          @click.stop="handleClear"
          class="absolute right-8 top-1/2 -translate-y-1/2 p-1 rounded-md text-slate-500 hover:text-rose-400 hover:bg-slate-800 transition-all"
          title="Clear selection"
        >
          <X class="size-3.5" />
        </button>
      </div>
    </PopoverTrigger>
    <PopoverContent class="w-80 sm:w-96 p-0 bg-slate-950 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden z-50">
      <Command class="bg-transparent" :filter-function="filterOptions">
        <!-- Search Input -->
        <div class="p-2 border-b border-slate-900 bg-slate-950">
          <CommandInput 
            :placeholder="`Type to filter or create...`" 
            v-model="searchQuery"
            @keydown.enter.prevent="handleEnterKey"
            class="h-10 border-none bg-slate-900/80 rounded-xl text-slate-100 text-xs px-3 font-bold placeholder:text-slate-500" 
          />
        </div>

        <!-- Quick Pill Suggestions Bar -->
        <div v-if="pillSuggestions.length > 0" class="p-2.5 bg-slate-900/40 border-b border-slate-900">
          <div class="text-[8px] font-black uppercase tracking-widest text-slate-500 mb-1.5 flex items-center gap-1">
            <Sparkles class="size-3 text-indigo-400 shrink-0" />
            <span>Quick Suggestions</span>
          </div>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="opt in pillSuggestions"
              :key="opt.id"
              type="button"
              @click="handleSelect(opt.id)"
              :class="modelValue === opt.id ? 'bg-indigo-600 text-white border-indigo-500' : 'bg-slate-900 text-slate-300 hover:text-indigo-200 hover:bg-indigo-950/40 border-slate-800'"
              class="px-3 py-1 rounded-full border text-[8px] font-black uppercase tracking-wider transition-all inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm"
            >
              {{ opt.label }}
            </button>
          </div>
        </div>

        <!-- Custom Typed Entry Action Pill/Bar -->
        <div 
          v-if="searchQuery.trim() && !hasExactMatch" 
          @click="handleSelect(searchQuery.trim())"
          class="p-2.5 bg-indigo-950/30 border-b border-indigo-900/30 flex items-center justify-between cursor-pointer hover:bg-indigo-950/60 text-indigo-300 transition-colors group"
        >
          <div class="flex items-center gap-2">
            <PlusIcon class="size-3.5 text-indigo-400 group-hover:scale-110 transition-transform" />
            <div class="flex flex-col">
              <span class="text-[8px] font-black uppercase tracking-widest text-indigo-400">Use Custom Typed Value</span>
              <span class="text-xs font-bold text-white font-mono">"{{ searchQuery.trim() }}"</span>
            </div>
          </div>
          <span class="text-[8px] font-mono uppercase bg-indigo-500/20 text-indigo-300 px-2.5 py-1 rounded-full border border-indigo-500/30 leading-none">
            Press Enter ↵
          </span>
        </div>

        <!-- Options List -->
        <CommandList class="max-h-[220px] overflow-y-auto custom-scrollbar p-1">
          <CommandEmpty class="p-4 text-center">
            <div 
              v-if="searchQuery.trim()"
              @click="handleSelect(searchQuery.trim())"
              class="p-3 bg-indigo-950/40 border border-indigo-500/30 rounded-xl cursor-pointer hover:bg-indigo-900/50 text-indigo-300 transition-all flex items-center justify-center gap-2"
            >
              <PlusIcon class="size-4 text-indigo-400" />
              <span class="text-xs font-bold">Use "{{ searchQuery.trim() }}"</span>
            </div>
            <div v-else class="text-xs text-slate-500 font-bold uppercase tracking-widest">
              No matching options
            </div>
          </CommandEmpty>
          
          <CommandGroup>
            <CommandItem
              v-for="option in filteredOptions"
              :key="option.id"
              :value="option.label"
              @select="handleSelect(option.id)"
              @click="handleSelect(option.id)"
              class="text-slate-200 aria-selected:bg-indigo-600/20 aria-selected:text-indigo-300 hover:bg-slate-900 cursor-pointer flex items-center justify-between p-2.5 rounded-xl transition-colors"
            >
              <div class="flex flex-col">
                <span class="text-xs font-bold text-slate-100">{{ option.label }}</span>
                <span v-if="option.id !== option.label" class="text-[8px] font-mono text-slate-500 mt-0.5">{{ option.id }}</span>
              </div>
              <Check
                :class="cn(
                  'ml-auto h-4 w-4 text-indigo-400 shrink-0 transition-opacity',
                  modelValue === option.id ? 'opacity-100' : 'opacity-0'
                )"
              />
            </CommandItem>
          </CommandGroup>
        </CommandList>
      </Command>
    </PopoverContent>
  </Popover>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Check, ChevronsUpDown, PlusIcon, X, Sparkles } from 'lucide-vue-next'
import { cn } from '~/utils/cn'
import { Button } from '~/components/ui/button'
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '~/components/ui/command'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '~/components/ui/popover'

interface Option {
  id: string
  label: string
}

const props = defineProps<{
  options: Option[]
  modelValue: string | null
  placeholder: string
  emptyMessage?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', val: string | null): void
}>()

const open = ref(false)
const searchQuery = ref('')

const selectedLabel = computed(() => {
  const option = props.options?.find((opt) => opt.id === props.modelValue)
  return option ? option.label : props.modelValue || ''
})

// Top 4 options as quick pill suggestions
const pillSuggestions = computed(() => {
  if (!props.options || props.options.length === 0) return []
  return props.options.slice(0, 5)
})

// Filtered options based on search query
const filteredOptions = computed(() => {
  if (!props.options) return []
  if (!searchQuery.value.trim()) return props.options
  const q = searchQuery.value.toLowerCase().trim()
  return props.options.filter(
    opt => opt.label.toLowerCase().includes(q) || opt.id.toLowerCase().includes(q)
  )
})

// Check if search query matches an existing option exactly
const hasExactMatch = computed(() => {
  if (!searchQuery.value.trim() || !props.options) return false
  const q = searchQuery.value.toLowerCase().trim()
  return props.options.some(opt => opt.label.toLowerCase() === q || opt.id.toLowerCase() === q)
})

// Custom filtering function for command
function filterOptions(val: string, search: string) {
  if (!search) return 1
  return val.toLowerCase().includes(search.toLowerCase()) ? 1 : 0
}

function handleSelect(optionIdOrCustomText: string) {
  emit('update:modelValue', optionIdOrCustomText)
  open.value = false
  searchQuery.value = ''
}

function handleEnterKey() {
  if (filteredOptions.value.length > 0) {
    handleSelect(filteredOptions.value[0].id)
  } else if (searchQuery.value.trim()) {
    handleSelect(searchQuery.value.trim())
  }
}

function handleClear() {
  emit('update:modelValue', null)
  searchQuery.value = ''
}
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #334155;
  border-radius: 10px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #6366f1;
}
</style>
