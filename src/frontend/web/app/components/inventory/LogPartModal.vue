<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  X,
  PackagePlus,
  Cpu,
  Boxes,
  Building,
  DollarSign,
  Tag,
  MapPin,
  CheckCircle2,
  Sliders,
  AlertTriangle
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type {
  InventoryPart,
  AssetTemplateDefinition,
  PartTrackingType,
  PartLifecycleCondition,
  PartOperationalState
} from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  open: boolean
  templates?: AssetTemplateDefinition[]
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'logged', part: Partial<InventoryPart>): void
}>()

const { indexedFields, fetchIndexedFields } = usePartsInventory()

const selectedTemplateId = ref<string>('')
const trackingType = ref<PartTrackingType>('serialized')
const name = ref('')
const category = ref<'Hardware' | 'Software'>('Hardware')
const manufacturer = ref('')
const supplier = ref('')
const ownerType = ref<'organization' | 'user'>('organization')
const ownerName = ref('Heimdall Manufacturing Org')
const location = ref('Warehouse Central / Rack 1')
const priceEur = ref<number>(500)
const condition = ref<PartLifecycleCondition>('new')
const operationalState = ref<PartOperationalState>('working')
const wearDepreciation = ref<number>(0)
const resellPriceOverride = ref<number | undefined>(undefined)
const quantity = ref<number>(1)
const minQuantity = ref<number>(1)
const minQuantityScalar = ref<number>(1.0)
const serialNumber = ref('')
const customIdentifier = ref('')
const specValues = ref<Record<string, any>>({})

const errorMessage = ref('')
const isSubmitting = ref(false)

const LIFECYCLE_CONDITIONS: { value: PartLifecycleCondition; label: string; desc: string }[] = [
  { value: 'new', label: 'New', desc: 'Brand new OEM part' },
  { value: 'used', label: 'Used', desc: 'Used but fully serviceable' },
  { value: 'donor', label: 'Donor', desc: 'Kept for harvesting donor components' },
  { value: 'obsolete', label: 'Obsolete', desc: 'Discontinued or deprecated model' },
  { value: 'scrap', label: 'Scrap', desc: 'Unserviceable - can be got rid of' }
]

const OPERATIONAL_STATES: { value: PartOperationalState; label: string }[] = [
  { value: 'working', label: 'Working / Verified' },
  { value: 'in_service', label: 'In Service / Maintenance' },
  { value: 'evaluation', label: 'Waiting for Evaluation' },
  { value: 'broken', label: 'Broken / Defective' }
]

const selectedTemplate = computed(() => {
  if (!selectedTemplateId.value || !props.templates) return null
  return props.templates.find(t => t.id === selectedTemplateId.value) || null
})

watch(
  () => selectedTemplateId.value,
  (tmplId) => {
    specValues.value = {}
    if (!tmplId || !props.templates) return
    const tmpl = props.templates.find(t => t.id === tmplId)
    if (tmpl) {
      name.value = tmpl.name
      category.value = tmpl.topLevelCategory
      manufacturer.value = tmpl.fixedFields.manufacturer
      supplier.value = tmpl.fixedFields.supplier || ''
      priceEur.value = tmpl.fixedFields.basePriceEur
      if (tmpl.identifierPattern) {
        customIdentifier.value = tmpl.identifierPattern.replace('{number}', String(tmpl.sequentialCounter + 1).padStart(4, '0'))
      }
      if (tmpl.specFields) {
        for (const f of tmpl.specFields) {
          specValues.value[f.key] = ''
        }
      }
    }
  }
)

watch(
  () => props.open,
  async (val) => {
    if (val) {
      errorMessage.value = ''
      selectedTemplateId.value = ''
      trackingType.value = 'serialized'
      name.value = ''
      category.value = 'Hardware'
      manufacturer.value = 'Siemens'
      supplier.value = 'Siemens Industrial Sales'
      ownerType.value = 'organization'
      ownerName.value = 'Heimdall Manufacturing Org'
      location.value = 'Warehouse Central / Rack 1'
      priceEur.value = 500
      condition.value = 'new'
      operationalState.value = 'working'
      wearDepreciation.value = 0
      resellPriceOverride.value = undefined
      quantity.value = 1
      minQuantity.value = 1
      minQuantityScalar.value = 1.0
      serialNumber.value = `SN-${Date.now().toString().slice(-6)}`
      customIdentifier.value = ''
      specValues.value = {}

      await fetchIndexedFields()
    }
  }
)

