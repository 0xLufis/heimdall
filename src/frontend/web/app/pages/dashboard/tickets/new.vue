<script setup lang="ts">
import { ref, computed, watch, onMounted, defineAsyncComponent } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  Wrench,
  AlertTriangle,
  Camera,
  Layers,
  Sparkles,
  Flame,
  Paperclip,
  UserCheck,
  ChevronDown,
  ChevronUp,
  RotateCcw,
  Send,
  ArrowLeft,
  X,
  WifiOff,
  CheckCircle2,
  Cpu,
  Factory
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator
} from '~/components/ui/breadcrumb'
import MachineSearchCombobox from '~/components/tickets/MachineSearchCombobox.vue'
import ImageAttachmentUploader from '~/components/tickets/ImageAttachmentUploader.vue'
import SearchableTargetCombobox, { type TargetItem } from '~/components/common/SearchableTargetCombobox.vue'
const QrScanner = defineAsyncComponent(() => import('~/components/ui/qr-scanner/QrScanner.vue'))
import { useOfflineTickets } from '~/composables/useOfflineTickets'
import { useMaintenance } from '~/composables/useMaintenance'
import {
  CATEGORIES,
  getGroupsByCategory,
  getTemplatesByGroup,
  getTemplateById
} from '~/utils/errorTemplateEngine'
import { resolvePreferredTechnician } from '~/utils/technicianInheritance'
import { authClient } from '~/utils/auth-client'
import type {
  TicketAttachment,
  TechnicianRule,
  ShiftAbsenceRecord,
  TicketIssueType,
  TicketOriginatorType,
  TicketPriority,
  ResponsibleDepartment
} from '~/types/maintenance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()
const route = useRoute()
const { queueOfflineTicket } = useOfflineTickets()
const { fetchTickets } = useMaintenance()

// ── State ───────────────────────────────────────────────────────────────────
const showScanner = ref(false)
const isSubmitting = ref(false)
const hasRestoredDraft = ref(false)
const showTemplateSelector = ref(false)

const form = ref({
  stationId: '',
  stationName: '',
  machineType: '',
  groupId: '',
  controllerId: '',
  title: '',
  description: '',
  priority: 'Medium' as TicketPriority,
  category: 'Error' as 'Prevention' | 'Error' | 'Improvement' | 'ETC',
  errorGroup: '',
  errorCode: '',
  tags: [] as string[],
  sfc: '',
  assignedTechnicianName: '',
  isLineStop: false,
  lineStopDurationMinutes: 0,
  responsibleDepartment: '' as ResponsibleDepartment,
  issueType: 'Maintenance' as TicketIssueType,
  originatorType: 'ManualUser' as TicketOriginatorType,
  externalOperatorId: '',
  externalOperatorName: ''
})

const attachments = ref<TicketAttachment[]>([])

// ── 4-Tier Template State ───────────────────────────────────────────────────
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

  if (tmpl.targetKanbanState === 'Pending') {
    form.value.priority = 'High'
  }

  showTemplateSelector.value = false
  toast.success(`Template ${tmpl.errorCode} applied`)
}

// ── Preferred Technician Suggestion ─────────────────────────────────────────
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
    const techName = suggestedTech.value.isAbsent && suggestedTech.value.backupTechnicianName
      ? suggestedTech.value.backupTechnicianName
      : suggestedTech.value.technicianName
    form.value.assignedTechnicianName = techName
    toast.success(`Assigned to ${techName}`)
  }
}

async function queryCandidateTechnicians(q: string): Promise<TargetItem[]> {
  try {
    const cands = await $fetch<any[]>('/api/technicians/candidates')
    return cands.map(c => ({
      id: c.id,
      label: c.name,
      sublabel: `${c.department} • ${c.specialization || ''}`,
      badge: c.role ? c.role.replace('_', ' ') : 'Technician',
      isOutOfOffice: c.isOutOfOffice,
      raw: c
    }))
  } catch {
    return []
  }
}

// ── Machine Selection & QR Scans ────────────────────────────────────────────
function onMachineSelected(m: any) {
  form.value.stationId = m.customIdentifier || m.name || m.id
  form.value.stationName = m.displayName || m.name || form.value.stationId
  form.value.machineType = m.machineType || ''
  form.value.groupId = m.groupId || ''
  form.value.controllerId = m.controllers?.[0]?.hostname || ''
}

