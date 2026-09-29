<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import {
  X,
  Clock,
  Cpu,
  User,
  Send,
  Paperclip,
  CheckCircle2,
  AlertCircle,
  Play,
  Package,
  Layers,
  Archive,
  ArrowRightLeft,
  Tag,
  Image as ImageIcon,
  ZoomIn,
  AlertTriangle,
  ShieldAlert,
  RotateCcw,
  QrCode,
  Flame,
  Activity,
  History,
  Edit3,
  BookmarkCheck,
  ChevronDown,
  ChevronUp
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import MachineSearchCombobox from '~/components/tickets/MachineSearchCombobox.vue'
import ImageAttachmentUploader from '~/components/tickets/ImageAttachmentUploader.vue'
import { authClient } from '~/utils/auth-client'
import { generateQrDataUrl } from '~/utils/qrSvgRenderer'
import type {
  MaintenanceTicket,
  TicketAttachment,
  TicketStatus,
  PendingReason,
  EscalationHandoverState,
  ResponsibleDepartment
} from '~/types/maintenance'
import {
  ANDON_STYLES,
  getAndonColorForStatus,
  getAndonPriorityStyle,
  PENDING_REASON_CONFIGS
} from '~/utils/andonColors'

const props = defineProps<{
  ticket: MaintenanceTicket | null
  open: boolean
  absences?: Array<{
    technicianName: string
    backupTechnicianName?: string
    active: boolean
  }>
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'updated', ticket: MaintenanceTicket): void
}>()

// ─── Local State ─────────────────────────────────────────────────────────────
const commentText = ref('')
const isSubmittingComment = ref(false)
const localTicket = ref<MaintenanceTicket | null>(null)
const isEditingEquipment = ref(false)

const STATUSES: { value: TicketStatus; label: string; color: string; dotClass: string }[] = [
  { value: 'Open',       label: 'Open',        color: 'bg-cyan-500/15 text-cyan-800 dark:text-cyan-300 border-cyan-500/30',     dotClass: 'bg-cyan-400' },
  { value: 'InProgress', label: 'In Progress', color: 'bg-blue-500/15 text-blue-800 dark:text-blue-300 border-blue-500/30',     dotClass: 'bg-blue-600' },
  { value: 'Pending',    label: 'Pending',     color: 'bg-amber-500/15 text-amber-800 dark:text-amber-300 border-amber-500/30', dotClass: 'bg-amber-500' },
  { value: 'Resolved',   label: 'Resolved',    color: 'bg-emerald-500/15 text-emerald-800 dark:text-emerald-300 border-emerald-500/30', dotClass: 'bg-emerald-500' },
  { value: 'Closed',     label: 'Closed',      color: 'bg-muted text-muted-foreground border-border',                           dotClass: 'bg-slate-400' },
]

const selectedStatus = ref<TicketStatus>('Open')
const selectedPendingReason = ref<PendingReason>('Parts')
const selectedPendingDetails = ref<string>('')
const isChangingStatus = ref(false)

// Escalation UI state
const isEscalatingOpen = ref(false)
const escalationReasonInput = ref('')
const selectedEscalationTarget = ref<'DedicatedEngineer' | 'Management'>('DedicatedEngineer')
const selectedHandoverState = ref<EscalationHandoverState>('Notification')
const isSubmittingEscalation = ref(false)

// QR Pickup state
const isClaimingQr = ref(false)
const showQrModal = ref(false)

// Telemetry Snapshot state
const showTelemetrySnapshot = ref(true)

// Change History & In-Place Editing
const isEditingDetails = ref(false)
const editTitle = ref('')
const editDescription = ref('')
const editDepartment = ref<string>('')
const editPriority = ref<string>('Medium')
const editIsLineStop = ref(false)
const editLineStopDuration = ref(0)
const editReasonNote = ref('')
const isSavingEdit = ref(false)
const showChangeHistory = ref(true)

// Tags
const newTagInput = ref('')
const isUpdatingTags = ref(false)

// Attachments
const ticketAttachmentsDraft = ref<TicketAttachment[]>([])
const isUploadingTicketAttachments = ref(false)
const commentAttachments = ref<TicketAttachment[]>([])
const showCommentAttachmentPanel = ref(false)

// Lightbox
const lightboxOpen = ref(false)
const lightboxSrc = ref('')
const lightboxName = ref('')

// ─── Watchers ────────────────────────────────────────────────────────────────
watch(() => props.ticket, (newVal) => {
  localTicket.value = newVal ? JSON.parse(JSON.stringify(newVal)) : null
  isEditingEquipment.value = false
  isEscalatingOpen.value = false
  isEditingDetails.value = false
  escalationReasonInput.value = ''
  selectedEscalationTarget.value = 'DedicatedEngineer'
  selectedHandoverState.value = 'Notification'
  if (newVal) {
    selectedStatus.value = newVal.status || 'Open'
    selectedPendingReason.value = newVal.pendingReason || 'Parts'
    selectedPendingDetails.value = newVal.pendingDetails || ''
  }
  ticketAttachmentsDraft.value = []
  commentAttachments.value = []
  showCommentAttachmentPanel.value = false
}, { immediate: true, deep: true })

// ─── Computed ────────────────────────────────────────────────────────────────
const currentUser = computed(() => {
  const session = (authClient as any).useSession?.()
  return session?.data?.value?.user?.name || 'On-Duty Tech'
})

const ticketLevelAttachments = computed<TicketAttachment[]>(() => {
  return (localTicket.value?.attachments ?? []).filter(a => !a.commentId)
})

const ticketQrDataUrl = computed(() => {
  if (!localTicket.value) return ''
  const payload = `heimdall://ticket/${localTicket.value.id}/pickup`
  return generateQrDataUrl(payload, { width: 140, margin: 1, darkColor: '#4f46e5' })
})

