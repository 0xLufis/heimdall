<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { CheckCircle2, AlertTriangle, Layers, ChevronDown, ChevronRight } from 'lucide-vue-next'
import TemplateFieldRenderer from './TemplateFieldRenderer.vue'
import type { TemplateFieldDefinition } from '~/types/templates'

const props = withDefaults(defineProps<{
  schema: TemplateFieldDefinition[]
  modelValue?: Record<string, any>
  errors?: Record<string, string>
  disabled?: boolean
  readonly?: boolean
  showSummary?: boolean
}>(), {
  modelValue: () => ({}),
  errors: () => ({}),
  disabled: false,
  readonly: false,
  showSummary: true
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: Record<string, any>): void
  (e: 'fieldChange', key: string, value: any): void
}>()

// Internal reactive form state
const formData = ref<Record<string, any>>({ ...props.modelValue })

watch(() => props.modelValue, (newVal) => {
  if (newVal) {
    formData.value = { ...newVal }
  }
}, { deep: true })

const updateField = (key: string, val: any) => {
  formData.value[key] = val
  emit('update:modelValue', { ...formData.value })
  emit('fieldChange', key, val)
}

// Collapsed sections state
const collapsedSections = ref<Set<string>>(new Set())

const toggleSection = (sectionName: string) => {
  if (collapsedSections.value.has(sectionName)) {
    collapsedSections.value.delete(sectionName)
  } else {
    collapsedSections.value.add(sectionName)
  }
}

// Group fields by section
interface SectionGroup {
  name: string
  fields: TemplateFieldDefinition[]
}

const sectionGroups = computed<SectionGroup[]>(() => {
  const map = new Map<string, TemplateFieldDefinition[]>()
  
  // Sort fields by section and order
  const sorted = [...props.schema].sort((a, b) => {
    const sDiff = (a.section || 'General').localeCompare(b.section || 'General')
    if (sDiff !== 0) return sDiff
    return (a.order || 0) - (b.order || 0)
  })

  for (const field of sorted) {
    const sec = field.section || 'General'
    if (!map.has(sec)) {
      map.set(sec, [])
    }
    map.get(sec)!.push(field)
  }

  return Array.from(map.entries()).map(([name, fields]) => ({
    name,
    fields
  }))
})

// Validation status calculations
const totalFields = computed(() => props.schema.length)
const requiredFields = computed(() => props.schema.filter(f => f.required))
const missingRequiredFields = computed(() => {
  return requiredFields.value.filter(f => {
    const val = formData.value[f.key]
    return val === undefined || val === null || val === '' || (Array.isArray(val) && val.length === 0)
  })
})

const isFormValid = computed(() => missingRequiredFields.value.length === 0 && Object.keys(props.errors).length === 0)
</script>

<template>
  <div class="space-y-4">
    <!-- Validation status bar -->
    <div
      v-if="showSummary && totalFields > 0"
      class="flex items-center justify-between p-2.5 rounded-lg border text-xs"
      :class="isFormValid ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-700 dark:text-emerald-300' : 'bg-amber-500/10 border-amber-500/30 text-amber-700 dark:text-amber-300'"
    >
      <div class="flex items-center gap-2">
        <CheckCircle2 v-if="isFormValid" class="w-4 h-4 text-emerald-500" />
        <AlertTriangle v-else class="w-4 h-4 text-amber-500" />
        <span class="font-medium">
          {{ isFormValid ? 'All required template specifications satisfied' : `${missingRequiredFields.length} required field(s) require input` }}
        </span>
      </div>
      <div class="flex items-center gap-2">
        <Badge variant="outline" class="text-[10px] font-mono">
          {{ totalFields }} specs defined
        </Badge>
        <Badge
          v-if="missingRequiredFields.length > 0"
          variant="destructive"
          class="text-[10px]"
        >
          {{ missingRequiredFields.length }} missing
        </Badge>
      </div>
    </div>

    <!-- Empty Schema State -->
    <div
      v-if="schema.length === 0"
      class="p-8 text-center border rounded-lg border-dashed text-muted-foreground"
    >
      <Layers class="w-8 h-8 mx-auto mb-2 opacity-40" />
      <p class="text-sm font-medium">No dynamic fields defined in this schema</p>
      <p class="text-xs opacity-75 mt-1">Add fields in the Schema Builder to customize this template</p>
    </div>

    <!-- Grouped Sections -->
    <div v-else class="space-y-3">
      <div
        v-for="group in sectionGroups"
        :key="group.name"
        class="border rounded-lg bg-card overflow-hidden transition-shadow"
      >
        <!-- Section Header -->
        <button
          type="button"
          @click="toggleSection(group.name)"
          class="w-full flex items-center justify-between px-3.5 py-2.5 bg-muted/30 hover:bg-muted/50 border-b transition-colors text-left"
        >
          <div class="flex items-center gap-2">
            <component
              :is="collapsedSections.has(group.name) ? ChevronRight : ChevronDown"
              class="w-3.5 h-3.5 text-muted-foreground"
            />
            <span class="text-xs font-semibold text-foreground tracking-wide uppercase">
              {{ group.name }}
            </span>
            <Badge variant="secondary" class="text-[10px] px-1.5 py-0 font-mono">
              {{ group.fields.length }}
            </Badge>
          </div>
          <div class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
            <span v-if="group.fields.some(f => f.required)" class="text-rose-500 font-bold">*</span>
            <span>{{ collapsedSections.has(group.name) ? 'Expand' : 'Collapse' }}</span>
          </div>
        </button>

        <!-- Section Fields -->
        <div
          v-show="!collapsedSections.has(group.name)"
          class="p-4 grid grid-cols-1 md:grid-cols-2 gap-4"
        >
          <div
            v-for="field in group.fields"
            :key="field.key"
            :class="[
              field.fieldType === 'textarea' || field.fieldType === 'code' || field.fieldType === 'json' || field.fieldType === 'tags'
                ? 'md:col-span-2'
                : ''
            ]"
          >
            <TemplateFieldRenderer
              :field="field"
              :model-value="formData[field.key]"
              :error="errors[field.key]"
              :disabled="disabled || readonly"
              @update:model-value="(val) => updateField(field.key, val)"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