function onQrScanned(code: string) {
  showScanner.value = false
  form.value.stationId = code
  form.value.stationName = `Station ${code}`
  toast.success(`Scanned equipment target: ${code}`)
}

// ── Draft Preservation ──────────────────────────────────────────────────────
const DRAFT_KEY = 'heimdall_new_incident_draft'

function saveDraft() {
  if (typeof localStorage === 'undefined') return
  try {
    const data = {
      form: form.value,
      attachments: attachments.value,
      savedAt: new Date().toISOString()
    }
    localStorage.setItem(DRAFT_KEY, JSON.stringify(data))
  } catch (e) {
    console.warn('Failed to save draft:', e)
  }
}

function loadDraft() {
  if (typeof localStorage === 'undefined') return
  try {
    const saved = localStorage.getItem(DRAFT_KEY)
    if (saved) {
      const parsed = JSON.parse(saved)
      if (parsed?.form && !route.query.templateId && !route.query.stationId) {
        form.value = { ...form.value, ...parsed.form }
        if (parsed.attachments && Array.isArray(parsed.attachments)) {
          attachments.value = parsed.attachments
        }
        hasRestoredDraft.value = true
      }
    }
  } catch (e) {
    console.warn('Failed to load draft:', e)
  }
}

function clearDraft() {
  if (typeof localStorage === 'undefined') return
  localStorage.removeItem(DRAFT_KEY)
  hasRestoredDraft.value = false
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
    assignedTechnicianName: '',
    isLineStop: false,
    lineStopDurationMinutes: 0,
    responsibleDepartment: '',
    issueType: 'Maintenance',
    originatorType: 'ManualUser',
    externalOperatorId: '',
    externalOperatorName: ''
  }
  attachments.value = []
  toast.info('Draft discarded')
}

// Watch form deeply to auto-save draft
watch(
  () => [form.value, attachments.value],
  () => {
    saveDraft()
  },
  { deep: true }
)

// ── Lifecycle & Route Query Prefill ─────────────────────────────────────────
onMounted(async () => {
  await fetchRulesAndAbsences()
  loadDraft()

  const q = route.query
  if (q.stationId) {
    form.value.stationId = q.stationId as string
    form.value.stationName = (q.stationName as string) || `Station ${q.stationId}`
  }
  if (q.machineType) {
    form.value.machineType = q.machineType as string
  }
  if (q.groupId) {
    form.value.groupId = q.groupId as string
  }
  if (q.controllerId) {
    form.value.controllerId = q.controllerId as string
  }
  if (q.templateId) {
    const tmpl = getTemplateById(q.templateId as string)
    if (tmpl) {
      selectedCategory.value = tmpl.category
      selectedGroup.value = tmpl.errorGroup
      selectedTemplateId.value = tmpl.id
      applySelectedTemplate()
    }
  }
})

