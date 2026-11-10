<script setup lang="ts">
import { computed } from 'vue'
import {
  UserCheck, AlertTriangle, Flame, Clock, Play,
  ChevronRight, Wrench, ShieldCheck, CheckCircle2
} from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { authClient } from '~/utils/auth-client'
import type { MaintenanceTicket } from '~/types/maintenance'
import {
  ANDON_STYLES,
  getAndonColorForStatus,
  getAndonPriorityStyle
} from '~/utils/andonColors'

const props = defineProps<{
  tickets: MaintenanceTicket[]
  currentUserName?: string
}>()

const emit = defineEmits<{
  (e: 'selectTicket', ticket: MaintenanceTicket): void
  (e: 'pickupTicket', ticketId: string): void
  (e: 'resumeTicket', ticketId: string): void
}>()

// Active logged in user
const session = (authClient as any).useSession?.()
const loggedInUser = computed(() => {
  if (props.currentUserName) return props.currentUserName
  return session?.data?.value?.user?.name || ''
})

// Filter tickets dedicated/assigned to the user
const dedicatedTickets = computed(() => {
  const user = loggedInUser.value.trim().toLowerCase()
  if (!user) {
    // If no explicit user logged in, check for tickets assigned to common demo technician or reserved
    return props.tickets.filter(t => 
      (t.reservedBy || t.assignedTechnicianName) &&
      (t.status === 'Open' || t.status === 'InProgress' || t.status === 'Pending')
    ).slice(0, 3)
  }

  return props.tickets.filter(t => {
    const isAssigned = (t.assignedTechnicianName?.toLowerCase() || '') === user
    const isReserved = (t.reservedBy?.toLowerCase() || '') === user
    const isActive = t.status === 'Open' || t.status === 'InProgress' || t.status === 'Pending'
    return (isAssigned || isReserved) && isActive
  })
})

const lineStopCount = computed(() => dedicatedTickets.value.filter(t => t.isLineStop).length)
const escalatedCount = computed(() => dedicatedTickets.value.filter(t => t.isEscalated).length)

function getStatusBadge(status: string) {
  const andon = getAndonColorForStatus(status)
  return ANDON_STYLES[andon]
}
</script>

<template>
  <div
    v-if="dedicatedTickets.length > 0"
    data-testid="ticket-personal-header"
    class="p-4 rounded-2xl bg-gradient-to-r from-indigo-500/10 via-card to-indigo-500/5 border border-indigo-500/30 shadow-sm space-y-3"
  >
    <!-- Header Title Bar -->
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex items-center gap-2.5">
        <div class="p-2 rounded-xl bg-indigo-500/20 text-indigo-600 dark:text-indigo-400 border border-indigo-500/30">
          <UserCheck class="size-5" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <h2 class="text-sm font-black uppercase tracking-wider text-foreground">
              My Dedicated Incidents &amp; Work Queue
            </h2>
            <Badge class="bg-indigo-600 text-white text-[10px] font-black px-2 py-0.5 rounded-full">
              {{ dedicatedTickets.length }} Assigned
            </Badge>
          </div>
          <p class="text-xs text-muted-foreground mt-0.5">
            Active factory floor tickets currently dedicated to you (<strong class="text-foreground font-semibold">{{ loggedInUser || 'Dedicated Technician' }}</strong>)
          </p>
        </div>
      </div>

      <!-- Quick Metrics Summary Chips -->
      <div class="flex items-center gap-2">
        <Badge
          v-if="lineStopCount > 0"
          variant="outline"
          class="bg-rose-500/15 border-rose-500/40 text-rose-700 dark:text-rose-300 font-bold text-xs flex items-center gap-1.5 animate-pulse"
        >
          <Flame class="size-3.5" />
          <span>{{ lineStopCount }} Line-Stop Incident{{ lineStopCount > 1 ? 's' : '' }}</span>
        </Badge>
        <Badge
          v-if="escalatedCount > 0"
          variant="outline"
          class="bg-amber-500/15 border-amber-500/40 text-amber-800 dark:text-amber-300 font-bold text-xs flex items-center gap-1.5"
        >
          <AlertTriangle class="size-3.5" />
          <span>{{ escalatedCount }} Escalated</span>
        </Badge>
      </div>
    </div>

    <!-- Cards Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2.5 pt-1">
      <div
        v-for="tkt in dedicatedTickets"
        :key="tkt.id"
        role="button"
        tabindex="0"
        @click="emit('selectTicket', tkt)"
        @keydown.enter="emit('selectTicket', tkt)"
        class="p-3 bg-card/90 hover:bg-muted/60 border border-border hover:border-indigo-500/40 rounded-xl shadow-xs transition-all duration-150 flex flex-col justify-between gap-2.5 cursor-pointer group text-left"
      >
        <div class="space-y-1.5">
          <div class="flex items-center justify-between gap-1.5">
            <span class="font-mono text-[11px] font-bold text-indigo-600 dark:text-indigo-400">
              {{ tkt.ticketNumber }}
            </span>
            <div class="flex items-center gap-1">
              <!-- Line Stop Badge -->
              <span
                v-if="tkt.isLineStop"
                class="px-1.5 py-0.5 rounded text-[9px] font-black uppercase tracking-wider bg-rose-600 text-white flex items-center gap-0.5 animate-pulse"
              >
                <Flame class="size-2.5" />
                Line Stop
              </span>
              <!-- Status Badge -->
              <span
                class="px-2 py-0.5 rounded text-[10px] font-semibold border flex items-center gap-1"
                :class="getStatusBadge(tkt.status).badgeClass"
              >
                <span class="size-1.5 rounded-full shrink-0" :class="getStatusBadge(tkt.status).dotClass" />
                {{ tkt.status }}
              </span>
            </div>
          </div>

          <h4 class="text-xs font-bold text-foreground line-clamp-1 group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors">
            {{ tkt.title }}
          </h4>

          <div class="flex items-center justify-between text-[11px] text-muted-foreground">
            <span class="truncate">{{ tkt.stationName || tkt.stationId || 'Station' }}</span>
            <span v-if="tkt.responsibleDepartment" class="font-semibold text-indigo-500 dark:text-indigo-400 text-[10px]">
              [{{ tkt.responsibleDepartment }}]
            </span>
          </div>
        </div>

        <!-- Footer / Action Row -->
        <div class="flex items-center justify-between pt-1 border-t border-border/50 text-[10px] text-muted-foreground">
          <span class="flex items-center gap-1 font-mono">
            <Clock class="size-3" />
            {{ new Date(tkt.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
          </span>

          <div class="flex items-center gap-1" @click.stop>
            <Button
              v-if="tkt.status === 'Open'"
              size="sm"
              class="h-6 px-2 text-[10px] font-bold bg-indigo-600 hover:bg-indigo-700 text-white rounded-md"
              @click="emit('pickupTicket', tkt.id)"
            >
              <Play class="size-2.5 mr-1" />
              Pickup
            </Button>
            <Button
              v-else-if="tkt.status === 'Pending'"
              size="sm"
              class="h-6 px-2 text-[10px] font-bold bg-amber-600 hover:bg-amber-700 text-white rounded-md"
              @click="emit('resumeTicket', tkt.id)"
            >
              <Play class="size-2.5 mr-1" />
              Resume
            </Button>
            <Button
              variant="ghost"
              size="sm"
              class="h-6 px-1.5 text-muted-foreground hover:text-foreground"
              @click="emit('selectTicket', tkt)"
            >
              Details
              <ChevronRight class="size-3 ml-0.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
