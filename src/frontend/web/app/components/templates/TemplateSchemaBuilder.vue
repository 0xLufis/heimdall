<script setup lang="ts">
import { ref, computed } from 'vue'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Plus,
  Trash2,
  Copy,
  ArrowUp,
  ArrowDown,
  Edit2,
  Check,
  X,
  Sliders,
  Type,
  Hash,
  ToggleLeft,
  List,
  AlignLeft,
  Code2,
  Tags,
  ChevronDown,
  ChevronRight
} from 'lucide-vue-next'
import type { TemplateFieldDefinition, TemplateFieldOption, TemplateFieldType } from '~/types/templates'

const props = withDefaults(defineProps<{
  schema: TemplateFieldDefinition[]
  readonly?: boolean
}>(), {
  readonly: false
})

const emit = defineEmits<{
  (e: 'update:schema', value: TemplateFieldDefinition[]): void
}>()

const localFields = computed<TemplateFieldDefinition[]>({
  get: () => props.schema || [],
  set: (val) => emit('update:schema', val)
})

// Expanded field index for detailed editing
const editingIndex = ref<number | null>(null)

// Field type options with icons
const fieldTypeOptions: { type: TemplateFieldType; label: string; icon: any }[] = [
  { type: 'string', label: 'Text (String)', icon: Type },
  { type: 'number', label: 'Numeric (Float/Int)', icon: Hash },
  { type: 'boolean', label: 'Boolean (Switch)', icon: ToggleLeft },
  { type: 'select', label: 'Single Select', icon: List },
  { type: 'multiselect', label: 'Multi-Select', icon: List },
  { type: 'textarea', label: 'Multi-line Text', icon: AlignLeft },
  { type: 'code', label: 'Script / Code', icon: Code2 },
  { type: 'json', label: 'JSON Document', icon: Code2 },
  { type: 'tags', label: 'Semantic Tags', icon: Tags }
]

const addField = () => {
  const newIndex = localFields.value.length + 1
  const newField: TemplateFieldDefinition = {
    key: `field_${newIndex}`,
    label: `Custom Spec ${newIndex}`,
    fieldType: 'string',
    section: 'Specifications',
    required: false,
    order: newIndex,
    defaultValue: ''
  }
  const updated = [...localFields.value, newField]
  emit('update:schema', updated)
  editingIndex.value = updated.length - 1
}

const removeField = (index: number) => {
  const updated = [...localFields.value]
  updated.splice(index, 1)
  emit('update:schema', updated)
  if (editingIndex.value === index) {
    editingIndex.value = null
  } else if (editingIndex.value !== null && editingIndex.value > index) {
    editingIndex.value--
  }
}

const duplicateField = (index: number) => {
  const original = localFields.value[index]
  const copy: TemplateFieldDefinition = {
    ...JSON.parse(JSON.stringify(original)),
    key: `${original.key}_copy`,
    label: `${original.label} (Copy)`,
    isInherited: false,
    order: localFields.value.length + 1
  }
  const updated = [...localFields.value]
  updated.splice(index + 1, 0, copy)
  emit('update:schema', updated)
  editingIndex.value = index + 1
}

const moveField = (index: number, direction: 'up' | 'down') => {
  const target = direction === 'up' ? index - 1 : index + 1
  if (target < 0 || target >= localFields.value.length) return
  const updated = [...localFields.value]
  const temp = updated[index]
  updated[index] = updated[target]
  updated[target] = temp
  // Update order property
  updated.forEach((f, i) => f.order = i + 1)
  emit('update:schema', updated)
  if (editingIndex.value === index) {
    editingIndex.value = target
  }
}

const toggleEdit = (index: number) => {
  editingIndex.value = editingIndex.value === index ? null : index
}

// Option management for Select / MultiSelect
const newOptionLabel = ref('')
const newOptionValue = ref('')

const addOptionToField = (field: TemplateFieldDefinition) => {
  if (!newOptionLabel.value.trim()) return
  const val = newOptionValue.value.trim() || newOptionLabel.value.trim().toLowerCase().replace(/\s+/g, '_')
  if (!field.options) field.options = []
  field.options.push({
    label: newOptionLabel.value.trim(),
    value: val
  })
  newOptionLabel.value = ''
  newOptionValue.value = ''
  emit('update:schema', [...localFields.value])
}

const removeOptionFromField = (field: TemplateFieldDefinition, optIndex: number) => {
  if (!field.options) return
  field.options.splice(optIndex, 1)
  emit('update:schema', [...localFields.value])
}

// Slugify helper
const slugifyKey = (field: TemplateFieldDefinition) => {
  if (!field.key || field.key.startsWith('field_')) {
    field.key = field.label
      .toLowerCase()
      .trim()
      .replace(/[^\w\s-]/g, '')
      .replace(/[\s_-]+/g, '_')
  }
}
</script>

