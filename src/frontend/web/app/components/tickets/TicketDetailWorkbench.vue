<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  X,
  ExternalLink,
  Flame,
  AlertTriangle,
  Clock,
  Play,
  CheckCircle2,
  ShieldAlert,
  UserCheck,
  Send,
  Factory,
  Cpu,
  Layers,
  Paperclip,
  ZoomIn
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { useMaintenance } from '~/composables/useMaintenance'
import { authClient } from '~/utils/auth-client'
import { ANDON_STYLES, getAndonColorForStatus, getAndonPriorityStyle } from '~/utils/andonColors'
import type { MaintenanceTicket, TicketStatus, PendingReason } from '~/types/maintenance'

const props = defineProps<{
  ticket: MaintenanceTicket | null
}>()

const emit = defineEmits<{
  'close': []
  'updated': [ticket: MaintenanceTicket]
}>()

const {
  updateStatus,
  setPending,
  resolveEscalation,
  reserveTicket,
  qrPickup,
  fetchTickets
} = useMaintenance()

const commentText = ref('')
const isPostingComment = ref(false)
const showPendingInput = ref(false)
const pendingReason = ref<PendingReason>('Parts')
const pendingNote = ref('')

const currentUser = computed(() => {
  const session = (authClient as any).useSession?.()
  return session?.data?.value?.user?.name || 'On-Duty Specialist'
})

const slaText = computed(() => {
  if (!props.ticket?.slaDueAt) return null
  const due = new Date(props.ticket.slaDueAt).getTime()
  const now = Date.now()
  const diff = due - now
  if (props.ticket.status === 'Resolved' || props.ticket.status === 'Closed') {
    return { text: 'SLA Met', overdue: false }
  }
  if (diff < 0) {
    const mins = Math.abs(Math.floor(diff / 60000))
    const h = Math.floor(mins / 60)
    return { text: `Overdue by ${h > 0 ? `${h}h ` : ''}${mins % 60}m`, overdue: true }
  }
  const mins = Math.floor(diff / 60000)
  const h = Math.floor(mins / 60)
  return { text: `Due in ${h > 0 ? `${h}h ` : ''}${mins % 60}m`, overdue: false }
})

async function onStartWork() {
  if (!props.ticket) return
  try {
    const updated = await updateStatus(props.ticket.id, 'InProgress', currentUser.value)
    toast.success('Investigation started. Status: In Progress.')
    if (updated) emit('updated', updated)
    await fetchTickets()
  } catch {
    toast.error('Failed to update status.')
  }
}

async function onConfirmPending() {
  if (!props.ticket) return
  try {
    const updated = await setPending(props.ticket.id, pendingReason.value, pendingNote.value)
    toast.warning(`Status set to Pending (${pendingReason.value}).`)
    showPendingInput.value = false
    pendingNote.value = ''
    if (updated) emit('updated', updated)
    await fetchTickets()
  } catch {
    toast.error('Failed to update status.')
  }
}

async function onResolve() {
  if (!props.ticket) return
  try {
    const updated = await updateStatus(props.ticket.id, 'Resolved', currentUser.value)
    toast.success('Incident resolved.')
    if (updated) emit('updated', updated)
    await fetchTickets()
  } catch {
    toast.error('Failed to resolve incident.')
  }
}

async function onSelfAssign() {
  if (!props.ticket) return
  try {
    const updated = await reserveTicket(props.ticket.id, currentUser.value)
    toast.success(`Assigned to ${currentUser.value}`)
    if (updated) emit('updated', updated)
    await fetchTickets()
  } catch {
    toast.error('Failed to claim ticket.')
  }
}

async function onSubmitComment() {
  if (!props.ticket || !commentText.value.trim()) return
  isPostingComment.value = true
  try {
    await $fetch(`/api/tickets/${props.ticket.id}/comments`, {
      method: 'POST',
      body: {
        authorName: currentUser.value,
        content: commentText.value.trim()
      }
    })
    toast.success('Comment logged.')
    commentText.value = ''
    await fetchTickets()
  } catch {
    toast.error('Failed to post comment.')
  } finally {
    isPostingComment.value = false
  }
}
</script>

