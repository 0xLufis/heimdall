<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft,
  Wrench,
  Clock,
  Flame,
  AlertTriangle,
  CheckCircle2,
  Cpu,
  User,
  UserCheck,
  Send,
  Paperclip,
  ZoomIn,
  X,
  Play,
  RotateCcw,
  ShieldAlert,
  Layers,
  ChevronRight,
  ExternalLink,
  MessageSquare,
  Activity,
  History,
  QrCode,
  Tag,
  Factory
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator
} from '~/components/ui/breadcrumb'
import ImageAttachmentUploader from '~/components/tickets/ImageAttachmentUploader.vue'
import { useMaintenance } from '~/composables/useMaintenance'
import { authClient } from '~/utils/auth-client'
import { ANDON_STYLES, getAndonColorForStatus, getAndonPriorityStyle } from '~/utils/andonColors'
import type {
  MaintenanceTicket,
  TicketStatus,
  PendingReason,
  TicketAttachment,
  EscalationHandoverState
} from '~/types/maintenance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const router = useRouter()
const ticketId = computed(() => route.params.id as string)

const {
  tickets,
  fetchTickets,
  updateStatus,
  setPending,
  escalateTicket,
  resolveEscalation,
  reserveTicket,
  qrPickup
} = useMaintenance()

// ── State ───────────────────────────────────────────────────────────────────
const isLoading = ref(true)
const localTicket = ref<MaintenanceTicket | null>(null)
const error = ref<string | null>(null)

// Action Panel State
const isChangingStatus = ref(false)
const showPendingForm = ref(false)
const pendingReason = ref<PendingReason>('Parts')
const pendingDetails = ref('')

const showEscalateForm = ref(false)
const escalationReason = ref('')
const escalationTarget = ref<'DedicatedEngineer' | 'Management'>('DedicatedEngineer')
const escalationHandover = ref<EscalationHandoverState>('Notification')

const showResolveForm = ref(false)
const resolutionNote = ref('')

// Comment state
const commentText = ref('')
const isSubmittingComment = ref(false)
const commentAttachments = ref<TicketAttachment[]>([])

// Lightbox state
const previewImage = ref<{ url: string; name: string } | null>(null)

// Current User
const currentUser = computed(() => {
  const session = (authClient as any).useSession?.()
  return session?.data?.value?.user?.name || 'On-Duty Specialist'
})

// ── Fetching Data ───────────────────────────────────────────────────────────
async function loadTicket() {
  isLoading.value = true
  error.value = null

  // 1. Check local cache in useMaintenance
  const cached = tickets.value.find(t => t.id === ticketId.value)
  if (cached) {
    localTicket.value = JSON.parse(JSON.stringify(cached))
  }

  // 2. Fetch fresh from API
  try {
    const res = await $fetch<{ ticket: MaintenanceTicket }>(`/api/tickets/${ticketId.value}`)
    if (res && res.ticket) {
      localTicket.value = res.ticket
      // Sync into global state if missing
      const idx = tickets.value.findIndex(t => t.id === res.ticket.id)
      if (idx !== -1) {
        tickets.value[idx] = res.ticket
      } else {
        tickets.value.unshift(res.ticket)
      }
    } else if (!localTicket.value) {
      error.value = `Incident ticket '${ticketId.value}' could not be located.`
    }
  } catch (err: any) {
    if (!localTicket.value) {
      error.value = err?.statusMessage || 'Failed to fetch incident ticket.'
    }
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadTicket()
})

watch(ticketId, () => {
  loadTicket()
})

// Watch global tickets updates from SignalR
watch(
  tickets,
  (list) => {
    const fresh = list.find(t => t.id === ticketId.value)
    if (fresh) {
      localTicket.value = JSON.parse(JSON.stringify(fresh))
    }
  },
  { deep: true }
)

// ── SLA Formatting ──────────────────────────────────────────────────────────
const slaDisplay = computed(() => {
  if (!localTicket.value) return { text: 'No SLA', overdue: false, hours: 0, mins: 0 }
  const status = localTicket.value.status
  if (status === 'Closed' || status === 'Resolved') {
    return { text: 'Target SLA Satisfied', overdue: false, hours: 0, mins: 0 }
  }
  if (!localTicket.value.slaDueAt) {
    return { text: 'No SLA Assigned', overdue: false, hours: 0, mins: 0 }
  }

  const due = new Date(localTicket.value.slaDueAt).getTime()
  const now = Date.now()
  const diff = due - now

  if (diff < 0) {
    const totalMins = Math.abs(Math.floor(diff / 60000))
    const h = Math.floor(totalMins / 60)
    const m = totalMins % 60
    return {
      text: `SLA Overdue by ${h > 0 ? `${h}h ` : ''}${m}m`,
      overdue: true,
      hours: h,
      mins: m
    }
  } else {
    const totalMins = Math.floor(diff / 60000)
    const h = Math.floor(totalMins / 60)
    const m = totalMins % 60
    return {
      text: `SLA Due in ${h > 0 ? `${h}h ` : ''}${m}m`,
      overdue: false,
      hours: h,
      mins: m
    }
  }
})