const effectiveMinPreview = computed(() => {
  const scalar = minQuantityScalar.value ? Number(minQuantityScalar.value) : 1.0
  return Math.round(Number(minQuantity.value || 0) * scalar * 100) / 100
})

const calculatedResellPrice = computed(() => {
  if (resellPriceOverride.value !== undefined && resellPriceOverride.value !== null && String(resellPriceOverride.value) !== '') {
    return Number(resellPriceOverride.value)
  }
  const wear = condition.value === 'new' ? 0 : wearDepreciation.value
  return Math.round(priceEur.value * (1 - (wear / 100)) * 100) / 100
})

const isValid = computed(() => {
  return name.value.trim().length > 0 && manufacturer.value.trim().length > 0 && priceEur.value >= 0
})

const handleSubmit = () => {
  if (!isValid.value) return
  errorMessage.value = ''

  // Validate required template spec fields
  if (selectedTemplate.value?.specFields) {
    for (const f of selectedTemplate.value.specFields) {
      if (f.required && (specValues.value[f.key] === undefined || specValues.value[f.key] === null || String(specValues.value[f.key]).trim() === '')) {
        errorMessage.value = `Required specification attribute missing: "${f.label}".`
        return
      }
    }
  }

  isSubmitting.value = true

  const payload: Partial<InventoryPart> = {
    templateId: selectedTemplateId.value || undefined,
    name: name.value.trim(),
    category: category.value,
    trackingType: trackingType.value,
    condition: condition.value,
    operationalState: trackingType.value === 'serialized' ? operationalState.value : undefined,
    manufacturer: { name: manufacturer.value.trim() },
    supplier: supplier.value ? { name: supplier.value.trim() } : undefined,
    owner: {
      type: ownerType.value,
      id: ownerType.value === 'organization' ? 'org-root' : 'usr-current',
      name: ownerName.value.trim()
    },
    location: location.value.trim(),
    priceEur: priceEur.value,
    wearDepreciationPercentage: wearDepreciation.value,
    resellPriceOverrideEur: resellPriceOverride.value !== undefined && String(resellPriceOverride.value) !== '' ? Number(resellPriceOverride.value) : undefined,
    estimatedResellPriceEur: calculatedResellPrice.value,
    quantity: trackingType.value === 'serialized' ? 1 : Math.max(1, quantity.value),
    minQuantity: minQuantity.value,
    minQuantityScalar: minQuantityScalar.value || 1.0,
    effectiveMinQuantity: effectiveMinPreview.value,
    serialNumber: trackingType.value === 'serialized' ? serialNumber.value.trim() : undefined,
    customIdentifier: customIdentifier.value.trim() || undefined,
    specifications: Object.keys(specValues.value).length > 0 ? { ...specValues.value } : undefined
  }

  emit('logged', payload)
  isSubmitting.value = false
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open" class="fixed inset-y-0 right-0 z-40 w-full max-w-xl bg-card border-l border-border shadow-2xl flex flex-col animate-in slide-in-from-right duration-200">
    <!-- Header -->
    <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/30">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <PackagePlus class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-semibold text-foreground">
              Log Part into Warehouse Stock
            </h2>
            <p class="text-xs text-muted-foreground mt-0.5">
              Intake new, used, or donor parts with dynamic specification validation & auto-indexing
            </p>
          </div>
        </div>
        <button
          type="button"
          @click="emit('update:open', false)"
          class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
        >
          <X class="size-5" />
        </button>
      </div>

      <!-- Datalists for autocomplete indexing -->
      <datalist id="log-manufacturers-datalist">
        <option v-for="m in indexedFields.manufacturers" :key="m" :value="m" />
      </datalist>
      <datalist id="log-suppliers-datalist">
        <option v-for="s in indexedFields.suppliers" :key="s" :value="s" />
      </datalist>
      <datalist id="log-locations-datalist">
        <option v-for="l in indexedFields.locations" :key="l" :value="l" />
      </datalist>

      <!-- Form Body -->
      <form @submit.prevent="handleSubmit" class="p-6 space-y-4 overflow-y-auto flex-1 custom-scrollbar">
        <!-- Error Banner -->
        <div v-if="errorMessage" class="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs flex items-center gap-2">
          <AlertTriangle class="size-4 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>

        <!-- Optional Template Picker -->
        <div v-if="templates && templates.length > 0" class="space-y-1.5 pb-2 border-b border-border">
          <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <Tag class="size-3.5 text-primary" />
            <span>Instantiate from Asset Template (Optional)</span>
          </Label>
          <select
            v-model="selectedTemplateId"
            class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
          >
            <option value="">-- Custom Part (No template) --</option>
            <option v-for="t in templates" :key="t.id" :value="t.id">
              [{{ t.topLevelCategory }}] {{ t.name }} ({{ t.fixedFields.manufacturer }})
            </option>
          </select>
          <p class="text-[11px] text-muted-foreground">
            Instantiating from a template pre-fills specs, dynamic attributes, and sequential identifier pattern.
          </p>
        </div>

        <!-- Tracking Type Switcher -->
        <div class="grid grid-cols-2 gap-3">
          <div
            @click="trackingType = 'serialized'"
            class="p-3 rounded-lg border cursor-pointer transition-all flex items-center gap-3"
            :class="trackingType === 'serialized' ? 'bg-teal-500/10 border-teal-500 text-foreground ring-1 ring-teal-500/40' : 'bg-muted/30 border-border text-muted-foreground hover:border-border/80'"
          >
            <Cpu class="size-5 text-teal-600 dark:text-teal-400" />
            <div>
              <div class="text-xs font-semibold">Serialized Part</div>
              <div class="text-[10px] text-muted-foreground">Individual unit with unique serial (Qty fixed to 1)</div>
            </div>
          </div>

          <div
            @click="trackingType = 'bulk'"
            class="p-3 rounded-lg border cursor-pointer transition-all flex items-center gap-3"
            :class="trackingType === 'bulk' ? 'bg-purple-500/10 border-purple-500 text-foreground ring-1 ring-purple-500/40' : 'bg-muted/30 border-border text-muted-foreground hover:border-border/80'"
          >
            <Boxes class="size-5 text-purple-600 dark:text-purple-400" />
            <div>
              <div class="text-xs font-semibold">Bulk Stock</div>
              <div class="text-[10px] text-muted-foreground">Consumable items tracked by quantity count</div>
            </div>
          </div>
        </div>

        <!-- Basic Information -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="space-y-1.5 sm:col-span-2">
            <Label class="text-xs font-semibold text-foreground">Part Name *</Label>
            <Input v-model="name" placeholder="e.g. Beckhoff CX5140 Embedded PC or Festo Cylinder" required class="h-9 text-xs" />
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Top-Level Category</Label>
            <select
              v-model="category"
              class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
            >
              <option value="Hardware">Hardware (Mechanical / Electrical)</option>
              <option value="Software">Software (License / Firmware)</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Custom Identifier / Barcode</Label>
            <Input v-model="customIdentifier" placeholder="e.g. IPC-0042 or leave blank for auto" class="h-9 text-xs font-mono" />
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <Building class="size-3.5 text-muted-foreground" />
              <span>Manufacturer *</span>
            </Label>
            <Input
              v-model="manufacturer"
              list="log-manufacturers-datalist"
              placeholder="e.g. Siemens, Beckhoff, Festo..."
              required
              class="h-9 text-xs"
            />
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <Building class="size-3.5 text-muted-foreground" />
              <span>Supplier (Vendor)</span>
            </Label>
            <Input
              v-model="supplier"
              list="log-suppliers-datalist"
              placeholder="e.g. RS Components, Endrich..."
              class="h-9 text-xs"
            />
          </div>
        </div>

        <!-- Dynamic Template Specification Fields (Rendered when template is selected) -->
        <div v-if="selectedTemplate?.specFields && selectedTemplate.specFields.length > 0" class="p-3.5 rounded-lg bg-primary/5 border border-primary/20 space-y-3">
          <div class="text-xs font-semibold text-primary flex items-center justify-between">
            <span class="flex items-center gap-1.5">
              <Sliders class="size-3.5" />
              <span>Dynamic Specifications: {{ selectedTemplate.name }}</span>
            </span>
            <span class="text-[10px] text-muted-foreground font-normal">Inherited Template Attributes</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div v-for="field in selectedTemplate.specFields" :key="field.key" class="space-y-1">
              <Label class="text-[11px] font-semibold text-foreground flex items-center gap-1">
                <span>{{ field.label }}</span>
                <span v-if="field.unit" class="text-muted-foreground font-normal">({{ field.unit }})</span>
                <span v-if="field.required" class="text-destructive">*</span>
              </Label>
              <Input
                v-model="specValues[field.key]"
                :type="field.type === 'number' ? 'number' : 'text'"
                :placeholder="`Enter ${field.label}...`"
                :required="field.required"
                class="h-8 text-xs"
              />
            </div>
          </div>
        </div>

        <!-- Serial Number (if serialized) -->
        <div v-if="trackingType === 'serialized'" class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground">Unit Serial Number *</Label>
          <Input v-model="serialNumber" placeholder="e.g. SN-884920" class="h-9 text-xs font-mono" required />
        </div>

        <!-- Quantities & Floating Minimum Buffers -->
        <div v-if="trackingType === 'bulk'" class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-3">
          <div class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <Boxes class="size-3.5 text-primary" />
            <span>Bulk Quantities & Floating Stock Alert Buffers</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="space-y-1">
              <Label class="text-xs font-semibold text-foreground">Intake Quantity *</Label>
              <Input v-model.number="quantity" type="number" min="1" class="h-8 text-xs font-mono" required />
            </div>
            <div class="space-y-1">
              <Label class="text-xs font-semibold text-foreground">Minimum Threshold</Label>
              <Input v-model.number="minQuantity" type="number" min="0" step="0.5" class="h-8 text-xs font-mono" />
            </div>
            <div class="space-y-1">
              <Label class="text-xs font-semibold text-foreground">Buffer Multiplier</Label>
              <Input v-model.number="minQuantityScalar" type="number" min="0.1" step="0.05" placeholder="1.0" class="h-8 text-xs font-mono" />
            </div>
          </div>
          <div class="text-[11px] text-muted-foreground flex items-center gap-2">
            <span>Effective Minimum Alert Trigger:</span>
            <span class="font-mono font-bold text-primary">{{ effectiveMinPreview }} units</span>
          </div>
        </div>

        <!-- Lifecycle Condition & Operational State -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 border-t border-border">
          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Lifecycle Condition</Label>
            <select
              v-model="condition"
              class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
            >
              <option v-for="c in LIFECYCLE_CONDITIONS" :key="c.value" :value="c.value">
                {{ c.label }} ({{ c.desc }})
              </option>
            </select>
          </div>

          <div v-if="trackingType === 'serialized'" class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Operational State</Label>
            <select
              v-model="operationalState"
              class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
            >
              <option v-for="s in OPERATIONAL_STATES" :key="s.value" :value="s.value">
                {{ s.label }}
              </option>
            </select>
          </div>

          <div class="space-y-1.5 sm:col-span-2">
            <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <MapPin class="size-3.5 text-muted-foreground" />
              <span>Storage Location</span>
            </Label>
            <Input
              v-model="location"
              list="log-locations-datalist"
              placeholder="e.g. Warehouse Central / Rack 2 / Shelf B4"
              class="h-9 text-xs"
            />
          </div>
        </div>

        <!-- Financial Valuation & Scalar Depreciation Math -->
        <div class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-3">
          <div class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <DollarSign class="size-3.5 text-amber-500" />
            <span>Valuation & Resell Wear Depreciation (EUR)</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="space-y-1">
              <Label class="text-[11px] text-muted-foreground">Base Price (EUR)</Label>
              <Input v-model.number="priceEur" type="number" min="0" class="h-8 text-xs font-mono" />
            </div>

            <div class="space-y-1">
              <Label class="text-[11px] text-muted-foreground">Wear Depreciation (%)</Label>
              <Input v-model.number="wearDepreciation" type="number" min="0" max="100" class="h-8 text-xs font-mono" />
            </div>

            <div class="space-y-1">
              <Label class="text-[11px] text-muted-foreground">Resell Override (EUR)</Label>
              <Input v-model.number="resellPriceOverride" type="number" min="0" placeholder="Optional" class="h-8 text-xs font-mono" />
            </div>
          </div>

          <div class="flex items-center justify-between text-xs pt-1 border-t border-border/50 text-muted-foreground">
            <span>Estimated Resell Valuation:</span>
            <span class="font-mono font-bold text-foreground">€{{ calculatedResellPrice }}</span>
          </div>
        </div>
      </form>

      <!-- Footer -->
      <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-border bg-muted/20">
        <Button
          type="button"
          variant="outline"
          size="sm"
          @click="emit('update:open', false)"
          class="text-xs h-8 cursor-pointer"
        >
          Cancel
        </Button>
        <Button
          type="button"
          size="sm"
          :disabled="!isValid || isSubmitting"
          @click="handleSubmit"
          class="bg-primary text-primary-foreground hover:bg-primary/90 text-xs h-8 gap-1.5 cursor-pointer disabled:opacity-50"
        >
          <CheckCircle2 class="size-3.5" />
          <span>Confirm Log Part</span>
        </Button>
      </div>
  </div>
</template>
