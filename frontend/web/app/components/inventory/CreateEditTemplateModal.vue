<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  FolderTree,
  Cpu,
  Code,
  GitBranch,
  Plus,
  Trash2,
  Check,
  AlertCircle,
  Tag,
  Boxes,
  Server,
  Camera,
  Wrench,
  ShieldCheck,
  Info
} from 'lucide-vue-next'
import type {
  AssetTemplateDefinition,
  AssetTemplateSchemaField,
  AssetTemplateFixedFields
} from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  open: boolean
  mode?: 'create' | 'edit'
  template?: AssetTemplateDefinition | null
  availableTemplates?: AssetTemplateDefinition[]
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved', template: AssetTemplateDefinition): void
}>()

const { createTemplate, updateTemplate } = usePartsInventory()

const activeTab = ref<'basic' | 'fixed' | 'schema'>('basic')
const isSaving = ref(false)
const errorMessage = ref<string | null>(null)

// Form state
const formName = ref('')
const formTopLevelCategory = ref<'Hardware' | 'Software'>('Hardware')
const formExtendsTemplateId = ref<string>('')
const formIdentifierPattern = ref('AST-{number}')
const formSequentialCounter = ref(100)
const formDescription = ref('')
const formIcon = ref('Cpu')
const formTags = ref('')

// Fixed Fields
const formManufacturer = ref('')
const formSupplier = ref('')
const formCategory = ref('')
const formBasePriceEur = ref(0)
const formDefaultOwnerType = ref<'organization' | 'machine' | 'user'>('organization')
const formDefaultOwnerName = ref('Heimdall Manufacturing Org')
const dynamicSpecs = ref<Array<{ key: string; value: string }>>([])

// Schema Fields
const schemaFields = ref<Array<{
  key: string
  label: string
  type: 'string' | 'number' | 'boolean' | 'date' | 'select'
  required: boolean
  defaultValue?: string
  options?: string
}>>([])

const availableParents = computed(() => {
  if (!props.availableTemplates) return []
  if (props.mode === 'edit' && props.template) {
    return props.availableTemplates.filter(t => t.id !== props.template!.id)
  }
  return props.availableTemplates
})

const initForm = () => {
  errorMessage.value = null
  activeTab.value = 'basic'

  if (props.mode === 'edit' && props.template) {
    const t = props.template
    formName.value = t.name
    formTopLevelCategory.value = t.topLevelCategory
    formExtendsTemplateId.value = t.extendsTemplateId || ''
    formIdentifierPattern.value = t.identifierPattern || 'AST-{number}'
    formSequentialCounter.value = t.sequentialCounter || 100
    formDescription.value = t.description || ''
    formIcon.value = t.icon || (t.topLevelCategory === 'Software' ? 'Code' : 'Cpu')
    formTags.value = (t.tags || []).join(', ')

    formManufacturer.value = t.fixedFields?.manufacturer || ''
    formSupplier.value = t.fixedFields?.supplier || ''
    formCategory.value = t.fixedFields?.category || ''
    formBasePriceEur.value = t.fixedFields?.basePriceEur || 0
    formDefaultOwnerType.value = t.fixedFields?.defaultOwner?.type || 'organization'
    formDefaultOwnerName.value = t.fixedFields?.defaultOwner?.name || 'Heimdall Manufacturing Org'

    dynamicSpecs.value = Object.entries(t.fixedFields?.specs || {}).map(([key, value]) => ({
      key,
      value: String(value)
    }))

    schemaFields.value = (t.instanceSpecificFieldsSchema || []).map(f => ({
      key: f.key,
      label: f.label,
      type: f.type,
      required: Boolean(f.required),
      defaultValue: f.defaultValue !== undefined ? String(f.defaultValue) : '',
      options: Array.isArray(f.options) ? f.options.join(', ') : ''
    }))
  } else {
    // Defaults for new template
    formName.value = ''
    formTopLevelCategory.value = 'Hardware'
    formExtendsTemplateId.value = ''
    formIdentifierPattern.value = 'AST-{number}'
    formSequentialCounter.value = 100
    formDescription.value = ''
    formIcon.value = 'Cpu'
    formTags.value = 'Industrial, Hardware'

    formManufacturer.value = ''
    formSupplier.value = ''
    formCategory.value = 'Hardware'
    formBasePriceEur.value = 500
    formDefaultOwnerType.value = 'organization'
    formDefaultOwnerName.value = 'Heimdall Manufacturing Org'

    dynamicSpecs.value = [
      { key: 'formFactor', value: 'DIN-Rail Mount' },
      { key: 'ipRating', value: 'IP65' }
    ]

    schemaFields.value = [
      { key: 'serialNumber', label: 'Serial Number', type: 'string', required: true, defaultValue: '' },
      { key: 'location', label: 'Storage / Bin Location', type: 'string', required: true, defaultValue: 'Cabinet-A' }
    ]
  }
}

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    initForm()
  }
})

