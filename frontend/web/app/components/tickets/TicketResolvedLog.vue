<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  ArrowUpDown, ChevronUp, ChevronDown, CheckCircle2,
  Clock, User, ZoomIn, ArrowRight, Activity, Database, Image, X
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import type { MaintenanceTicket, TicketAttachment } from '~/types/maintenance'

const props = defineProps<{
  tickets: MaintenanceTicket[]
}>()

// ── Types ──────────────────────────────────────────────────────────────
type SortKey =
  | 'ticketNumber' | 'stationName' | 'machineType' | 'errorCode'
  | 'sfc' | 'priority' | 'resolvedAt' | 'mttr' | 'assignedTechnicianName'

const RESOLVED_STATUSES = new Set(['Resolved', 'Closed'])

// ── Filtered source ────────────────────────────────────────────────────
const resolvedTickets = computed(() =>
  props.tickets.filter(t => RESOLVED_STATUSES.has(t.status))
)

// ── Sorting ────────────────────────────────────────────────────────────
const sortKey = ref<SortKey>('resolvedAt')
const sortDir = ref<'asc' | 'desc'>('desc')

function toggleSort(key: SortKey) {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortDir.value = 'desc'
  }
}

function getMttrMinutes(ticket: MaintenanceTicket): number {
  if (!ticket.resolvedAt) return 0
  const created = new Date(ticket.createdAt).getTime()
  const resolved = new Date(ticket.resolvedAt).getTime()
  return Math.max(0, Math.round((resolved - created) / 60000))
}

function formatMttr(mins: number): string {
  if (mins === 0) return '—'
  const h = Math.floor(mins / 60)
  const m = mins % 60
  return h > 0 ? `${h}h ${m}m` : `${m}m`
}

function formatDate(iso: string | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString(undefined, {
    month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit'
  })
}

const sortedTickets = computed(() => {
  return [...resolvedTickets.value].sort((a, b) => {
    let aVal: string | number = ''
    let bVal: string | number = ''
    switch (sortKey.value) {
      case 'ticketNumber': aVal = a.ticketNumber; bVal = b.ticketNumber; break
      case 'stationName': aVal = a.stationName ?? ''; bVal = b.stationName ?? ''; break
      case 'machineType': aVal = a.machineType ?? ''; bVal = b.machineType ?? ''; break
      case 'errorCode': aVal = a.errorCode ?? ''; bVal = b.errorCode ?? ''; break
      case 'sfc': aVal = a.sfc ?? ''; bVal = b.sfc ?? ''; break
      case 'priority': {
        const ord: Record<string, number> = { Critical: 4, High: 3, Medium: 2, Low: 1 }
        aVal = ord[a.priority] ?? 0; bVal = ord[b.priority] ?? 0; break
      }
      case 'resolvedAt': aVal = a.resolvedAt ?? ''; bVal = b.resolvedAt ?? ''; break
      case 'mttr': aVal = getMttrMinutes(a); bVal = getMttrMinutes(b); break
      case 'assignedTechnicianName': aVal = a.assignedTechnicianName ?? ''; bVal = b.assignedTechnicianName ?? ''; break
    }
    if (aVal < bVal) return sortDir.value === 'asc' ? -1 : 1
    if (aVal > bVal) return sortDir.value === 'asc' ? 1 : -1
    return 0
  })
})

function priorityClass(p: string): string {
  switch (p) {
    case 'Critical': return 'bg-rose-500/10 text-rose-700 dark:text-rose-400 border-rose-500/30'
    case 'High': return 'bg-orange-500/10 text-orange-700 dark:text-orange-400 border-orange-500/30'
    case 'Medium': return 'bg-amber-500/10 text-amber-800 dark:text-amber-400 border-amber-500/30'
    default: return 'bg-muted text-muted-foreground border-border'
  }
}

function statusClass(s: string): string {
  switch (s) {
    case 'Resolved': return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border-emerald-500/30'
    case 'Closed': return 'bg-muted text-muted-foreground border-border'
    default: return 'bg-muted text-muted-foreground border-border'
  }
}

