<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  X,
  History,
  Send,
  PackagePlus,
  Trash2,
  Sliders,
  DollarSign,
  Building,
  Briefcase,
  Layers,
  User,
  Clock
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import type { PartAuditRecord } from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = withDefaults(
  defineProps<{
    open: boolean
    auditLogs?: PartAuditRecord[]
    part?: any
  }>(),
  {
    auditLogs: undefined,
    part: null
  }
)

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()

const { auditLogs: fetchedLogs, fetchAuditLogs } = usePartsInventory()

watch(
  () => props.open,
  async (isOpen) => {
    if (isOpen && (!props.auditLogs || props.auditLogs.length === 0)) {
      await fetchAuditLogs(props.part?.id)
    }
  },
  { immediate: true }
)

const effectiveLogs = computed(() => {
  if (props.auditLogs && props.auditLogs.length > 0) return props.auditLogs
  return fetchedLogs.value || []
})

const filterAction = ref<'all' | 'use_part' | 'log_part' | 'scrap'>('all')

const filteredLogs = computed(() => {
  const list = effectiveLogs.value
  if (filterAction.value === 'all') return list
  return list.filter(l => l.action === filterAction.value)
})

const formatDate = (iso: string) => {
  try {
    const d = new Date(iso)
    return d.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
  } catch {
    return iso
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-y-0 right-0 z-40 w-full max-w-xl bg-card border-l border-border shadow-2xl flex flex-col animate-in slide-in-from-right duration-200">
    <!-- Drawer Header -->
      <div class="flex items-center justify-between p-4 border-b border-border bg-muted/30">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <History class="size-5" />
          </div>
          <div>
            <h2 class="text-sm font-bold text-foreground">
              Parts & Consumption Audit Log
            </h2>
            <p class="text-[11px] text-muted-foreground">
              Immutable ledger of part intake, consumption, scrap, and cost center allocations
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

      <!-- Action Filter Buttons -->
      <div class="flex items-center gap-1.5 p-3 border-b border-border bg-muted/15 text-xs overflow-x-auto">
        <Button
          variant="ghost"
          size="sm"
          @click="filterAction = 'all'"
          :class="filterAction === 'all' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
          class="h-7 px-2.5 text-xs cursor-pointer"
        >
          All ({{ effectiveLogs.length }})
        </Button>
        <Button
          variant="ghost"
          size="sm"
          @click="filterAction = 'use_part'"
          :class="filterAction === 'use_part' ? 'bg-amber-600 text-white shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
          class="h-7 px-2.5 text-xs cursor-pointer"
        >
          Used Parts
        </Button>
        <Button
          variant="ghost"
          size="sm"
          @click="filterAction = 'log_part'"
          :class="filterAction === 'log_part' ? 'bg-teal-600 text-white shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
          class="h-7 px-2.5 text-xs cursor-pointer"
        >
          Intake / Log
        </Button>
        <Button
          variant="ghost"
          size="sm"
          @click="filterAction = 'scrap'"
          :class="filterAction === 'scrap' ? 'bg-destructive text-destructive-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
          class="h-7 px-2.5 text-xs cursor-pointer"
        >
          Scrapped
        </Button>
      </div>

      <!-- Timeline Records List -->
      <div class="p-4 space-y-3 overflow-y-auto flex-1 custom-scrollbar">
        <div v-if="filteredLogs.length === 0" class="p-8 text-center text-xs text-muted-foreground border border-dashed border-border rounded-lg">
          No audit records found.
        </div>

        <div
          v-for="record in filteredLogs"
          :key="record.id"
          class="p-3.5 rounded-xl border border-border bg-card shadow-xs space-y-2 text-xs"
        >
          <!-- Record Header -->
          <div class="flex items-start justify-between gap-2">
            <div>
              <div class="flex items-center gap-1.5">
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wider"
                  :class="{
                    'bg-amber-500/15 text-amber-600': record.action === 'use_part',
                    'bg-teal-500/15 text-teal-600': record.action === 'log_part' || record.action === 'bulk_log',
                    'bg-rose-500/15 text-rose-600': record.action === 'scrap',
                    'bg-blue-500/15 text-blue-600': record.action === 'resell_price_update' || record.action === 'condition_update'
                  }"
                >
                  {{ record.action.replace('_', ' ') }}
                </span>
                <span class="font-mono font-bold text-primary">{{ record.partIdentifier }}</span>
              </div>
              <div class="font-semibold text-foreground mt-0.5">{{ record.partName }}</div>
            </div>

            <div class="text-right shrink-0">
              <div class="text-[10px] text-muted-foreground flex items-center justify-end gap-1">
                <Clock class="size-3" />
                <span>{{ formatDate(record.timestamp) }}</span>
              </div>
              <div class="text-[11px] font-medium text-foreground mt-0.5">
                {{ record.actorName }}
              </div>
            </div>
          </div>

          <!-- Mandatory Cost Center Tagging (if use_part) -->
          <div v-if="record.costCenter" class="p-2.5 rounded-lg bg-amber-500/5 border border-amber-500/20 space-y-1">
            <div class="text-[10px] font-bold uppercase tracking-wider text-amber-600 flex items-center gap-1">
              <Layers class="size-3" />
              <span>Mandatory Cost Center Attribution:</span>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-1.5 text-[11px] font-mono">
              <div class="text-foreground">
                <span class="text-muted-foreground font-sans">Line:</span> {{ record.costCenter.prodLine }}
              </div>
              <div class="text-foreground">
                <span class="text-muted-foreground font-sans">Project:</span> {{ record.costCenter.project }}
              </div>
              <div class="text-foreground">
                <span class="text-muted-foreground font-sans">Dept:</span> {{ record.costCenter.department }}
              </div>
            </div>
          </div>

          <!-- Notes -->
          <p v-if="record.notes" class="text-muted-foreground text-[11px] italic">
            "{{ record.notes }}"
          </p>
        </div>
      </div>
  </div>
</template>