const isStatusOrPendingChanged = computed(() => {
  if (!localTicket.value) return false
  if (selectedStatus.value !== localTicket.value.status) return true
  if (selectedStatus.value === 'Pending') {
    if (selectedPendingReason.value !== (localTicket.value.pendingReason || 'None')) return true
    if (selectedPendingDetails.value !== (localTicket.value.pendingDetails || '')) return true
  }
  return false
})

// ─── Helpers ─────────────────────────────────────────────────────────────────
function statusColor(status: string) {
  return STATUSES.find(s => s.value === status)?.color ?? 'bg-muted text-muted-foreground border-border'
}

function openLightbox(att: TicketAttachment) {
  lightboxSrc.value = att.url ?? ''
  lightboxName.value = att.fileName
  lightboxOpen.value = true
}

// ─── Equipment ───────────────────────────────────────────────────────────────
async function updateEquipment(machine: any) {
  if (!localTicket.value) return
  const stationId = machine.customIdentifier || machine.name || machine.id
  const stationName = machine.displayName || machine.name || stationId
  const controllerId = machine.controllers?.[0]?.hostname || localTicket.value.controllerId

  try {
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: { stationId, stationName, controllerId }
    })
    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
      isEditingEquipment.value = false
    }
  } catch (err) {
    console.error('Error updating equipment:', err)
  }
}

// ─── Status & Pending Change ─────────────────────────────────────────────────
async function applyStatusChange() {
  if (!localTicket.value) return
  const oldStatus = localTicket.value.status
  const newStatus = selectedStatus.value

  isChangingStatus.value = true
  try {
    const techName = currentUser.value
    const payload: Record<string, any> = { status: newStatus }

    if (newStatus === 'InProgress' && (!localTicket.value.assignedTechnicianName || localTicket.value.assignedTechnicianName === 'Unassigned')) {
      payload.assignedTechnicianName = techName
    }
    if (newStatus === 'Pending') {
      payload.pendingReason = selectedPendingReason.value
      payload.pendingDetails = selectedPendingDetails.value
    } else {
      payload.pendingReason = 'None'
      payload.pendingDetails = null
    }

    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: payload
    })

    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)

      if (oldStatus !== newStatus) {
        await $fetch(`/api/tickets/${localTicket.value.id}/comments`, {
          method: 'POST',
          body: {
            authorName: techName,
            content: '',
            transition: {
              fromStatus: oldStatus,
              toStatus: newStatus,
              actor: techName
            }
          }
        })
      }
      const fresh = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`)
      if (fresh?.ticket) {
        localTicket.value = fresh.ticket
        emit('updated', fresh.ticket)
      }
    }
  } catch (err) {
    console.error('Error updating ticket status:', err)
  } finally {
    isChangingStatus.value = false
  }
}

async function updateStatus(newStatus: TicketStatus) {
  selectedStatus.value = newStatus
  await applyStatusChange()
}

// ─── QR Code Pickup ──────────────────────────────────────────────────────────
async function handleQrPickup() {
  if (!localTicket.value) return
  isClaimingQr.value = true
  try {
    const techName = currentUser.value
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}/qr-pickup`, {
      method: 'POST',
      body: { technician: techName }
    })
    if (res?.success && res.ticket) {
      localTicket.value = res.ticket
      selectedStatus.value = res.ticket.status
      emit('updated', res.ticket)
    }
  } catch (err) {
    console.error('Error during QR pickup:', err)
  } finally {
    isClaimingQr.value = false
  }
}

// ─── In-Place Edits & Change History (Going back in time) ───────────────────
function openEditDetails() {
  if (!localTicket.value) return
  editTitle.value = localTicket.value.title || ''
  editDescription.value = localTicket.value.description || ''
  editDepartment.value = localTicket.value.responsibleDepartment || ''
  editPriority.value = localTicket.value.priority || 'Medium'
  editIsLineStop.value = Boolean(localTicket.value.isLineStop)
  editLineStopDuration.value = localTicket.value.lineStopDurationMinutes || 0
  editReasonNote.value = ''
  isEditingDetails.value = true
}

async function saveDetailsEdit() {
  if (!localTicket.value) return
  isSavingEdit.value = true
  try {
    const techName = currentUser.value
    const payload = {
      title: editTitle.value.trim(),
      description: editDescription.value.trim(),
      responsibleDepartment: editDepartment.value || undefined,
      priority: editPriority.value as any,
      isLineStop: editIsLineStop.value,
      lineStopDurationMinutes: editIsLineStop.value ? Number(editLineStopDuration.value) : 0,
      editorName: techName,
      changeReason: editReasonNote.value.trim() || 'Updated incident context'
    }

    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: payload
    })

    if (res?.success && res.ticket) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
      isEditingDetails.value = false
    }
  } catch (err) {
    console.error('Error saving edits:', err)
  } finally {
    isSavingEdit.value = false
  }
}

// ─── Orthogonal Escalation ──────────────────────────────────────────────────
async function confirmEscalate() {
  if (!localTicket.value || !escalationReasonInput.value.trim()) return
  isSubmittingEscalation.value = true
  try {
    const techName = currentUser.value
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: {
        isEscalated: true,
        escalationReason: escalationReasonInput.value.trim(),
        escalationTarget: selectedEscalationTarget.value,
        escalationHandoverState: selectedHandoverState.value,
        escalatedBy: techName,
        escalatedAt: new Date().toISOString()
      }
    })
    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
      isEscalatingOpen.value = false
      escalationReasonInput.value = ''
    }
  } catch (err) {
    console.error('Error escalating ticket:', err)
  } finally {
    isSubmittingEscalation.value = false
  }
}

