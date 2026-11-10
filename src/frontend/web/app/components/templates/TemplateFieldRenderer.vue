<script setup lang="ts">
import { computed, ref } from 'vue'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Check, X, Plus, AlertCircle, Info } from 'lucide-vue-next'
import type { TemplateFieldDefinition } from '~/types/templates'

const props = withDefaults(defineProps<{
  field: TemplateFieldDefinition
  modelValue?: any
  error?: string | null
  disabled?: boolean
}>(), {
  modelValue: undefined,
  error: null,
  disabled: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: any): void
}>()

const currentValue = computed({
  get: () => props.modelValue !== undefined ? props.modelValue : (props.field.defaultValue ?? ''),
  set: (val: any) => emit('update:modelValue', val)
})

// Number handler
const numberValue = computed({
  get: () => {
    if (props.modelValue === null || props.modelValue === undefined || props.modelValue === '') {
      return props.field.defaultValue ?? ''
    }
    return props.modelValue
  },
  set: (val: string | number) => {
    const num = Number(val)
    emit('update:modelValue', isNaN(num) ? val : num)
  }
})

// Boolean toggle
const toggleBoolean = () => {
  if (props.disabled) return
  emit('update:modelValue', !currentValue.value)
}

// MultiSelect array
const selectedOptions = computed<string[]>({
  get: () => {
    if (Array.isArray(props.modelValue)) return props.modelValue
    if (typeof props.modelValue === 'string' && props.modelValue) return props.modelValue.split(',').map(s => s.trim())
    return []
  },
  set: (val: string[]) => emit('update:modelValue', val)
})

const toggleOption = (val: string) => {
  if (props.disabled) return
  const current = [...selectedOptions.value]
  const idx = current.indexOf(val)
  if (idx !== -1) {
    current.splice(idx, 1)
  } else {
    current.push(val)
  }
  selectedOptions.value = current
}

// Tags array
const newTagInput = ref('')
const tagsList = computed<string[]>({
  get: () => {
    if (Array.isArray(props.modelValue)) return props.modelValue
    if (typeof props.modelValue === 'string' && props.modelValue) return props.modelValue.split(',').map(s => s.trim())
    return []
  },
  set: (val: string[]) => emit('update:modelValue', val)
})

const addTag = () => {
  const val = newTagInput.value.trim()
  if (!val || props.disabled) return
  if (!tagsList.value.includes(val)) {
    tagsList.value = [...tagsList.value, val]
  }
  newTagInput.value = ''
}

const removeTag = (tag: string) => {
  if (props.disabled) return
  tagsList.value = tagsList.value.filter(t => t !== tag)
}

const codePlaceholder = computed(() => {
  if (props.field.placeholder) return props.field.placeholder
  return props.field.fieldType === 'json' ? '{\n  "key": "value"\n}' : '// Lua or expression script'
})
</script>

