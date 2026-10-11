<script setup lang="ts">
import { ref, watch } from 'vue'
import {
  History,
  X,
  ArrowRight,
  User,
  Calendar,
  AlertTriangle,
  Archive,
  Wrench,
  CheckCircle2,
  FileText,
  Tag
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import type { ComponentPartHistory, MachineComponentSlot } from '~/types/inventory'

const props = defineProps<{
  open: boolean
  slot: MachineComponentSlot | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()

const history = ref<ComponentPartHistory[]>([])
const loading = ref(false)

const fetchHistory = async () => {
  if (!props.slot?.id) {
    history.value = []
    return
  }
  loading.value = true
  try {
    const list = await $fetch<ComponentPartHistory[]>(`/api/components/${props.slot.id}/history`).catch(() => [])
    history.value = list
  } catch (err) {
    console.error('Error fetching part history:', err)
  } finally {
    loading.value = false
  }
}

watch(
  () => [props.open, props.slot?.id],
  ([isOpen, slotId]) => {
    if (isOpen && slotId) {
      fetchHistory()
    }
  },
  { immediate: true }
)

const close = () => {
  emit('update:open', false)
}

const getReasonBadgeClass = (reason: string) => {
  switch (reason) {
    case 'FailureBreakdown':
      return 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/30'
    case 'WearDepreciation':
      return 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30'
    case 'ScheduledPM':
      return 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/30'
    case 'RetrofitUpgrade':
      return 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30'
    case 'InitialCommissioning':
      return 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/30'
    default:
      return 'bg-muted text-muted-foreground border-border'
  }
}

const getDispositionBadgeClass = (disp: string) => {
  switch (disp) {
    case 'ReturnedToStock':
      return 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30'
    case 'SentForRepair':
    case 'SentToRepair':
      return 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30'
    case 'Scrapped':
      return 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/30'
    case 'RetainedForAnalysis':
    case 'RetainedForInvestigation':
      return 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border-indigo-500/30'
    default:
      return 'bg-muted text-muted-foreground border-border'
  }
}

const formatDate = (iso: string) => {
  try {
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
  } catch {
    return iso
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 overflow-hidden bg-background/80 backdrop-blur-xs">
    <div class="fixed inset-y-0 right-0 max-w-full flex pl-10">
      <div class="w-screen max-w-xl bg-card border-l border-border shadow-2xl flex flex-col">
        <!-- Drawer Header -->
        <div class="p-6 border-b border-border flex items-center justify-between bg-muted/20">
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-xl bg-primary/10 text-primary border border-primary/20">
              <History class="size-5" />
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h2 class="text-base font-bold text-foreground">
                  Component Lineage Ledger
                </h2>
                <Badge variant="outline" class="font-mono text-[11px] px-1.5 py-0">
                  {{ slot?.functionalSlotIdentifier }}
                </Badge>
              </div>
              <p class="text-xs text-muted-foreground">
                {{ slot?.name }} &bull; Immutable swap audit trail
              </p>
            </div>
          </div>
          <Button variant="ghost" size="icon" @click="close" class="cursor-pointer text-muted-foreground hover:text-foreground">
            <X class="size-4" />
          </Button>
        </div>

        <!-- Drawer Content -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6">
          <!-- Active Current Part Summary Card -->
          <div class="p-4 rounded-xl border border-primary/20 bg-primary/5 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-primary uppercase tracking-wider flex items-center gap-1.5">
                <CheckCircle2 class="size-3.5" />
                Currently Installed Physical Part
              </span>
              <Badge variant="default" class="text-[10px]">Active in Slot</Badge>
            </div>
            <div class="flex items-center justify-between font-mono text-sm font-bold text-foreground">
              <span>Serial: {{ slot?.currentPartSerialNumber || 'N/A' }}</span>
              <span class="text-xs text-muted-foreground">ID: {{ slot?.currentPartId?.slice(0, 8) || 'Direct' }}</span>
            </div>
            <div class="text-xs text-muted-foreground flex items-center gap-2 pt-1 border-t border-primary/10">
              <span>Blueprint Recipe:</span>
              <span class="font-mono font-medium text-foreground">{{ slot?.componentRecipeCode || 'Custom Slot' }}</span>
            </div>
          </div>

          <!-- History Timeline -->
          <div class="space-y-4">
            <h3 class="text-xs font-bold uppercase tracking-wider text-muted-foreground">
              Chronological Swap Ledger ({{ history.length }})
            </h3>

            <div v-if="loading" class="p-8 text-center text-xs text-muted-foreground">
              Loading lineage history...
            </div>

            <div v-else-if="history.length === 0" class="p-8 text-center rounded-xl border border-dashed border-border text-xs text-muted-foreground space-y-2">
              <History class="size-8 mx-auto text-muted-foreground/40" />
              <p>No prior part replacements recorded for this functional slot.</p>
              <p class="text-[11px] text-muted-foreground/70">Initial part commissioned at station setup.</p>
            </div>

            <div v-else class="relative pl-6 space-y-6 before:absolute before:left-2.5 before:top-3 before:bottom-3 before:w-0.5 before:bg-border">
              <div
                v-for="(item, idx) in history"
                :key="item.id"
                class="relative space-y-2.5 bg-muted/30 p-4 rounded-xl border border-border"
              >
                <!-- Timeline bullet point -->
                <div class="absolute -left-6 top-4 size-3 rounded-full bg-primary border-2 border-background ring-2 ring-primary/20"></div>

                <!-- Swap Header -->
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <div class="flex items-center gap-2 text-xs font-semibold text-foreground">
                    <Calendar class="size-3.5 text-muted-foreground" />
                    <span>{{ formatDate(item.swappedAt) }}</span>
                  </div>
                  <div class="flex items-center gap-1.5">
                    <span
                      class="px-2 py-0.5 rounded-md text-[10px] font-bold border"
                      :class="getReasonBadgeClass(item.reason)"
                    >
                      {{ item.reason }}
                    </span>
                    <span
                      class="px-2 py-0.5 rounded-md text-[10px] font-bold border"
                      :class="getDispositionBadgeClass(item.previousPartDisposition)"
                    >
                      Disposition: {{ item.previousPartDisposition }}
                    </span>
                  </div>
                </div>

                <!-- Parts Swapped Transition -->
                <div class="p-2.5 rounded-lg bg-background/80 border border-border/80 flex items-center justify-between font-mono text-xs">
                  <div class="space-y-0.5">
                    <div class="text-[10px] text-muted-foreground font-sans font-medium uppercase">Removed Part</div>
                    <div class="font-bold text-rose-500">
                      {{ item.previousPartSerialNumber || 'N/A' }}
                    </div>
                  </div>
                  <ArrowRight class="size-4 text-muted-foreground shrink-0 mx-2" />
                  <div class="space-y-0.5 text-right">
                    <div class="text-[10px] text-muted-foreground font-sans font-medium uppercase">Installed Part</div>
                    <div class="font-bold text-emerald-500">
                      {{ item.newPartSerialNumber || 'N/A' }}
                    </div>
                  </div>
                </div>

                <!-- Technician & Details -->
                <div class="text-xs text-muted-foreground flex items-center justify-between">
                  <div class="flex items-center gap-1.5">
                    <User class="size-3.5 text-muted-foreground" />
                    <span>Swapped by: <strong class="text-foreground">{{ item.swappedByUserName || item.swappedByUserId }}</strong></span>
                  </div>
                  <div v-if="item.associatedTicketId" class="text-[11px] text-primary flex items-center gap-1">
                    <FileText class="size-3" />
                    <span>Ticket #{{ item.associatedTicketId.slice(0, 6) }}</span>
                  </div>
                </div>

                <!-- Notes / Root Cause -->
                <div v-if="item.notes" class="text-xs text-muted-foreground/90 bg-muted/40 p-2.5 rounded-md border border-border/50 italic">
                  &ldquo;{{ item.notes }}&rdquo;
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Drawer Footer -->
        <div class="p-4 border-t border-border bg-muted/10 flex justify-end">
          <Button variant="outline" size="sm" @click="close" class="cursor-pointer text-xs">
            Close
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
