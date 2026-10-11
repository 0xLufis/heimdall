<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import {
  History,
  Search,
  Filter,
  RefreshCw,
  ShieldAlert,
  Info,
  AlertTriangle,
  User,
  ArrowRight,
  Clock,
  Layers,
  CheckCircle2,
  ChevronDown,
  Eye
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'

export interface AuditEntry {
  id: string
  timestamp: string
  action: string
  entityType: string
  entityId: string
  entityName?: string
  userId: string
  userName: string
  summary: string
  severity: 'info' | 'warning' | 'critical'
  details?: Record<string, any>
}

const entries = ref<AuditEntry[]>([])
const isLoading = ref(false)
const searchQuery = ref('')
const selectedEntityType = ref('all')
const selectedEntry = ref<AuditEntry | null>(null)
const isDetailModalOpen = ref(false)

const ENTITY_OPTIONS = [
  { value: 'all', label: 'All Entities' },
  { value: 'Ticket', label: 'Tickets' },
  { value: 'InventoryItem', label: 'Inventory / Parts' },
  { value: 'Machine', label: 'Machines' },
  { value: 'SystemPolicy', label: 'System Policies' }
]

async function fetchAuditLogs() {
  isLoading.value = true
  try {
    const params = new URLSearchParams()
    if (selectedEntityType.value !== 'all') params.set('entityType', selectedEntityType.value)
    if (searchQuery.value.trim()) params.set('query', searchQuery.value.trim())

    const res = await $fetch<{ total: number; entries: AuditEntry[] }>(`/api/analytics/audit-logs?${params.toString()}`)
    if (res?.entries) {
      entries.value = res.entries
    }
  } catch (err) {
    console.error('Error fetching audit logs:', err)
  } finally {
    isLoading.value = false
  }
}

watch([selectedEntityType], () => {
  fetchAuditLogs()
})

let searchDebounceTimer: any = null
function handleSearchInput() {
  clearTimeout(searchDebounceTimer)
  searchDebounceTimer = setTimeout(() => {
    fetchAuditLogs()
  }, 300)
}

function openDetail(entry: AuditEntry) {
  selectedEntry.value = entry
  isDetailModalOpen.value = true
}

function actionBadgeClass(action: string) {
  switch (action) {
    case 'CREATE':
      return 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30'
    case 'DELETE':
    case 'DECOMMISSION':
      return 'bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30'
    case 'ESCALATION':
      return 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30 font-bold'
    case 'STATUS_CHANGE':
      return 'bg-blue-500/15 text-blue-700 dark:text-blue-300 border-blue-500/30'
    default:
      return 'bg-muted text-muted-foreground border-border'
  }
}

function severityIcon(severity: string) {
  if (severity === 'critical') return ShieldAlert
  if (severity === 'warning') return AlertTriangle
  return Info
}

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleDateString([], {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    })
  } catch {
    return iso
  }
}

onMounted(() => {
  fetchAuditLogs()
})
</script>

