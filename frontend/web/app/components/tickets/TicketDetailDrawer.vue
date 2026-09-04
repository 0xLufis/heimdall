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
  Archive,
  ArrowRightLeft,
  Tag,
  Image as ImageIcon,
  ZoomIn,
  AlertTriangle,
  ShieldAlert,
  ExternalLink
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import MachineSearchCombobox from '~/components/tickets/MachineSearchCombobox.vue'
import ImageAttachmentUploader from '~/components/tickets/ImageAttachmentUploader.vue'
import { authClient } from '~/utils/auth-client'
import type { MaintenanceTicket, TicketAttachment, TicketStatus } from '~/types/maintenance'

// ─── Props / Emits ───────────────────────────────────────────────────────────

const props = defineProps<{
  ticket: MaintenanceTicket | null
  open: boolean
  /** Optional list of active absence records to check for OOO warnings */
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

// Status selector
const STATUSES: { value: TicketStatus; label: string; color: string }[] = [
  { value: 'Open',               label: 'Open',               color: 'bg-blue-500/10 text-blue-400 border-blue-500/30' },
  { value: 'In_Progress',        label: 'In Progress',        color: 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30' },
  { value: 'Pending_Parts',      label: 'Pending Parts',      color: 'bg-amber-500/10 text-amber-400 border-amber-500/30' },
  { value: 'Escalated',          label: 'Escalated',          color: 'bg-orange-500/10 text-orange-400 border-orange-500/30' },
  { value: 'Escalated_External', label: 'Escalated External', color: 'bg-rose-500/10 text-rose-400 border-rose-500/30' },
  { value: 'Closure_Pending',    label: 'Closure Pending',    color: 'bg-purple-500/10 text-purple-400 border-purple-500/30' },
  { value: 'Resolved',           label: 'Resolved',           color: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' },
  { value: 'Closed_Unresolved',  label: 'Closed Unresolved',  color: 'bg-slate-500/10 text-slate-400 border-slate-500/30' },
]

const EXTERNAL_TARGETS = ['SAP Engineers', 'IT Department', 'Production Operations', 'OEM Vendor'] as const

const selectedStatus = ref<TicketStatus>('Open')
const selectedExternalTarget = ref<string>('')
const isChangingStatus = ref(false)

// Tags
const newTagInput = ref('')
const isUpdatingTags = ref(false)

// Ticket-level image attachments
const ticketAttachmentsDraft = ref<TicketAttachment[]>([])
const isUploadingTicketAttachments = ref(false)

// Comment attachments
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
  if (newVal) {
    selectedStatus.value = newVal.status as TicketStatus
    selectedExternalTarget.value = newVal.externalEscalationTarget || ''
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

/** Ticket-level attachments (no commentId) */
const ticketLevelAttachments = computed<TicketAttachment[]>(() => {
  return (localTicket.value?.attachments ?? []).filter(a => !a.commentId)
})

/** OOO warning */
const oooWarning = computed(() => {
  if (!localTicket.value?.assignedTechnicianName || !props.absences) return null
  const techName = localTicket.value.assignedTechnicianName
  const match = props.absences.find(a => a.active && a.technicianName === techName)
  return match ? match : null
})

/** Closure pending = needs AOK banner */
const showAokBanner = computed(() => selectedStatus.value === 'Closure_Pending')

const statusBadgeColor = computed(() => {
  const s = STATUSES.find(s => s.value === (localTicket.value?.status ?? 'Open'))
  return s?.color ?? 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30'
})

// ─── Helpers ─────────────────────────────────────────────────────────────────

function statusColor(status: string) {
  return STATUSES.find(s => s.value === status)?.color ?? 'bg-slate-500/10 text-slate-400 border-slate-500/30'
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

// ─── Status Change ───────────────────────────────────────────────────────────

async function applyStatusChange() {
  if (!localTicket.value) return
  const oldStatus = localTicket.value.status
  const newStatus = selectedStatus.value
  if (oldStatus === newStatus) return

  isChangingStatus.value = true
  try {
    const techName = currentUser.value
    const payload: Record<string, any> = { status: newStatus }

    if (newStatus === 'In_Progress' && (!localTicket.value.assignedTechnicianName || localTicket.value.assignedTechnicianName === 'Unassigned')) {
      payload.assignedTechnicianName = techName
    }
    if (newStatus === 'Escalated_External') {
      payload.externalEscalationTarget = selectedExternalTarget.value || 'SAP Engineers'
    }

    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: payload
    })

    if (res?.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)

      // Post a state-transition comment
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
      // Refresh ticket to pick up new comment
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

/** Quick workflow shortcut buttons */
async function updateStatus(newStatus: TicketStatus) {
  selectedStatus.value = newStatus
  await applyStatusChange()
}

/** AOK grant + resolve */
async function grantAokAndResolve() {
  selectedStatus.value = 'Resolved'
  await applyStatusChange()
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

async function removeTag(tag: string) {
  if (!localTicket.value) return
  const newTags = (localTicket.value.tags ?? []).filter(t => t !== tag)
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
  }
}

// ─── Ticket-level Attachment Upload ──────────────────────────────────────────

async function uploadTicketAttachments() {
  if (!localTicket.value || ticketAttachmentsDraft.value.length === 0) return
  isUploadingTicketAttachments.value = true
  try {
    for (const att of ticketAttachmentsDraft.value) {
      await $fetch(`/api/tickets/${localTicket.value.id}/attachments`, {
        method: 'POST',
        body: {
          id: att.id,
          fileName: att.fileName,
          contentType: att.contentType,
          fileSize: att.fileSize,
          url: att.url
        }
      })
    }
    // Refresh
    const fresh = await $fetch<{ ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`)
    if (fresh?.ticket) {
      localTicket.value = fresh.ticket
      emit('updated', fresh.ticket)
    }
    ticketAttachmentsDraft.value = []
  } catch (err) {
    console.error('Error uploading ticket attachments:', err)
  } finally {
    isUploadingTicketAttachments.value = false
  }
}

// ─── Comments ────────────────────────────────────────────────────────────────

async function addComment() {
  if (!commentText.value.trim() && commentAttachments.value.length === 0) return
  if (!localTicket.value) return
  isSubmittingComment.value = true

  try {
    const res = await $fetch<{ success: boolean; comment: any; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}/comments`, {
      method: 'POST',
      body: {
        authorName: currentUser.value,
        content: commentText.value.trim(),
        ...(commentAttachments.value.length > 0 ? { attachments: commentAttachments.value } : {})
      }
    })

    if (res?.success) {
      localTicket.value = res.ticket
      commentText.value = ''
      commentAttachments.value = []
      showCommentAttachmentPanel.value = false
      emit('updated', res.ticket)
    }
  } catch (err) {
    console.error('Error submitting comment:', err)
  } finally {
    isSubmittingComment.value = false
  }
}

function handleClose() {
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open && localTicket" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex justify-end">
    <div class="bg-slate-900 border-l border-slate-800 w-full max-w-2xl h-full flex flex-col shadow-2xl animate-in slide-in-from-right duration-300">

      <!-- Header -->
      <div class="p-5 border-b border-slate-800 flex items-center justify-between bg-slate-900/60 shrink-0">
        <div class="flex items-center gap-3 min-w-0">
          <div class="p-2.5 rounded-xl bg-slate-800 border border-slate-700 text-indigo-400 shrink-0">
            <Cpu class="h-5 w-5" />
          </div>
          <div class="min-w-0">
            <span class="text-xs font-mono font-black text-indigo-400">{{ localTicket.ticketNumber }}</span>
            <h3 class="text-sm font-black uppercase text-slate-100 tracking-tight leading-tight truncate">
              {{ localTicket.stationName }}
            </h3>
          </div>
        </div>
        <Button variant="ghost" size="icon" @click="handleClose" class="text-slate-400 hover:text-white rounded-xl shrink-0">
          <X class="h-5 w-5" />
        </Button>
      </div>

      <!-- Main Scrollable Content -->
      <div class="flex-1 overflow-y-auto p-5 space-y-5">

        <!-- ── OOO Warning ─────────────────────────────────────────── -->
        <div
          v-if="oooWarning"
          class="flex items-start gap-3 p-3 rounded-xl border border-amber-500/30 bg-amber-500/10 text-amber-300 text-xs"
        >
          <AlertTriangle class="h-4 w-4 shrink-0 mt-0.5" />
          <div>
            <span class="font-black">⚠️ {{ oooWarning.technicianName }} is currently out of office.</span>
            <span v-if="oooWarning.backupTechnicianName" class="ml-1">
              Backup: <span class="font-bold text-amber-200">{{ oooWarning.backupTechnicianName }}</span>
            </span>
          </div>
        </div>

        <!-- ── Status & Priority Banner ─────────────────────────────── -->
        <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-3">
          <div class="flex items-center justify-between">
            <Badge
              variant="outline"
              :class="['text-xs font-black uppercase tracking-widest px-3 py-1 border', statusBadgeColor]"
            >
              {{ localTicket.status.replace(/_/g, ' ') }}
            </Badge>
            <Badge variant="outline" class="text-xs font-black uppercase tracking-widest px-3 py-1 bg-rose-500/10 text-rose-400 border-rose-500/30">
              Priority: {{ localTicket.priority }}
            </Badge>
          </div>

          <!-- Status Selector -->
          <div class="pt-2 border-t border-slate-900 space-y-2">
            <p class="text-[10px] font-black uppercase tracking-widest text-slate-500">Change Status</p>
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-1.5">
              <button
                v-for="s in STATUSES"
                :key="s.value"
                type="button"
                @click="selectedStatus = s.value"
                :class="[
                  'px-2 py-1.5 rounded-lg border text-[10px] font-black uppercase tracking-wide text-center transition-all',
                  selectedStatus === s.value ? ['ring-2 ring-indigo-500 scale-105', s.color] : ['opacity-60 hover:opacity-90', s.color]
                ]"
              >
                {{ s.label }}
              </button>
            </div>

            <!-- External Target if Escalated_External -->
            <div v-if="selectedStatus === 'Escalated_External'" class="flex items-center gap-2">
              <ExternalLink class="h-3.5 w-3.5 text-rose-400 shrink-0" />
              <select
                v-model="selectedExternalTarget"
                class="flex-1 bg-slate-950 border border-rose-500/40 text-rose-300 text-xs rounded-xl px-3 py-1.5 focus:outline-none focus:border-rose-400"
              >
                <option v-for="t in EXTERNAL_TARGETS" :key="t" :value="t">{{ t }}</option>
              </select>
            </div>

            <!-- AOK Banner if Closure_Pending -->
            <div v-if="showAokBanner" class="flex items-center justify-between gap-3 p-3 rounded-xl border border-purple-500/30 bg-purple-500/10">
              <div class="flex items-center gap-2 text-purple-300 text-xs">
                <ShieldAlert class="h-4 w-4 shrink-0" />
                <span class="font-bold">⚠️ Needs Outside AOK Sign-off</span>
              </div>
              <Button
                size="sm"
                @click="grantAokAndResolve"
                class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-[10px] font-black uppercase tracking-wider h-7 shrink-0"
              >
                Grant AOK &amp; Resolve
              </Button>
            </div>

            <Button
              v-if="selectedStatus !== localTicket.status"
              @click="applyStatusChange"
              :disabled="isChangingStatus"
              class="w-full bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase tracking-wider h-8"
            >
              <span v-if="!isChangingStatus">Apply Status Change</span>
              <span v-else>Updating…</span>
            </Button>
          </div>

          <!-- Quick Action Workflow Buttons -->
          <div class="flex flex-wrap gap-2 pt-2 border-t border-slate-900">
            <Button
              v-if="localTicket.status === 'Open'"
              size="sm"
              @click="updateStatus('In_Progress')"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8"
            >
              <Play class="h-3.5 w-3.5 mr-1" />
              Start Work
            </Button>

            <Button
              v-if="localTicket.status === 'In_Progress'"
              size="sm"
              @click="updateStatus('Pending_Parts')"
              class="bg-amber-600 hover:bg-amber-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8"
            >
              <Package class="h-3.5 w-3.5 mr-1" />
              Pending Parts
            </Button>

            <Button
              v-if="localTicket.status === 'In_Progress' || localTicket.status === 'Pending_Parts'"
              size="sm"
              @click="updateStatus('Resolved')"
              class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-[10px] font-black uppercase tracking-wider h-8"
            >
              <CheckCircle2 class="h-3.5 w-3.5 mr-1" />
              Mark Resolved
            </Button>

            <Button
              v-if="localTicket.status === 'Resolved'"
              size="sm"
              @click="updateStatus('Closed_Unresolved')"
              class="bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-[10px] font-black uppercase tracking-wider h-8"
            >
              <Archive class="h-3.5 w-3.5 mr-1" />
              Close Ticket
            </Button>
          </div>
        </div>

        <!-- ── Incident Details ─────────────────────────────────────── -->
        <div>
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-2">Incident Details</h4>
          <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-2">
            <h5 class="text-sm font-bold text-slate-200">{{ localTicket.title }}</h5>
            <p class="text-xs text-slate-400 leading-relaxed">{{ localTicket.description }}</p>
          </div>
        </div>

        <!-- ── FB State ────────────────────────────────────────────── -->
        <div v-if="localTicket.fbState">
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-2">Function Block State</h4>
          <div class="p-3 bg-slate-950 rounded-2xl border border-slate-800">
            <code class="text-xs font-mono text-emerald-400 leading-relaxed">
              {{ localTicket.fbState.blockName }}: {{ localTicket.fbState.state }}
              <span v-if="localTicket.fbState.subState"> / {{ localTicket.fbState.subState }}</span>
              <span v-if="localTicket.fbState.errorCode" class="text-rose-400"> ({{ localTicket.fbState.errorCode }})</span>
            </code>
          </div>
        </div>

        <!-- ── SFC Serial ──────────────────────────────────────────── -->
        <div v-if="localTicket.sfc" class="flex items-center gap-2">
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400">SFC Serial:</h4>
          <Badge variant="outline" class="text-xs font-mono bg-slate-950 border-slate-700 text-cyan-400">
            {{ localTicket.sfc }}
          </Badge>
        </div>

        <!-- ── Telemetry Snapshot ──────────────────────────────────── -->
        <div v-if="localTicket.telemetrySnapshot && Object.keys(localTicket.telemetrySnapshot.metrics).length > 0">
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-2">Telemetry Snapshot</h4>
          <div class="p-3 bg-slate-950 rounded-2xl border border-slate-800 overflow-x-auto">
            <table class="w-full text-xs">
              <tbody>
                <tr
                  v-for="(val, key) in localTicket.telemetrySnapshot.metrics"
                  :key="key"
                  class="border-b border-slate-800/60 last:border-0"
                >
                  <td class="py-1 pr-4 font-mono text-slate-500 whitespace-nowrap">{{ key }}</td>
                  <td class="py-1 font-mono font-bold text-emerald-400">{{ val }}</td>
                </tr>
              </tbody>
            </table>
            <p class="text-[10px] font-mono text-slate-600 mt-2">
              Captured: {{ new Date(localTicket.telemetrySnapshot.timestamp).toLocaleString() }}
            </p>
          </div>
        </div>

        <!-- ── Target Machine / Equipment ─────────────────────────── -->
        <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-2">
          <div class="flex items-center justify-between">
            <h4 class="text-xs font-black uppercase tracking-widest text-slate-400">Target Machine / Equipment</h4>
            <Button
              variant="ghost"
              size="sm"
              @click="isEditingEquipment = !isEditingEquipment"
              class="h-6 text-[10px] font-bold text-indigo-400 hover:text-indigo-300"
            >
              {{ isEditingEquipment ? 'Cancel' : 'Reassign / Search' }}
            </Button>
          </div>

          <div v-if="!isEditingEquipment" class="flex items-center justify-between text-xs">
            <div>
              <div class="font-bold text-slate-100">{{ localTicket.stationName }}</div>
              <div class="text-[10px] font-mono text-slate-500">{{ localTicket.stationId }}</div>
            </div>
            <span v-if="localTicket.controllerId" class="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-900 border border-slate-800 text-indigo-400">
              {{ localTicket.controllerId }}
            </span>
          </div>

          <div v-else class="space-y-2 pt-1">
            <MachineSearchCombobox
              :model-value="localTicket.stationId"
              :station-name="localTicket.stationName"
              placeholder="Type machine ID or search from found machines..."
              @select="updateEquipment"
            />
            <p class="text-[10px] text-slate-500">
              Selecting a machine automatically updates the ticket's station and controller binding.
            </p>
          </div>
        </div>

        <!-- ── Metadata Info Grid ─────────────────────────────────── -->
        <div class="grid grid-cols-2 gap-3 text-xs">
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
            <span class="text-[10px] font-black uppercase tracking-widest text-slate-500 block">Reported By</span>
            <span class="font-bold text-slate-300 mt-1 block">{{ localTicket.reportedByUserName }}</span>
          </div>
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
            <span class="text-[10px] font-black uppercase tracking-widest text-slate-500 block">Assigned Tech</span>
            <span class="font-bold text-indigo-400 mt-1 block">{{ localTicket.assignedTechnicianName || 'Unassigned' }}</span>
          </div>
        </div>

        <!-- ── Tags ──────────────────────────────────────────────── -->
        <div>
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-2 flex items-center gap-1.5">
            <Tag class="h-3.5 w-3.5" /> Tags
          </h4>
          <div class="flex flex-wrap gap-1.5 mb-2">
            <span
              v-for="tag in localTicket.tags ?? []"
              :key="tag"
              class="flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-indigo-500/10 text-indigo-300 border border-indigo-500/20"
            >
              {{ tag }}
              <button
                @click="removeTag(tag)"
                class="text-indigo-500 hover:text-rose-400 transition-colors ml-0.5"
                :title="`Remove ${tag}`"
              >
                <X class="h-2.5 w-2.5" />
              </button>
            </span>
            <span v-if="!localTicket.tags?.length" class="text-[10px] text-slate-600">No tags yet</span>
          </div>
          <div class="flex gap-2">
            <Input
              v-model="newTagInput"
              placeholder="Add tag (e.g. #Milling)"
              class="bg-slate-950 border-slate-800 rounded-xl text-xs flex-1 h-8"
              @keyup.enter="addTag"
            />
            <Button
              size="sm"
              @click="addTag"
              :disabled="isUpdatingTags || !newTagInput.trim()"
              class="bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-xl text-[10px] font-black uppercase h-8 shrink-0"
            >
              Add
            </Button>
          </div>
        </div>

        <!-- ── Ticket Image Attachments ──────────────────────────── -->
        <div>
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-2 flex items-center gap-1.5">
            <ImageIcon class="h-3.5 w-3.5" /> Attachments
          </h4>

          <!-- Existing ticket-level thumbnails -->
          <div v-if="ticketLevelAttachments.length > 0" class="grid grid-cols-3 sm:grid-cols-4 gap-2 mb-3">
            <div
              v-for="att in ticketLevelAttachments"
              :key="att.id"
              class="relative group aspect-square bg-slate-950 rounded-xl overflow-hidden border border-slate-800 hover:border-indigo-500/50 transition-colors cursor-pointer"
              @click="openLightbox(att)"
            >
              <img
                v-if="att.url"
                :src="att.url"
                :alt="att.fileName"
                class="w-full h-full object-cover"
              />
              <div v-else class="w-full h-full flex items-center justify-center">
                <ImageIcon class="h-6 w-6 text-slate-600" />
              </div>
              <div class="absolute inset-0 bg-slate-950/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                <ZoomIn class="h-5 w-5 text-white" />
              </div>
              <div class="absolute bottom-0 inset-x-0 px-1.5 py-1 bg-slate-950/80 opacity-0 group-hover:opacity-100 transition-opacity">
                <p class="text-[9px] font-mono text-slate-300 truncate">{{ att.fileName }}</p>
              </div>
            </div>
          </div>

          <!-- Upload new ticket-level images -->
          <ImageAttachmentUploader
            v-model="ticketAttachmentsDraft"
            label="Add Images"
            :max-files="10"
          />
          <Button
            v-if="ticketAttachmentsDraft.length > 0"
            @click="uploadTicketAttachments"
            :disabled="isUploadingTicketAttachments"
            class="mt-2 w-full bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase h-8"
          >
            {{ isUploadingTicketAttachments ? 'Uploading…' : `Upload ${ticketAttachmentsDraft.length} Image(s)` }}
          </Button>
        </div>

        <!-- ── Comments & Activity Log ──────────────────────────── -->
        <div>
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-3">
            Activity Timeline &amp; Technician Notes
          </h4>

          <div class="space-y-2.5 mb-4 max-h-72 overflow-y-auto pr-1">
            <template v-if="localTicket.comments.length === 0">
              <div class="p-4 text-center text-slate-600 text-xs uppercase font-bold bg-slate-950/40 rounded-xl border border-slate-900">
                No technician notes recorded yet.
              </div>
            </template>
            <template v-else>
              <div
                v-for="cmt in localTicket.comments"
                :key="cmt.id"
                class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1.5"
              >
                <!-- Author / time row -->
                <div class="flex items-center justify-between text-[10px]">
                  <span class="font-bold text-indigo-400 uppercase">{{ cmt.authorName }}</span>
                  <span class="text-slate-500 font-mono">
                    {{ new Date(cmt.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
                  </span>
                </div>

                <!-- State Transition Badge -->
                <div
                  v-if="cmt.transition"
                  class="flex items-center gap-1.5 flex-wrap"
                >
                  <span
                    :class="['text-[10px] font-black px-2 py-0.5 rounded-full border', statusColor(cmt.transition.fromStatus)]"
                  >
                    {{ cmt.transition.fromStatus.replace(/_/g, ' ') }}
                  </span>
                  <ArrowRightLeft class="h-3 w-3 text-slate-500" />
                  <span
                    :class="['text-[10px] font-black px-2 py-0.5 rounded-full border', statusColor(cmt.transition.toStatus)]"
                  >
                    {{ cmt.transition.toStatus.replace(/_/g, ' ') }}
                  </span>
                  <span v-if="cmt.transition.actor" class="text-[10px] text-slate-500 ml-1">
                    by {{ cmt.transition.actor }}
                  </span>
                </div>

                <!-- Comment text (skip if empty and we only have a transition) -->
                <p v-if="cmt.content" class="text-xs text-slate-300 leading-relaxed">{{ cmt.content }}</p>

                <!-- Inline comment attachments -->
                <div v-if="cmt.attachments && cmt.attachments.length > 0" class="grid grid-cols-3 gap-1.5 pt-1">
                  <div
                    v-for="att in cmt.attachments"
                    :key="att.id"
                    class="relative group aspect-square bg-slate-900 rounded-lg overflow-hidden border border-slate-800 cursor-pointer"
                    @click="openLightbox(att)"
                  >
                    <img v-if="att.url" :src="att.url" :alt="att.fileName" class="w-full h-full object-cover" />
                    <div v-else class="w-full h-full flex items-center justify-center">
                      <ImageIcon class="h-4 w-4 text-slate-600" />
                    </div>
                    <div class="absolute inset-0 bg-slate-950/60 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                      <ZoomIn class="h-4 w-4 text-white" />
                    </div>
                  </div>
                </div>
              </div>
            </template>
          </div>

          <!-- Comment attachment panel -->
          <div v-if="showCommentAttachmentPanel" class="mb-2 p-3 bg-slate-950 rounded-xl border border-slate-800">
            <ImageAttachmentUploader
              v-model="commentAttachments"
              label="Attach to this comment"
              :max-files="5"
            />
          </div>

          <!-- Add Comment Input -->
          <div class="flex gap-2 items-center">
            <!-- Paperclip toggle -->
            <button
              type="button"
              @click="showCommentAttachmentPanel = !showCommentAttachmentPanel"
              :class="[
                'p-2 rounded-xl border transition-colors shrink-0',
                showCommentAttachmentPanel || commentAttachments.length > 0
                  ? 'bg-indigo-600/20 border-indigo-500/50 text-indigo-400'
                  : 'bg-slate-950 border-slate-800 text-slate-500 hover:text-indigo-400 hover:border-indigo-500/30'
              ]"
              title="Attach images to comment"
            >
              <Paperclip class="h-4 w-4" />
            </button>

            <Input
              v-model="commentText"
              placeholder="Add technician observation or note..."
              class="bg-slate-950 border-slate-800 rounded-xl text-xs flex-1"
              @keyup.enter="addComment"
            />
            <Button
              @click="addComment"
              :disabled="isSubmittingComment || (!commentText.trim() && commentAttachments.length === 0)"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-4 text-xs font-black uppercase shrink-0"
            >
              <Send class="h-3.5 w-3.5" />
            </Button>
          </div>

          <!-- Attachment count hint -->
          <p v-if="commentAttachments.length > 0" class="text-[10px] text-indigo-400 mt-1 flex items-center gap-1">
            <Paperclip class="h-2.5 w-2.5" />
            {{ commentAttachments.length }} image(s) will be attached to this comment
          </p>
        </div>

      </div>
    </div>
  </div>

  <!-- Lightbox -->
  <Dialog v-model:open="lightboxOpen">
    <DialogContent class="max-w-4xl bg-slate-950 border-slate-800 p-2">
      <DialogHeader class="px-4 pt-4">
        <DialogTitle class="text-sm font-mono text-slate-300 truncate">{{ lightboxName }}</DialogTitle>
      </DialogHeader>
      <div class="flex items-center justify-center p-4 max-h-[80vh] overflow-auto">
        <img :src="lightboxSrc" :alt="lightboxName" class="max-w-full max-h-full object-contain rounded-xl" />
      </div>
    </DialogContent>
  </Dialog>
</template>