async function resolveEscalation() {
  if (!localTicket.value) return
  isSubmittingEscalation.value = true
  try {
    const techName = currentUser.value
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: {
        isEscalated: false,
        escalationReason: null,
        escalationTarget: null,
        escalationHandoverState: null,
        escalationClosedBy: techName,
        escalationClosedAt: new Date().toISOString()
      }
    })
    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
    }
  } catch (err) {
    console.error('Error resolving escalation:', err)
  } finally {
    isSubmittingEscalation.value = false
  }
}

// ─── Tags ─────────────────────────────────────────────────────────────────────
async function addTag() {
  const raw = newTagInput.value.trim()
  if (!raw || !localTicket.value) return
  const tag = raw.startsWith('#') ? raw : `#${raw}`
  const existing = localTicket.value.tags ?? []
  if (existing.includes(tag)) {
    newTagInput.value = ''
    return
  }
  const newTags = [...existing, tag]
  isUpdatingTags.value = true
  try {
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: { tags: newTags }
    })
    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
    }
  } catch (err) {
    console.error('Error adding tag:', err)
  } finally {
    isUpdatingTags.value = false
    newTagInput.value = ''
  }
}

async function removeTag(tagToRemove: string) {
  if (!localTicket.value) return
  const newTags = (localTicket.value.tags ?? []).filter(t => t !== tagToRemove)
  isUpdatingTags.value = true
  try {
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: { tags: newTags }
    })
    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
    }
  } catch (err) {
    console.error('Error removing tag:', err)
  } finally {
    isUpdatingTags.value = false
  }
}

// ─── Attachments ─────────────────────────────────────────────────────────────
async function uploadTicketAttachments() {
  if (!localTicket.value || ticketAttachmentsDraft.value.length === 0) return
  isUploadingTicketAttachments.value = true
  try {
    for (const att of ticketAttachmentsDraft.value) {
      await $fetch(`/api/tickets/${localTicket.value.id}/attachments`, {
        method: 'POST',
        body: {
          fileName: att.fileName,
          contentType: att.contentType,
          fileSize: att.fileSize,
          url: att.url
        }
      })
    }
    const fresh = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`)
    if (fresh?.ticket) {
      localTicket.value = fresh.ticket
      emit('updated', fresh.ticket)
    }
    ticketAttachmentsDraft.value = []
  } catch (err) {
    console.error('Error uploading attachments:', err)
  } finally {
    isUploadingTicketAttachments.value = false
  }
}

// ─── Comments ────────────────────────────────────────────────────────────────
async function addComment() {
  if (!localTicket.value) return
  const text = commentText.value.trim()
  if (!text && commentAttachments.value.length === 0) return

  isSubmittingComment.value = true
  try {
    const commentRes = await $fetch<{ success: boolean; comment: any }>(`/api/tickets/${localTicket.value.id}/comments`, {
      method: 'POST',
      body: {
        authorName: currentUser.value,
        content: text
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
        })
      }
    }

    const fresh = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`)
    if (fresh?.ticket) {
      localTicket.value = fresh.ticket
      emit('updated', fresh.ticket)
    }

    commentText.value = ''
    commentAttachments.value = []
    showCommentAttachmentPanel.value = false
  } catch (err) {
    console.error('Error submitting comment:', err)
  } finally {
    isSubmittingComment.value = false
  }
}
</script>

