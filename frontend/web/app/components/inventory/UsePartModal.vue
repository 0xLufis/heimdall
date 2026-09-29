<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  X,
  Send,
  AlertTriangle,
  Building,
  Briefcase,
  Layers,
  User,
  FileText,
  Boxes,
  CheckCircle2
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { InventoryPart, PartUsageCostCenter } from '~/types/inventory'

const props = defineProps<{
  open: boolean
  part: InventoryPart | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'used', data: { partId: string; costCenter: PartUsageCostCenter; quantity: number }): void
}>()

const form = ref<PartUsageCostCenter & { quantity: number }>({
  prodLine: '',
  project: '',
  department: '',
  technician: '',
  notes: '',
  quantity: 1
})

const errorMessage = ref('')
const isSubmitting = ref(false)
const showSuccess = ref(false)

const PROD_LINES = [
  'Line 1 - OP10 CNC Milling Cell',
  'Line 1 - OP20 Robotic Welding Station',
  'Line 1 - OP30 Vision Inspection Cell',
  'Line 2 - Battery Module Assembly',
  'Line 2 - Automated Pack Testing',
  'Line 3 - Final Stamping & Dispensing'
]

const DEPARTMENTS = [
  'Plant Maintenance & Automation',
  'Mechanical Maintenance',
  'Electrical Engineering',
  'Controls & Robotics',
  'Quality Assurance',
  'Plant Operations'
]

watch(
  () => props.open,
  (val) => {
    if (val) {
      errorMessage.value = ''
      showSuccess.value = false
      form.value = {
        prodLine: '',
        project: '',
        department: '',
        technician: '',
        notes: '',
        quantity: 1
      }
    }
  }
)

const hasCostCenter = computed(() => {
  return Boolean(
    (form.value.prodLine && form.value.prodLine.trim().length > 0) ||
    (form.value.project && form.value.project.trim().length > 0) ||
    (form.value.department && form.value.department.trim().length > 0)
  )
})

const isFormValid = computed(() => {
  return hasCostCenter.value && form.value.quantity > 0
})