<template>
  <div class="space-y-4 animate-in fade-in duration-200">
    <!-- Filter and Search Toolbar -->
    <div class="p-4 bg-card rounded-2xl border border-border shadow-xs flex flex-col sm:flex-row items-center justify-between gap-3">
      <div class="flex items-center gap-2 w-full sm:w-auto">
        <div class="relative w-full sm:w-72">
          <Search class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
          <Input
            v-model="searchQuery"
            @input="handleSearchInput"
            placeholder="Search audit records..."
            class="pl-9 text-xs h-9 bg-background border-border"
          />
        </div>

        <select
          v-model="selectedEntityType"
          class="h-9 px-3 bg-background border border-border rounded-lg text-xs text-foreground focus:outline-hidden font-medium"
        >
          <option v-for="opt in ENTITY_OPTIONS" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>
      </div>

      <div class="flex items-center gap-2 self-end sm:self-auto shrink-0">
        <span class="text-xs text-muted-foreground font-mono">
          {{ entries.length }} recorded event{{ entries.length === 1 ? '' : 's' }}
        </span>
        <Button
          variant="outline"
          size="sm"
          @click="fetchAuditLogs"
          class="h-9 text-xs"
        >
          <RefreshCw :class="{ 'animate-spin': isLoading }" class="size-3.5 mr-1.5" />
          Refresh
        </Button>
      </div>
    </div>

    <!-- Audit Events Table / Feed -->
    <div class="bg-card rounded-2xl border border-border shadow-xs overflow-hidden">
      <div v-if="isLoading && entries.length === 0" class="p-12 text-center text-xs text-muted-foreground">
        <RefreshCw class="size-6 animate-spin mx-auto mb-2 text-primary" />
        Loading audit logs...
      </div>

      <div v-else-if="entries.length === 0" class="p-12 text-center text-xs text-muted-foreground space-y-2">
        <History class="size-8 mx-auto text-muted-foreground/60" />
        <p class="font-medium">No audit events match your query.</p>
      </div>

      <div v-else class="divide-y divide-border">
        <div
          v-for="entry in entries"
          :key="entry.id"
          class="p-4 hover:bg-muted/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs"
        >
          <div class="flex items-start gap-3 min-w-0">
            <div
              :class="entry.severity === 'critical' ? 'text-rose-500 bg-rose-500/10' : (entry.severity === 'warning' ? 'text-amber-500 bg-amber-500/10' : 'text-primary bg-primary/10')"
              class="p-2 rounded-xl shrink-0 border border-border/40"
            >
              <component :is="severityIcon(entry.severity)" class="size-4" />
            </div>

            <div class="space-y-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <Badge :class="actionBadgeClass(entry.action)" class="text-[9px] px-1.5 py-0 font-mono font-bold uppercase">
                  {{ entry.action }}
                </Badge>
                <span class="font-mono text-[10px] text-muted-foreground bg-muted px-1.5 py-0.5 rounded border border-border">
                  {{ entry.entityType }}
                </span>
                <span v-if="entry.entityName" class="font-semibold text-foreground truncate max-w-xs">
                  {{ entry.entityName }}
                </span>
              </div>
              <p class="text-xs text-foreground/90 font-normal leading-relaxed">
                {{ entry.summary }}
              </p>
              <div class="flex items-center gap-3 text-[11px] text-muted-foreground pt-0.5">
                <span class="flex items-center gap-1 font-mono">
                  <User class="size-3 text-muted-foreground/70" />
                  {{ entry.userName }}
                </span>
                <span class="flex items-center gap-1 font-mono">
                  <Clock class="size-3 text-muted-foreground/70" />
                  {{ formatDate(entry.timestamp) }}
                </span>
              </div>
            </div>
          </div>

          <div class="flex items-center gap-2 shrink-0 self-end md:self-center">
            <Button
              v-if="entry.details"
              variant="ghost"
              size="sm"
              class="h-7 text-[11px] px-2.5 text-muted-foreground hover:text-foreground"
              @click="openDetail(entry)"
            >
              <Eye class="size-3 mr-1" />
              View Diff
            </Button>
          </div>
        </div>
      </div>
    </div>

    <!-- Detail / Diff Modal -->
    <Dialog v-model:open="isDetailModalOpen">
      <DialogContent class="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-sm font-bold">
            <History class="size-4 text-primary" />
            Audit Record Details
          </DialogTitle>
          <DialogDescription class="text-xs">
            Cryptographic change audit context for {{ selectedEntry?.entityType }}: {{ selectedEntry?.entityName || selectedEntry?.entityId }}
          </DialogDescription>
        </DialogHeader>

        <div v-if="selectedEntry" class="space-y-4 pt-2 text-xs">
          <div class="p-3 bg-muted/40 rounded-xl border border-border space-y-2">
            <div class="grid grid-cols-2 gap-2 text-[11px]">
              <div>
                <span class="text-muted-foreground block">Action</span>
                <strong class="font-mono text-foreground">{{ selectedEntry.action }}</strong>
              </div>
              <div>
                <span class="text-muted-foreground block">Actor</span>
                <strong class="font-mono text-foreground">{{ selectedEntry.userName }}</strong>
              </div>
              <div>
                <span class="text-muted-foreground block">Timestamp</span>
                <strong class="font-mono text-foreground">{{ formatDate(selectedEntry.timestamp) }}</strong>
              </div>
              <div>
                <span class="text-muted-foreground block">Entity ID</span>
                <strong class="font-mono text-foreground">{{ selectedEntry.entityId }}</strong>
              </div>
            </div>
            <div class="pt-2 border-t border-border">
              <span class="text-muted-foreground block text-[11px]">Summary</span>
              <p class="text-foreground font-medium mt-0.5">{{ selectedEntry.summary }}</p>
            </div>
          </div>

          <div v-if="selectedEntry.details" class="space-y-1.5">
            <h5 class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Payload Diff &amp; Context</h5>
            <pre class="p-3 bg-zinc-950 text-zinc-100 rounded-xl text-[11px] font-mono overflow-x-auto border border-zinc-800 max-h-60 leading-normal">{{ JSON.stringify(selectedEntry.details, null, 2) }}</pre>
          </div>

          <div class="flex justify-end pt-2">
            <Button size="sm" variant="outline" class="text-xs h-8" @click="isDetailModalOpen = false">
              Close
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
