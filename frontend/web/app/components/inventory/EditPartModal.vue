<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  X,
  Edit3,
  AlertTriangle,
  CheckCircle2,
  DollarSign,
  Tag,
  MapPin,
  Cpu,
  Layers,
  Archive,
  RefreshCw,
  Trash2
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type {
  InventoryPart,
  PartLifecycleCondition,
  PartOperationalState
} from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  open: boolean
  part: InventoryPart | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'updated', part: InventoryPart): void
}>()

const { editPart, transitionLifecycle, indexedFields, fetchIndexedFields, formatCurrency, activeCurrency } = usePartsInventory()

// Form state
const name = ref('')
const displayName = ref('')
const location = ref('')
const condition = ref<PartLifecycleCondition>('new')
const operationalState = ref<PartOperationalState>('working')
const wearDepreciation = ref<number>(0)
const resellPriceOverride = ref<number | undefined>(undefined)
const minQuantity = ref<number>(1)
const minQuantityScalar = ref<number>(1.0)
const tagsInput = ref('')
const serialNumber = ref('')
const notes = ref('')

// Lifecycle transition state
const selectedLifecycleAction = ref<'none' | 'in_machine' | 'removed_from_machine' | 'decommissioned' | 'donor' | 'scrap'>('none')
const targetMachineId = ref('')
const lifecycleJustification = ref('')

const isSubmitting = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

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