<template>
  <div v-if="ticket" class="flex flex-col h-full bg-card border border-border rounded-2xl shadow-xl overflow-hidden animate-in fade-in duration-200">
    <!-- Header -->
    <div class="p-4 border-b border-border bg-muted/30 flex items-center justify-between gap-3">
      <div class="space-y-1 min-w-0">
        <div class="flex items-center gap-2 flex-wrap">
          <NuxtLink
            :to="`/dashboard/tickets/${ticket.id}`"
            class="font-mono text-xs font-bold text-indigo-600 dark:text-indigo-400 bg-indigo-500/10 hover:bg-indigo-500/20 px-2 py-0.5 rounded-md border border-indigo-500/20 transition-colors flex items-center gap-1"
            title="Open Dedicated Full Page"
          >
            <span>{{ ticket.ticketNumber }}</span>
            <ExternalLink class="size-3" />
          </NuxtLink>

          <Badge
            variant="outline"
            :class="[
              'text-[10px] font-bold uppercase px-2 py-0.5 flex items-center gap-1',
              ANDON_STYLES[getAndonColorForStatus(ticket.status)].badgeClass
            ]"
          >
            <span class="size-1.5 rounded-full" :class="ANDON_STYLES[getAndonColorForStatus(ticket.status)].dotClass" />
            <span>{{ ticket.status }}</span>
          </Badge>

          <Badge
            variant="outline"
            :class="[
              'text-[10px] font-bold uppercase px-2 py-0.5',
              getAndonPriorityStyle(ticket.priority).badgeClass
            ]"
          >
            {{ ticket.priority }}
          </Badge>

          <Badge
            v-if="ticket.isLineStop"
            variant="outline"
            class="text-[9px] bg-rose-600 text-white font-black uppercase px-2 py-0.5 border-transparent flex items-center gap-1 animate-pulse"
          >
            <Flame class="size-2.5" />
            <span>Line Stop ({{ ticket.lineStopDurationMinutes || 0 }}m)</span>
          </Badge>
        </div>

        <h3 class="text-sm font-bold text-foreground truncate">{{ ticket.title }}</h3>
      </div>

      <div class="flex items-center gap-1 shrink-0">
        <NuxtLink :to="`/dashboard/tickets/${ticket.id}`">
          <Button
            variant="ghost"
            size="icon"
            class="size-7 rounded-lg text-muted-foreground hover:text-foreground"
            title="Open Full Page View"
          >
            <ExternalLink class="size-3.5" />
          </Button>
        </NuxtLink>
        <Button
          variant="ghost"
          size="icon"
          class="size-7 rounded-lg text-muted-foreground hover:text-foreground"
          @click="emit('close')"
          title="Close Inspector"
        >
          <X class="size-3.5" />
        </Button>
      </div>
    </div>

    <!-- Scrollable Body -->
    <div class="flex-1 overflow-y-auto p-4 space-y-4 text-xs">
      <!-- SLA Alert Bar -->
      <div
        v-if="slaText"
        class="flex items-center justify-between px-3 py-1.5 rounded-lg border"
        :class="slaText.overdue ? 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/20 font-semibold' : 'bg-muted/40 text-muted-foreground border-border'"
      >
        <div class="flex items-center gap-1.5">
          <Clock class="size-3.5" />
          <span>{{ slaText.text }}</span>
        </div>
        <span class="text-[10px] uppercase font-mono font-medium">Target Response</span>
      </div>

      <!-- Quick Action Transitions -->
      <div class="p-3 bg-muted/20 border border-border rounded-xl space-y-2">
        <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider block">
          Investigation Actions
        </span>
        <div class="flex items-center gap-2 flex-wrap">
          <Button
            v-if="ticket.status === 'Open'"
            size="sm"
            @click="onStartWork"
            class="bg-blue-600 hover:bg-blue-700 text-white text-xs h-7 px-3 rounded-lg gap-1 cursor-pointer"
          >
            <Play class="size-3" />
            <span>Start Work</span>
          </Button>

          <Button
            v-if="ticket.status === 'InProgress'"
            size="sm"
            variant="outline"
            @click="showPendingInput = !showPendingInput"
            class="border-amber-500/40 text-amber-800 dark:text-amber-400 hover:bg-amber-500/10 text-xs h-7 px-3 rounded-lg gap-1 cursor-pointer"
          >
            <Clock class="size-3" />
            <span>Hold</span>
          </Button>

          <Button
            v-if="ticket.status === 'Pending'"
            size="sm"
            @click="onStartWork"
            class="bg-blue-600 hover:bg-blue-700 text-white text-xs h-7 px-3 rounded-lg gap-1 cursor-pointer"
          >
            <Play class="size-3" />
            <span>Resume</span>
          </Button>

          <Button
            v-if="ticket.status === 'InProgress' || ticket.status === 'Pending'"
            size="sm"
            @click="onResolve"
            class="bg-emerald-600 hover:bg-emerald-700 text-white text-xs h-7 px-3 rounded-lg gap-1 cursor-pointer"
          >
            <CheckCircle2 class="size-3" />
            <span>Resolve</span>
          </Button>

          <NuxtLink :to="`/dashboard/tickets/${ticket.id}`" class="ml-auto">
            <Button variant="outline" size="sm" class="text-xs h-7 px-2.5 rounded-lg border-border">
              Full Page &rsaquo;
            </Button>
          </NuxtLink>
        </div>

        <!-- Pending Form Accordion -->
        <div v-if="showPendingInput" class="pt-2 border-t border-border space-y-2">
          <div class="grid grid-cols-2 gap-2">
            <select
              v-model="pendingReason"
              class="h-7 bg-background border border-border rounded-lg px-2 text-xs text-foreground focus:outline-hidden"
            >
              <option value="Parts">Parts Required</option>
              <option value="ExternalOk">External Vendor</option>
              <option value="Action">Process Action</option>
              <option value="SignOff">Quality SignOff</option>
            </select>
            <Input
              v-model="pendingNote"
              placeholder="Note / Part #..."
              class="h-7 text-xs bg-background"
            />
          </div>
          <div class="flex justify-end gap-1.5">
            <Button size="sm" variant="ghost" @click="showPendingInput = false" class="h-6 text-[11px]">Cancel</Button>
            <Button size="sm" @click="onConfirmPending" class="bg-amber-600 hover:bg-amber-500 text-white h-6 text-[11px] px-2.5">
              Confirm Hold
            </Button>
          </div>
        </div>
      </div>

      <!-- Station & Machine Metadata -->
      <div class="p-3 bg-muted/20 border border-border rounded-xl space-y-2">
        <div class="flex items-center justify-between text-muted-foreground text-[11px]">
          <span class="font-bold uppercase tracking-wider">Equipment Node</span>
          <span class="font-mono text-foreground font-semibold">{{ ticket.stationId }}</span>
        </div>
        <div v-if="ticket.stationName" class="text-foreground font-medium">
          {{ ticket.stationName }}
        </div>
        <div class="grid grid-cols-2 gap-2 text-[11px] text-muted-foreground pt-1 border-t border-border">
          <div>Controller: <span class="text-foreground font-mono font-medium">{{ ticket.controllerId || 'Local IPC' }}</span></div>
          <div>Machine: <span class="text-foreground font-medium">{{ ticket.machineType || 'Standard' }}</span></div>
        </div>
      </div>

      <!-- Specialist Ownership -->
      <div class="p-3 bg-muted/20 border border-border rounded-xl flex items-center justify-between">
        <div class="flex items-center gap-2 min-w-0">
          <div class="size-7 rounded-lg bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 font-bold flex items-center justify-center shrink-0">
            {{ ticket.assignedTechnicianName ? ticket.assignedTechnicianName.charAt(0).toUpperCase() : '?' }}
          </div>
          <div class="min-w-0">
            <div class="font-bold text-foreground truncate">
              {{ ticket.assignedTechnicianName || 'Unassigned Queue' }}
            </div>
            <div class="text-[10px] text-muted-foreground">
              {{ ticket.responsibleDepartment || 'Operations Floor' }}
            </div>
          </div>
        </div>

        <Button
          v-if="ticket.assignedTechnicianName !== currentUser"
          size="sm"
          variant="outline"
          @click="onSelfAssign"
          class="h-7 text-[11px] px-2.5 rounded-lg border-indigo-500/30 text-indigo-600 dark:text-indigo-400 hover:bg-indigo-500/10"
        >
          Claim Ticket
        </Button>
      </div>

      <!-- Description -->
      <div class="space-y-1">
        <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider block">
          Failure Description
        </span>
        <p class="text-foreground leading-relaxed whitespace-pre-wrap bg-muted/10 p-2.5 rounded-xl border border-border">
          {{ ticket.description || 'No failure description provided.' }}
        </p>
      </div>

      <!-- Attachments Preview -->
      <div v-if="ticket.attachments && ticket.attachments.length > 0" class="space-y-1.5">
        <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider block">
          Attachments ({{ ticket.attachments.length }})
        </span>
        <div class="grid grid-cols-3 gap-2">
          <div
            v-for="att in ticket.attachments"
            :key="att.id"
            class="border border-border rounded-lg overflow-hidden bg-muted/30 group relative"
          >
            <img :src="att.url" :alt="att.fileName" class="w-full h-16 object-cover" />
            <div class="p-1 text-[10px] truncate bg-card border-t border-border">
              {{ att.fileName }}
            </div>
          </div>
        </div>
      </div>

      <!-- Recent Comments Stream -->
      <div class="space-y-2 pt-2 border-t border-border">
        <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider block">
          Activity Stream ({{ ticket.comments?.length || 0 }})
        </span>
        <div class="space-y-2 max-h-40 overflow-y-auto">
          <div
            v-for="cmt in ticket.comments"
            :key="cmt.id"
            class="p-2 rounded-lg bg-muted/30 border border-border space-y-1 text-[11px]"
          >
            <div class="flex justify-between font-medium">
              <span class="font-bold text-foreground">{{ cmt.authorName }}</span>
              <span class="text-muted-foreground text-[10px] font-mono">
                {{ new Date(cmt.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
              </span>
            </div>
            <p class="text-muted-foreground">{{ cmt.content }}</p>
          </div>
        </div>

        <!-- Quick Post -->
        <div class="flex gap-1.5 pt-1">
          <Input
            v-model="commentText"
            placeholder="Quick progress note..."
            class="h-7 text-xs bg-background flex-1"
            @keydown.enter.prevent="onSubmitComment"
          />
          <Button
            size="sm"
            @click="onSubmitComment"
            :disabled="isPostingComment || !commentText.trim()"
            class="h-7 px-2.5 text-xs bg-primary text-primary-foreground rounded-lg"
          >
            <Send class="size-3" />
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