const handleSubmit = async () => {
  if (!props.part) return

  if (!hasCostCenter.value) {
    errorMessage.value = 'Mandatory cost center tagging required: provide at least one of Production Line, Project, or Department.'
    return
  }

  if (props.part.trackingType === 'bulk' && form.value.quantity > props.part.quantity) {
    errorMessage.value = `Cannot use ${form.value.quantity} items. Stock has only ${props.part.quantity} units available.`
    return
  }

  isSubmitting.value = true
  errorMessage.value = ''

  try {
    emit('used', {
      partId: props.part.id,
      costCenter: {
        prodLine: form.value.prodLine.trim() || undefined,
        project: form.value.project.trim() || undefined,
        department: form.value.department.trim() || undefined,
        technician: form.value.technician?.trim() || undefined,
        notes: form.value.notes?.trim() || undefined
      },
      quantity: props.part.trackingType === 'serialized' ? 1 : form.value.quantity
    })
    showSuccess.value = true
    setTimeout(() => {
      emit('update:open', false)
    }, 600)
  } catch (err: any) {
    errorMessage.value = err.message || 'Failed to use part.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
    <div class="relative w-full max-w-lg bg-card border border-border rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/30">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-500">
            <Send class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-semibold text-foreground">
              Use Part / Consume from Stock
            </h2>
            <p class="text-xs text-muted-foreground mt-0.5">
              Requires mandatory cost center tagging for production accounting
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

      <!-- Part Summary Banner -->
      <div v-if="part" class="px-6 py-3 bg-muted/20 border-b border-border flex items-center justify-between text-xs">
        <div class="flex items-center gap-2">
          <span class="font-mono font-bold text-primary">{{ part.customIdentifier }}</span>
          <span class="text-foreground font-medium truncate max-w-[200px]">{{ part.name }}</span>
        </div>
        <div class="flex items-center gap-2">
          <span class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wider"
            :class="part.trackingType === 'serialized' ? 'bg-teal-500/15 text-teal-600 dark:text-teal-400' : 'bg-purple-500/15 text-purple-600 dark:text-purple-400'">
            {{ part.trackingType }}
          </span>
          <span class="font-mono text-muted-foreground">Avail: {{ part.quantity }}</span>
        </div>
      </div>

      <!-- Form Body -->
      <form @submit.prevent="handleSubmit" class="p-6 space-y-4 overflow-y-auto flex-1">
        <!-- Error Banner -->
        <div v-if="errorMessage" class="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs flex items-center gap-2">
          <AlertTriangle class="size-4 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>

        <div v-if="showSuccess" class="p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400 text-xs flex items-center gap-2">
          <CheckCircle2 class="size-4 shrink-0" />
          <span>Part successfully allocated and cost center recorded!</span>
        </div>

        <!-- Mandatory Notice -->
        <div class="p-2.5 rounded-lg bg-primary/5 border border-primary/20 text-primary text-xs">
          <span class="font-semibold">Cost Center Accounting Policy:</span>
          At least one cost center tagging field is strictly mandatory: Production Line, Project ID, or Department (OR relation).
        </div>

        <!-- Production Line -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold flex items-center justify-between text-foreground">
            <span class="flex items-center gap-1.5">
              <Layers class="size-3.5 text-primary" />
              <span>Production Line</span>
            </span>
            <span class="text-[10px] text-muted-foreground font-normal">OR relation</span>
          </Label>
          <select
            v-model="form.prodLine"
            class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
          >
            <option value="">Select target production line (optional)...</option>
            <option v-for="line in PROD_LINES" :key="line" :value="line">
              {{ line }}
            </option>
          </select>
        </div>

        <!-- Project ID -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold flex items-center justify-between text-foreground">
            <span class="flex items-center gap-1.5">
              <Briefcase class="size-3.5 text-primary" />
              <span>Project Identifier / Cost Code</span>
            </span>
            <span class="text-[10px] text-muted-foreground font-normal">OR relation</span>
          </Label>
          <Input
            v-model="form.project"
            placeholder="e.g. PRJ-2026-NMC-STACKING or LINE1-UPGRADE"
            class="h-9 text-xs font-mono"
          />
        </div>

        <!-- Department -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold flex items-center justify-between text-foreground">
            <span class="flex items-center gap-1.5">
              <Building class="size-3.5 text-primary" />
              <span>Department</span>
            </span>
            <span class="text-[10px] text-muted-foreground font-normal">OR relation</span>
          </Label>
          <select
            v-model="form.department"
            class="w-full h-9 px-3 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
          >
            <option value="">Select department (optional)...</option>
            <option v-for="dept in DEPARTMENTS" :key="dept" :value="dept">
              {{ dept }}
            </option>
          </select>
        </div>

        <!-- Quantity (if bulk) -->
        <div v-if="part?.trackingType === 'bulk'" class="space-y-1.5">
          <Label class="text-xs font-semibold flex items-center gap-1.5 text-foreground">
            <Boxes class="size-3.5 text-primary" />
            <span>Quantity to Consume <span class="text-destructive">*</span></span>
          </Label>
          <Input
            v-model.number="form.quantity"
            type="number"
            min="1"
            :max="part?.quantity || 1"
            class="h-9 text-xs font-mono"
            required
          />
          <p class="text-[11px] text-muted-foreground">
            Available in warehouse: {{ part?.quantity }} units
          </p>
        </div>

        <!-- Technician / User Assigned -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold flex items-center gap-1.5 text-foreground">
            <User class="size-3.5 text-muted-foreground" />
            <span>Technician / Authorized Requester</span>
          </Label>
          <Input
            v-model="form.technician"
            placeholder="e.g. Elena Rostova or Gábor Varga"
            class="h-9 text-xs"
          />
        </div>

        <!-- Notes / Ticket Reference -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold flex items-center gap-1.5 text-foreground">
            <FileText class="size-3.5 text-muted-foreground" />
            <span>Maintenance Ticket / Reason</span>
          </Label>
          <Input
            v-model="form.notes"
            placeholder="e.g. Emergency spindle replacement for Ticket TCK-1092"
            class="h-9 text-xs"
          />
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
          :disabled="!isFormValid || isSubmitting"
          @click="handleSubmit"
          class="bg-amber-600 hover:bg-amber-700 text-white text-xs h-8 gap-1.5 cursor-pointer disabled:opacity-50"
        >
          <Send class="size-3.5" />
          <span>Confirm Use Part</span>
        </Button>
      </div>
    </div>
  </div>
</template>