watch(
  () => props.open,
  async (val) => {
    if (val && props.part) {
      errorMessage.value = ''
      successMessage.value = ''
      selectedLifecycleAction.value = 'none'
      targetMachineId.value = props.part.linkedMachineId || ''
      lifecycleJustification.value = ''

      name.value = props.part.name
      displayName.value = props.part.displayName || ''
      location.value = props.part.location
      condition.value = props.part.condition
      operationalState.value = props.part.operationalState
      wearDepreciation.value = props.part.wearDepreciationPercentage || 0
      resellPriceOverride.value = props.part.resellPriceOverrideEur
      minQuantity.value = props.part.minQuantity
      minQuantityScalar.value = props.part.minQuantityScalar || 1.0
      tagsInput.value = props.part.tags ? props.part.tags.join(', ') : ''
      serialNumber.value = props.part.serialNumber || ''
      notes.value = ''

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
  const basePrice = props.part?.priceEur || 0
  const wear = condition.value === 'new' ? 0 : (wearDepreciation.value || 0)
  return Math.round(basePrice * (1 - (wear / 100)) * 100) / 100
})

const handleSubmit = async () => {
  if (!props.part) return
  isSubmitting.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    // 1. If a formal lifecycle transition was requested
    if (selectedLifecycleAction.value !== 'none') {
      const targetMachine = indexedFields.value.machines.find(m => m.id === targetMachineId.value)
      await transitionLifecycle(
        props.part.id,
        selectedLifecycleAction.value as any,
        {
          machineId: targetMachineId.value || undefined,
          machineName: targetMachine?.name || undefined,
          justification: lifecycleJustification.value || undefined,
          notes: notes.value || `Transitioned to ${selectedLifecycleAction.value}`
        }
      )
    }

    // 2. Update metadata fields
    const parsedTags = tagsInput.value
      .split(',')
      .map(t => t.trim())
      .filter(t => t.length > 0)

    const updated = await editPart(props.part.id, {
      name: name.value.trim(),
      displayName: displayName.value.trim() || undefined,
      location: location.value.trim(),
      condition: condition.value,
      operationalState: operationalState.value,
      wearDepreciationPercentage: wearDepreciation.value,
      resellPriceOverrideEur: resellPriceOverride.value !== undefined && String(resellPriceOverride.value) !== '' ? Number(resellPriceOverrideEur) : undefined,
      minQuantity: minQuantity.value,
      minQuantityScalar: minQuantityScalar.value,
      tags: parsedTags,
      serialNumber: props.part.trackingType === 'serialized' ? serialNumber.value.trim() : undefined
    })

    successMessage.value = 'Part updated successfully with full audit trail!'
    emit('updated', updated)
    setTimeout(() => {
      emit('update:open', false)
    }, 700)
  } catch (err: any) {
    errorMessage.value = err.data?.statusMessage || err.message || 'Failed to update part.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
    <div class="relative w-full max-w-2xl bg-card border border-border rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/30">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <Edit3 class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-semibold text-foreground flex items-center gap-2">
              <span>Edit Part Post-Intake</span>
              <span class="font-mono text-xs text-primary px-2 py-0.5 rounded bg-primary/10 border border-primary/20">
                {{ part?.customIdentifier }}
              </span>
            </h2>
            <p class="text-xs text-muted-foreground mt-0.5">
              Update wear condition, storage location, resell override, and state machine transitions
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

      <!-- Datalist for indexed locations -->
      <datalist id="edit-locations-datalist">
        <option v-for="loc in indexedFields.locations" :key="loc" :value="loc" />
      </datalist>

      <!-- Form Body -->
      <form @submit.prevent="handleSubmit" class="p-6 space-y-4 overflow-y-auto flex-1">
        <!-- Error & Success Banners -->
        <div v-if="errorMessage" class="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs flex items-center gap-2">
          <AlertTriangle class="size-4 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>

        <div v-if="successMessage" class="p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400 text-xs flex items-center gap-2">
          <CheckCircle2 class="size-4 shrink-0" />
          <span>{{ successMessage }}</span>
        </div>

        <!-- Part Identification -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Part Name</Label>
            <Input v-model="name" required class="h-9 text-xs" />
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Display Name Override</Label>
            <Input v-model="displayName" placeholder="Optional human-readable label" class="h-9 text-xs" />
          </div>
        </div>

        <!-- Storage Location with Autocomplete -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <MapPin class="size-3.5 text-primary" />
            <span>Storage Location / Cabinet</span>
          </Label>
          <Input
            v-model="location"
            list="edit-locations-datalist"
            placeholder="Type or select warehouse shelf / bin..."
            class="h-9 text-xs"
            required
          />
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

          <div class="space-y-1.5">
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
        </div>

        <!-- Wear Depreciation & Valuation Math -->
        <div class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-3">
          <div class="text-xs font-semibold text-foreground flex items-center justify-between">
            <span class="flex items-center gap-1.5">
              <DollarSign class="size-3.5 text-amber-500" />
              <span>Valuation & Wear Depreciation</span>
            </span>
            <span class="text-xs font-mono text-muted-foreground">
              Base: €{{ part?.priceEur }}
            </span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1">
              <Label class="text-[11px] text-muted-foreground">Wear Depreciation (%)</Label>
              <Input v-model.number="wearDepreciation" type="number" min="0" max="100" class="h-8 text-xs font-mono" />
            </div>

            <div class="space-y-1">
              <Label class="text-[11px] text-muted-foreground">Resell Price Override (EUR)</Label>
              <Input v-model.number="resellPriceOverride" type="number" min="0" placeholder="Leave empty for auto math" class="h-8 text-xs font-mono" />
            </div>
          </div>

          <div class="flex items-center justify-between text-xs pt-1 border-t border-border/50 text-muted-foreground">
            <span>Estimated Resell Valuation:</span>
            <span class="font-mono font-bold text-foreground">€{{ calculatedResellPrice }}</span>
          </div>
        </div>

        <!-- Minimum Quantities & Buffers -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Min Quantity</Label>
            <Input v-model.number="minQuantity" type="number" min="0" class="h-9 text-xs font-mono" />
          </div>

          <div class="space-y-1.5">
            <Label class="text-xs font-semibold text-foreground">Scalar Multiplier</Label>
            <Input v-model.number="minQuantityScalar" type="number" min="0.1" step="0.05" class="h-9 text-xs font-mono" />
          </div>

          <div class="flex flex-col justify-end pb-1.5 text-xs">
            <span class="text-muted-foreground text-[10px]">Effective Min:</span>
            <span class="font-mono font-bold text-primary">{{ effectiveMinPreview }} units</span>
          </div>
        </div>

        <!-- Serial Number (if serialized) -->
        <div v-if="part?.trackingType === 'serialized'" class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground">Serial Number / Hardware ID</Label>
          <Input v-model="serialNumber" class="h-9 text-xs font-mono" />
        </div>

        <!-- Tags -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <Tag class="size-3.5 text-muted-foreground" />
            <span>Tags (comma-separated)</span>
          </Label>
          <Input v-model="tagsInput" placeholder="critical, spare, line1, motor" class="h-9 text-xs" />
        </div>

        <!-- Formal Lifecycle Transition Workflow -->
        <div class="p-3.5 rounded-lg bg-primary/5 border border-primary/20 space-y-3">
          <div class="text-xs font-semibold text-primary flex items-center gap-1.5">
            <RefreshCw class="size-3.5" />
            <span>Formal Lifecycle State Machine Transition</span>
          </div>

          <div class="space-y-1.5">
            <select
              v-model="selectedLifecycleAction"
              class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
            >
              <option value="none">No lifecycle transition (metadata edits only)</option>
              <option value="in_machine">Install to Machine (In-Machine)</option>
              <option value="removed_from_machine">Remove from Machine (Staging Bay)</option>
              <option value="decommissioned">Decommission (Buffer Area)</option>
              <option value="donor">Convert to Donor Asset (Harvest Cabinet)</option>
              <option value="scrap">Mark as Scrap / Recycle</option>
            </select>
          </div>

          <!-- Machine selection if in_machine -->
          <div v-if="selectedLifecycleAction === 'in_machine'" class="space-y-2 pt-2 border-t border-primary/20">
            <div class="space-y-1">
              <Label class="text-[11px] font-semibold text-foreground">Target Machine (or enter justification)</Label>
              <select
                v-model="targetMachineId"
                class="w-full h-8 px-2.5 rounded-md bg-background border border-border text-xs text-foreground"
              >
                <option value="">Select indexed machine...</option>
                <option v-for="m in indexedFields.machines" :key="m.id" :value="m.id">
                  {{ m.name }} ({{ m.id }})
                </option>
              </select>
            </div>

            <div class="space-y-1">
              <Label class="text-[11px] font-semibold text-foreground">Installation Justification / Location</Label>
              <Input
                v-model="lifecycleJustification"
                placeholder="Required if machine is not indexed (e.g. Line 3 Dispenser Head)"
                class="h-8 text-xs"
              />
            </div>
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
          :disabled="isSubmitting"
          @click="handleSubmit"
          class="bg-primary text-primary-foreground hover:bg-primary/90 text-xs h-8 gap-1.5 cursor-pointer disabled:opacity-50"
        >
          <CheckCircle2 class="size-3.5" />
          <span>Save Changes</span>
        </Button>
      </div>
    </div>
  </div>
</template>
