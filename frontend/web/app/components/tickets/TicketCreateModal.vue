<script setup lang="ts">
import { ref, watch, computed, defineAsyncComponent, onMounted } from 'vue'
import {
  Plus, Camera, AlertTriangle, Check, X, ShieldAlert,
  Sparkles, Layers, UserCheck, Paperclip, ChevronDown, ChevronUp
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import MachineSearchCombobox from '~/components/tickets/MachineSearchCombobox.vue'
import ImageAttachmentUploader from '~/components/tickets/ImageAttachmentUploader.vue'
import SearchableTargetCombobox, { type TargetItem } from '~/components/common/SearchableTargetCombobox.vue'
const QrScanner = defineAsyncComponent(() => import('~/components/ui/qr-scanner/QrScanner.vue'))
import { useOfflineTickets } from '~/composables/useOfflineTickets'
import {
  ERROR_TEMPLATES, CATEGORIES, getGroupsByCategory,
  getTemplatesByGroup, getTemplateById
} from '~/utils/errorTemplateEngine'
import { resolvePreferredTechnician } from '~/utils/technicianInheritance'
import type { TicketAttachment, TechnicianRule, ShiftAbsenceRecord } from '~/types/maintenance'

const props = defineProps<{
  open: boolean
  prefilledStation?: string
  prefilledMachineType?: string
  prefilledGroupId?: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'created', ticket: any): void
}>()

const { queueOfflineTicket } = useOfflineTickets()

const showScanner = ref(false)
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)

// ── Form State ─────────────────────────────────────────────────────────────
const form = ref({
  stationId: props.prefilledStation || '',
  stationName: props.prefilledStation ? `Station ${props.prefilledStation}` : '',
  machineType: props.prefilledMachineType || '',
  groupId: props.prefilledGroupId || '',
  controllerId: '',
  title: '',
  description: '',
  priority: 'Medium' as 'Low' | 'Medium' | 'High' | 'Critical',
  category: 'Error' as 'Prevention' | 'Error' | 'Improvement' | 'ETC',
  errorGroup: '',
  errorCode: '',
  tags: [] as string[],
  sfc: '',
  assignedTechnicianName: ''
})

const attachments = ref<TicketAttachment[]>([])

// ── Template Engine State ──────────────────────────────────────────────────
const showTemplateSelector = ref(false)
const selectedCategory = ref<string>('Error')
const selectedGroup = ref<string>('')
const selectedTemplateId = ref<string>('')

const availableGroups = computed(() => getGroupsByCategory(selectedCategory.value))
const availableTemplates = computed(() => {
  if (!selectedGroup.value) return []
  return getTemplatesByGroup(selectedGroup.value)
})

watch(selectedCategory, (cat) => {
  const groups = getGroupsByCategory(cat)
  selectedGroup.value = groups[0] || ''
  selectedTemplateId.value = ''
})

watch(selectedGroup, (grp) => {
  const tmpls = getTemplatesByGroup(grp)
  selectedTemplateId.value = tmpls[0]?.id || ''
})

function applySelectedTemplate() {
  const tmpl = getTemplateById(selectedTemplateId.value)
  if (!tmpl) return

  form.value.title = `[${tmpl.errorCode}] ${tmpl.shortDescription}`
  form.value.description = tmpl.detailedDescription
  form.value.category = tmpl.category
  form.value.errorGroup = tmpl.errorGroup
  form.value.errorCode = tmpl.errorCode
  form.value.tags = [...tmpl.defaultTags]

  if (tmpl.targetKanbanState === 'Escalated' || tmpl.targetKanbanState === 'Escalated_External') {
    form.value.priority = 'Critical'
  } else if (tmpl.targetKanbanState === 'Pending_Parts') {
    form.value.priority = 'High'
  }

  showTemplateSelector.value = false
}

// ── Preferred Technician Suggestion ────────────────────────────────────────
const rules = ref<TechnicianRule[]>([])
const absences = ref<ShiftAbsenceRecord[]>([])

async function fetchRulesAndAbsences() {
  try {
    const [r, a] = await Promise.all([
      $fetch<TechnicianRule[]>('/api/technicians/rules').catch(() => []),
      $fetch<ShiftAbsenceRecord[]>('/api/technicians/absences').catch(() => [])
    ])
    rules.value = r
    absences.value = a
  } catch (_) {}
}

onMounted(() => {
  fetchRulesAndAbsences()
})

const suggestedTech = computed(() => {
  return resolvePreferredTechnician(
    form.value.stationId,
    form.value.machineType,
    form.value.groupId,
    rules.value,
    absences.value
  )
})