// ── Status & Workflow Actions ───────────────────────────────────────────────
async function handleStartWork() {
  if (!localTicket.value) return
  isChangingStatus.value = true
  try {
    await updateStatus(localTicket.value.id, 'InProgress', currentUser.value)
    toast.success('Investigation started. Status changed to In Progress.')
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to change status to In Progress.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleConfirmPending() {
  if (!localTicket.value) return
  isChangingStatus.value = true
  try {
    await setPending(localTicket.value.id, pendingReason.value, pendingDetails.value)
    toast.warning(`Incident placed in Pending status (${pendingReason.value}).`)
    showPendingForm.value = false
    pendingDetails.value = ''
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to put ticket in Pending state.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleConfirmResolve() {
  if (!localTicket.value) return
  isChangingStatus.value = true
  try {
    // Add resolution comment if note entered
    if (resolutionNote.value.trim()) {
      await $fetch(`/api/tickets/${localTicket.value.id}/comments`, {
        method: 'POST',
        body: {
          authorName: currentUser.value,
          content: `Resolution Summary: ${resolutionNote.value.trim()}`
        }
      })
    }

    await updateStatus(localTicket.value.id, 'Resolved', currentUser.value)
    toast.success('Incident resolved successfully.')
    showResolveForm.value = false
    resolutionNote.value = ''
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to resolve incident.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleCloseTicket() {
  if (!localTicket.value) return
  isChangingStatus.value = true
  try {
    await updateStatus(localTicket.value.id, 'Closed', currentUser.value)
    toast.success('Ticket closed.')
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to close ticket.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleReopenTicket() {
  if (!localTicket.value) return
  isChangingStatus.value = true
  try {
    await updateStatus(localTicket.value.id, 'Open', currentUser.value)
    toast.info('Ticket reopened.')
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to reopen ticket.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleConfirmEscalate() {
  if (!localTicket.value) return
  if (!escalationReason.value.trim()) {
    toast.error('Please specify an escalation reason.')
    return
  }

  isChangingStatus.value = true
  try {
    await $fetch(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: {
        isEscalated: true,
        escalationReason: escalationReason.value.trim(),
        escalationTarget: escalationTarget.value,
        escalationHandoverState: escalationHandover.value,
        escalatedBy: currentUser.value,
        escalatedAt: new Date().toISOString()
      }
    })
    toast.error(`Incident escalated to ${escalationTarget.value}.`)
    showEscalateForm.value = false
    escalationReason.value = ''
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to escalate incident.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleResolveEscalation() {
  if (!localTicket.value) return
  isChangingStatus.value = true
  try {
    await resolveEscalation(localTicket.value.id, currentUser.value)
    toast.success('Escalation cleared.')
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to resolve escalation.')
  } finally {
    isChangingStatus.value = false
  }
}

async function handleSelfAssign() {
  if (!localTicket.value) return
  try {
    await reserveTicket(localTicket.value.id, currentUser.value)
    toast.success(`Assigned to ${currentUser.value}`)
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to reserve ticket.')
  }
}

async function handleQrPickup() {
  if (!localTicket.value) return
  try {
    await qrPickup(localTicket.value.id, currentUser.value)
    toast.success(`QR Pickup logged: ${currentUser.value} responded to scene`)
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to log QR pickup.')
  }
}

// ── Comment & Timeline Handlers ─────────────────────────────────────────────
async function handleAddComment() {
  if (!localTicket.value) return
  const text = commentText.value.trim()
  if (!text && commentAttachments.value.length === 0) return

  isSubmittingComment.value = true
  try {
    const commentRes = await $fetch<{ success: boolean; comment: any }>(`/api/tickets/${localTicket.value.id}/comments`, {
      method: 'POST',
      body: {
        authorName: currentUser.value,
        content: text || 'Uploaded attachments'
      }
    })

    if (commentRes?.comment && commentAttachments.value.length > 0) {
      for (const att of commentAttachments.value) {
        await $fetch(`/api/tickets/${localTicket.value.id}/attachments`, {
          method: 'POST',
          body: {
            commentId: commentRes.comment.id,
            fileName: att.fileName,
            contentType: att.contentType,
            fileSize: att.fileSize,
            url: att.url
          }
        }).catch(e => console.warn('Comment attachment failed', e))
      }
    }

    toast.success('Comment posted.')
    commentText.value = ''
    commentAttachments.value = []
    await loadTicket()
  } catch (e: any) {
    toast.error('Failed to post comment.')
  } finally {
    isSubmittingComment.value = false
  }
}
</script>

<template>
  <div class="space-y-6 pb-20">
    <!-- Breadcrumbs -->
    <Breadcrumb>
      <BreadcrumbList>
        <BreadcrumbItem>
          <BreadcrumbLink as-child>
            <NuxtLink to="/dashboard" class="text-xs">Dashboard</NuxtLink>
          </BreadcrumbLink>
        </BreadcrumbItem>
        <BreadcrumbSeparator />
        <BreadcrumbItem>
          <BreadcrumbLink as-child>
            <NuxtLink to="/dashboard/tickets" class="text-xs">Incidents</NuxtLink>
          </BreadcrumbLink>
        </BreadcrumbItem>
        <BreadcrumbSeparator />
        <BreadcrumbItem>
          <BreadcrumbPage class="text-xs font-mono font-bold">
            {{ localTicket?.ticketNumber || ticketId }}
          </BreadcrumbPage>
        </BreadcrumbItem>
      </BreadcrumbList>
    </Breadcrumb>

    <!-- Loading State -->
    <div v-if="isLoading && !localTicket" class="flex flex-col items-center justify-center py-24 gap-3">
      <div class="animate-spin rounded-full size-8 border-b-2 border-primary"></div>
      <p class="text-xs font-medium text-muted-foreground">Loading incident docket and telemetry data...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="bg-rose-500/10 border border-rose-500/20 p-6 rounded-2xl text-center space-y-3">
      <div class="w-10 h-10 bg-rose-500/20 rounded-xl flex items-center justify-center mx-auto text-rose-600 dark:text-rose-400">
        <AlertTriangle class="size-5" />
      </div>
      <h3 class="text-base font-bold text-rose-700 dark:text-rose-300">Incident Record Not Found</h3>
      <p class="text-xs text-muted-foreground">{{ error }}</p>
      <NuxtLink to="/dashboard/tickets">
        <Button size="sm" variant="outline" class="text-xs mt-2">
          Back to Incidents List
        </Button>
      </NuxtLink>
    </div>

    <!-- Main Content -->
    <div v-else-if="localTicket" class="space-y-6">

      <!-- Line-Stop Active Alarm Banner -->
      <div
        v-if="localTicket.isLineStop"
        class="p-4 rounded-2xl bg-rose-600 text-white shadow-lg flex flex-col sm:flex-row sm:items-center justify-between gap-4 animate-in fade-in duration-200"
      >
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-white/20 text-white shrink-0">
            <Flame class="size-6 animate-pulse" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-xs font-black uppercase tracking-wider bg-white text-rose-700 px-2 py-0.5 rounded">
                CRITICAL LINE STOP
              </span>
              <span class="text-xs font-mono opacity-90">
                Duration: {{ localTicket.lineStopDurationMinutes || 0 }} min active plant downtime
              </span>
            </div>
            <p class="text-sm font-bold mt-0.5">
              Station {{ localTicket.stationId }} has interrupted production flow. High dispatch priority.
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2 shrink-0">
          <Badge variant="outline" class="border-white/40 text-white text-xs bg-white/10">
            {{ localTicket.responsibleDepartment || 'Operations' }}
          </Badge>
        </div>
      </div>

      <!-- Active Escalation Banner -->
      <div
        v-if="localTicket.isEscalated"
        class="p-4 rounded-2xl bg-rose-500/10 border border-rose-500/40 text-rose-800 dark:text-rose-300 flex flex-col sm:flex-row sm:items-center justify-between gap-4"
      >
        <div class="flex items-center gap-3">
          <ShieldAlert class="size-6 text-rose-600 dark:text-rose-400 shrink-0 animate-pulse" />
          <div>
            <div class="flex items-center gap-2">
              <span class="text-xs font-bold uppercase tracking-wider text-rose-700 dark:text-rose-400">
                ESCALATED TO {{ localTicket.escalationTarget || 'ENGINEERING' }}
              </span>
              <Badge v-if="localTicket.escalationHandoverState" variant="outline" class="text-[10px] border-rose-500/30">
                {{ localTicket.escalationHandoverState }}
              </Badge>
            </div>
            <p class="text-xs font-medium text-foreground mt-0.5">
              Reason: {{ localTicket.escalationReason || 'Specialist intervention requested' }}
              <span v-if="localTicket.escalatedBy" class="text-muted-foreground ml-1">
                (by {{ localTicket.escalatedBy }})
              </span>
            </p>
          </div>
        </div>

        <Button
          size="sm"
          variant="outline"
          @click="handleResolveEscalation"
          class="border-rose-500/40 text-rose-700 dark:text-rose-300 hover:bg-rose-500/20 text-xs font-semibold shrink-0 cursor-pointer"
        >
          Resolve Escalation
        </Button>
      </div>

      <!-- Page Top Header -->
      <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-4 border-b border-border">
        <div class="space-y-1.5">
          <div class="flex items-center gap-2.5 flex-wrap">
            <NuxtLink to="/dashboard/tickets">
              <Button variant="ghost" size="icon" class="size-8 rounded-lg text-muted-foreground hover:text-foreground">
                <ArrowLeft class="size-4" />
              </Button>
            </NuxtLink>

            <span class="font-mono text-sm font-bold text-indigo-600 dark:text-indigo-400 bg-indigo-500/10 px-2.5 py-1 rounded-lg border border-indigo-500/20">
              {{ localTicket.ticketNumber }}
            </span>

            <!-- Status Badge -->
            <Badge
              variant="outline"
              :class="[
                'text-xs font-bold uppercase px-2.5 py-0.5 flex items-center gap-1.5',
                ANDON_STYLES[getAndonColorForStatus(localTicket.status)].badgeClass
              ]"
            >
              <span
                class="size-2 rounded-full"
                :class="ANDON_STYLES[getAndonColorForStatus(localTicket.status)].dotClass"
              />
              <span>{{ localTicket.status }}</span>
              <span v-if="localTicket.status === 'Pending' && localTicket.pendingReason" class="font-normal opacity-80">
                ({{ localTicket.pendingReason }})
              </span>
            </Badge>

            <!-- Priority Badge -->
            <Badge
              variant="outline"
              :class="[
                'text-xs font-bold uppercase px-2.5 py-0.5',
                getAndonPriorityStyle(localTicket.priority).badgeClass
              ]"
            >
              {{ localTicket.priority }} PRIORITY
            </Badge>

            <!-- Category -->
            <Badge v-if="localTicket.category" variant="outline" class="text-xs bg-muted/30">
              {{ localTicket.category }}
            </Badge>

            <!-- SLA Timer Pill -->
            <div
              class="flex items-center gap-1.5 text-xs font-mono font-medium px-2.5 py-0.5 rounded-lg border"
              :class="slaDisplay.overdue ? 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/30' : 'bg-muted/40 text-muted-foreground border-border'"
            >
              <Clock class="size-3.5" />
              <span>{{ slaDisplay.text }}</span>
            </div>
          </div>

          <h1 class="text-2xl font-bold tracking-tight text-foreground">
            {{ localTicket.title }}
          </h1>

          <div class="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
            <span>Reported by <strong class="text-foreground">{{ localTicket.reportedByUserName || 'Operator' }}</strong></span>
            <span>•</span>
            <span>Created {{ new Date(localTicket.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }) }}</span>
            <span v-if="localTicket.updatedAt && localTicket.updatedAt !== localTicket.createdAt">
              • Updated {{ new Date(localTicket.updatedAt).toLocaleTimeString([], { timeStyle: 'short' }) }}
            </span>
          </div>
        </div>

        <!-- Primary Action Transition Bar -->
        <div class="flex items-center gap-2 flex-wrap">
          <!-- Start Work (Open -> InProgress) -->
          <Button
            v-if="localTicket.status === 'Open'"
            size="sm"
            @click="handleStartWork"
            :disabled="isChangingStatus"
            class="bg-blue-600 hover:bg-blue-700 text-white font-semibold text-xs h-9 px-3.5 rounded-xl gap-1.5 cursor-pointer shadow-xs"
          >
            <Play class="size-3.5" />
            <span>Start Investigation</span>
          </Button>

          <!-- Put on Hold (InProgress -> Pending) -->
          <Button
            v-if="localTicket.status === 'InProgress'"
            size="sm"
            variant="outline"
            @click="showPendingForm = !showPendingForm"
            class="border-amber-500/40 text-amber-800 dark:text-amber-400 hover:bg-amber-500/10 font-semibold text-xs h-9 px-3.5 rounded-xl gap-1.5 cursor-pointer"
          >
            <Clock class="size-3.5" />
            <span>Hold / Pending</span>
          </Button>

          <!-- Resume Work (Pending -> InProgress) -->
          <Button
            v-if="localTicket.status === 'Pending'"
            size="sm"
            @click="handleStartWork"
            :disabled="isChangingStatus"
            class="bg-blue-600 hover:bg-blue-700 text-white font-semibold text-xs h-9 px-3.5 rounded-xl gap-1.5 cursor-pointer shadow-xs"
          >
            <Play class="size-3.5" />
            <span>Resume Work</span>
          </Button>

          <!-- Resolve Incident -->
          <Button
            v-if="localTicket.status === 'InProgress' || localTicket.status === 'Pending'"
            size="sm"
            @click="showResolveForm = !showResolveForm"
            class="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs h-9 px-3.5 rounded-xl gap-1.5 cursor-pointer shadow-xs"
          >
            <CheckCircle2 class="size-3.5" />
            <span>Resolve Incident</span>
          </Button>

          <!-- Close Ticket -->
          <Button
            v-if="localTicket.status === 'Resolved'"
            size="sm"
            @click="handleCloseTicket"
            :disabled="isChangingStatus"
            class="bg-slate-700 hover:bg-slate-800 text-white font-semibold text-xs h-9 px-3.5 rounded-xl gap-1.5 cursor-pointer"
          >
            <span>Close Ticket</span>
          </Button>

          <!-- Reopen -->
          <Button
            v-if="localTicket.status === 'Closed' || localTicket.status === 'Resolved'"
            size="sm"
            variant="outline"
            @click="handleReopenTicket"
            :disabled="isChangingStatus"
            class="border-border text-xs h-9 px-3 rounded-xl gap-1.5 cursor-pointer"
          >
            <RotateCcw class="size-3.5" />
            <span>Re-open</span>
          </Button>

          <!-- Escalate Trigger -->
          <Button
            v-if="!localTicket.isEscalated && localTicket.status !== 'Closed'"
            size="sm"
            variant="outline"
            @click="showEscalateForm = !showEscalateForm"
            class="border-rose-500/30 text-rose-700 dark:text-rose-400 hover:bg-rose-500/10 font-semibold text-xs h-9 px-3 rounded-xl gap-1.5 cursor-pointer"
          >
            <AlertTriangle class="size-3.5" />
            <span>Escalate</span>
          </Button>
        </div>
      </div>

      <!-- Critical Line-Stop Production Stoppage Banner -->
      <div
        v-if="localTicket.isLineStop"
        class="p-4 rounded-2xl border-2 border-rose-500/60 bg-rose-950/40 text-rose-300 flex items-center justify-between gap-4 animate-pulse shadow-lg"
      >
        <div class="flex items-center gap-3">
          <div class="size-10 rounded-xl bg-rose-500/20 border border-rose-500/40 text-rose-400 flex items-center justify-center shrink-0">
            <Flame class="size-6 text-rose-500 animate-bounce" />
          </div>
          <div>
            <div class="font-black text-sm text-rose-400 uppercase tracking-wider flex items-center gap-2">
              <span>Line-Stop Alarm Active — Production Halted</span>
            </div>
            <div class="text-xs text-rose-300/80 font-mono mt-0.5">
              Affected Station: {{ localTicket.stationName || localTicket.stationId }} | Critical factory line stoppage timer active
            </div>
          </div>
        </div>
        <Badge variant="outline" class="border-rose-500/50 text-rose-400 bg-rose-500/20 text-xs font-mono font-bold px-3 py-1">
          LINE-STOP ACTIVE
        </Badge>
      </div>

      <!-- Collapsible Pending Dialog Inline Form -->
      <Card v-if="showPendingForm" class="border-amber-500/30 bg-amber-500/5 animate-in fade-in duration-200">
        <CardHeader class="pb-3 pt-4 px-5">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 text-amber-800 dark:text-amber-400 font-bold text-sm">
              <Clock class="size-4" />
              <span>Place Incident in Pending / Blocked State</span>
            </div>
            <Button variant="ghost" size="sm" @click="showPendingForm = false" class="size-7 p-0">
              <X class="size-4" />
            </Button>
          </div>
        </CardHeader>
        <CardContent class="px-5 pb-4 space-y-3">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
            <div>
              <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">
                Pending Reason
              </label>
              <select
                v-model="pendingReason"
                class="w-full h-8 bg-background border border-border rounded-lg px-2 text-xs text-foreground focus:outline-hidden"
              >
                <option value="Parts">Parts (Waiting on spare components/stock)</option>
                <option value="ExternalOk">External Ok (Waiting on vendor or OEM sign-off)</option>
                <option value="Action">Action (Awaiting process engineering instruction)</option>
                <option value="SignOff">SignOff (Quality / QA inspection validation)</option>
                <option value="Custom">Custom / Shift Handover</option>
              </select>
            </div>
            <div>
              <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">
                Details / Component Tracking Note
              </label>
              <Input
                v-model="pendingDetails"
                placeholder="e.g. Bearing part #SKF-6004 on order from crib"
                class="h-8 text-xs bg-background"
              />
            </div>
          </div>
          <div class="flex justify-end gap-2 pt-2 border-t border-amber-500/20">
            <Button size="sm" variant="ghost" @click="showPendingForm = false" class="text-xs h-7">Cancel</Button>
            <Button
              size="sm"
              @click="handleConfirmPending"
              :disabled="isChangingStatus"
              class="bg-amber-600 hover:bg-amber-500 text-white font-bold text-xs h-7 px-4 cursor-pointer"
            >
              Confirm Pending Hold
            </Button>
          </div>
        </CardContent>
      </Card>

      <!-- Collapsible Escalation Inline Form -->
      <Card v-if="showEscalateForm" class="border-rose-500/30 bg-rose-500/5 animate-in fade-in duration-200">
        <CardHeader class="pb-3 pt-4 px-5">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 text-rose-700 dark:text-rose-400 font-bold text-sm">
              <AlertTriangle class="size-4" />
              <span>Escalate Maintenance Incident</span>
            </div>
            <Button variant="ghost" size="sm" @click="showEscalateForm = false" class="size-7 p-0">
              <X class="size-4" />
            </Button>
          </div>
        </CardHeader>
        <CardContent class="px-5 pb-4 space-y-3">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
            <div>
              <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">
                Escalation Target
              </label>
              <select
                v-model="escalationTarget"
                class="w-full h-8 bg-background border border-border rounded-lg px-2 text-xs text-foreground focus:outline-hidden"
              >
                <option value="DedicatedEngineer">Dedicated Area Automation Engineer</option>
                <option value="Management">Shopfloor Shift Operations Management</option>
              </select>
            </div>
            <div>
              <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">
                Handover Protocol
              </label>
              <select
                v-model="escalationHandover"
                class="w-full h-8 bg-background border border-border rounded-lg px-2 text-xs text-foreground focus:outline-hidden"
              >
                <option value="Notification">Notification (Alert only, maintain on-duty owner)</option>
                <option value="HandOff">Complete HandOff (Transfer responsibility)</option>
                <option value="ParallelWork">Parallel Work (Co-diagnostic session)</option>
              </select>
            </div>
          </div>
          <div>
            <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">
              Escalation Justification & Assistance Required *
            </label>
            <Input
              v-model="escalationReason"
              placeholder="e.g. Requires PLC source code diagnostic / Beckhoff TwinCAT engineering access"
              class="h-8 text-xs bg-background"
            />
          </div>
          <div class="flex justify-end gap-2 pt-2 border-t border-rose-500/20">
            <Button size="sm" variant="ghost" @click="showEscalateForm = false" class="text-xs h-7">Cancel</Button>
            <Button
              size="sm"
              @click="handleConfirmEscalate"
              :disabled="isChangingStatus"
              class="bg-rose-600 hover:bg-rose-500 text-white font-bold text-xs h-7 px-4 cursor-pointer"
            >
              Submit Escalation
            </Button>
          </div>
        </CardContent>
      </Card>

      <!-- Collapsible Resolve Summary Inline Form -->
      <Card v-if="showResolveForm" class="border-emerald-500/30 bg-emerald-500/5 animate-in fade-in duration-200">
        <CardHeader class="pb-3 pt-4 px-5">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 text-emerald-700 dark:text-emerald-400 font-bold text-sm">
              <CheckCircle2 class="size-4" />
              <span>Mark Incident as Resolved</span>
            </div>
            <Button variant="ghost" size="sm" @click="showResolveForm = false" class="size-7 p-0">
              <X class="size-4" />
            </Button>
          </div>
        </CardHeader>
        <CardContent class="px-5 pb-4 space-y-3">
          <div>
            <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">
              Corrective Action Taken & Root Cause Note
            </label>
            <textarea
              v-model="resolutionNote"
              rows="2"
              placeholder="e.g. Cleared workpiece jam on sensor S3, replaced defective pneumatic valve, recalibrated home position..."
              class="w-full bg-background border border-border rounded-xl p-2.5 text-xs text-foreground focus:outline-hidden"
            ></textarea>
          </div>
          <div class="flex justify-end gap-2 pt-2 border-t border-emerald-500/20">
            <Button size="sm" variant="ghost" @click="showResolveForm = false" class="text-xs h-7">Cancel</Button>
            <Button
              size="sm"
              @click="handleConfirmResolve"
              :disabled="isChangingStatus"
              class="bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs h-7 px-4 cursor-pointer"
            >
              Confirm Resolution
            </Button>
          </div>
        </CardContent>
      </Card>

      <!-- Detail Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        
        <!-- Left: Incident Narrative, Timeline & Activity -->
        <div class="lg:col-span-8 space-y-6">

          <!-- Description Card -->
          <Card class="border-border bg-card shadow-xs">
            <CardHeader class="pb-3">
              <CardTitle class="text-sm font-bold">Failure Description & Observations</CardTitle>
            </CardHeader>
            <CardContent class="space-y-4">
              <p class="text-xs text-foreground leading-relaxed whitespace-pre-wrap">
                {{ localTicket.description || 'No detailed failure description provided.' }}
              </p>

              <!-- SFC Serial & Error Code Tagging -->
              <div class="flex items-center gap-3 pt-3 border-t border-border flex-wrap text-xs">
                <div v-if="localTicket.errorCode" class="flex items-center gap-1.5 font-mono">
                  <span class="text-muted-foreground font-semibold">Error Code:</span>
                  <Badge variant="outline" class="font-bold border-indigo-500/30 text-indigo-600 dark:text-indigo-400 bg-indigo-500/10">
                    {{ localTicket.errorCode }}
                  </Badge>
                </div>

                <div v-if="localTicket.errorGroup" class="flex items-center gap-1.5 font-mono">
                  <span class="text-muted-foreground font-semibold">Group:</span>
                  <span class="text-foreground font-medium">{{ localTicket.errorGroup }}</span>
                </div>

                <div v-if="localTicket.sfc" class="flex items-center gap-1.5 font-mono">
                  <span class="text-muted-foreground font-semibold">SFC Serial:</span>
                  <Badge variant="outline" class="border-cyan-500/30 text-cyan-600 dark:text-cyan-400 bg-cyan-500/10">
                    {{ localTicket.sfc }}
                  </Badge>
                </div>

                <div v-if="localTicket.tags && localTicket.tags.length > 0" class="flex items-center gap-1.5 flex-wrap">
                  <span class="text-muted-foreground font-semibold">Tags:</span>
                  <Badge
                    v-for="tg in localTicket.tags"
                    :key="tg"
                    variant="outline"
                    class="text-[11px] font-mono py-0 px-2 bg-muted/40"
                  >
                    {{ tg }}
                  </Badge>
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Attachments Gallery Card -->
          <Card class="border-border bg-card shadow-xs">
            <CardHeader class="pb-3 flex flex-row items-center justify-between">
              <div>
                <CardTitle class="text-sm font-bold flex items-center gap-2">
                  <Paperclip class="size-4 text-muted-foreground" />
                  <span>Defect Photos & Attachments</span>
                  <Badge variant="outline" class="text-xs ml-1 font-mono">
                    {{ localTicket.attachments?.length || 0 }}
                  </Badge>
                </CardTitle>
                <CardDescription class="text-xs">
                  Photographic evidence, error screen captures, and thermal scans.
                </CardDescription>
              </div>
            </CardHeader>
            <CardContent>
              <div v-if="!localTicket.attachments || localTicket.attachments.length === 0" class="text-xs text-muted-foreground italic py-3">
                No attachments logged for this incident.
              </div>
              <div v-else class="grid grid-cols-2 sm:grid-cols-4 gap-3">
                <div
                  v-for="att in localTicket.attachments"
                  :key="att.id"
                  class="group relative border border-border rounded-xl overflow-hidden bg-muted/20 hover:border-primary transition-all cursor-pointer"
                  @click="previewImage = { url: att.url, name: att.fileName }"
                >
                  <img
                    :src="att.url"
                    :alt="att.fileName"
                    class="w-full h-28 object-cover group-hover:scale-105 transition-transform duration-200"
                  />
                  <div class="p-2 bg-card border-t border-border text-[11px] truncate font-medium">
                    {{ att.fileName }}
                  </div>
                  <div class="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center text-white">
                    <ZoomIn class="size-5" />
                  </div>
                </div>
              </div>

              <!-- Inline Lightbox / Expanded Preview (Zero Blocking Backdrop) -->
              <div
                v-if="previewImage"
                class="mt-4 p-4 border border-border rounded-2xl bg-muted/30 relative animate-in fade-in duration-200"
              >
                <div class="flex items-center justify-between pb-2 mb-3 border-b border-border">
                  <span class="text-xs font-bold text-foreground truncate">{{ previewImage.name }}</span>
                  <Button variant="ghost" size="sm" @click="previewImage = null" class="size-6 p-0">
                    <X class="size-3.5" />
                  </Button>
                </div>
                <div class="flex justify-center bg-black/10 dark:bg-black/40 rounded-xl p-2 overflow-hidden max-h-96">
                  <img :src="previewImage.url" :alt="previewImage.name" class="max-h-88 object-contain rounded-lg" />
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Timeline & Comments Feed Card -->
          <Card class="border-border bg-card shadow-xs">
            <CardHeader class="pb-3">
              <CardTitle class="text-sm font-bold flex items-center gap-2">
                <MessageSquare class="size-4 text-indigo-500" />
                <span>Timeline & Collaboration Log</span>
              </CardTitle>
              <CardDescription class="text-xs">
                Chronological sequence of diagnostics, technician findings, and automated events.
              </CardDescription>
            </CardHeader>
            <CardContent class="space-y-4">
              <!-- Chronological Comments Stream -->
              <div class="space-y-3">
                <div
                  v-if="!localTicket.comments || localTicket.comments.length === 0"
                  class="text-xs text-muted-foreground italic py-2"
                >
                  No comments logged yet. Post an update below.
                </div>

                <div
                  v-for="cmt in localTicket.comments"
                  :key="cmt.id"
                  class="p-3.5 rounded-xl border border-border bg-muted/20 space-y-1.5"
                >
                  <div class="flex items-center justify-between text-xs">
                    <div class="flex items-center gap-2">
                      <div class="size-6 rounded-full bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 flex items-center justify-center font-bold text-[10px]">
                        {{ cmt.authorName ? cmt.authorName.charAt(0).toUpperCase() : 'U' }}
                      </div>
                      <span class="font-bold text-foreground">{{ cmt.authorName }}</span>
                      <Badge v-if="cmt.transition" variant="outline" class="text-[9px] font-mono">
                        {{ cmt.transition.fromStatus }} -> {{ cmt.transition.toStatus }}
                      </Badge>
                    </div>
                    <span class="text-muted-foreground text-[11px] font-mono">
                      {{ new Date(cmt.createdAt).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' }) }}
                    </span>
                  </div>
                  <p class="text-xs text-foreground whitespace-pre-wrap pl-8">
                    {{ cmt.content }}
                  </p>
                </div>
              </div>

              <!-- New Comment Box -->
              <div class="pt-3 border-t border-border space-y-3">
                <label class="block text-[11px] font-semibold text-muted-foreground">
                  Post Diagnostic Finding / Progress Note
                </label>
                <textarea
                  v-model="commentText"
                  rows="3"
                  placeholder="Record symptoms checked, voltage readings, mechanical adjustments, or parts ordered..."
                  class="w-full bg-background border border-border rounded-xl p-3 text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
                ></textarea>

                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2 text-xs text-muted-foreground">
                    <User class="size-3.5" />
                    <span>Posting as <strong class="text-foreground">{{ currentUser }}</strong></span>
                  </div>

                  <Button
                    size="sm"
                    @click="handleAddComment"
                    :disabled="isSubmittingComment || (!commentText.trim() && commentAttachments.length === 0)"
                    class="bg-primary hover:bg-primary/90 text-primary-foreground font-semibold text-xs h-8 px-4 rounded-xl gap-1.5 cursor-pointer shadow-xs"
                  >
                    <Send class="size-3.5" />
                    <span>Post Note</span>
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- Right: Equipment, Ownership & Live Telemetry -->
        <div class="lg:col-span-4 space-y-6">

          <!-- Equipment & Physical Cell Details -->
          <Card class="border-border bg-card shadow-xs">
            <CardHeader class="pb-3">
              <CardTitle class="text-sm font-bold flex items-center gap-2">
                <Factory class="size-4 text-indigo-500" />
                <span>Equipment & Cell Topology</span>
              </CardTitle>
            </CardHeader>
            <CardContent class="space-y-3 text-xs">
              <div class="flex justify-between items-center py-1 border-b border-border">
                <span class="text-muted-foreground">Station ID:</span>
                <span class="font-mono font-bold text-foreground">{{ localTicket.stationId || '—' }}</span>
              </div>
              <div class="flex justify-between items-center py-1 border-b border-border">
                <span class="text-muted-foreground">Station Name:</span>
                <span class="font-medium text-foreground text-right truncate max-w-44">{{ localTicket.stationName || '—' }}</span>
              </div>
              <div class="flex justify-between items-center py-1 border-b border-border">
                <span class="text-muted-foreground">Controller Node:</span>
                <span class="font-mono text-indigo-600 dark:text-indigo-400 font-semibold">{{ localTicket.controllerId || 'Local IPC' }}</span>
              </div>
              <div class="flex justify-between items-center py-1 border-b border-border">
                <span class="text-muted-foreground">Machine Type:</span>
                <span class="text-foreground">{{ localTicket.machineType || 'Industrial Equipment' }}</span>
              </div>
              <div class="flex justify-between items-center py-1">
                <span class="text-muted-foreground">Line / Cluster:</span>
                <NuxtLink v-if="localTicket.groupId" to="/dashboard/machine-groups" class="text-indigo-600 dark:text-indigo-400 font-mono hover:underline flex items-center gap-1">
                  <span>{{ localTicket.groupId }}</span>
                  <ExternalLink class="size-2.5" />
                </NuxtLink>
                <span v-else class="text-muted-foreground">—</span>
              </div>
            </CardContent>
          </Card>

          <!-- Assigned Specialist & Ownership -->
          <Card class="border-border bg-card shadow-xs">
            <CardHeader class="pb-3">
              <CardTitle class="text-sm font-bold flex items-center gap-2">
                <UserCheck class="size-4 text-indigo-500" />
                <span>Assigned Specialist</span>
              </CardTitle>
            </CardHeader>
            <CardContent class="space-y-3.5 text-xs">
              <div class="flex items-center gap-3 p-3 rounded-xl bg-muted/30 border border-border">
                <div class="size-10 rounded-xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20 flex items-center justify-center font-bold text-sm shrink-0">
                  {{ localTicket.assignedTechnicianName ? localTicket.assignedTechnicianName.charAt(0).toUpperCase() : '?' }}
                </div>
                <div class="min-w-0 flex-1">
                  <div class="font-bold text-foreground truncate">
                    {{ localTicket.assignedTechnicianName || 'Unassigned Queue' }}
                  </div>
                  <div class="text-[11px] text-muted-foreground">
                    {{ localTicket.responsibleDepartment || 'Operations Floor' }}
                  </div>
                </div>
              </div>

              <!-- Quick Assignment Actions -->
              <div class="space-y-2">
                <Button
                  v-if="!localTicket.assignedTechnicianName || localTicket.assignedTechnicianName !== currentUser"
                  size="sm"
                  variant="outline"
                  @click="handleSelfAssign"
                  class="w-full text-xs font-semibold h-8 rounded-xl gap-1.5 cursor-pointer"
                >
                  <UserCheck class="size-3.5 text-indigo-500" />
                  <span>Claim / Self-Assign to {{ currentUser }}</span>
                </Button>

                <Button
                  size="sm"
                  variant="outline"
                  @click="handleQrPickup"
                  class="w-full text-xs font-semibold h-8 rounded-xl gap-1.5 border-indigo-500/30 text-indigo-600 dark:text-indigo-400 hover:bg-indigo-500/10 cursor-pointer"
                >
                  <QrCode class="size-3.5" />
                  <span>Log Scene Arrival (QR Pickup)</span>
                </Button>
              </div>
            </CardContent>
          </Card>

          <!-- Telemetry & Diagnostic Snapshot -->
          <Card v-if="localTicket.telemetrySnapshot" class="border-border bg-card shadow-xs">
            <CardHeader class="pb-3">
              <CardTitle class="text-sm font-bold flex items-center gap-2">
                <Activity class="size-4 text-cyan-500" />
                <span>Telemetry at Time of Incident</span>
              </CardTitle>
            </CardHeader>
            <CardContent class="space-y-2 font-mono text-xs">
              <div
                v-for="(val, key) in (localTicket.telemetrySnapshot.metrics || localTicket.telemetrySnapshot)"
                :key="key"
                class="flex justify-between items-center py-1 border-b border-border"
              >
                <span class="text-muted-foreground text-[11px]">{{ key }}:</span>
                <span class="text-foreground font-semibold">{{ val }}</span>
              </div>
            </CardContent>
          </Card>

        </div>
      </div>

    </div>
  </div>
</template>