<template>
  <div class="space-y-1.5" :data-field-key="field.key">
    <!-- Field Header -->
    <div class="flex items-center justify-between gap-2">
      <div class="flex items-center gap-1.5 flex-wrap">
        <Label :for="field.key" class="text-xs font-semibold text-foreground flex items-center gap-1">
          <span>{{ field.label }}</span>
          <span v-if="field.required" class="text-rose-500 font-bold">*</span>
        </Label>

        <!-- Inherited badge -->
        <Badge
          v-if="field.isInherited"
          variant="outline"
          class="text-[10px] px-1.5 py-0 bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/30"
          title="Inherited from parent template lineage"
        >
          Inherited: {{ field.inheritedFromCode || 'Base' }}
        </Badge>

        <!-- Unit badge -->
        <Badge
          v-if="field.unit"
          variant="secondary"
          class="text-[10px] px-1.5 py-0 font-mono text-muted-foreground"
        >
          {{ field.unit }}
        </Badge>
      </div>

      <span class="text-[10px] font-mono text-muted-foreground opacity-60">
        {{ field.key }}
      </span>
    </div>

    <!-- Description -->
    <p v-if="field.description" class="text-[11px] text-muted-foreground leading-snug">
      {{ field.description }}
    </p>

    <!-- 1. STRING / TEXT -->
    <div v-if="field.fieldType === 'string'">
      <Input
        :id="field.key"
        v-model="currentValue"
        :placeholder="field.placeholder || `Enter ${field.label}...`"
        :disabled="disabled"
        class="h-8 text-xs font-sans"
        :class="{ 'border-rose-500 focus-visible:ring-rose-500': error }"
      />
    </div>

    <!-- 2. NUMBER -->
    <div v-else-if="field.fieldType === 'number'">
      <div class="relative flex items-center">
        <Input
          :id="field.key"
          type="number"
          v-model="numberValue"
          :placeholder="field.placeholder || '0'"
          :min="field.min"
          :max="field.max"
          :step="field.step || 'any'"
          :disabled="disabled"
          class="h-8 text-xs font-mono pr-12"
          :class="{ 'border-rose-500 focus-visible:ring-rose-500': error }"
        />
        <span
          v-if="field.unit"
          class="absolute right-3 text-[11px] font-mono text-muted-foreground pointer-events-none select-none"
        >
          {{ field.unit }}
        </span>
      </div>
      <div v-if="field.min !== undefined || field.max !== undefined" class="flex items-center gap-2 text-[10px] text-muted-foreground mt-0.5">
        <span v-if="field.min !== undefined">Min: {{ field.min }}</span>
        <span v-if="field.max !== undefined">Max: {{ field.max }}</span>
      </div>
    </div>

    <!-- 3. BOOLEAN -->
    <div v-else-if="field.fieldType === 'boolean'" class="pt-0.5">
      <button
        type="button"
        role="switch"
        :aria-checked="!!currentValue"
        :disabled="disabled"
        @click="toggleBoolean"
        class="inline-flex items-center gap-2 px-3 py-1.5 rounded-md border text-xs font-medium transition-all"
        :class="[
          currentValue
            ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30'
            : 'bg-muted/30 text-muted-foreground border-border hover:bg-muted/60',
          disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'
        ]"
      >
        <span
          class="w-4 h-4 rounded-full flex items-center justify-center transition-transform"
          :class="currentValue ? 'bg-emerald-600 text-white' : 'bg-muted-foreground/30 text-muted-foreground'"
        >
          <Check v-if="currentValue" class="w-2.5 h-2.5 stroke-[3]" />
          <X v-else class="w-2.5 h-2.5 stroke-[2]" />
        </span>
        <span>{{ currentValue ? 'Enabled (True)' : 'Disabled (False)' }}</span>
      </button>
    </div>

    <!-- 4. SELECT -->
    <div v-else-if="field.fieldType === 'select'">
      <select
        :id="field.key"
        v-model="currentValue"
        :disabled="disabled"
        class="w-full h-8 px-2.5 text-xs rounded-md border border-input bg-background text-foreground ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
        :class="{ 'border-rose-500 focus:ring-rose-500': error }"
      >
        <option value="" disabled>{{ field.placeholder || 'Select an option...' }}</option>
        <option
          v-for="opt in (field.options || [])"
          :key="opt.value"
          :value="opt.value"
        >
          {{ opt.label }} ({{ opt.value }})
        </option>
      </select>
    </div>

    <!-- 5. MULTISELECT -->
    <div v-else-if="field.fieldType === 'multiselect'" class="space-y-1.5">
      <div class="flex flex-wrap gap-1.5">
        <button
          v-for="opt in (field.options || [])"
          :key="opt.value"
          type="button"
          :disabled="disabled"
          @click="toggleOption(opt.value)"
          class="px-2 py-1 rounded text-xs border transition-colors flex items-center gap-1 cursor-pointer"
          :class="[
            selectedOptions.includes(opt.value)
              ? 'bg-primary text-primary-foreground border-primary'
              : 'bg-muted/40 text-muted-foreground border-border hover:bg-muted'
          ]"
        >
          <Check v-if="selectedOptions.includes(opt.value)" class="w-3 h-3 stroke-[3]" />
          <span>{{ opt.label }}</span>
        </button>
      </div>
    </div>

    <!-- 6. TEXTAREA -->
    <div v-else-if="field.fieldType === 'textarea'">
      <textarea
        :id="field.key"
        v-model="currentValue"
        :rows="3"
        :placeholder="field.placeholder || `Enter ${field.label}...`"
        :disabled="disabled"
        class="w-full rounded-md border border-input bg-background p-2 text-xs ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
        :class="{ 'border-rose-500 focus-visible:ring-rose-500': error }"
      />
    </div>

    <!-- 7. CODE / JSON -->
    <div v-else-if="field.fieldType === 'code' || field.fieldType === 'json'">
      <textarea
        :id="field.key"
        v-model="currentValue"
        :rows="4"
        :placeholder="codePlaceholder"
        :disabled="disabled"
        class="w-full rounded-md border border-input bg-zinc-950 text-zinc-100 dark:bg-black font-mono p-2 text-xs leading-relaxed ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
        :class="{ 'border-rose-500': error }"
      />
    </div>

    <!-- 8. TAGS -->
    <div v-else-if="field.fieldType === 'tags'" class="space-y-1.5">
      <div class="flex flex-wrap gap-1 items-center min-h-7 p-1 rounded border border-input bg-background">
        <span
          v-for="tag in tagsList"
          :key="tag"
          class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[11px] bg-secondary text-secondary-foreground font-mono"
        >
          <span>#{{ tag }}</span>
          <button
            v-if="!disabled"
            type="button"
            @click="removeTag(tag)"
            class="hover:text-rose-500 focus:outline-none"
          >
            <X class="w-2.5 h-2.5" />
          </button>
        </span>
        <input
          v-if="!disabled"
          v-model="newTagInput"
          @keydown.enter.prevent="addTag"
          @keydown.,.prevent="addTag"
          placeholder="Add tag and press Enter..."
          class="flex-1 min-w-[120px] bg-transparent text-xs outline-none px-1 py-0.5 placeholder:text-muted-foreground"
        />
      </div>
    </div>

    <!-- Validation Error Alert -->
    <div v-if="error" class="flex items-center gap-1.5 text-rose-500 text-[11px] mt-1 font-medium">
      <AlertCircle class="w-3.5 h-3.5 shrink-0" />
      <span>{{ error }}</span>
    </div>
  </div>
</template>