function transitionClass(s: string): string {
  const map: Record<string, string> = {
    Open: 'bg-blue-500/10 text-blue-700 dark:text-blue-400 border-blue-500/30',
    InProgress: 'bg-indigo-500/10 text-indigo-700 dark:text-indigo-400 border-indigo-500/30',
    Pending: 'bg-amber-500/10 text-amber-800 dark:text-amber-400 border-amber-500/30',
    Resolved: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border-emerald-500/30',
    Closed: 'bg-muted text-muted-foreground border-border'
  }
  return map[s] ?? 'bg-muted text-muted-foreground border-border'
}

// ── Drawer / Inspection ────────────────────────────────────────────────
const drawerOpen = ref(false)
const selectedTicket = ref<MaintenanceTicket | null>(null)
const lightboxOpen = ref(false)
const lightboxSrc = ref('')
const lightboxName = ref('')

function openDrawer(ticket: MaintenanceTicket) {
  selectedTicket.value = ticket
  drawerOpen.value = true
}

function openLightbox(att: TicketAttachment) {
  lightboxSrc.value = att.url ?? ''
  lightboxName.value = att.fileName
  lightboxOpen.value = true
}

function sortIcon(key: SortKey) {
  if (sortKey.value !== key) return 'both'
  return sortDir.value === 'asc' ? 'up' : 'down'
}
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center gap-3">
      <CheckCircle2 class="w-5 h-5 text-emerald-600 dark:text-emerald-400" />
      <h2 class="text-sm font-black uppercase tracking-wider text-foreground">Resolved &amp; Closed Incidents</h2>
      <Badge class="text-[10px] font-mono bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border-emerald-500/30 border">
        {{ resolvedTickets.length }}
      </Badge>
    </div>

    <!-- Empty state -->
    <div
      v-if="resolvedTickets.length === 0"
      class="flex flex-col items-center justify-center py-16 text-center border border-dashed border-border rounded-2xl"
    >
      <CheckCircle2 class="w-10 h-10 text-muted-foreground/40 mb-3" />
      <p class="text-sm font-bold text-muted-foreground">No resolved tickets</p>
      <p class="text-xs text-muted-foreground/80 mt-1">Resolved and Closed tickets appear here</p>
    </div>

    <!-- Table -->
    <div v-else class="overflow-x-auto rounded-2xl border border-border bg-card">
      <table class="w-full text-left text-xs border-collapse">
        <thead>
          <tr class="border-b border-border">
            <th
              v-for="col in [
                { key: 'ticketNumber', label: 'Ticket #' },
                { key: 'stationName', label: 'Machine' },
                { key: 'machineType', label: 'Type' },
                { key: 'errorCode', label: 'Error' },
                { key: 'sfc', label: 'SFC' },
                { key: 'priority', label: 'Priority' },
                { key: 'resolvedAt', label: 'Resolved At' },
                { key: 'mttr', label: 'MTTR' },
                { key: 'assignedTechnicianName', label: 'Resolved By' }
              ]"
              :key="col.key"
              class="px-4 py-3 font-black uppercase tracking-wider text-[10px] text-muted-foreground bg-muted/50 whitespace-nowrap cursor-pointer hover:text-foreground select-none group"
              @click="toggleSort(col.key as SortKey)"
            >
              <div class="flex items-center gap-1">
                {{ col.label }}
                <ChevronUp
                  v-if="sortIcon(col.key as SortKey) === 'up'"
                  class="w-3 h-3 text-indigo-600 dark:text-indigo-400"
                />
                <ChevronDown
                  v-else-if="sortIcon(col.key as SortKey) === 'down'"
                  class="w-3 h-3 text-indigo-600 dark:text-indigo-400"
                />
                <ArrowUpDown v-else class="w-3 h-3 text-muted-foreground/60 group-hover:text-muted-foreground" />
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="ticket in sortedTickets"
            :key="ticket.id"
            class="border-b border-border hover:bg-muted/50 cursor-pointer transition-colors group"
            @click="openDrawer(ticket)"
          >
            <td class="px-4 py-3 font-mono text-[11px] text-indigo-600 dark:text-indigo-400 font-bold whitespace-nowrap">
              {{ ticket.ticketNumber }}
            </td>
            <td class="px-4 py-3 whitespace-nowrap">
              <span class="text-foreground font-semibold">{{ ticket.stationName || '—' }}</span>
            </td>
            <td class="px-4 py-3 whitespace-nowrap text-muted-foreground">
              {{ ticket.machineType || '—' }}
            </td>
            <td class="px-4 py-3 whitespace-nowrap">
              <span v-if="ticket.errorCode" class="font-mono text-[10px] bg-muted text-amber-800 dark:text-amber-400 px-1.5 py-0.5 rounded border border-border">
                {{ ticket.errorCode }}
              </span>
              <span v-else class="text-muted-foreground/60">—</span>
            </td>
            <td class="px-4 py-3 whitespace-nowrap text-muted-foreground font-mono text-[10px]">
              {{ ticket.sfc || '—' }}
            </td>
            <td class="px-4 py-3 whitespace-nowrap">
              <Badge class="text-[9px] font-mono uppercase px-1.5 py-0.5 border" :class="priorityClass(ticket.priority)">
                {{ ticket.priority }}
              </Badge>
            </td>
            <td class="px-4 py-3 whitespace-nowrap text-muted-foreground text-[10px] font-mono">
              {{ formatDate(ticket.resolvedAt) }}
            </td>
            <td class="px-4 py-3 whitespace-nowrap">
              <span
                class="text-[10px] font-mono font-bold"
                :class="getMttrMinutes(ticket) > 240 ? 'text-rose-600 dark:text-rose-400' : getMttrMinutes(ticket) > 60 ? 'text-amber-700 dark:text-amber-400' : 'text-emerald-700 dark:text-emerald-400'"
              >
                {{ formatMttr(getMttrMinutes(ticket)) }}
              </span>
            </td>
            <td class="px-4 py-3 whitespace-nowrap">
              <div class="flex items-center gap-1.5 text-foreground">
                <User class="w-3 h-3 text-muted-foreground shrink-0" />
                {{ ticket.assignedTechnicianName || '—' }}
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- ── Inspection Drawer (Dialog) ── -->
    <Dialog v-model:open="drawerOpen">
      <DialogContent
        v-if="selectedTicket"
        :show-close="false"
        class="max-w-3xl bg-card border-border text-foreground max-h-[90vh] overflow-y-auto"
      >
        <DialogHeader class="border-b border-border pb-4">
          <div class="flex items-start justify-between gap-4">
            <div>
              <div class="flex items-center gap-2 mb-1">
                <span class="text-[10px] font-mono text-muted-foreground">{{ selectedTicket.ticketNumber }}</span>
                <Badge class="text-[9px] border" :class="statusClass(selectedTicket.status)">
                  {{ selectedTicket.status }}
                </Badge>
                <Badge class="text-[9px] border" :class="priorityClass(selectedTicket.priority)">
                  {{ selectedTicket.priority }}
                </Badge>
              </div>
              <DialogTitle class="text-base font-black text-foreground leading-tight">
                {{ selectedTicket.title }}
              </DialogTitle>
              <p v-if="selectedTicket.description" class="text-xs text-muted-foreground mt-1 leading-relaxed">
                {{ selectedTicket.description }}
              </p>
            </div>
            <Button
              variant="ghost"
              size="icon"
              class="h-8 w-8 text-muted-foreground hover:text-foreground shrink-0 rounded-lg cursor-pointer"
              @click="drawerOpen = false"
            >
              <X class="w-4 h-4" />
            </Button>
          </div>
          <div class="flex flex-wrap gap-4 mt-3 text-[10px] text-muted-foreground">
            <span v-if="selectedTicket.stationName" class="flex items-center gap-1">
              <Activity class="w-3 h-3" /> {{ selectedTicket.stationName }}
            </span>
            <span v-if="selectedTicket.machineType">Type: {{ selectedTicket.machineType }}</span>
            <span v-if="selectedTicket.errorCode" class="font-mono text-amber-600 dark:text-amber-400">ERR: {{ selectedTicket.errorCode }}</span>
            <span v-if="selectedTicket.sfc" class="font-mono">SFC: {{ selectedTicket.sfc }}</span>
            <span class="flex items-center gap-1"><Clock class="w-3 h-3" /> MTTR: {{ formatMttr(getMttrMinutes(selectedTicket)) }}</span>
          </div>
        </DialogHeader>

        <div class="space-y-6 pt-2">
          <!-- Comment / Audit Timeline -->
          <div v-if="selectedTicket.comments?.length">
            <h3 class="text-[10px] font-black uppercase tracking-wider text-muted-foreground mb-3 flex items-center gap-2">
              <Activity class="w-3.5 h-3.5" /> Timeline
            </h3>
            <div class="space-y-3 border-l border-border ml-3 pl-5">
              <div
                v-for="comment in selectedTicket.comments"
                :key="comment.id"
                class="relative"
              >
                <div class="absolute -left-[29px] top-1 w-2 h-2 rounded-full border border-border bg-card" />
                <div v-if="comment.transition" class="flex items-center gap-1 mb-1 flex-wrap">
                  <Badge class="text-[9px] border px-1.5 py-0 font-mono" :class="transitionClass(comment.transition.fromStatus)">
                    {{ comment.transition.fromStatus }}
                  </Badge>
                  <ArrowRight class="w-3 h-3 text-muted-foreground/60 shrink-0" />
                  <Badge class="text-[9px] border px-1.5 py-0 font-mono" :class="transitionClass(comment.transition.toStatus)">
                    {{ comment.transition.toStatus }}
                  </Badge>
                </div>
                <div class="bg-muted/30 border border-border rounded-xl p-3">
                  <div class="flex items-center justify-between mb-1.5">
                    <div class="flex items-center gap-1.5">
                      <User class="w-3 h-3 text-muted-foreground" />
                      <span class="text-[10px] font-bold text-foreground">{{ comment.authorName }}</span>
                    </div>
                    <span class="text-[10px] font-mono text-muted-foreground">{{ formatDate(comment.createdAt) }}</span>
                  </div>
                  <p class="text-xs text-muted-foreground leading-relaxed">{{ comment.content }}</p>
                </div>
              </div>
            </div>
          </div>

          <!-- Attachments Gallery -->
          <div v-if="selectedTicket.attachments?.length">
            <h3 class="text-[10px] font-black uppercase tracking-wider text-muted-foreground mb-3 flex items-center gap-2">
              <Image class="w-3.5 h-3.5" /> Attached Images
            </h3>
            <div class="grid grid-cols-3 sm:grid-cols-4 gap-2">
              <div
                v-for="att in selectedTicket.attachments"
                :key="att.id"
                class="relative group aspect-square bg-muted rounded-xl overflow-hidden border border-border hover:border-primary/50 cursor-pointer transition-colors"
                @click="openLightbox(att)"
              >
                <img v-if="att.url" :src="att.url" :alt="att.fileName" class="w-full h-full object-cover" />
                <div class="absolute inset-0 bg-background/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                  <ZoomIn class="w-4 h-4 text-foreground" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>

    <!-- Lightbox -->
    <Dialog v-model:open="lightboxOpen">
      <DialogContent :show-close="false" class="max-w-4xl bg-card border-border p-2">
        <DialogHeader class="px-4 pt-4 flex flex-row items-center justify-between">
          <DialogTitle class="text-sm font-mono text-foreground truncate">{{ lightboxName }}</DialogTitle>
          <Button
            variant="ghost"
            size="icon"
            class="h-7 w-7 text-muted-foreground hover:text-foreground rounded-lg cursor-pointer shrink-0"
            @click="lightboxOpen = false"
          >
            <X class="w-4 h-4" />
          </Button>
        </DialogHeader>
        <div class="flex items-center justify-center p-4 max-h-[80vh] overflow-auto">
          <img :src="lightboxSrc" :alt="lightboxName" class="max-w-full max-h-full object-contain rounded-xl" />
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
