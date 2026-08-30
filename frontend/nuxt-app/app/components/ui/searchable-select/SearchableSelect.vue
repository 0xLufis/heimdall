<template>
  <Popover v-model:open="open">
    <PopoverTrigger as-child>
      <div class="relative group">
        <Button
          variant="outline"
          role="combobox"
          :aria-expanded="open"
          class="w-full justify-between rounded-xl h-11 border-slate-800 bg-slate-900 text-slate-200 font-bold hover:bg-slate-800 transition-all text-left px-3 pr-10"
        >
          <span class="truncate">{{ selectedLabel || placeholder }}</span>
          <ChevronsUpDown class="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
        
        <!-- Clear Button -->
        <button 
          v-if="modelValue"
          @click.stop="handleClear"
          class="absolute right-9 top-1/2 -translate-y-1/2 p-1 text-slate-500 hover:text-rose-400 opacity-0 group-hover:opacity-100 transition-all"
        >
          <PlusIcon class="rotate-45 size-4" />
        </button>
      </div>
    </PopoverTrigger>
    <PopoverContent class="w-full p-0 bg-slate-900 border-slate-800 shadow-2xl">
      <Command class="bg-transparent" v-model:search-term="searchQuery">
        <CommandInput 
          :placeholder="placeholder" 
          class="h-9 border-none bg-slate-950/50 text-slate-200" 
        />
        <CommandList class="max-h-[250px] overflow-y-auto custom-scrollbar">
          <CommandEmpty class="p-0">
             <div 
               v-if="searchQuery"
               @click="handleSelect(searchQuery)"
               class="p-4 flex items-center gap-3 cursor-pointer hover:bg-indigo-500/20 text-indigo-400 transition-all"
             >
                <PlusIcon class="size-4" />
                <div class="flex flex-col">
                   <span class="text-[10px] font-black uppercase tracking-widest">Create New Entry</span>
                   <span class="text-sm font-bold text-slate-200">"{{ searchQuery }}"</span>
                </div>
             </div>
             <div v-else class="p-4 text-xs text-slate-500 font-bold uppercase tracking-widest text-center">
                Type to search or create
             </div>
          </CommandEmpty>
          
          <CommandGroup>
            <CommandItem
              v-for="option in options"
              :key="option.id"
              :value="option.label"
              @select="handleSelect(option.id)"
              class="text-slate-300 aria-selected:bg-indigo-500/20 aria-selected:text-indigo-400 cursor-pointer flex items-center justify-between"
            >
              <div class="flex flex-col">
                <span class="font-bold">{{ option.label }}</span>
                <span v-if="option.id !== option.label" class="text-[9px] opacity-40">{{ option.id.slice(0, 8) }}</span>
              </div>
              <Check
                :class="cn(
                  'ml-auto h-4 w-4 text-indigo-500',
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
import { ref, computed, watch } from 'vue'
import { Check, ChevronsUpDown, PlusIcon } from 'lucide-vue-next'
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

const emit = defineEmits(['update:modelValue'])

const open = ref(false)
const searchQuery = ref('')

const selectedLabel = computed(() => {
  const option = props.options.find((opt) => opt.id === props.modelValue)
  return option ? option.label : props.modelValue || ''
})

function handleSelect(optionId: string) {
  emit('update:modelValue', optionId)
  open.value = false
  searchQuery.value = ''
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
  background: #1e293b;
  border-radius: 10px;
}
</style>