// ── Submission Handler ──────────────────────────────────────────────────────
async function handleSubmit() {
  if (!form.value.title.trim()) {
    toast.error('Please enter an incident title.')
    return
  }
  if (!form.value.description.trim()) {
    toast.error('Please describe the observed symptoms and failure.')
    return
  }

  isSubmitting.value = true

  const session = (authClient as any).useSession?.()
  const currentUserName = session?.data?.value?.user?.name || 'Floor Operator'
  const currentUserId = session?.data?.value?.user?.id || 'usr-operator'

  const payload = {
    stationId: form.value.stationId || 'UNKNOWN-STATION',
    stationName: form.value.stationName || (form.value.stationId ? `Station ${form.value.stationId}` : 'Plant Floor Cell'),
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
    reportedByUserId: currentUserId,
    reportedByUserName: currentUserName,
    isLineStop: form.value.isLineStop,
    lineStopDurationMinutes: form.value.isLineStop ? Number(form.value.lineStopDurationMinutes || 0) : 0,
    responsibleDepartment: (form.value.responsibleDepartment as any) || undefined,
    issueType: form.value.issueType,
    originatorType: form.value.originatorType,
    externalOperatorId: form.value.externalOperatorId.trim() || undefined,
    externalOperatorName: form.value.externalOperatorName.trim() || undefined,
    attachments: attachments.value
  }

  try {
    if (typeof navigator !== 'undefined' && !navigator.onLine) {
      await queueOfflineTicket(payload)
      if (typeof localStorage !== 'undefined') localStorage.removeItem(DRAFT_KEY)
      toast.success('Incident saved offline. Will synchronize automatically when connected.')
      await fetchTickets()
      router.push('/dashboard/tickets')
      return
    }

    const res = await $fetch<{ success: boolean; ticket: any }>('/api/tickets', {
      method: 'POST',
      body: payload
    })

    if (res && res.success) {
      const ticketId = res.ticket?.id

      if (attachments.value.length > 0 && ticketId) {
        for (const att of attachments.value) {
          await $fetch(`/api/tickets/${ticketId}/attachments`, {
            method: 'POST',
            body: att
          }).catch(e => console.warn('Attachment upload failed:', e))
        }
      }

      if (typeof localStorage !== 'undefined') localStorage.removeItem(DRAFT_KEY)
      toast.success('Incident reported successfully')
      await fetchTickets()

      if (ticketId) {
        router.push(`/dashboard/tickets/${ticketId}`)
      } else {
        router.push('/dashboard/tickets')
      }
    } else {
      toast.error('Failed to submit incident ticket.')
    }
  } catch (err: any) {
    console.warn('Network request failed, queueing offline:', err)
    await queueOfflineTicket(payload)
    if (typeof localStorage !== 'undefined') localStorage.removeItem(DRAFT_KEY)
    toast.success('Offline mode: Incident queued for synchronization.')
    await fetchTickets()
    router.push('/dashboard/tickets')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6 pb-16">
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
          <BreadcrumbPage class="text-xs font-semibold">Report New Incident</BreadcrumbPage>
        </BreadcrumbItem>
      </BreadcrumbList>
    </Breadcrumb>

    <!-- Page Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-border">
      <div class="flex items-center gap-3">
        <NuxtLink to="/dashboard/tickets">
          <Button variant="ghost" size="icon" class="rounded-xl h-9 w-9 text-muted-foreground hover:text-foreground">
            <ArrowLeft class="size-4" />
          </Button>
        </NuxtLink>
        <div>
          <div class="flex items-center gap-2.5">
            <div class="p-2 rounded-xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20">
              <Wrench class="size-5" />
            </div>
            <h1 class="text-2xl font-bold tracking-tight text-foreground">
              Report Maintenance Incident
            </h1>
          </div>
          <p class="text-xs text-muted-foreground mt-1">
            Dispatch plant floor failure records, link Beckhoff PLC telemetry, and auto-route to on-duty specialists.
          </p>
        </div>
      </div>

      <!-- Header Actions -->
      <div class="flex items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          @click="showTemplateSelector = !showTemplateSelector"
          class="border-indigo-500/30 bg-indigo-500/10 text-indigo-600 dark:text-indigo-300 hover:bg-indigo-500/20 rounded-xl text-xs font-medium gap-1.5 h-8"
        >
          <Sparkles class="size-3.5" />
          <span>{{ showTemplateSelector ? 'Hide Templates' : '4-Tier Templates' }}</span>
          <ChevronDown v-if="!showTemplateSelector" class="size-3" />
          <ChevronUp v-else class="size-3" />
        </Button>

        <NuxtLink to="/dashboard/tickets/templates">
          <Button variant="outline" size="sm" class="border-border text-xs rounded-xl h-8">
            Catalog
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Restored Draft Notice Banner -->
    <div
      v-if="hasRestoredDraft"
      class="flex items-center justify-between p-3 bg-muted/40 border border-border rounded-xl text-xs"
    >
      <div class="flex items-center gap-2 text-foreground font-medium">
        <CheckCircle2 class="size-4 text-emerald-500 shrink-0" />
        <span>Restored unsubmitted draft from your local device session.</span>
      </div>
      <Button
        variant="ghost"
        size="sm"
        @click="clearDraft"
        class="h-7 text-xs text-muted-foreground hover:text-destructive gap-1"
      >
        <RotateCcw class="size-3" />
        <span>Discard Draft</span>
      </Button>
    </div>

    <!-- 4-Tier Template Inline Drawer / Accordion -->
    <Card v-if="showTemplateSelector" class="border-indigo-500/30 bg-indigo-500/5 shadow-xs animate-in fade-in duration-200">
      <CardHeader class="pb-3 pt-4 px-5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Sparkles class="size-4 text-indigo-600 dark:text-indigo-400" />
            <CardTitle class="text-sm font-bold text-indigo-950 dark:text-indigo-200">
              Standard Error Catalog (4-Level Hierarchy)
            </CardTitle>
          </div>
          <span class="text-[11px] text-muted-foreground">Select category, group, and code to auto-populate failure details</span>
        </div>
      </CardHeader>
      <CardContent class="px-5 pb-4 space-y-3">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div>
            <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">1. Category</label>
            <select
              v-model="selectedCategory"
              class="w-full h-8 rounded-lg bg-background border border-border text-xs text-foreground px-2 focus:outline-hidden"
            >
              <option v-for="c in CATEGORIES" :key="c" :value="c">{{ c }}</option>
            </select>
          </div>

          <div>
            <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">2. Error Group</label>
            <select
              v-model="selectedGroup"
              class="w-full h-8 rounded-lg bg-background border border-border text-xs text-foreground px-2 focus:outline-hidden"
            >
              <option v-for="g in availableGroups" :key="g" :value="g">{{ g }}</option>
            </select>
          </div>

          <div>
            <label class="block text-[10px] font-bold text-muted-foreground uppercase mb-1">3. Error Code & Template</label>
            <select
              v-model="selectedTemplateId"
              class="w-full h-8 rounded-lg bg-background border border-border text-xs text-foreground px-2 focus:outline-hidden"
            >
              <option v-for="t in availableTemplates" :key="t.id" :value="t.id">
                [{{ t.errorCode }}] {{ t.shortDescription }}
              </option>
            </select>
          </div>
        </div>

        <div class="flex justify-end gap-2 pt-1 border-t border-indigo-500/20">
          <Button
            size="sm"
            @click="applySelectedTemplate"
            class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold px-4 h-7 cursor-pointer"
          >
            Apply Template & Prefill
          </Button>
        </div>
      </CardContent>
    </Card>

    <!-- Inline QR Scanner Dynamic View -->
    <div v-if="showScanner" class="p-4 bg-card border border-border rounded-2xl shadow-lg animate-in fade-in duration-200">
      <div class="flex items-center justify-between pb-3 mb-3 border-b border-border">
        <div class="flex items-center gap-2">
          <Camera class="size-4 text-indigo-500" />
          <span class="text-xs font-bold text-foreground">Scan Equipment / Machine Action QR</span>
        </div>
        <Button variant="ghost" size="sm" @click="showScanner = false" class="size-7 p-0 rounded-lg">
          <X class="size-4" />
        </Button>
      </div>
      <QrScanner @scanned="onQrScanned" @close="showScanner = false" />
    </div>

    <!-- Main Creation Form Grid -->
    <form @submit.prevent="handleSubmit" class="grid grid-cols-1 lg:grid-cols-12 gap-6">
      
      <!-- Left Column: Core Incident Definition -->
      <div class="lg:col-span-8 space-y-6">

        <!-- 1. Equipment & Station Selection -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <div class="flex items-center gap-2">
              <Factory class="size-4 text-indigo-500" />
              <CardTitle class="text-sm font-bold">Equipment & Floor Target</CardTitle>
            </div>
            <CardDescription class="text-xs">
              Select or scan the affected station, machine cluster, and controller node.
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            <div>
              <label class="block text-[11px] font-semibold text-muted-foreground mb-1.5">
                Station / Machine Target <span class="text-rose-500">*</span>
              </label>
              <div class="flex gap-2">
                <MachineSearchCombobox
                  v-model="form.stationId"
                  v-model:station-name="form.stationName"
                  placeholder="Type machine ID, barcode, or search from discovered fleet..."
                  class="flex-1"
                  @select="onMachineSelected"
                />
                <Button
                  type="button"
                  variant="outline"
                  @click="showScanner = !showScanner"
                  class="border-indigo-500/30 bg-indigo-500/10 text-indigo-600 dark:text-indigo-300 hover:bg-indigo-500/20 rounded-xl text-xs font-semibold gap-1.5 shrink-0 h-9"
                >
                  <Camera class="size-4" />
                  <span>Scan QR</span>
                </Button>
              </div>
              <div v-if="form.stationName && form.stationName !== form.stationId" class="mt-1.5 text-xs text-muted-foreground flex items-center gap-1.5">
                <span class="text-indigo-600 dark:text-indigo-400 font-semibold">Identified:</span>
                <span class="text-foreground font-medium">{{ form.stationName }}</span>
                <span v-if="form.machineType" class="text-muted-foreground font-mono">({{ form.machineType }})</span>
              </div>
            </div>

            <!-- Controller & Line Sub-grid -->
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
              <div>
                <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                  Controller Node / Hostname
                </label>
                <Input
                  v-model="form.controllerId"
                  placeholder="e.g. CPC-OP10-CX"
                  class="h-8 text-xs font-mono bg-background"
                />
              </div>

              <div>
                <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                  Machine Type / Class
                </label>
                <Input
                  v-model="form.machineType"
                  placeholder="e.g. CNC Milling"
                  class="h-8 text-xs bg-background"
                />
              </div>

              <div>
                <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                  Line / Cluster Group
                </label>
                <Input
                  v-model="form.groupId"
                  placeholder="e.g. grp-line06"
                  class="h-8 text-xs font-mono bg-background"
                />
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- 2. Incident Summary & Failure Description -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <CardTitle class="text-sm font-bold">Failure Details & Observed Symptoms</CardTitle>
            <CardDescription class="text-xs">
              Clear title and detailed symptoms help technicians diagnose issues faster.
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            <div>
              <label class="block text-[11px] font-semibold text-muted-foreground mb-1.5">
                Incident Title <span class="text-rose-500">*</span>
              </label>
              <Input
                v-model="form.title"
                placeholder="e.g. Spindle Overheating & Abnormal Vibration on Axis 3"
                class="bg-background border-border rounded-xl text-xs font-medium text-foreground"
              />
            </div>

            <div>
              <label class="block text-[11px] font-semibold text-muted-foreground mb-1.5">
                Observed Symptoms & Fault Description <span class="text-rose-500">*</span>
              </label>
              <textarea
                v-model="form.description"
                rows="4"
                placeholder="Describe what occurred, any PLC error codes on the HMI, thermal readings, abnormal noise, or cycle interruption..."
                class="w-full bg-background border border-border rounded-xl p-3 text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:ring-1 focus:ring-primary"
              ></textarea>
            </div>

            <!-- SFC Batch Serial Tracking -->
            <div>
              <label class="block text-[11px] font-semibold text-muted-foreground mb-1.5 flex items-center gap-1.5">
                <Layers class="size-3.5 text-cyan-600 dark:text-cyan-400" />
                <span>Workpiece SFC Serial Number (Shop Floor Control)</span>
                <span class="text-[10px] text-muted-foreground font-normal">(Optional)</span>
              </label>
              <Input
                v-model="form.sfc"
                placeholder="e.g. SFC-BAT-20261015-0941 (Serial active at time of defect)"
                class="bg-background border-border rounded-xl text-xs font-mono text-cyan-700 dark:text-cyan-300"
              />
            </div>

            <!-- Tags -->
            <div>
              <label class="block text-[11px] font-semibold text-muted-foreground mb-1.5">
                Contextual Tags
              </label>
              <div class="flex flex-wrap gap-1.5 items-center">
                <Badge
                  v-for="(tag, idx) in form.tags"
                  :key="idx"
                  variant="outline"
                  class="text-xs py-0.5 px-2 font-mono gap-1 bg-muted/30"
                >
                  <span>{{ tag }}</span>
                  <button
                    type="button"
                    @click="form.tags.splice(idx, 1)"
                    class="text-muted-foreground hover:text-foreground"
                  >
                    <X class="size-3" />
                  </button>
                </Badge>
                <Input
                  placeholder="Type tag and press enter..."
                  class="w-48 h-7 text-xs bg-background"
                  @keydown.enter.prevent="(e) => {
                    const val = (e.target as HTMLInputElement).value.trim()
                    if (val && !form.tags.includes(val)) {
                      form.tags.push(val.startsWith('#') ? val : `#${val}`)
                      ;(e.target as HTMLInputElement).value = ''
                    }
                  }"
                />
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- 3. Photo & Diagnostic Attachments -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <div class="flex items-center gap-2">
              <Paperclip class="size-4 text-muted-foreground" />
              <CardTitle class="text-sm font-bold">Defect Photos & Attachments</CardTitle>
            </div>
            <CardDescription class="text-xs">
              Upload machine photos, error screen captures, or thermal camera frames.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ImageAttachmentUploader
              v-model="attachments"
              label="Upload machine or defect photos"
              :max-files="5"
            />
          </CardContent>
        </Card>
      </div>

      <!-- Right Column: Priority, Classification, Line Stop, Routing -->
      <div class="lg:col-span-4 space-y-6">

        <!-- 1. Priority Selector -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <CardTitle class="text-sm font-bold">Priority Level</CardTitle>
          </CardHeader>
          <CardContent class="space-y-2">
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="pr in [
                  { id: 'Low', label: 'Low', color: 'border-slate-500/20 bg-slate-500/10 text-slate-700 dark:text-slate-300' },
                  { id: 'Medium', label: 'Medium', color: 'border-blue-500/20 bg-blue-500/10 text-blue-700 dark:text-blue-400' },
                  { id: 'High', label: 'High', color: 'border-amber-500/30 bg-amber-500/10 text-amber-800 dark:text-amber-400' },
                  { id: 'Critical', label: 'Critical', color: 'border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-400 font-bold' }
                ]"
                :key="pr.id"
                type="button"
                @click="form.priority = pr.id as TicketPriority"
                :class="[
                  'p-2.5 rounded-xl border text-xs font-semibold text-center transition-all cursor-pointer',
                  form.priority === pr.id ? 'ring-2 ring-primary shadow-sm scale-102 font-bold' : 'opacity-70 hover:opacity-100',
                  pr.color
                ]"
              >
                {{ pr.label }}
              </button>
            </div>
          </CardContent>
        </Card>

        <!-- 2. Category Selector -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <CardTitle class="text-sm font-bold">Category</CardTitle>
          </CardHeader>
          <CardContent>
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="cat in CATEGORIES"
                :key="cat"
                type="button"
                @click="form.category = cat"
                :class="[
                  'py-2 px-3 rounded-xl border text-xs font-semibold text-center transition-all cursor-pointer',
                  form.category === cat
                    ? 'bg-primary text-primary-foreground border-primary shadow-xs font-bold'
                    : 'bg-muted/40 text-muted-foreground border-border hover:text-foreground'
                ]"
              >
                {{ cat }}
              </button>
            </div>
          </CardContent>
        </Card>

        <!-- 3. Line Stop & Active Plant Downtime -->
        <Card
          class="border-border bg-card shadow-xs transition-colors"
          :class="form.isLineStop ? 'border-rose-500/40 bg-rose-500/5' : ''"
        >
          <CardHeader class="pb-3">
            <div class="flex items-center justify-between">
              <label class="flex items-center gap-2 cursor-pointer font-bold text-xs">
                <input
                  type="checkbox"
                  v-model="form.isLineStop"
                  class="rounded size-4 text-rose-600 focus:ring-rose-500 cursor-pointer"
                />
                <span class="flex items-center gap-1.5 text-rose-600 dark:text-rose-400">
                  <Flame class="size-4" />
                  Line Stop (Active Downtime)
                </span>
              </label>
            </div>
          </CardHeader>
          <CardContent v-if="form.isLineStop" class="space-y-3 pt-0">
            <div class="flex items-center justify-between gap-2 p-2.5 rounded-xl bg-rose-500/10 border border-rose-500/20 text-xs">
              <span class="text-muted-foreground font-medium">Estimated Stoppage Duration:</span>
              <div class="flex items-center gap-1.5">
                <Input
                  v-model.number="form.lineStopDurationMinutes"
                  type="number"
                  min="0"
                  class="w-20 h-7 text-xs bg-background text-right font-mono"
                  placeholder="0"
                />
                <span class="text-muted-foreground font-mono">min</span>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- 4. Department & Attribution -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <CardTitle class="text-sm font-bold">Ownership & Department</CardTitle>
          </CardHeader>
          <CardContent class="space-y-3.5 text-xs">
            <div>
              <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                Responsible Department
              </label>
              <select
                v-model="form.responsibleDepartment"
                class="w-full px-2.5 py-1.5 bg-background border border-border rounded-xl text-xs text-foreground focus:outline-hidden"
              >
                <option value="">(Unassigned / General)</option>
                <option value="Assy">Assy (Assembly)</option>
                <option value="SMT">SMT (Surface Mount)</option>
                <option value="Test">Test (End of Line)</option>
                <option value="IT">IT Infrastructure</option>
                <option value="MES">MES Operations</option>
                <option value="SAP">SAP / ERP</option>
                <option value="ProcessEngineering">Process Engineering</option>
                <option value="ProductOwner">Product Owner</option>
                <option value="Robotics">Robotics & Automation</option>
                <option value="Vision">Machine Vision</option>
              </select>
            </div>

            <div>
              <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                Issue Type
              </label>
              <select
                v-model="form.issueType"
                class="w-full px-2.5 py-1.5 bg-background border border-border rounded-xl text-xs text-foreground focus:outline-hidden"
              >
                <option value="Maintenance">Maintenance</option>
                <option value="Transient">Transient</option>
                <option value="Improvement">Improvement</option>
                <option value="Other">Other</option>
              </select>
            </div>

            <div>
              <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                Originator Type
              </label>
              <select
                v-model="form.originatorType"
                class="w-full px-2.5 py-1.5 bg-background border border-border rounded-xl text-xs text-foreground focus:outline-hidden"
              >
                <option value="ManualUser">Manual User (Floor Operator)</option>
                <option value="MachineAutomatic">Machine Automatic (PLC/SCADA)</option>
                <option value="ScheduledMaintenance">Scheduled PM Routine</option>
              </select>
            </div>

            <!-- Optional External Operator -->
            <div class="grid grid-cols-2 gap-2 pt-1 border-t border-border">
              <div>
                <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                  Operator Badge ID
                </label>
                <Input
                  v-model="form.externalOperatorId"
                  placeholder="e.g. OP-8821"
                  class="h-7 text-xs bg-background"
                />
              </div>
              <div>
                <label class="block text-[10px] font-semibold text-muted-foreground uppercase mb-1">
                  Operator Name
                </label>
                <Input
                  v-model="form.externalOperatorName"
                  placeholder="e.g. John Doe"
                  class="h-7 text-xs bg-background"
                />
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- 5. Assigned Technician & Auto-Routing -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="pb-3">
            <CardTitle class="text-sm font-bold">Technician Assignment</CardTitle>
          </CardHeader>
          <CardContent class="space-y-3">
            <!-- Suggested Technician Recommendation -->
            <div
              v-if="suggestedTech"
              class="p-3 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-between"
            >
              <div class="flex items-center gap-2 min-w-0">
                <UserCheck class="size-4 text-indigo-600 dark:text-indigo-400 shrink-0" />
                <div class="min-w-0">
                  <div class="text-xs font-bold text-foreground truncate">
                    {{ suggestedTech.isAbsent ? (suggestedTech.backupTechnicianName || suggestedTech.technicianName) : suggestedTech.technicianName }}
                  </div>
                  <div class="text-[10px] text-muted-foreground">
                    Suggested via {{ suggestedTech.sourceLabel }}
                  </div>
                  <div v-if="suggestedTech.isAbsent" class="text-[10px] text-amber-600 dark:text-amber-400 font-medium">
                    Primary tech away. Rerouted to backup.
                  </div>
                </div>
              </div>
              <Button
                type="button"
                size="sm"
                @click="useSuggestedTechnician"
                class="h-7 text-xs px-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold cursor-pointer shrink-0"
              >
                Assign
              </Button>
            </div>

            <!-- Searchable Combobox for Technician -->
            <div>
              <SearchableTargetCombobox
                v-model="form.assignedTechnicianName"
                placeholder="Search candidate technician or type name..."
                category-label="Floor Technicians & Engineers"
                icon-type="user"
                :query-fn="queryCandidateTechnicians"
              />
            </div>
          </CardContent>
        </Card>

        <!-- Submit & Navigation Action Card -->
        <Card class="border-border bg-card shadow-sm">
          <CardContent class="pt-6 space-y-3">
            <Button
              type="submit"
              :disabled="isSubmitting"
              class="w-full h-10 text-xs font-bold uppercase tracking-wider bg-primary hover:bg-primary/90 text-primary-foreground rounded-xl flex items-center justify-center gap-2 cursor-pointer shadow-xs"
            >
              <Send class="size-4" />
              <span v-if="!isSubmitting">Dispatch Incident Ticket</span>
              <span v-else>Submitting...</span>
            </Button>

            <NuxtLink to="/dashboard/tickets" class="block w-full">
              <Button
                type="button"
                variant="ghost"
                class="w-full h-9 text-xs font-semibold text-muted-foreground hover:text-foreground rounded-xl"
              >
                Cancel & Return
              </Button>
            </NuxtLink>
          </CardContent>
        </Card>

      </div>
    </form>
  </div>
</template>