<template>
  <div class="space-y-3">
    <!-- Top Bar -->
    <div class="flex items-center justify-between pb-2 border-b">
      <div>
        <h4 class="text-xs font-semibold text-foreground uppercase tracking-wider">
          Visual Field Schema Designer
        </h4>
        <p class="text-[11px] text-muted-foreground">
          Define dynamic fields, data validation constraints, and groupings for this template.
        </p>
      </div>
      <Button
        v-if="!readonly"
        type="button"
        size="sm"
        @click="addField"
        class="h-7 text-xs gap-1.5 cursor-pointer"
      >
        <Plus class="w-3.5 h-3.5" />
        <span>Add Field</span>
      </Button>
    </div>

    <!-- Empty State -->
    <div
      v-if="localFields.length === 0"
      class="p-8 text-center border rounded-lg border-dashed text-muted-foreground bg-muted/10"
    >
      <Sliders class="w-8 h-8 mx-auto mb-2 opacity-40" />
      <p class="text-xs font-medium">No fields in this template schema</p>
      <p class="text-[11px] text-muted-foreground mt-0.5">Click "Add Field" above to configure specifications.</p>
    </div>

    <!-- Fields List -->
    <div v-else class="space-y-2">
      <div
        v-for="(field, index) in localFields"
        :key="field.key || index"
        class="border rounded-lg bg-card transition-all"
        :class="{ 'ring-1 ring-primary shadow-sm': editingIndex === index }"
      >
        <!-- Field Summary Row -->
        <div class="flex items-center justify-between px-3 py-2 bg-muted/20 hover:bg-muted/40 transition-colors">
          <div class="flex items-center gap-2 flex-1 min-w-0">
            <button
              type="button"
              @click="toggleEdit(index)"
              class="hover:text-primary transition-colors cursor-pointer"
            >
              <component
                :is="editingIndex === index ? ChevronDown : ChevronRight"
                class="w-3.5 h-3.5 text-muted-foreground"
              />
            </button>

            <!-- Type badge -->
            <Badge variant="outline" class="text-[10px] px-1.5 py-0 font-mono uppercase">
              {{ field.fieldType }}
            </Badge>

            <!-- Field Label & Key -->
            <div class="flex items-center gap-2 truncate">
              <span class="text-xs font-medium text-foreground truncate">{{ field.label }}</span>
              <span class="text-[10px] font-mono text-muted-foreground truncate opacity-70">({{ field.key }})</span>
            </div>

            <!-- Required badge -->
            <Badge
              v-if="field.required"
              variant="destructive"
              class="text-[9px] px-1 py-0 h-4"
            >
              Required
            </Badge>

            <!-- Section badge -->
            <Badge
              v-if="field.section"
              variant="secondary"
              class="text-[10px] px-1.5 py-0 hidden sm:inline-flex"
            >
              {{ field.section }}
            </Badge>

            <!-- Unit badge -->
            <Badge
              v-if="field.unit"
              variant="outline"
              class="text-[10px] px-1.5 py-0 font-mono text-muted-foreground hidden sm:inline-flex"
            >
              {{ field.unit }}
            </Badge>
          </div>

          <!-- Actions -->
          <div v-if="!readonly" class="flex items-center gap-1">
            <button
              type="button"
              :disabled="index === 0"
              @click="moveField(index, 'up')"
              class="p-1 hover:bg-muted rounded text-muted-foreground hover:text-foreground disabled:opacity-30 cursor-pointer"
              title="Move Up"
            >
              <ArrowUp class="w-3 h-3" />
            </button>
            <button
              type="button"
              :disabled="index === localFields.length - 1"
              @click="moveField(index, 'down')"
              class="p-1 hover:bg-muted rounded text-muted-foreground hover:text-foreground disabled:opacity-30 cursor-pointer"
              title="Move Down"
            >
              <ArrowDown class="w-3 h-3" />
            </button>
            <button
              type="button"
              @click="duplicateField(index)"
              class="p-1 hover:bg-muted rounded text-muted-foreground hover:text-foreground cursor-pointer"
              title="Duplicate Field"
            >
              <Copy class="w-3 h-3" />
            </button>
            <button
              type="button"
              @click="toggleEdit(index)"
              class="p-1 hover:bg-muted rounded text-muted-foreground hover:text-foreground cursor-pointer"
              title="Edit Field"
            >
              <Edit2 class="w-3 h-3" />
            </button>
            <button
              type="button"
              @click="removeField(index)"
              class="p-1 hover:bg-rose-500/10 rounded text-rose-500 hover:text-rose-600 cursor-pointer"
              title="Delete Field"
            >
              <Trash2 class="w-3 h-3" />
            </button>
          </div>
        </div>

        <!-- Expanded Field Editor Form -->
        <div v-if="editingIndex === index" class="p-3.5 border-t bg-card space-y-3">
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
            <!-- Label -->
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Display Label</Label>
              <Input
                v-model="field.label"
                @blur="slugifyKey(field)"
                placeholder="e.g. Rated Torque"
                class="h-7 text-xs"
              />
            </div>

            <!-- Key -->
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Semantic Key</Label>
              <Input
                v-model="field.key"
                placeholder="e.g. rated_torque_nm"
                class="h-7 text-xs font-mono"
              />
            </div>

            <!-- Field Type -->
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Data Type</Label>
              <select
                v-model="field.fieldType"
                class="w-full h-7 px-2 text-xs rounded-md border border-input bg-background"
              >
                <option
                  v-for="opt in fieldTypeOptions"
                  :key="opt.type"
                  :value="opt.type"
                >
                  {{ opt.label }}
                </option>
              </select>
            </div>

            <!-- Section -->
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Section / Group</Label>
              <Input
                v-model="field.section"
                placeholder="e.g. Specifications"
                class="h-7 text-xs"
              />
            </div>

            <!-- Unit -->
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Unit of Measure</Label>
              <Input
                v-model="field.unit"
                placeholder="e.g. RPM, bar, °C, ms"
                class="h-7 text-xs font-mono"
              />
            </div>

            <!-- Default Value -->
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Default Value</Label>
              <Input
                v-model="field.defaultValue"
                placeholder="Initial default value"
                class="h-7 text-xs font-mono"
              />
            </div>
          </div>

          <!-- Constraints & Flags -->
          <div class="flex flex-wrap items-center gap-4 pt-1 text-xs">
            <!-- Required switch -->
            <label class="flex items-center gap-1.5 cursor-pointer">
              <input
                type="checkbox"
                v-model="field.required"
                class="rounded border-input text-primary focus:ring-primary h-3.5 w-3.5"
              />
              <span class="font-medium">Mandatory / Required Field</span>
            </label>

            <!-- Numeric bounds if number -->
            <div v-if="field.fieldType === 'number'" class="flex items-center gap-2">
              <div class="flex items-center gap-1">
                <span class="text-[11px] text-muted-foreground">Min:</span>
                <input
                  type="number"
                  v-model.number="field.min"
                  placeholder="-∞"
                  class="w-16 h-6 px-1.5 text-xs rounded border border-input bg-background font-mono"
                />
              </div>
              <div class="flex items-center gap-1">
                <span class="text-[11px] text-muted-foreground">Max:</span>
                <input
                  type="number"
                  v-model.number="field.max"
                  placeholder="+∞"
                  class="w-16 h-6 px-1.5 text-xs rounded border border-input bg-background font-mono"
                />
              </div>
            </div>
          </div>

          <!-- Description / Placeholder -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Help Text / Description</Label>
              <Input
                v-model="field.description"
                placeholder="Explains what this field configures..."
                class="h-7 text-xs"
              />
            </div>
            <div class="space-y-1">
              <Label class="text-[11px] font-medium">Input Placeholder</Label>
              <Input
                v-model="field.placeholder"
                placeholder="e.g. Enter target speed..."
                class="h-7 text-xs"
              />
            </div>
          </div>

          <!-- Select / MultiSelect Options Builder -->
          <div
            v-if="field.fieldType === 'select' || field.fieldType === 'multiselect'"
            class="space-y-2 p-2.5 rounded bg-muted/30 border"
          >
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold">Dropdown Options List</span>
              <span class="text-[10px] text-muted-foreground">{{ (field.options || []).length }} options</span>
            </div>

            <!-- Current options chips -->
            <div class="flex flex-wrap gap-1.5 min-h-6">
              <div
                v-for="(opt, optIdx) in (field.options || [])"
                :key="opt.value"
                class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs bg-background border font-mono"
              >
                <span>{{ opt.label }}</span>
                <span class="text-[10px] text-muted-foreground">({{ opt.value }})</span>
                <button
                  type="button"
                  @click="removeOptionFromField(field, optIdx)"
                  class="text-muted-foreground hover:text-rose-500 ml-1"
                >
                  <X class="w-3 h-3" />
                </button>
              </div>
            </div>

            <!-- Add new option row -->
            <div class="flex items-center gap-2 pt-1">
              <Input
                v-model="newOptionLabel"
                placeholder="Option Label (e.g. EtherCAT)"
                class="h-7 text-xs flex-1"
              />
              <Input
                v-model="newOptionValue"
                placeholder="Option Value (e.g. ETHERCAT)"
                class="h-7 text-xs flex-1 font-mono"
                @keydown.enter.prevent="addOptionToField(field)"
              />
              <Button
                type="button"
                size="sm"
                variant="secondary"
                @click="addOptionToField(field)"
                class="h-7 text-xs px-2 cursor-pointer"
              >
                <Plus class="w-3 h-3 mr-1" />
                <span>Add</span>
              </Button>
            </div>
          </div>

          <!-- Done Editing Button -->
          <div class="flex justify-end pt-1">
            <Button
              type="button"
              variant="outline"
              size="sm"
              @click="editingIndex = null"
              class="h-6 text-xs gap-1 cursor-pointer"
            >
              <Check class="w-3 h-3" />
              <span>Done Editing</span>
            </Button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