<template>
  <!-- Backdrop -->
  <div
    v-if="open"
    class="fixed inset-0 bg-background/80 backdrop-blur-xs z-40 transition-opacity"
    @click="emit('update:open', false)"
  />

  <!-- Drawer panel -->
  <div
    v-if="open && localTicket"
    class="fixed inset-y-0 right-0 z-50 w-full max-w-2xl bg-card border-l border-border shadow-2xl flex flex-col overflow-hidden text-foreground animate-in slide-in-from-right duration-200"
  >
    <!-- Header -->
    <div class="p-6 border-b border-border bg-muted/20 flex items-center justify-between">
      <div class="space-y-1 min-w-0 pr-4">
        <div class="flex items-center gap-2 flex-wrap">
          <span class="font-mono text-xs font-black text-indigo-600 dark:text-indigo-400 bg-indigo-500/10 px-2 py-0.5 rounded-md border border-indigo-500/20">
            {{ localTicket.ticketNumber }}
          </span>
          <span class="text-xs text-muted-foreground font-mono">
            {{ new Date(localTicket.createdAt).toLocaleDateString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) }}
          </span>

          <!-- Line Stop Header Badge -->
          <Badge
            v-if="localTicket.isLineStop"
            variant="outline"
            class="text-[9px] bg-rose-600 text-white font-black uppercase px-2 py-0.5 flex items-center gap-1 animate-pulse border-transparent"
          >
            <Flame class="size-3" />
            <span>Line Stop ({{ localTicket.lineStopDurationMinutes || 0 }}m)</span>
          </Badge>

          <!-- Escalation Badge -->
          <Badge
            v-if="localTicket.isEscalated"
            variant="outline"
            class="text-[9px] bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/40 uppercase font-black tracking-wider flex items-center gap-1 animate-pulse"
          >
            <AlertTriangle class="size-2.5" />
            Escalated {{ localTicket.escalationHandoverState ? `[${localTicket.escalationHandoverState}]` : '' }}
          </Badge>
        </div>
        <h3 class="text-lg font-bold text-foreground truncate">{{ localTicket.title }}</h3>
      </div>
      <Button
        variant="ghost"
        size="icon"
        class="h-8 w-8 text-muted-foreground hover:text-foreground rounded-lg cursor-pointer shrink-0"
        @click="emit('update:open', false)"
      >
        <X class="h-4 w-4" />
      </Button>
    </div>

    <!-- Scrollable Content -->
    <div class="flex-1 overflow-y-auto p-6 space-y-6">

      <!-- Active Escalation Banner -->
      <div
        v-if="localTicket.isEscalated"
        class="p-4 bg-rose-500/10 border-2 border-rose-500/40 rounded-2xl space-y-2 animate-in fade-in"
      >
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2 text-rose-700 dark:text-rose-300 font-black text-xs uppercase tracking-wider">
            <AlertTriangle class="size-4 shrink-0 text-rose-500" />
            <span>Active Incident Escalation</span>
            <Badge v-if="localTicket.escalationHandoverState" class="bg-rose-600 text-white text-[9px] px-1.5 py-0">
              State: {{ localTicket.escalationHandoverState }}
            </Badge>
          </div>
          <Button
            size="sm"
            @click="resolveEscalation"
            :disabled="isSubmittingEscalation"
            class="bg-rose-600 hover:bg-rose-700 text-white rounded-xl text-[10px] font-black uppercase h-7 px-3 cursor-pointer"
          >
            Resolve Escalation
          </Button>
        </div>
        <div class="text-xs text-rose-800 dark:text-rose-200">
          <strong>Reason:</strong> {{ localTicket.escalationReason || 'Operational priority review' }}
        </div>
        <div class="flex items-center justify-between text-[10px] text-rose-600 dark:text-rose-400 font-mono">
          <span>Target: {{ localTicket.escalationTarget || 'DedicatedEngineer' }}</span>
          <span>Escalated by {{ localTicket.escalatedBy || 'Technician' }}</span>
        </div>
      </div>

      <!-- Scannable QR Code & Pickup Box -->
      <div class="p-4 bg-indigo-500/5 rounded-2xl border border-indigo-500/20 flex flex-col sm:flex-row items-center gap-4">
        <div class="p-2 bg-white rounded-xl shadow-xs shrink-0 border border-border">
          <img :src="ticketQrDataUrl" alt="Ticket QR Code" class="size-24 rounded-lg" />
        </div>
        <div class="flex-1 space-y-1.5 text-center sm:text-left">
          <div class="flex items-center justify-center sm:justify-start gap-2">
            <QrCode class="size-4 text-indigo-600 dark:text-indigo-400" />
            <h4 class="text-xs font-black uppercase tracking-wider text-foreground">
              Direct Floor QR Pickup
            </h4>
          </div>
          <p class="text-xs text-muted-foreground">
            Scan at physical controller to claim ticket and track exact reaction time.
          </p>
          <div v-if="localTicket.reactionTimeMinutes !== undefined" class="text-xs font-mono text-emerald-600 dark:text-emerald-400 font-bold">
            Reaction Time: {{ localTicket.reactionTimeMinutes }} min{{ localTicket.reactionTimeMinutes === 1 ? '' : 's' }}
          </div>
          <div class="pt-1">
            <Button
              v-if="localTicket.status === 'Open'"
              size="sm"
              @click="handleQrPickup"
              :disabled="isClaimingQr"
              class="bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-black uppercase tracking-wide h-8 px-3 rounded-xl cursor-pointer"
            >
              <BookmarkCheck class="size-3.5 mr-1" />
              <span>{{ isClaimingQr ? 'Claiming…' : 'Scan & Pickup Ticket' }}</span>
            </Button>
          </div>
        </div>
      </div>

      <!-- Equipment Box -->
      <div class="p-4 bg-muted/40 rounded-2xl border border-border space-y-3">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Cpu class="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
            <span class="text-xs font-black uppercase tracking-wider text-muted-foreground">Target Machine &amp; Controller</span>
          </div>
          <button
            type="button"
            @click="isEditingEquipment = !isEditingEquipment"
            class="text-[10px] text-indigo-600 dark:text-indigo-400 font-bold hover:underline cursor-pointer"
          >
            {{ isEditingEquipment ? 'Cancel' : 'Reassign Equipment' }}
          </button>
        </div>

        <div v-if="!isEditingEquipment" class="grid grid-cols-2 sm:grid-cols-3 gap-2 text-xs">
          <div>
            <span class="text-muted-foreground text-[10px] block font-medium">Station</span>
            <span class="font-bold text-foreground">{{ localTicket.stationName || 'Unassigned Station' }}</span>
          </div>
          <div>
            <span class="text-muted-foreground text-[10px] block font-medium">IPC / Controller</span>
            <span class="font-mono text-muted-foreground">{{ localTicket.controllerId || 'Direct' }}</span>
          </div>
          <div>
            <span class="text-muted-foreground text-[10px] block font-medium">Department</span>
            <span class="font-semibold text-indigo-600 dark:text-indigo-400">{{ localTicket.responsibleDepartment || 'Unassigned' }}</span>
          </div>
        </div>
        <div v-else class="pt-2">
          <MachineSearchCombobox @select="updateEquipment" />
        </div>
      </div>

      <!-- Status & Pending Management -->
      <div class="p-4 bg-muted/40 rounded-2xl border border-border space-y-4">
        <div class="flex items-center justify-between flex-wrap gap-2">
          <div class="flex items-center gap-2">
            <Clock class="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
            <span class="text-xs font-black uppercase tracking-wider text-muted-foreground">Status &amp; Workflow</span>
          </div>
          <Badge variant="outline" :class="getAndonPriorityStyle(localTicket.priority).badgeClass" class="text-xs font-black uppercase tracking-widest px-3 py-1 flex items-center gap-1.5">
            <span class="size-1.5 rounded-full" :class="getAndonPriorityStyle(localTicket.priority).dotClass" />
            <span>Priority: {{ getAndonPriorityStyle(localTicket.priority).label }}</span>
          </Badge>
        </div>

        <!-- 5 Status Options -->
        <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5 pt-1">
          <button
            v-for="s in STATUSES"
            :key="s.value"
            type="button"
            @click="selectedStatus = s.value"
            :class="[
              'px-2.5 py-2 rounded-lg border text-xs font-bold uppercase tracking-wide text-left transition-all flex items-center gap-2 cursor-pointer',
              selectedStatus === s.value ? ['ring-2 ring-primary scale-[1.02] shadow-sm', s.color] : ['opacity-60 hover:opacity-90', s.color]
            ]"
          >
            <span class="size-2 rounded-full shrink-0" :class="s.dotClass" />
            <span class="truncate">{{ s.label }}</span>
          </button>
        </div>

        <!-- Pending Reason Config when Pending is Selected -->
        <div v-if="selectedStatus === 'Pending'" class="p-3 bg-amber-500/10 border border-amber-500/30 rounded-xl space-y-2.5 animate-in fade-in">
          <span class="text-[10px] font-black uppercase tracking-widest text-amber-800 dark:text-amber-300 flex items-center gap-1">
            <Package class="size-3 text-amber-600" />
            Select Pending Reason:
          </span>

          <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
            <button
              v-for="(cfg, rKey) in PENDING_REASON_CONFIGS"
              :key="rKey"
              type="button"
              @click="selectedPendingReason = rKey"
              :class="[
                'p-2 rounded-lg border text-left transition-all flex flex-col gap-0.5 cursor-pointer',
                selectedPendingReason === rKey
                  ? 'bg-amber-500/20 border-amber-500 text-amber-950 dark:text-amber-100 ring-2 ring-amber-500/50 shadow-xs'
                  : 'bg-background/80 border-border text-foreground hover:bg-muted/80 opacity-80'
              ]"
            >
              <span class="font-bold text-xs">{{ cfg.label }}</span>
              <p class="text-[10px] text-muted-foreground line-clamp-1 leading-tight">{{ cfg.description }}</p>
            </button>
          </div>

          <div class="space-y-1 pt-1">
            <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
              Pending Details / Custom Reason (optional)
            </label>
            <input
              v-model="selectedPendingDetails"
              type="text"
              placeholder="e.g., Awaiting shipment ETA or QA sign-off stamp"
              class="w-full px-2.5 py-1.5 bg-background border border-border rounded-lg text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden"
            />
          </div>
        </div>

        <!-- Apply Status Change Button -->
        <Button
          v-if="isStatusOrPendingChanged"
          @click="applyStatusChange"
          :disabled="isChangingStatus"
          class="w-full bg-primary hover:bg-primary/90 text-primary-foreground rounded-xl text-xs font-black uppercase tracking-wider h-8 shadow-sm cursor-pointer"
        >
          <span v-if="!isChangingStatus">Apply Status Change</span>
          <span v-else>Updating…</span>
        </Button>

        <!-- Quick Workflow Actions -->
        <div class="flex flex-wrap gap-2 pt-2 border-t border-border">
          <Button
            v-if="localTicket.status === 'Open'"
            size="sm"
            @click="updateStatus('InProgress')"
            class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer"
          >
            <Play class="h-3.5 w-3.5 mr-1" />
            Start Work
          </Button>

          <Button
            v-if="localTicket.status === 'InProgress'"
            size="sm"
            @click="updateStatus('Pending')"
            class="bg-amber-600 hover:bg-amber-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer"
          >
            <Package class="h-3.5 w-3.5 mr-1" />
            Set Pending
          </Button>

          <Button
            v-if="localTicket.status === 'Pending'"
            size="sm"
            @click="updateStatus('InProgress')"
            class="bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer"
          >
            <Play class="h-3.5 w-3.5 mr-1" />
            Resume Work
          </Button>

          <Button
            v-if="localTicket.status === 'InProgress' || localTicket.status === 'Pending'"
            size="sm"
            @click="updateStatus('Resolved')"
            class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer"
          >
            <CheckCircle2 class="h-3.5 w-3.5 mr-1" />
            Mark Resolved
          </Button>

          <Button
            v-if="localTicket.status === 'Resolved'"
            size="sm"
            @click="updateStatus('Closed')"
            class="bg-muted hover:bg-muted/80 text-foreground rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer"
          >
            <Archive class="h-3.5 w-3.5 mr-1" />
            Close Ticket
          </Button>

          <Button
            v-if="localTicket.status === 'Closed'"
            size="sm"
            @click="updateStatus('Open')"
            class="bg-cyan-600 hover:bg-cyan-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer"
          >
            <RotateCcw class="h-3.5 w-3.5 mr-1" />
            Re-Open
          </Button>

          <!-- Escalate Action Trigger -->
          <Button
            v-if="!localTicket.isEscalated"
            size="sm"
            variant="outline"
            @click="isEscalatingOpen = !isEscalatingOpen"
            class="border-rose-500/40 text-rose-700 dark:text-rose-300 hover:bg-rose-500/10 rounded-xl text-[10px] font-black uppercase tracking-wider h-8 cursor-pointer ml-auto"
          >
            <AlertTriangle class="h-3.5 w-3.5 mr-1 text-rose-500" />
            Escalate
          </Button>
        </div>

        <!-- Inline Escalation Input Form with Handover Sub-State -->
        <div v-if="isEscalatingOpen && !localTicket.isEscalated" class="p-3.5 bg-rose-500/10 border border-rose-500/30 rounded-xl space-y-3 animate-in fade-in">
          <div class="text-xs font-bold text-rose-700 dark:text-rose-300 flex items-center justify-between">
            <span>Configure Incident Escalation &amp; Handover</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
            <div>
              <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground block mb-1">
                Escalation Target
              </label>
              <select
                v-model="selectedEscalationTarget"
                class="w-full px-2 py-1.5 bg-background border border-border rounded-lg text-xs text-foreground focus:outline-hidden"
              >
                <option value="DedicatedEngineer">Dedicated Shift Engineer</option>
                <option value="Management">Plant / Area Management</option>
              </select>
            </div>
            <div>
              <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground block mb-1">
                Handover State
              </label>
              <select
                v-model="selectedHandoverState"
                class="w-full px-2 py-1.5 bg-background border border-border rounded-lg text-xs text-foreground focus:outline-hidden"
              >
                <option value="Notification">Notification (Informational)</option>
                <option value="HandOff">HandOff (Transfer Work)</option>
                <option value="ParallelWork">ParallelWork (Joint Work)</option>
              </select>
            </div>
          </div>

          <div class="space-y-1">
            <label class="text-[10px] font-bold uppercase tracking-wider text-rose-700 dark:text-rose-300">
              Escalation Reason *
            </label>
            <input
              v-model="escalationReasonInput"
              type="text"
              placeholder="e.g. Robot collision, critical line stoppage exceeded threshold"
              class="w-full px-2.5 py-1.5 bg-background border border-border rounded-lg text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden"
            />
          </div>

          <div class="flex justify-end gap-2 pt-1">
            <Button
              size="sm"
              variant="outline"
              class="text-xs h-7"
              @click="isEscalatingOpen = false"
            >
              Cancel
            </Button>
            <Button
              size="sm"
              :disabled="!escalationReasonInput.trim() || isSubmittingEscalation"
              class="bg-rose-600 hover:bg-rose-500 text-white text-xs h-7 font-bold cursor-pointer"
              @click="confirmEscalate"
            >
              Confirm Escalation
            </Button>
          </div>
        </div>
      </div>

      <!-- Incident Description & In-Place Edit (Going back in time) -->
      <div class="space-y-2">
        <div class="flex items-center justify-between">
          <h4 class="text-xs font-black uppercase tracking-widest text-muted-foreground">Incident Description</h4>
          <Button
            size="sm"
            variant="ghost"
            class="h-6 px-2 text-[10px] font-bold text-indigo-600 dark:text-indigo-400 hover:bg-indigo-500/10 cursor-pointer"
            @click="isEditingDetails ? (isEditingDetails = false) : openEditDetails()"
          >
            <Edit3 class="size-3 mr-1" />
            <span>{{ isEditingDetails ? 'Cancel Edit' : 'Edit / Fix Typos' }}</span>
          </Button>
        </div>

        <!-- Normal View -->
        <div v-if="!isEditingDetails" class="p-4 bg-muted/30 rounded-2xl border border-border space-y-2">
          <h5 class="text-sm font-bold text-foreground">{{ localTicket.title }}</h5>
          <p class="text-xs text-muted-foreground leading-relaxed whitespace-pre-wrap">
            {{ localTicket.description || 'No detailed description provided.' }}
          </p>
          <div class="flex items-center gap-3 pt-2 text-[11px] text-muted-foreground border-t border-border/40">
            <span>Department: <strong class="text-foreground">{{ localTicket.responsibleDepartment || 'Unassigned' }}</strong></span>
            <span>Issue Type: <strong class="text-foreground">{{ localTicket.issueType || 'Maintenance' }}</strong></span>
            <span>Originator: <strong class="text-foreground">{{ localTicket.originatorType || 'ManualUser' }}</strong></span>
          </div>
        </div>

        <!-- In-Place Edit Form -->
        <div v-else class="p-4 bg-card rounded-2xl border border-indigo-500/40 shadow-sm space-y-3 animate-in fade-in">
          <div class="space-y-1">
            <label class="text-[10px] font-bold uppercase text-muted-foreground">Title</label>
            <Input v-model="editTitle" class="text-xs h-8" />
          </div>
          <div class="space-y-1">
            <label class="text-[10px] font-bold uppercase text-muted-foreground">Description</label>
            <textarea
              v-model="editDescription"
              rows="3"
              class="w-full px-2.5 py-1.5 text-xs bg-muted/30 border border-border rounded-lg text-foreground focus:outline-hidden"
            />
          </div>
          <div class="grid grid-cols-2 gap-2 text-xs">
            <div>
              <label class="text-[10px] font-bold uppercase text-muted-foreground">Responsible Department</label>
              <select
                v-model="editDepartment"
                class="w-full px-2 py-1.5 bg-background border border-border rounded-lg text-xs"
              >
                <option value="">(None)</option>
                <option value="Assy">Assy</option>
                <option value="SMT">SMT</option>
                <option value="Test">Test</option>
                <option value="IT">IT</option>
                <option value="MES">MES</option>
                <option value="SAP">SAP</option>
                <option value="ProcessEngineering">ProcessEngineering</option>
                <option value="ProductOwner">ProductOwner</option>
                <option value="Robotics">Robotics</option>
                <option value="Vision">Vision</option>
              </select>
            </div>
            <div class="flex items-center gap-3 pt-4">
              <label class="flex items-center gap-1.5 cursor-pointer text-xs font-semibold">
                <input type="checkbox" v-model="editIsLineStop" class="rounded text-rose-600 size-4" />
                <span>Line Stop</span>
              </label>
              <div v-if="editIsLineStop" class="flex-1">
                <Input v-model.number="editLineStopDuration" type="number" placeholder="Mins" class="text-xs h-8" />
              </div>
            </div>
          </div>
          <div class="space-y-1">
            <label class="text-[10px] font-bold uppercase text-muted-foreground">Reason for Edit (recorded in change history)</label>
            <Input v-model="editReasonNote" placeholder="e.g. Fixed typo in alarm description" class="text-xs h-8" />
          </div>
          <div class="flex justify-end gap-2 pt-1">
            <Button size="sm" variant="outline" class="text-xs h-7" @click="isEditingDetails = false">Cancel</Button>
            <Button size="sm" class="bg-primary text-primary-foreground text-xs h-7 font-bold" :disabled="isSavingEdit" @click="saveDetailsEdit">
              {{ isSavingEdit ? 'Saving…' : 'Save Changes' }}
            </Button>
          </div>
        </div>
      </div>

      <!-- Telemetry Snapshot at Ticket Raise -->
      <div v-if="localTicket.telemetrySnapshot" class="space-y-2">
        <button
          type="button"
          @click="showTelemetrySnapshot = !showTelemetrySnapshot"
          class="flex items-center justify-between w-full text-xs font-black uppercase tracking-widest text-muted-foreground hover:text-foreground cursor-pointer"
        >
          <div class="flex items-center gap-1.5">
            <Activity class="size-3.5 text-indigo-600 dark:text-indigo-400" />
            <span>Telemetry Snapshot at Incident Raise</span>
          </div>
          <ChevronUp v-if="showTelemetrySnapshot" class="size-4" />
          <ChevronDown v-else class="size-4" />
        </button>

        <div v-if="showTelemetrySnapshot" class="p-3.5 bg-muted/20 border border-border rounded-2xl grid grid-cols-2 sm:grid-cols-3 gap-2.5 text-xs font-mono">
          <div v-for="(val, key) in localTicket.telemetrySnapshot" :key="String(key)" class="p-2 bg-card rounded-lg border border-border/60">
            <span class="text-[10px] text-muted-foreground block truncate uppercase">{{ key }}</span>
            <span class="font-bold text-foreground text-xs truncate block">{{ typeof val === 'object' ? JSON.stringify(val) : val }}</span>
          </div>
        </div>
      </div>

      <!-- Change History / Audit Log -->
      <div v-if="localTicket.changeHistory && localTicket.changeHistory.length > 0" class="space-y-2">
        <button
          type="button"
          @click="showChangeHistory = !showChangeHistory"
          class="flex items-center justify-between w-full text-xs font-black uppercase tracking-widest text-muted-foreground hover:text-foreground cursor-pointer"
        >
          <div class="flex items-center gap-1.5">
            <History class="size-3.5 text-indigo-600 dark:text-indigo-400" />
            <span>Audit Trail &amp; Change History ({{ localTicket.changeHistory.length }})</span>
          </div>
          <ChevronUp v-if="showChangeHistory" class="size-4" />
          <ChevronDown v-else class="size-4" />
        </button>

        <div v-if="showChangeHistory" class="space-y-1.5">
          <div
            v-for="chg in localTicket.changeHistory"
            :key="chg.id"
            class="p-2.5 rounded-xl bg-muted/20 border border-border text-xs space-y-1"
          >
            <div class="flex items-center justify-between text-[10px] text-muted-foreground">
              <span class="font-bold text-foreground">{{ chg.changedBy }}</span>
              <span>{{ new Date(chg.timestamp).toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) }}</span>
            </div>
            <div class="text-[11px] font-mono">
              <strong class="text-indigo-600 dark:text-indigo-400">{{ chg.field }}:</strong>
              <span class="line-through text-muted-foreground mx-1">{{ String(chg.oldValue) }}</span>
              &rarr;
              <span class="text-foreground font-semibold ml-1">{{ String(chg.newValue) }}</span>
            </div>
            <p v-if="chg.note" class="text-[10px] text-muted-foreground italic">&ldquo;{{ chg.note }}&rdquo;</p>
          </div>
        </div>
      </div>

      <!-- Function Block State -->
      <div v-if="localTicket.fbState">
        <h4 class="text-xs font-black uppercase tracking-widest text-muted-foreground mb-2">Function Block State</h4>
        <div class="p-3 bg-muted/30 rounded-2xl border border-border">
          <code class="text-xs font-mono text-emerald-700 dark:text-emerald-400 leading-relaxed">
            {{ localTicket.fbState.blockName }}: {{ localTicket.fbState.state }}
            <span v-if="localTicket.fbState.subState"> / {{ localTicket.fbState.subState }}</span>
            <span v-if="localTicket.fbState.errorCode" class="text-rose-600 dark:text-rose-400"> ({{ localTicket.fbState.errorCode }})</span>
          </code>
        </div>
      </div>

      <!-- Tags -->
      <div>
        <h4 class="text-xs font-black uppercase tracking-widest text-muted-foreground mb-2">Tags</h4>
        <div class="flex flex-wrap gap-1.5 mb-2">
          <span
            v-for="t in localTicket.tags ?? []"
            :key="t"
            class="text-[11px] font-mono px-2 py-0.5 rounded-md bg-muted text-foreground border border-border flex items-center gap-1 group"
          >
            {{ t }}
            <button
              type="button"
              @click="removeTag(t)"
              class="text-muted-foreground hover:text-rose-500 text-xs leading-none"
            >
              &times;
            </button>
          </span>
        </div>
        <div class="flex gap-2">
          <Input
            v-model="newTagInput"
            placeholder="Add tag (e.g. #Safety)..."
            class="bg-muted/40 border-border rounded-xl text-xs h-8 text-foreground"
            @keyup.enter="addTag"
          />
          <Button
            size="sm"
            @click="addTag"
            :disabled="!newTagInput.trim() || isUpdatingTags"
            class="rounded-xl text-xs font-bold px-3 h-8 cursor-pointer"
          >
            Add
          </Button>
        </div>
      </div>

      <!-- Ticket Attachments with Rendered Image Thumbnails -->
      <div>
        <h4 class="text-xs font-black uppercase tracking-widest text-muted-foreground mb-2">Evidence &amp; Photos</h4>
        <div v-if="ticketLevelAttachments.length > 0" class="grid grid-cols-4 gap-2 mb-3">
          <div
            v-for="att in ticketLevelAttachments"
            :key="att.id"
            class="relative group aspect-square bg-muted rounded-xl overflow-hidden border border-border cursor-pointer shadow-xs"
            @click="openLightbox(att)"
          >
            <img v-if="att.url" :src="att.url" :alt="att.fileName" class="w-full h-full object-cover" />
            <div v-else class="w-full h-full flex items-center justify-center">
              <ImageIcon class="h-6 w-6 text-muted-foreground" />
            </div>
            <div class="absolute inset-0 bg-background/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
              <ZoomIn class="h-5 w-5 text-foreground" />
            </div>
          </div>
        </div>

        <ImageAttachmentUploader
          v-model="ticketAttachmentsDraft"
          label="Add Images"
          :max-files="10"
        />
        <Button
          v-if="ticketAttachmentsDraft.length > 0"
          @click="uploadTicketAttachments"
          :disabled="isUploadingTicketAttachments"
          class="mt-2 w-full bg-primary hover:bg-primary/90 text-primary-foreground rounded-xl text-xs font-black uppercase h-8 cursor-pointer"
        >
          {{ isUploadingTicketAttachments ? 'Uploading…' : `Upload ${ticketAttachmentsDraft.length} Image(s)` }}
        </Button>
      </div>

      <!-- Comments Timeline -->
      <div>
        <h4 class="text-xs font-black uppercase tracking-widest text-muted-foreground mb-3">
          Activity Timeline &amp; Notes
        </h4>
        <div class="space-y-2.5 mb-4 max-h-72 overflow-y-auto pr-1">
          <div v-if="localTicket.comments.length === 0" class="p-4 text-center text-muted-foreground text-xs uppercase font-bold bg-muted/20 rounded-xl border border-border">
            No technician notes recorded yet.
          </div>
          <div
            v-for="cmt in localTicket.comments"
            :key="cmt.id"
            class="p-3 bg-muted/30 rounded-xl border border-border space-y-1.5"
          >
            <div class="flex items-center justify-between text-[10px]">
              <span class="font-bold text-indigo-600 dark:text-indigo-400 uppercase">{{ cmt.authorName }}</span>
              <span class="text-muted-foreground font-mono">
                {{ new Date(cmt.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
              </span>
            </div>

            <div v-if="cmt.transition" class="flex items-center gap-1.5 flex-wrap">
              <span :class="['text-[10px] font-black px-2 py-0.5 rounded-full border', statusColor(cmt.transition.fromStatus)]">
                {{ cmt.transition.fromStatus }}
              </span>
              <ArrowRightLeft class="h-3 w-3 text-muted-foreground" />
              <span :class="['text-[10px] font-black px-2 py-0.5 rounded-full border', statusColor(cmt.transition.toStatus)]">
                {{ cmt.transition.toStatus }}
              </span>
              <span v-if="cmt.transition.actor" class="text-[10px] text-muted-foreground ml-1">
                by {{ cmt.transition.actor }}
              </span>
            </div>

            <p v-if="cmt.content" class="text-xs text-foreground leading-relaxed">{{ cmt.content }}</p>

            <!-- Rendered thumbnail images for comment attachments -->
            <div v-if="cmt.attachments && cmt.attachments.length > 0" class="grid grid-cols-3 gap-1.5 pt-1">
              <div
                v-for="att in cmt.attachments"
                :key="att.id"
                class="relative group aspect-square bg-muted rounded-lg overflow-hidden border border-border cursor-pointer shadow-xs"
                @click="openLightbox(att)"
              >
                <img v-if="att.url" :src="att.url" :alt="att.fileName" class="w-full h-full object-cover" />
                <div class="absolute inset-0 bg-background/60 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                  <ZoomIn class="h-4 w-4 text-foreground" />
                </div>
              </div>
            </div>
          </div>
        </div>

        <div v-if="showCommentAttachmentPanel" class="mb-2 p-3 bg-muted/40 rounded-xl border border-border">
          <ImageAttachmentUploader
            v-model="commentAttachments"
            label="Attach to this comment"
            :max-files="5"
          />
        </div>

        <div class="flex gap-2 items-center">
          <button
            type="button"
            @click="showCommentAttachmentPanel = !showCommentAttachmentPanel"
            :class="[
              'p-2 rounded-xl border transition-colors shrink-0 cursor-pointer',
              showCommentAttachmentPanel || commentAttachments.length > 0
                ? 'bg-primary/20 border-primary/50 text-primary'
                : 'bg-background border-border text-muted-foreground hover:text-primary hover:border-primary/40'
            ]"
            title="Attach images to comment"
          >
            <Paperclip class="h-4 w-4" />
          </button>

          <Input
            v-model="commentText"
            placeholder="Add technician observation or note..."
            class="bg-background border-border rounded-xl text-xs flex-1 text-foreground placeholder:text-muted-foreground"
            @keyup.enter="addComment"
          />
          <Button
            @click="addComment"
            :disabled="isSubmittingComment || (!commentText.trim() && commentAttachments.length === 0)"
            class="bg-primary hover:bg-primary/90 text-primary-foreground rounded-xl px-4 text-xs font-black uppercase shrink-0 cursor-pointer"
          >
            <Send class="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>

    </div>
  </div>

  <!-- Lightbox Modal -->
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
</template>