function useSuggestedTechnician() {
  if (suggestedTech.value) {
    if (suggestedTech.value.isAbsent && suggestedTech.value.backupTechnicianName) {
      form.value.assignedTechnicianName = suggestedTech.value.backupTechnicianName
    } else {
      form.value.assignedTechnicianName = suggestedTech.value.technicianName
    }
  }
}

async function queryCandidateTechnicians(q: string): Promise<TargetItem[]> {
  try {
    const cands = await $fetch<any[]>('/api/technicians/candidates')
    return cands.map(c => ({
      id: c.id,
      label: c.name,
      sublabel: `${c.department} • ${c.specialization || ''}`,
      badge: c.role.replace('_', ' '),
      isOutOfOffice: c.isOutOfOffice,
      raw: c
    }))
  } catch {
    return []
  }
}

// ── Handlers ───────────────────────────────────────────────────────────────
function onQrScanned(code: string) {
  showScanner.value = false
  form.value.stationId = code
  form.value.stationName = `Station ${code}`
}

function onMachineSelected(m: any) {
  form.value.stationId = m.customIdentifier || m.name || m.id
  form.value.stationName = m.displayName || m.name || form.value.stationId
  form.value.machineType = m.machineType || ''
  form.value.groupId = m.groupId || ''
  form.value.controllerId = m.controllers?.[0]?.hostname || ''
}

async function handleSubmit() {
  if (!form.value.title.trim() || !form.value.description.trim()) {
    errorMessage.value = 'Please provide both ticket title and failure description.'
    return
  }

  isSubmitting.value = true
  errorMessage.value = null

  const payload = {
    stationId: form.value.stationId,
    stationName: form.value.stationName,
    machineType: form.value.machineType || undefined,
    groupId: form.value.groupId || undefined,
    controllerId: form.value.controllerId || undefined,
    title: form.value.title.trim(),
    description: form.value.description.trim(),
    priority: form.value.priority,
    category: form.value.category,
    errorGroup: form.value.errorGroup || undefined,
    errorCode: form.value.errorCode || undefined,
    tags: form.value.tags,
    sfc: form.value.sfc.trim() || undefined,
    assignedTechnicianName: form.value.assignedTechnicianName.trim() || undefined,
    reportedByUserName: 'Operator (PWA)',
    attachments: attachments.value
  }

  try {
    if (typeof navigator !== 'undefined' && !navigator.onLine) {
      const offlineTicket = await queueOfflineTicket(payload)
      emit('created', offlineTicket)
      emit('update:open', false)
      resetForm()
      return
    }

    const res = await $fetch<{ success: boolean; ticket: any }>('/api/tickets', {
      method: 'POST',
      body: payload
    })

    if (res && res.success) {
      // If there are attachments, upload them to the ticket
      if (attachments.value.length > 0 && res.ticket?.id) {
        for (const att of attachments.value) {
          await $fetch(`/api/tickets/${res.ticket.id}/attachments`, {
            method: 'POST',
            body: att
          }).catch(e => console.warn('Attachment upload failed', e))
        }
      }

      emit('created', res.ticket)
      emit('update:open', false)
      resetForm()
    }
  } catch (err: any) {
    console.warn('Network request failed, queueing ticket offline:', err)
    const offlineTicket = await queueOfflineTicket(payload)
    emit('created', offlineTicket)
    emit('update:open', false)
    resetForm()
  } finally {
    isSubmitting.value = false
  }
}

function resetForm() {
  form.value = {
    stationId: '',
    stationName: '',
    machineType: '',
    groupId: '',
    controllerId: '',
    title: '',
    description: '',
    priority: 'Medium',
    category: 'Error',
    errorGroup: '',
    errorCode: '',
    tags: [],
    sfc: '',
    assignedTechnicianName: ''
  }
  attachments.value = []
  errorMessage.value = null
}

