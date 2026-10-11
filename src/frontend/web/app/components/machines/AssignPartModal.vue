<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  Wrench,
  X,
  Package,
  Layers,
  FileSpreadsheet,
  AlertTriangle,
  CheckCircle2,
  Tag,
  ArrowRight
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import type { MachineComponentSlot, ComponentRecipe } from '~/types/inventory'

const props = defineProps<{
  open: boolean
  machineId: string
  existingSlot?: MachineComponentSlot | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'success'): void
}>()

const isNewSlot = ref(false)
const availableParts = ref<any[]>([])
const availableRecipes = ref<ComponentRecipe[]>([])
const loadingParts = ref(false)
const submitting = ref(false)
const errorMsg = ref('')

// Form inputs
const selectedSlotId = ref('')
const newSlotIdentifier = ref('')
const newSlotName = ref('')
const selectedRecipeId = ref('')
const selectedPartId = ref('')
const technicianName = ref('Engineer Orwell')
const reason = ref('WearDepreciation')
const previousDisposition = ref('SentForRepair')
const notes = ref('')

// EPLAN/CAD fields
const deviceTag = ref('')
const drawingPage = ref('')
const cadHandle = ref('')
const bomPosition = ref('')

const isEditingExisting = computed(() => !!props.existingSlot && !isNewSlot.value)

const fetchPrerequisites = async () => {
  loadingParts.value = true
  try {
    const [partsRes, recipesRes] = await Promise.all([
      $fetch<any>('/api/inventory/parts').catch(() => ({ parts: [] })),
      $fetch<ComponentRecipe[]>('/api/components/recipes').catch(() => [])
    ])
    availableParts.value = partsRes.parts || []
    availableRecipes.value = recipesRes || []
  } catch (err) {
    console.error('Error fetching parts/recipes for assignment modal:', err)
  } finally {
    loadingParts.value = false
  }
}

watch(
  () => [props.open, props.existingSlot],
  ([isOpen, slot]) => {
    if (isOpen) {
      errorMsg.value = ''
      fetchPrerequisites()
      if (slot) {
        isNewSlot.value = false
        selectedSlotId.value = slot.id
        newSlotIdentifier.value = slot.functionalSlotIdentifier
        newSlotName.value = slot.name
        selectedRecipeId.value = slot.componentRecipeId || ''
        deviceTag.value = slot.eplanCadRef?.deviceTag || ''
        drawingPage.value = slot.eplanCadRef?.drawingPage || ''
        cadHandle.value = slot.eplanCadRef?.cadHandle || ''
        bomPosition.value = slot.eplanCadRef?.bomPosition || ''
      } else {
        isNewSlot.value = true
        selectedSlotId.value = ''
        newSlotIdentifier.value = ''
        newSlotName.value = ''
        selectedRecipeId.value = ''
        deviceTag.value = ''
        drawingPage.value = ''
        cadHandle.value = ''
        bomPosition.value = ''
      }
    }
  },
  { immediate: true }
)

const onRecipeSelected = () => {
  const recipe = availableRecipes.value.find(r => r.id === selectedRecipeId.value)
  if (recipe) {
    if (!newSlotName.value) newSlotName.value = recipe.name
    if (recipe.eplanCadDefaults) {
      if (!deviceTag.value && recipe.eplanCadDefaults.deviceTag) deviceTag.value = recipe.eplanCadDefaults.deviceTag
      if (!drawingPage.value && recipe.eplanCadDefaults.drawingPage) drawingPage.value = recipe.eplanCadDefaults.drawingPage
    }
  }
}

const close = () => {
  emit('update:open', false)
}

