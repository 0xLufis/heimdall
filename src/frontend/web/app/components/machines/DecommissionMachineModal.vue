<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  X,
  AlertTriangle,
  Archive,
  Wrench,
  Trash2,
  Calendar,
  CheckCircle2,
  Cpu,
  Layers
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const props = defineProps<{
  open: boolean
  machine: any | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'decommissioned', result: any): void
}>()

const reason = ref('')
const targetDisposition = ref<'decommissioned' | 'dismantled'>('decommissioned')
const partsHandling = ref<'salvage_donor' | 'scrap' | 'retain'>('salvage_donor')
const decommissionDate = ref('')
const isSubmitting = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

watch(
  () => props.open,
  (val) => {
    if (val) {
      reason.value = ''
      targetDisposition.value = 'decommissioned'
      partsHandling.value = 'salvage_donor'
      decommissionDate.value = new Date().toISOString().substring(0, 10)
      errorMessage.value = ''
      successMessage.value = ''
    }
  }
)

const isValid = computed(() => {
  return reason.value.trim().length > 0 && decommissionDate.value.length > 0
})

const handleSubmit = async () => {
  if (!props.machine || !isValid.value) return

  isSubmitting.value = true
  errorMessage.value = ''

  try {
    const res = await $fetch<any>(`/api/machines/${props.machine.id}/decommission`, {
      method: 'POST',
      body: {
        reason: reason.value.trim(),
        targetDisposition: targetDisposition.value,
        partsHandling: partsHandling.value,
        decommissionDate: new Date(decommissionDate.value).toISOString()
      }
    })

    successMessage.value = `Machine successfully ${targetDisposition.value === 'dismantled' ? 'dismantled' : 'decommissioned'}.`
    emit('decommissioned', res)
    setTimeout(() => {
      emit('update:open', false)
    }, 700)
  } catch (err: any) {
    errorMessage.value = err.data?.statusMessage || err.message || 'Failed to decommission machine.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-y-0 right-0 z-40 w-full max-w-md bg-card border-l border-border shadow-2xl flex flex-col animate-in slide-in-from-right duration-200">
    <!-- Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-border bg-destructive/5">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive">
            <Archive class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-semibold text-foreground flex items-center gap-2">
              <span>Decommission Production Machine</span>
              <span class="font-mono text-xs text-primary px-2 py-0.5 rounded bg-primary/10 border border-primary/20">
                {{ machine?.customIdentifier || machine?.name }}
              </span>
            </h2>
            <p class="text-xs text-muted-foreground mt-0.5">
              Permanent retirement, inventory salvage handling, and operational archival
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

      <!-- Machine Summary Banner -->
      <div v-if="machine" class="px-6 py-3 bg-muted/20 border-b border-border flex items-center justify-between text-xs">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-foreground">{{ machine.displayName || machine.name }}</span>
          <span class="text-muted-foreground">({{ machine.machineType || 'General Machinery' }})</span>
        </div>
        <div class="text-muted-foreground font-mono">
          Line: {{ machine.groupId || 'Unassigned' }}
        </div>
      </div>

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

        <!-- Target Lifecycle Disposition -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground">Target Decommission Disposition *</Label>
          <div class="grid grid-cols-2 gap-3">
            <div
              @click="targetDisposition = 'decommissioned'"
              class="p-3 rounded-lg border cursor-pointer transition-all flex items-center gap-2.5"
              :class="targetDisposition === 'decommissioned' ? 'bg-destructive/10 border-destructive text-foreground ring-1 ring-destructive/40' : 'bg-muted/30 border-border text-muted-foreground hover:border-border/80'"
            >
              <Archive class="size-4 text-destructive" />
              <div>
                <div class="text-xs font-semibold">Decommission</div>
                <div class="text-[10px] text-muted-foreground">Retired & preserved as cold asset</div>
              </div>
            </div>

            <div
              @click="targetDisposition = 'dismantled'"
              class="p-3 rounded-lg border cursor-pointer transition-all flex items-center gap-2.5"
              :class="targetDisposition === 'dismantled' ? 'bg-purple-500/10 border-purple-500 text-foreground ring-1 ring-purple-500/40' : 'bg-muted/30 border-border text-muted-foreground hover:border-border/80'"
            >
              <Wrench class="size-4 text-purple-600 dark:text-purple-400" />
              <div>
                <div class="text-xs font-semibold">Dismantle</div>
                <div class="text-[10px] text-muted-foreground">Taken apart & salvaged for parts</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Installed Parts Handling -->
        <div class="p-3.5 rounded-lg bg-muted/30 border border-border space-y-2.5">
          <div class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <Cpu class="size-3.5 text-primary" />
            <span>Installed Machine Assets & Parts Handling *</span>
          </div>

          <div class="space-y-2">
            <label
              class="flex items-start gap-2.5 p-2 rounded-md border cursor-pointer transition-colors text-xs"
              :class="partsHandling === 'salvage_donor' ? 'bg-primary/10 border-primary/40 text-foreground' : 'bg-background border-border text-muted-foreground hover:bg-muted/50'"
            >
              <input type="radio" v-model="partsHandling" value="salvage_donor" class="mt-0.5 text-primary" />
              <div>
                <div class="font-semibold text-foreground">Salvage to Warehouse (Used / Donor Spares)</div>
                <div class="text-[11px] text-muted-foreground">Unlink and return serviceable modules to spare parts inventory cabinet</div>
              </div>
            </label>

            <label
              class="flex items-start gap-2.5 p-2 rounded-md border cursor-pointer transition-colors text-xs"
              :class="partsHandling === 'scrap' ? 'bg-destructive/10 border-destructive/40 text-foreground' : 'bg-background border-border text-muted-foreground hover:bg-muted/50'"
            >
              <input type="radio" v-model="partsHandling" value="scrap" class="mt-0.5 text-destructive" />
              <div>
                <div class="font-semibold text-foreground">Mark Installed Parts as Scrap</div>
                <div class="text-[11px] text-muted-foreground">Designate non-serviceable assemblies for recycling quarantine</div>
              </div>
            </label>

            <label
              class="flex items-start gap-2.5 p-2 rounded-md border cursor-pointer transition-colors text-xs"
              :class="partsHandling === 'retain' ? 'bg-muted border-border text-foreground' : 'bg-background border-border text-muted-foreground hover:bg-muted/50'"
            >
              <input type="radio" v-model="partsHandling" value="retain" class="mt-0.5" />
              <div>
                <div class="font-semibold text-foreground">Retain with Machine Assembly</div>
                <div class="text-[11px] text-muted-foreground">Keep part hierarchy attached to archived machine record for audit</div>
              </div>
            </label>
          </div>
        </div>

        <!-- Decommission Date -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
            <Calendar class="size-3.5 text-muted-foreground" />
            <span>Effective Decommission Date *</span>
          </Label>
          <Input v-model="decommissionDate" type="date" required class="h-9 text-xs font-mono" />
        </div>

        <!-- Reason / Justification -->
        <div class="space-y-1.5">
          <Label class="text-xs font-semibold text-foreground">
            Justification & Engineering Rationale <span class="text-destructive">*</span>
          </Label>
          <textarea
            v-model="reason"
            rows="3"
            placeholder="e.g. End of operational lifecycle; replaced by high-speed laser welding unit per Capex PRJ-2026-NMC."
            class="w-full p-2.5 rounded-md bg-background border border-border text-xs text-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
            required
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
          :disabled="!isValid || isSubmitting"
          @click="handleSubmit"
          class="bg-destructive hover:bg-destructive/90 text-destructive-foreground text-xs h-8 gap-1.5 cursor-pointer disabled:opacity-50"
        >
          <Archive class="size-3.5" />
          <span>Confirm Decommission</span>
        </Button>
      </div>
    </div>
</template>