const addSpecRow = () => {
  dynamicSpecs.value.push({ key: '', value: '' })
}

const removeSpecRow = (idx: number) => {
  dynamicSpecs.value.splice(idx, 1)
}

const addSchemaField = () => {
  schemaFields.value.push({
    key: `field_${schemaFields.value.length + 1}`,
    label: 'New Field',
    type: 'string',
    required: false,
    defaultValue: ''
  })
}

const removeSchemaField = (idx: number) => {
  schemaFields.value.splice(idx, 1)
}

const onInheritChange = () => {
  if (!formExtendsTemplateId.value) return
  const parent = props.availableTemplates?.find(t => t.id === formExtendsTemplateId.value)
  if (parent) {
    if (!formManufacturer.value) formManufacturer.value = parent.fixedFields.manufacturer || ''
    if (!formSupplier.value) formSupplier.value = parent.fixedFields.supplier || ''
    if (formTopLevelCategory.value !== parent.topLevelCategory) {
      formTopLevelCategory.value = parent.topLevelCategory
    }
  }
}

const handleSave = async () => {
  errorMessage.value = null
  if (!formName.value.trim()) {
    errorMessage.value = 'Template name is mandatory.'
    activeTab.value = 'basic'
    return
  }

  // Construct specs object
  const specsObj: Record<string, string | number | boolean> = {}
  for (const s of dynamicSpecs.value) {
    if (s.key.trim()) {
      specsObj[s.key.trim()] = s.value.trim()
    }
  }

  // Construct instance schema
  const constructedSchema: AssetTemplateSchemaField[] = schemaFields.value
    .filter(f => f.key.trim() && f.label.trim())
    .map(f => {
      const field: AssetTemplateSchemaField = {
        key: f.key.trim(),
        label: f.label.trim(),
        type: f.type,
        required: f.required
      }
      if (f.defaultValue && f.defaultValue.trim()) {
        field.defaultValue = f.defaultValue.trim()
      }
      if (f.type === 'select' && f.options && f.options.trim()) {
        field.options = f.options.split(',').map(o => o.trim()).filter(Boolean)
      }
      return field
    })

  const fixedFields: AssetTemplateFixedFields = {
    manufacturer: formManufacturer.value.trim(),
    supplier: formSupplier.value.trim(),
    category: formCategory.value.trim() || formTopLevelCategory.value,
    basePriceEur: Number(formBasePriceEur.value) || 0,
    specs: specsObj,
    defaultOwner: {
      type: formDefaultOwnerType.value,
      id: formDefaultOwnerType.value === 'organization' ? 'org-root' : 'ent-custom',
      name: formDefaultOwnerName.value.trim() || 'Heimdall Manufacturing Org'
    }
  }

  const tagList = formTags.value
    .split(',')
    .map(t => t.trim())
    .filter(Boolean)

  const payload: Partial<AssetTemplateDefinition> = {
    name: formName.value.trim(),
    topLevelCategory: formTopLevelCategory.value,
    extendsTemplateId: formExtendsTemplateId.value || undefined,
    identifierPattern: formIdentifierPattern.value.trim() || 'AST-{number}',
    sequentialCounter: Number(formSequentialCounter.value) || 100,
    description: formDescription.value.trim(),
    icon: formIcon.value.trim() || (formTopLevelCategory.value === 'Software' ? 'Code' : 'Cpu'),
    fixedFields,
    instanceSpecificFieldsSchema: constructedSchema,
    tags: tagList
  }

  isSaving.value = true
  try {
    let result: AssetTemplateDefinition
    if (props.mode === 'edit' && props.template) {
      result = await updateTemplate(props.template.id, payload)
    } else {
      result = await createTemplate(payload)
    }
    emit('saved', result)
    emit('update:open', false)
  } catch (err: any) {
    errorMessage.value = err.message || 'Failed to save template.'
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="$emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[90vh] flex flex-col p-0 overflow-hidden bg-card border border-border shadow-xl">
      <!-- Header -->
      <DialogHeader class="p-5 pb-3 border-b border-border bg-muted/20">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-xl bg-primary/10 border border-primary/20 text-primary">
            <FolderTree class="size-5" />
          </div>
          <div>
            <DialogTitle class="text-lg font-bold text-foreground">
              {{ mode === 'edit' ? `Edit Template: ${template?.name}` : 'Create Asset Template' }}
            </DialogTitle>
            <DialogDescription class="text-xs text-muted-foreground mt-0.5">
              Define reusable asset blueprints with OOP inheritance, fixed OEM specs, and instance schema
            </DialogDescription>
          </div>
        </div>

        <!-- Navigation Sub-Tabs -->
        <div class="flex items-center gap-1.5 mt-4 p-1 bg-muted/60 rounded-lg border border-border w-fit text-xs">
          <button
            type="button"
            @click="activeTab = 'basic'"
            class="px-3 py-1 rounded-md font-semibold transition-all cursor-pointer"
            :class="activeTab === 'basic' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            1. Identity & Scheme
          </button>
          <button
            type="button"
            @click="activeTab = 'fixed'"
            class="px-3 py-1 rounded-md font-semibold transition-all cursor-pointer"
            :class="activeTab === 'fixed' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            2. OEM & Fixed Specs
          </button>
          <button
            type="button"
            @click="activeTab = 'schema'"
            class="px-3 py-1 rounded-md font-semibold transition-all cursor-pointer"
            :class="activeTab === 'schema' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            3. Instance Schema ({{ schemaFields.length }})
          </button>
        </div>
      </DialogHeader>

      <!-- Scrollable Form Body -->
      <div class="p-6 overflow-y-auto custom-scrollbar flex-1 space-y-5">
        <!-- Error Banner -->
        <div
          v-if="errorMessage"
          class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2"
        >
          <AlertCircle class="size-4 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>

        <!-- TAB 1: Identity & Scheme -->
        <div v-show="activeTab === 'basic'" class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Template Name *</Label>
              <Input
                v-model="formName"
                placeholder="e.g. Beckhoff CX5140 Modular Controller IPC"
                class="text-xs"
              />
            </div>

            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Top-Level Category</Label>
              <div class="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  @click="formTopLevelCategory = 'Hardware'"
                  class="flex-1 text-xs gap-1.5 cursor-pointer"
                  :class="formTopLevelCategory === 'Hardware' ? 'border-primary bg-primary/10 text-primary font-bold' : ''"
                >
                  <Cpu class="size-3.5" />
                  <span>Hardware</span>
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  @click="formTopLevelCategory = 'Software'"
                  class="flex-1 text-xs gap-1.5 cursor-pointer"
                  :class="formTopLevelCategory === 'Software' ? 'border-primary bg-primary/10 text-primary font-bold' : ''"
                >
                  <Code class="size-3.5" />
                  <span>Software</span>
                </Button>
              </div>
            </div>
          </div>

          <!-- Inheritance Selector -->
          <div class="space-y-1.5 p-3 rounded-lg bg-muted/20 border border-border">
            <div class="flex items-center gap-1.5 text-xs font-semibold text-foreground">
              <GitBranch class="size-3.5 text-purple-500" />
              <span>OOP Inheritance (Extends Parent Template)</span>
            </div>
            <select
              v-model="formExtendsTemplateId"
              @change="onInheritChange"
              class="w-full h-8 px-2.5 bg-background border border-border rounded-md text-xs text-foreground focus:outline-hidden focus:border-primary"
            >
              <option value="">None (Root Template - Standalone)</option>
              <option
                v-for="parent in availableParents"
                :key="parent.id"
                :value="parent.id"
              >
                {{ parent.name }} ({{ parent.identifierPattern }})
              </option>
            </select>
            <p class="text-[11px] text-muted-foreground">
              Child templates inherit root attributes, default configurations, and can extend instance fields.
            </p>
          </div>

          <!-- Identifier Pattern & Counter -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Identifier Pattern</Label>
              <Input
                v-model="formIdentifierPattern"
                placeholder="e.g. IPC-{number}, CAM-{number}"
                class="text-xs font-mono"
              />
              <p class="text-[11px] text-muted-foreground">
                Replaces <code class="font-mono">{number}</code> with sequential counter
              </p>
            </div>

            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Next Sequential Counter</Label>
              <Input
                v-model.number="formSequentialCounter"
                type="number"
                min="1"
                class="text-xs font-mono"
              />
            </div>
          </div>

          <!-- Description & Tags -->
          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Description & Technical Role</Label>
            <textarea
              v-model="formDescription"
              rows="3"
              placeholder="Describe operating envelope, intended plant location, or system integration details..."
              class="w-full p-2.5 bg-background border border-border rounded-md text-xs text-foreground focus:outline-hidden focus:border-primary"
            />
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Search Tags (Comma-separated)</Label>
            <Input
              v-model="formTags"
              placeholder="Beckhoff, IPC, PLC, Realtime, EtherCAT"
              class="text-xs"
            />
          </div>
        </div>

        <!-- TAB 2: OEM & Fixed Specs -->
        <div v-show="activeTab === 'fixed'" class="space-y-4">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">OEM Manufacturer</Label>
              <Input
                v-model="formManufacturer"
                placeholder="e.g. Beckhoff Automation, Festo, Siemens"
                class="text-xs"
              />
            </div>

            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Authorized Supplier</Label>
              <Input
                v-model="formSupplier"
                placeholder="e.g. Beckhoff Direct Germany, RS Components"
                class="text-xs"
              />
            </div>

            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Sub-Category</Label>
              <Input
                v-model="formCategory"
                placeholder="e.g. Controller, Vision System, Valve Terminal"
                class="text-xs"
              />
            </div>

            <div class="space-y-1.5">
              <Label class="text-xs font-semibold text-foreground">Base List Price (EUR €)</Label>
              <Input
                v-model.number="formBasePriceEur"
                type="number"
                min="0"
                step="0.01"
                class="text-xs font-mono"
              />
            </div>
          </div>

          <!-- Default Owner -->
          <div class="p-3 rounded-lg bg-muted/20 border border-border space-y-2">
            <div class="text-xs font-semibold text-foreground">Default Asset Ownership</div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <Label class="text-[11px] text-muted-foreground">Owner Entity Type</Label>
                <select
                  v-model="formDefaultOwnerType"
                  class="w-full h-8 px-2.5 bg-background border border-border rounded-md text-xs text-foreground focus:outline-hidden focus:border-primary"
                >
                  <option value="organization">Plant Organization</option>
                  <option value="machine">Machine Host</option>
                  <option value="user">Individual Custodian</option>
                </select>
              </div>
              <div>
                <Label class="text-[11px] text-muted-foreground">Owner Entity Name</Label>
                <Input
                  v-model="formDefaultOwnerName"
                  placeholder="Heimdall Manufacturing Org"
                  class="text-xs"
                />
              </div>
            </div>
          </div>

          <!-- Dynamic Key-Value Specs Editor -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <div class="text-xs font-semibold text-foreground">
                Fixed Specifications (Shared across all instances)
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                @click="addSpecRow"
                class="h-7 text-xs gap-1 cursor-pointer"
              >
                <Plus class="size-3" />
                <span>Add Spec</span>
              </Button>
            </div>

            <div class="space-y-2">
              <div
                v-for="(spec, idx) in dynamicSpecs"
                :key="idx"
                class="flex items-center gap-2"
              >
                <Input
                  v-model="spec.key"
                  placeholder="Key (e.g. ipRating, powerSupply)"
                  class="flex-1 text-xs font-mono"
                />
                <Input
                  v-model="spec.value"
                  placeholder="Value (e.g. IP65, 24V DC)"
                  class="flex-1 text-xs"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  @click="removeSpecRow(idx)"
                  class="h-8 w-8 p-0 text-muted-foreground hover:text-destructive cursor-pointer"
                >
                  <Trash2 class="size-3.5" />
                </Button>
              </div>
              <div v-if="dynamicSpecs.length === 0" class="text-xs text-muted-foreground italic py-2 text-center">
                No custom specs configured yet. Click "Add Spec" above.
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 3: Instance Specific Schema Builder -->
        <div v-show="activeTab === 'schema'" class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-xs font-semibold text-foreground">
                Instance-Specific Field Schema
              </div>
              <p class="text-[11px] text-muted-foreground">
                Fields collected whenever an individual serialized unit is logged into stock
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              @click="addSchemaField"
              class="h-7 text-xs gap-1 cursor-pointer"
            >
              <Plus class="size-3" />
              <span>Add Field</span>
            </Button>
          </div>

          <div class="space-y-3">
            <div
              v-for="(field, idx) in schemaFields"
              :key="idx"
              class="p-3.5 rounded-lg border border-border bg-card space-y-2.5 shadow-xs"
            >
              <div class="flex items-center justify-between gap-2">
                <span class="text-xs font-bold text-primary font-mono">Field #{{ idx + 1 }}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  @click="removeSchemaField(idx)"
                  class="h-6 px-2 text-xs text-muted-foreground hover:text-destructive cursor-pointer"
                >
                  <Trash2 class="size-3 mr-1" />
                  <span>Remove</span>
                </Button>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
                <div>
                  <Label class="text-[11px] text-muted-foreground">Field Key (JSON property)</Label>
                  <Input
                    v-model="field.key"
                    placeholder="e.g. firmwareVersion"
                    class="text-xs font-mono"
                  />
                </div>
                <div>
                  <Label class="text-[11px] text-muted-foreground">Display Label</Label>
                  <Input
                    v-model="field.label"
                    placeholder="e.g. Firmware Build"
                    class="text-xs"
                  />
                </div>
                <div>
                  <Label class="text-[11px] text-muted-foreground">Data Type</Label>
                  <select
                    v-model="field.type"
                    class="w-full h-8 px-2 bg-background border border-border rounded-md text-xs text-foreground focus:outline-hidden focus:border-primary"
                  >
                    <option value="string">String / Text</option>
                    <option value="number">Number</option>
                    <option value="boolean">Boolean (Yes/No)</option>
                    <option value="date">Date</option>
                    <option value="select">Dropdown Select</option>
                  </select>
                </div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5 pt-1">
                <div>
                  <Label class="text-[11px] text-muted-foreground">Default Value (Optional)</Label>
                  <Input
                    v-model="field.defaultValue"
                    placeholder="e.g. Cabinet-1 / v1.0.0"
                    class="text-xs"
                  />
                </div>
                <div v-if="field.type === 'select'">
                  <Label class="text-[11px] text-muted-foreground">Options (Comma-separated)</Label>
                  <Input
                    v-model="field.options"
                    placeholder="Option A, Option B, Option C"
                    class="text-xs"
                  />
                </div>
                <div class="flex items-center gap-2 pt-4">
                  <input
                    :id="`req_${idx}`"
                    v-model="field.required"
                    type="checkbox"
                    class="rounded border-border text-primary focus:ring-primary size-4"
                  />
                  <Label :for="`req_${idx}`" class="text-xs text-foreground cursor-pointer select-none">
                    Mandatory field during intake
                  </Label>
                </div>
              </div>
            </div>

            <div v-if="schemaFields.length === 0" class="text-xs text-muted-foreground italic py-6 text-center border border-dashed border-border rounded-lg">
              No instance-specific fields defined yet. Click "Add Field" to define properties.
            </div>
          </div>
        </div>
      </div>

      <!-- Footer Actions -->
      <DialogFooter class="p-4 border-t border-border bg-muted/20 flex items-center justify-between sm:justify-between">
        <div class="text-[11px] text-muted-foreground flex items-center gap-1.5">
          <Info class="size-3.5 text-primary" />
          <span>Templates are synchronized across all connected plant terminals</span>
        </div>

        <div class="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            @click="$emit('update:open', false)"
            class="text-xs cursor-pointer"
          >
            Cancel
          </Button>

          <Button
            type="button"
            size="sm"
            :disabled="isSaving"
            @click="handleSave"
            class="text-xs gap-1.5 cursor-pointer bg-primary text-primary-foreground font-semibold"
          >
            <Check class="size-3.5" />
            <span>{{ mode === 'edit' ? 'Save Changes' : 'Create Template' }}</span>
          </Button>
        </div>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