const submit = async () => {
  if (!selectedPartId.value) {
    errorMsg.value = 'Please select a replacement or new physical part from inventory stock.'
    return
  }

  if (isNewSlot.value && !newSlotIdentifier.value.trim()) {
    errorMsg.value = 'Functional Slot Identifier (e.g. "=LINE1+OP10-1V1") is required for new slots.'
    return
  }

  submitting.value = true
  errorMsg.value = ''

  try {
    const payload = {
      machineId: props.machineId,
      existingComponentId: isNewSlot.value ? undefined : selectedSlotId.value,
      newComponentSlotIdentifier: isNewSlot.value ? newSlotIdentifier.value.trim() : undefined,
      newComponentName: isNewSlot.value ? (newSlotName.value.trim() || undefined) : undefined,
      componentRecipeId: selectedRecipeId.value || undefined,
      partId: selectedPartId.value,
      swappedByUserId: 'usr-orwell',
      swappedByUserName: technicianName.value,
      reason: reason.value,
      previousPartDisposition: previousDisposition.value,
      notes: notes.value,
      eplanCadRef: (deviceTag.value || drawingPage.value || cadHandle.value || bomPosition.value) ? {
        deviceTag: deviceTag.value,
        drawingPage: drawingPage.value,
        cadHandle: cadHandle.value,
        bomPosition: bomPosition.value
      } : undefined
    }

    await $fetch('/api/components/assign-part', {
      method: 'POST',
      body: payload
    })

    emit('success')
    close()
  } catch (err: any) {
    errorMsg.value = err?.data?.message || err?.message || 'Failed to assign part to component.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 overflow-y-auto bg-background/80 backdrop-blur-xs flex items-center justify-center p-4">
    <div class="w-full max-w-2xl bg-card border border-border shadow-2xl rounded-2xl overflow-hidden flex flex-col">
      <!-- Modal Header -->
      <div class="p-6 border-b border-border flex items-center justify-between bg-muted/20">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-primary/10 text-primary border border-primary/20">
            <Wrench class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-bold text-foreground">
              {{ isEditingExisting ? 'Swap Component Part (Lineage Replacement)' : 'Commission Component Slot' }}
            </h2>
            <p class="text-xs text-muted-foreground">
              Direct inventory bridge with immutable part history tracking
            </p>
          </div>
        </div>
        <Button variant="ghost" size="icon" @click="close" class="cursor-pointer text-muted-foreground hover:text-foreground">
          <X class="size-4" />
        </Button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 space-y-5 overflow-y-auto max-h-[75vh]">
        <div v-if="errorMsg" class="p-3.5 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-600 dark:text-rose-400 text-xs font-semibold flex items-center gap-2">
          <AlertTriangle class="size-4 shrink-0" />
          <span>{{ errorMsg }}</span>
        </div>

        <!-- Target Slot Context -->
        <div v-if="isEditingExisting && existingSlot" class="p-4 rounded-xl border border-primary/20 bg-primary/5 space-y-2">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-primary uppercase">Target Operational Slot</span>
            <Badge variant="outline" class="font-mono text-xs font-bold">{{ existingSlot.functionalSlotIdentifier }}</Badge>
          </div>
          <div class="flex items-center justify-between text-xs">
            <span class="text-muted-foreground">Currently Installed:</span>
            <span class="font-mono font-bold text-rose-500">{{ existingSlot.currentPartSerialNumber || 'Unknown Serial' }}</span>
          </div>
        </div>

        <!-- Mode Toggle if not pre-locked -->
        <div v-if="!existingSlot" class="space-y-3">
          <label class="text-xs font-bold uppercase tracking-wider text-muted-foreground">
            Functional Slot Configuration
          </label>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-foreground">Functional Slot Identifier *</label>
              <input
                v-model="newSlotIdentifier"
                placeholder="e.g. =LINE1+OP10-1V1 or AXIS-X-MOTOR"
                class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs font-mono focus:outline-none focus:ring-1 focus:ring-primary"
              />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs font-medium text-foreground">Component Display Name</label>
              <input
                v-model="newSlotName"
                placeholder="e.g. Main Clamping Pneumatic Valve"
                class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
              />
            </div>
          </div>
        </div>

        <!-- Component Blueprint Recipe Selection -->
        <div class="space-y-1.5">
          <div class="flex items-center justify-between">
            <label class="text-xs font-medium text-foreground flex items-center gap-1.5">
              <Layers class="size-3.5 text-muted-foreground" />
              <span>Blueprint Recipe (OOP Inheritance &amp; Auto-Tags)</span>
            </label>
            <span class="text-[11px] text-muted-foreground">Optional blueprint</span>
          </div>
          <select
            v-model="selectedRecipeId"
            @change="onRecipeSelected"
            class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
          >
            <option value="">None / Custom Non-Recipe Component</option>
            <option v-for="rec in availableRecipes" :key="rec.id" :value="rec.id">
              {{ rec.recipeCode }} — {{ rec.name }} ({{ rec.category }})
            </option>
          </select>
        </div>

        <!-- Inventory Physical Part Selection -->
        <div class="space-y-1.5">
          <label class="text-xs font-medium text-foreground flex items-center gap-1.5">
            <Package class="size-3.5 text-muted-foreground" />
            <span>Select Replacement Part from Warehouse Shelf Stock *</span>
          </label>
          <select
            v-model="selectedPartId"
            class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
          >
            <option value="" disabled>-- Choose available warehouse part --</option>
            <option v-for="part in availableParts" :key="part.id" :value="part.id">
              {{ part.serialNumber ? `[${part.serialNumber}] ` : '' }}{{ part.name }} &bull; Stock: {{ part.stockQuantity ?? 1 }} &bull; ({{ part.storageLocation || 'Warehouse' }})
            </option>
          </select>
        </div>

        <!-- Replacement Parameters (Reason & Disposition) -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3 pt-2 border-t border-border">
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Swap Justification / Reason *</label>
            <select
              v-model="reason"
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
            >
              <option value="FailureBreakdown">FailureBreakdown (Immediate Breakdown)</option>
              <option value="WearDepreciation">WearDepreciation (Preventive Wear Threshold)</option>
              <option value="ScheduledPM">ScheduledPM (Routine Periodic Maintenance)</option>
              <option value="RetrofitUpgrade">RetrofitUpgrade (Engineering Enhancement)</option>
              <option value="CalibrationSwap">CalibrationSwap (Metrology Calibration Rotation)</option>
              <option value="InitialCommissioning">InitialCommissioning (Station Setup)</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Removed Part Disposition *</label>
            <select
              v-model="previousDisposition"
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
            >
              <option value="SentForRepair">SentForRepair (External / Internal Reconditioning)</option>
              <option value="ReturnedToStock">ReturnedToStock (Good / Serviced Spare)</option>
              <option value="RetainedForAnalysis">RetainedForAnalysis (Root Cause QA Quarantine)</option>
              <option value="Scrapped">Scrapped (Damaged Beyond Repair / Discarded)</option>
              <option value="DonorStock">DonorStock (Harvest Spare Sub-components)</option>
            </select>
          </div>
        </div>

        <!-- EPLAN & CAD Reference (Optional) -->
        <div class="space-y-2 pt-2 border-t border-border">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-muted-foreground uppercase flex items-center gap-1.5">
              <FileSpreadsheet class="size-3.5" />
              EPLAN Schematics &amp; CAD Layout Mapping
            </span>
            <span class="text-[11px] text-muted-foreground">Digital Twin Link</span>
          </div>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-2.5">
            <div>
              <label class="text-[11px] text-muted-foreground">BMK Device Tag</label>
              <input
                v-model="deviceTag"
                placeholder="-10K1"
                class="w-full px-2.5 py-1.5 rounded-md bg-background border border-border text-xs font-mono"
              />
            </div>
            <div>
              <label class="text-[11px] text-muted-foreground">Drawing Page/Path</label>
              <input
                v-model="drawingPage"
                placeholder="Page 14.3"
                class="w-full px-2.5 py-1.5 rounded-md bg-background border border-border text-xs"
              />
            </div>
            <div>
              <label class="text-[11px] text-muted-foreground">CAD Handle</label>
              <input
                v-model="cadHandle"
                placeholder="3B4F"
                class="w-full px-2.5 py-1.5 rounded-md bg-background border border-border text-xs font-mono"
              />
            </div>
            <div>
              <label class="text-[11px] text-muted-foreground">BOM Position</label>
              <input
                v-model="bomPosition"
                placeholder="Pos 10.4"
                class="w-full px-2.5 py-1.5 rounded-md bg-background border border-border text-xs"
              />
            </div>
          </div>
        </div>

        <!-- Technician & Notes -->
        <div class="space-y-3 pt-2 border-t border-border">
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Executing Technician Name</label>
            <input
              v-model="technicianName"
              placeholder="Full name of technician"
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs"
            />
          </div>
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Journal Audit Notes &amp; Diagnostics</label>
            <textarea
              v-model="notes"
              rows="2"
              placeholder="Document observed failure symptoms, calibration values, or swap justification..."
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs resize-none"
            ></textarea>
          </div>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="p-4 border-t border-border bg-muted/10 flex items-center justify-end gap-2.5">
        <Button variant="outline" size="sm" @click="close" :disabled="submitting" class="cursor-pointer text-xs">
          Cancel
        </Button>
        <Button size="sm" @click="submit" :disabled="submitting" class="cursor-pointer text-xs font-bold gap-1.5">
          <CheckCircle2 class="size-3.5" />
          <span>{{ submitting ? 'Recording Swap...' : (isEditingExisting ? 'Commit Part Swap' : 'Commission Slot') }}</span>
        </Button>
      </div>
    </div>
  </div>
</template>