function handleClose() {
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open" role="dialog" aria-modal="true" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
    <div class="bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl w-full max-w-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-200">
      
      <!-- Header -->
      <div class="p-6 border-b border-slate-800 flex items-center justify-between bg-slate-900/50">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-2xl bg-indigo-600/10 text-indigo-400 border border-indigo-500/20">
            <Plus class="h-6 w-6" />
          </div>
          <div>
            <h3 class="text-lg font-black uppercase tracking-tight text-slate-100">
              Report Maintenance Ticket
            </h3>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-widest mt-0.5">
              Industrial Equipment Failure & Incident Report
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            @click="showTemplateSelector = !showTemplateSelector"
            class="border-indigo-500/30 bg-indigo-500/10 text-indigo-300 hover:bg-indigo-500/20 rounded-xl text-xs font-bold gap-1.5"
          >
            <Sparkles class="h-3.5 w-3.5" />
            <span>Use Template</span>
            <ChevronDown v-if="!showTemplateSelector" class="h-3 w-3" />
            <ChevronUp v-else class="h-3 w-3" />
          </Button>

          <Button variant="ghost" size="icon" @click="handleClose" class="text-slate-400 hover:text-white rounded-xl">
            <X class="h-5 w-5" />
          </Button>
        </div>
      </div>

      <!-- Template Selector Drawer (Collapsible) -->
      <div v-if="showTemplateSelector" class="p-4 bg-indigo-950/20 border-b border-indigo-500/20 space-y-3">
        <div class="flex items-center justify-between">
          <span class="text-xs font-black uppercase tracking-wider text-indigo-300 flex items-center gap-1.5">
            <Sparkles class="w-3.5 h-3.5" />
            Standard Error Catalog (4-Level Hierarchy)
          </span>
          <span class="text-[10px] text-slate-500">Auto-populates description, code & tags</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-2">
          <div>
            <label class="block text-[9px] font-bold text-slate-400 uppercase mb-1">1. Category</label>
            <select
              v-model="selectedCategory"
              class="w-full h-8 rounded-lg bg-slate-900 border border-slate-700 text-xs text-slate-200 px-2"
            >
              <option v-for="c in CATEGORIES" :key="c" :value="c">{{ c }}</option>
            </select>
          </div>

          <div>
            <label class="block text-[9px] font-bold text-slate-400 uppercase mb-1">2. Error Group</label>
            <select
              v-model="selectedGroup"
              class="w-full h-8 rounded-lg bg-slate-900 border border-slate-700 text-xs text-slate-200 px-2"
            >
              <option v-for="g in availableGroups" :key="g" :value="g">{{ g }}</option>
            </select>
          </div>

          <div>
            <label class="block text-[9px] font-bold text-slate-400 uppercase mb-1">3. Error Code</label>
            <select
              v-model="selectedTemplateId"
              class="w-full h-8 rounded-lg bg-slate-900 border border-slate-700 text-xs text-slate-200 px-2"
            >
              <option v-for="t in availableTemplates" :key="t.id" :value="t.id">
                [{{ t.errorCode }}] {{ t.shortDescription }}
              </option>
            </select>
          </div>
        </div>

        <div class="flex justify-end gap-2 pt-1">
          <Button
            size="sm"
            class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-bold px-4 h-7"
            @click="applySelectedTemplate"
          >
            Apply Template
          </Button>
        </div>
      </div>

      <!-- QR Scanner Overlay Modal -->
      <div v-if="showScanner" class="p-4 bg-slate-950 border-b border-slate-800">
        <QrScanner @scanned="onQrScanned" @close="showScanner = false" />
      </div>

      <!-- Form Body -->
      <div class="p-6 space-y-4 max-h-[70vh] overflow-y-auto">
        <!-- Error Alert -->
        <div v-if="errorMessage" class="p-3 bg-rose-950/40 border border-rose-900/50 rounded-xl flex items-center gap-3 text-rose-400 text-xs">
          <AlertTriangle class="h-4 w-4 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>

        <!-- Equipment ID / QR Scanner Trigger -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Equipment / Station Target
          </label>
          <div class="flex gap-2">
            <MachineSearchCombobox
              v-model="form.stationId"
              v-model:station-name="form.stationName"
              placeholder="Type machine ID or search from found machines..."
              class="flex-1"
              @select="onMachineSelected"
            />
            <Button
              type="button"
              variant="outline"
              @click="showScanner = !showScanner"
              class="border-indigo-500/30 bg-indigo-500/10 text-indigo-300 hover:bg-indigo-500/20 rounded-xl text-xs font-black uppercase tracking-wider flex items-center gap-2 shrink-0 h-9"
            >
              <Camera class="h-4 w-4" />
              <span>Scan QR</span>
            </Button>
          </div>
          <div v-if="form.stationName && form.stationName !== form.stationId" class="mt-1.5 text-[11px] text-slate-400 flex items-center gap-1.5">
            <span class="text-indigo-400 font-bold">Target:</span>
            <span class="text-slate-200">{{ form.stationName }}</span>
            <span v-if="form.machineType" class="text-slate-500 font-mono">({{ form.machineType }})</span>
          </div>
        </div>

        <!-- Ticket Title -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Incident Title
          </label>
          <Input
            v-model="form.title"
            placeholder="e.g. Spindle Overheating & Abnormal Noise"
            class="bg-slate-950 border-slate-800 rounded-xl text-xs"
          />
        </div>

        <!-- Workpiece SFC Tracking Serial (Optional) -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5 flex items-center gap-1">
            <Layers class="w-3 h-3 text-cyan-400" />
            <span>Workpiece SFC Serial Number (Shop Floor Control)</span>
          </label>
          <Input
            v-model="form.sfc"
            placeholder="e.g. SFC-BAT-20260904-0042 (Serial in work at time of defect)"
            class="bg-slate-950 border-slate-800 rounded-xl text-xs font-mono text-cyan-300"
          />
        </div>

        <!-- Priority Level -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Priority Level
          </label>
          <div class="grid grid-cols-4 gap-2">
            <button
              v-for="pr in [
                { id: 'Low', label: 'Low', class: 'border-slate-700 bg-slate-950 text-slate-400' },
                { id: 'Medium', label: 'Medium', class: 'border-blue-700 bg-blue-950/20 text-blue-400' },
                { id: 'High', label: 'High', class: 'border-amber-700 bg-amber-950/20 text-amber-400' },
                { id: 'Critical', label: 'Critical', class: 'border-rose-700 bg-rose-950/20 text-rose-400 font-black' }
              ]"
              :key="pr.id"
              type="button"
              @click="form.priority = pr.id as any"
              :class="[
                'p-2.5 rounded-xl border text-xs uppercase font-bold text-center transition-all',
                form.priority === pr.id ? 'ring-2 ring-indigo-500 shadow-lg scale-105' : 'opacity-70 hover:opacity-100',
                pr.class
              ]"
            >
              {{ pr.label }}
            </button>
          </div>
        </div>

        <!-- Description -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Failure Description & Observed Symptoms
          </label>
          <textarea
            v-model="form.description"
            rows="3"
            placeholder="Describe the defect, error code on PLC/HMI display, temperature reading, sound..."
            class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3 text-xs text-slate-200 focus:outline-none focus:border-indigo-500"
          ></textarea>
        </div>

        <!-- Preferred Technician Suggestion Banner -->
        <div v-if="suggestedTech" class="p-3 rounded-xl bg-slate-950 border border-slate-800/80 flex items-center justify-between">
          <div class="flex items-center gap-2">
            <UserCheck class="w-4 h-4 text-indigo-400 shrink-0" />
            <div>
              <span class="text-xs font-bold text-slate-200">
                {{ suggestedTech.isAbsent ? (suggestedTech.backupTechnicianName || suggestedTech.technicianName) : suggestedTech.technicianName }}
              </span>
              <span class="text-[10px] text-slate-500 ml-1.5">({{ suggestedTech.sourceLabel }})</span>
              <div v-if="suggestedTech.isAbsent" class="text-[10px] text-amber-400">
                Primary tech away ({{ suggestedTech.absenceReason }}). Rerouting to backup.
              </div>
            </div>
          </div>
          <button
            type="button"
            class="px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider rounded bg-indigo-600 hover:bg-indigo-500 text-white transition"
            @click="useSuggestedTechnician"
          >
            Assign
          </button>
        </div>

        <!-- Technician Assignment Input (Combobox with Query & Free-Text) -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5 flex items-center justify-between">
            <span>Assign Technician or Engineer (Query or Free-Text)</span>
            <span class="text-[9px] text-slate-500 lowercase font-normal">optional</span>
          </label>
          <SearchableTargetCombobox
            v-model="form.assignedTechnicianName"
            placeholder="Search candidate technician / engineer or type free-text..."
            category-label="Floor Technicians & Engineers"
            icon-type="user"
            :query-fn="queryCandidateTechnicians"
          />
        </div>

        <!-- Image Attachments Uploader -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5 flex items-center gap-1.5">
            <Paperclip class="w-3 h-3 text-slate-400" />
            <span>Incident Photos & Defect Frames</span>
          </label>
          <ImageAttachmentUploader
            v-model="attachments"
            label="Upload machine / defect images"
            :max-files="5"
          />
        </div>
      </div>

      <!-- Footer -->
      <div class="p-6 border-t border-slate-800 bg-slate-900/50 flex items-center justify-end gap-3">
        <Button variant="ghost" @click="handleClose" class="text-xs font-black uppercase text-slate-400 rounded-xl">
          Cancel
        </Button>
        <Button
          @click="handleSubmit"
          :disabled="isSubmitting"
          class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase tracking-widest px-6"
        >
          <span v-if="!isSubmitting">Submit Ticket</span>
          <span v-else>Submitting...</span>
        </Button>
      </div>

    </div>
  </div>
</template>
