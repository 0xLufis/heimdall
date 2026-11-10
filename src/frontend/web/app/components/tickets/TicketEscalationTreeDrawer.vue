<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  X,
  Search,
  Building2,
  Cpu,
  Wrench,
  ShieldAlert,
  Hash,
  Users,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  AlertTriangle,
  Clock,
  Send,
  Calendar,
  Share2,
  MessageSquare,
  Sparkles,
  ArrowRight,
  Info
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { useMaintenance } from '~/composables/useMaintenance'
import { authClient } from '~/utils/auth-client'
import { getAndonPriorityStyle } from '~/utils/andonColors'
import type { MaintenanceTicket, EscalationHandoverState } from '~/types/maintenance'

export interface EscalationTargetNode {
  id: string
  name: string
  type: 'person' | 'channel'
  role?: string
  department: string
  specialization?: string
  email?: string
  presence?: 'Available' | 'Busy' | 'Away' | 'OutOfOffice' | 'Offline'
  isOutOfOffice?: boolean
  oooReturnDate?: string
  oooDelegate?: string
  membersCount?: number
  channelPurpose?: string
  level?: number
}

export interface EscalationDepartmentGroup {
  id: string
  name: string
  iconName: 'command' | 'engineering' | 'mechanical' | 'safety'
  description: string
  targets: EscalationTargetNode[]
}

const props = defineProps<{
  open: boolean
  ticket: MaintenanceTicket | null
}>()

const emit = defineEmits<{
  'close': []
  'escalated': [ticket: MaintenanceTicket]
}>()

const { escalateTicket, fetchTickets } = useMaintenance()

// Current logged in user
// Subscribe once at setup; calling useSession() inside a computed re-subscribes on every evaluation.
const authSession = (authClient as any).useSession?.()
const currentUser = computed(() => authSession?.data?.value?.user?.name || 'On-Duty Specialist')

// Search query
const searchQuery = ref('')

// Collapsed state for groups
const collapsedGroups = ref<Record<string, boolean>>({
  'command': false,
  'engineering': false,
  'mechanical': false,
  'safety': false
})

function toggleGroup(groupId: string) {
  collapsedGroups.value[groupId] = !collapsedGroups.value[groupId]
}

// Master Escalation Hierarchy Directory
const departments: EscalationDepartmentGroup[] = [
  {
    id: 'command',
    name: 'Plant Operations & Command',
    iconName: 'command',
    description: 'Executive plant floor escalation & shift governance',
    targets: [
      {
        id: 'usr-manager-andras',
        name: 'András Molnár',
        type: 'person',
        role: 'Plant Operations Manager',
        department: 'Plant Operations & Command',
        specialization: 'Plant-wide Cluster Allocation & Executive Escalation',
        email: 'andras.manager@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 4
      },
      {
        id: 'usr-ferenc',
        name: 'Shift Leader Ferenc',
        type: 'person',
        role: 'Shift Operations Leader',
        department: 'Plant Operations & Command',
        specialization: 'Shift Operations, Manning & Line Allocation',
        email: 'ferenc.leader@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 3
      },
      {
        id: 'chan-incident-command',
        name: '#Incident-Command',
        type: 'channel',
        department: 'Plant Operations & Command',
        channelPurpose: 'High-Priority Incident Broadcast Channel (Floor incidents, critical stops)',
        membersCount: 14,
        email: 'incident-command@heimdall.plant'
      }
    ]
  },
  {
    id: 'engineering',
    name: 'Engineering & Controls (OT / PLC / Vision)',
    iconName: 'engineering',
    description: 'Automation, Beckhoff TwinCAT, Siemens S7, Cognex AOI specialists',
    targets: [
      {
        id: 'usr-sally',
        name: 'Engineer Sally',
        type: 'person',
        role: 'Lead Controls & CNC Specialist',
        department: 'Engineering & Controls',
        specialization: 'Milling Technology, CNC Tooling & TwinCAT 3',
        email: 'sally.milling@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 2
      },
      {
        id: 'usr-katalin',
        name: 'Katalin Nagy',
        type: 'person',
        role: 'Vision AI & Cognex Lead',
        department: 'Engineering & Controls',
        specialization: 'Automatic Optical Inspection, Cognex In-Sight & Camera Lighting',
        email: 'katalin.aoi@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 2
      },
      {
        id: 'usr-pap',
        name: 'Orsolya Pap',
        type: 'person',
        role: 'Testing & Firmware Diagnostics',
        department: 'Engineering & Controls',
        specialization: 'EOL Testing, Firmware Diagnostics & Sensor Calibration',
        email: 'orsolya.pap@heimdall.dev',
        presence: 'Away',
        isOutOfOffice: false,
        level: 2
      },
      {
        id: 'chan-controls-emergency',
        name: '#Controls-Emergency',
        type: 'channel',
        department: 'Engineering & Controls',
        channelPurpose: 'Instant PLC & Automation Emergency Triage Dispatch',
        membersCount: 8,
        email: 'controls-emergency@heimdall.plant'
      }
    ]
  },
  {
    id: 'mechanical',
    name: 'Mechanical Maintenance & Robotics',
    iconName: 'mechanical',
    description: 'Robotics, hydraulic actuators, tooling spindles and press fixtures',
    targets: [
      {
        id: 'usr-orwell',
        name: 'Engineer Orwell',
        type: 'person',
        role: 'Mechanical & Robotics Lead',
        department: 'Mechanical Maintenance & Robotics',
        specialization: 'KUKA Robotics, Servo Kinematics & Mechanical Gearing',
        email: 'orwell.audi@heimdall.dev',
        presence: 'OutOfOffice',
        isOutOfOffice: true,
        oooReturnDate: 'Oct 20, 07:00 AM',
        oooDelegate: 'Gábor Varga',
        level: 3
      },
      {
        id: 'usr-varga',
        name: 'Gábor Varga',
        type: 'person',
        role: 'Hydraulics & Dispensing Specialist',
        department: 'Mechanical Maintenance & Robotics',
        specialization: 'Hydraulic Seals, Dispensing Valves & Pressure Lines',
        email: 'gabor.varga@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 1
      },
      {
        id: 'usr-kovacs',
        name: 'István Kovács',
        type: 'person',
        role: 'Line 06 Mechanical Specialist',
        department: 'Mechanical Maintenance & Robotics',
        specialization: 'Bearing Replacement, High-Speed Spindles & Alignment',
        email: 'istvan.kovacs@heimdall.dev',
        presence: 'Busy',
        isOutOfOffice: false,
        level: 1
      },
      {
        id: 'usr-nemeth',
        name: 'Zoltán Németh',
        type: 'person',
        role: 'Robotics & Fasteners Specialist',
        department: 'Mechanical Maintenance & Robotics',
        specialization: 'Atlas Copco Fasteners, Torque Calibration & KUKA Servos',
        email: 'zoltan.nemeth@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 1
      },
      {
        id: 'usr-horvath',
        name: 'Bence Horváth',
        type: 'person',
        role: 'Pressing & Tooling Specialist',
        department: 'Mechanical Maintenance & Robotics',
        specialization: 'Hydraulic Presses, Die Tooling & Force Sensors',
        email: 'bence.horvath@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 1
      },
      {
        id: 'chan-mechanical-floor',
        name: '#Mechanical-Floor-Emergency',
        type: 'channel',
        department: 'Mechanical Maintenance & Robotics',
        channelPurpose: 'Shopfloor Mechanical Breakdowns & Emergency Tooling',
        membersCount: 12,
        email: 'mechanical-floor@heimdall.plant'
      }
    ]
  },
  {
    id: 'safety',
    name: 'Safety & EHS Critical Response',
    iconName: 'safety',
    description: 'EHS Duty officers, optical curtain interlocks, emergency stop lines',
    targets: [
      {
        id: 'usr-ehs-duty',
        name: 'Safety Duty Officer',
        type: 'person',
        role: 'EHS Critical Incident Lead',
        department: 'Safety & EHS Critical Response',
        specialization: 'Personnel Safety, Interlock Verification & Incident Reporting',
        email: 'ehs.duty@heimdall.dev',
        presence: 'Available',
        isOutOfOffice: false,
        level: 2
      },
      {
        id: 'chan-safety-stop',
        name: '#Safety-Critical-Stop',
        type: 'channel',
        department: 'Safety & EHS Critical Response',
        channelPurpose: 'Direct Emergency Pager Hook for Cat-4 E-Stop Trips',
        membersCount: 6,
        email: 'safety-stop@heimdall.plant'
      }
    ]
  }
]

// Filtered departments based on search query
const filteredDepartments = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return departments

  return departments
    .map(dept => {
      const matchingTargets = dept.targets.filter(target => {
        return (
          target.name.toLowerCase().includes(query) ||
          target.department.toLowerCase().includes(query) ||
          (target.role && target.role.toLowerCase().includes(query)) ||
          (target.specialization && target.specialization.toLowerCase().includes(query)) ||
          (target.channelPurpose && target.channelPurpose.toLowerCase().includes(query)) ||
          (target.email && target.email.toLowerCase().includes(query))
        )
      })

      if (matchingTargets.length > 0 || dept.name.toLowerCase().includes(query)) {
        return {
          ...dept,
          targets: matchingTargets.length > 0 ? matchingTargets : dept.targets
        }
      }
      return null
    })
    .filter((d): d is EscalationDepartmentGroup => d !== null)
})

// Auto-expand groups when user searches
watch(searchQuery, (newVal) => {
  if (newVal.trim()) {
    departments.forEach(d => {
      collapsedGroups.value[d.id] = false
    })
  }
})

// Selected Target State
const selectedTarget = ref<EscalationTargetNode | null>(null)

// Handover State: 'Notification' | 'HandOff' | 'ParallelWork'
const handoverState = ref<EscalationHandoverState>('Notification')

// Escalation Reason Draft
const escalationReason = ref('')
const isSubmitting = ref(false)

// Adaptive Card Preview Toggle
const showAdaptiveCardPreview = ref(false)

// Quick Template Reasons
const quickTemplates = [
  'PLC / TwinCAT 3 motion interlock unresponsive. Specialist online diagnostic required.',
  'Critical Safety Guard / Cat-4 E-Stop circuit tripped during automatic cycle.',
  'Spare Part Out of Stock. Triage needed from Lead for emergency vendor sourcing.',
  'Root cause unknown after 30m investigation. Specialist intervention requested.',
  'Cognex Vision AI defect false-reject rate exceeded 5% threshold on Line 06.'
]

function applyTemplate(template: string) {
  if (!escalationReason.value.trim()) {
    escalationReason.value = template
  } else {
    escalationReason.value = `${escalationReason.value.trim()}\n- ${template}`
  }
}

// Presence Helpers
function getPresenceBadge(presence?: string) {
  switch (presence) {
    case 'Available':
      return {
        label: 'Available',
        dotClass: 'bg-emerald-500 ring-2 ring-background',
        badgeClass: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30'
      }
    case 'Busy':
      return {
        label: 'Busy',
        dotClass: 'bg-rose-500 ring-2 ring-background',
        badgeClass: 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/30'
      }
    case 'Away':
      return {
        label: 'Away',
        dotClass: 'bg-amber-500 ring-2 ring-background',
        badgeClass: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30'
      }
    case 'OutOfOffice':
      return {
        label: 'Out of Office',
        dotClass: 'bg-purple-500 ring-2 ring-background',
        badgeClass: 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/30'
      }
    default:
      return {
        label: 'Offline',
        dotClass: 'bg-muted-foreground ring-2 ring-background',
        badgeClass: 'bg-muted text-muted-foreground border-border'
      }
  }
}

// Reset state when drawer opens with a ticket
watch(
  () => props.ticket,
  (t) => {
    if (t) {
      escalationReason.value = ''
      // Default to Controls Specialist or Plant Operations
      selectedTarget.value = departments[1].targets[0] // Engineer Sally
      handoverState.value = 'Notification'
      showAdaptiveCardPreview.value = false
    }
  },
  { immediate: true }
)

// Confirm Escalation Handler
async function handleDispatchEscalation() {
  if (!props.ticket) return
  if (!selectedTarget.value) {
    toast.error('Please select an escalation recipient or broadcast channel.')
    return
  }
  if (!escalationReason.value.trim()) {
    toast.error('Please specify an escalation justification or select a quick template.')
    return
  }

  isSubmitting.value = true
  const targetLabel = selectedTarget.value.type === 'channel'
    ? selectedTarget.value.name
    : `${selectedTarget.value.name} (${selectedTarget.value.role || selectedTarget.value.department})`

  try {
    const updated = await escalateTicket(
      props.ticket.id,
      escalationReason.value.trim(),
      currentUser.value,
      handoverState.value,
      targetLabel
    )

    toast.success(`Escalated to ${targetLabel} (${handoverState.value} protocol).`, {
      description: `Incident #${props.ticket.ticketNumber} flagged for priority response.`
    })

    if (updated) {
      emit('escalated', updated as MaintenanceTicket)
    } else {
      emit('escalated', {
        ...props.ticket,
        isEscalated: true,
        escalationReason: escalationReason.value.trim(),
        escalationTarget: targetLabel,
        escalationHandoverState: handoverState.value,
        escalatedBy: currentUser.value,
        escalatedAt: new Date().toISOString()
      })
    }

    await fetchTickets()
    emit('close')
  } catch (err: any) {
    toast.error('Failed to dispatch escalation: ' + (err.message || 'Network error'))
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div
    v-if="open && ticket"
    class="fixed inset-y-0 right-0 z-40 w-full max-w-xl bg-card border-l border-border shadow-2xl flex flex-col animate-in slide-in-from-right duration-200"
    role="dialog"
    aria-label="Escalation Hierarchy Tree Dispatcher"
  >
    <!-- Top Header -->
    <div class="px-5 py-4 border-b border-border bg-gradient-to-r from-primary/10 via-primary/5 to-transparent flex items-center justify-between gap-3 shrink-0">
      <div class="flex items-center gap-3 min-w-0">
        <div class="size-9 rounded-xl bg-primary text-primary-foreground flex items-center justify-center shadow-xs shrink-0">
          <MessageSquare class="size-5" />
        </div>
        <div class="min-w-0">
          <div class="flex items-center gap-2">
            <h3 class="text-sm font-bold text-foreground truncate">Escalation Dispatcher</h3>
            <Badge class="bg-primary/10 text-primary border-primary/20 text-[10px] font-bold uppercase tracking-wider">
              Tree Hierarchy
            </Badge>
          </div>
          <div class="flex items-center gap-2 text-xs text-muted-foreground mt-0.5 truncate">
            <span class="font-mono font-bold text-foreground">{{ ticket.ticketNumber }}</span>
            <span>&bull;</span>
            <span class="truncate">{{ ticket.stationName || ticket.stationId || 'Production Cell' }}</span>
            <span>&bull;</span>
            <span
              class="font-bold text-[10px] uppercase px-1.5 py-0.2 rounded-md"
              :class="getAndonPriorityStyle(ticket.priority).badgeClass"
            >
              {{ ticket.priority }}
            </span>
          </div>
        </div>
      </div>

      <Button
        variant="ghost"
        size="icon"
        class="size-8 rounded-lg text-muted-foreground hover:text-foreground shrink-0 cursor-pointer"
        @click="emit('close')"
        title="Close Escalation Dispatcher"
      >
        <X class="size-4" />
      </Button>
    </div>

    <!-- Scrollable Center Body -->
    <div class="flex-1 overflow-y-auto p-5 space-y-5 text-xs">
      <!-- Search Filter Bar -->
      <div class="space-y-1.5">
        <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground flex items-center justify-between">
          <span>Search Escalation Directory</span>
          <span class="font-normal lowercase text-[10px]">people &bull; channels &bull; disciplines</span>
        </label>
        <div class="relative">
          <Search class="size-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
          <input
            v-model="searchQuery"
            type="text"
            placeholder="Filter by name, role, technology (e.g. TwinCAT, KUKA, AOI) or #channel..."
            class="w-full h-9 pl-9 pr-8 bg-background border border-border rounded-xl text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:ring-2 focus:ring-primary/50 transition-all"
          />
          <button
            v-if="searchQuery"
            type="button"
            @click="searchQuery = ''"
            class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground p-0.5 rounded cursor-pointer"
          >
            <X class="size-3.5" />
          </button>
        </div>
      </div>

      <!-- Interactive Hierarchy Tree Structure -->
      <div class="space-y-2">
        <div class="flex items-center justify-between text-[10px] font-bold uppercase tracking-wider text-muted-foreground px-1">
          <span>Organization Tree & On-Call Channels</span>
          <span>Click to Select Target</span>
        </div>

        <div class="space-y-2 border border-border rounded-2xl bg-muted/10 p-2">
          <!-- Department Groups -->
          <div
            v-for="dept in filteredDepartments"
            :key="dept.id"
            class="border border-border/70 rounded-xl bg-card overflow-hidden shadow-xs transition-all"
          >
            <!-- Group Header Accordion -->
            <button
              type="button"
              @click="toggleGroup(dept.id)"
              class="w-full px-3 py-2.5 bg-muted/30 hover:bg-muted/50 flex items-center justify-between text-left transition-colors cursor-pointer select-none"
            >
              <div class="flex items-center gap-2.5 min-w-0">
                <ChevronDown
                  v-if="!collapsedGroups[dept.id]"
                  class="size-4 text-muted-foreground shrink-0 transition-transform duration-200"
                />
                <ChevronRight
                  v-else
                  class="size-4 text-muted-foreground shrink-0 transition-transform duration-200"
                />

                <Building2 v-if="dept.iconName === 'command'" class="size-4 text-indigo-500 shrink-0" />
                <Cpu v-else-if="dept.iconName === 'engineering'" class="size-4 text-blue-500 shrink-0" />
                <Wrench v-else-if="dept.iconName === 'mechanical'" class="size-4 text-amber-500 shrink-0" />
                <ShieldAlert v-else class="size-4 text-rose-500 shrink-0" />

                <div class="min-w-0">
                  <div class="font-bold text-foreground text-xs truncate">{{ dept.name }}</div>
                  <div class="text-[10px] text-muted-foreground truncate">{{ dept.description }}</div>
                </div>
              </div>

              <Badge variant="outline" class="text-[10px] font-mono shrink-0 ml-2 border-border">
                {{ dept.targets.length }} targets
              </Badge>
            </button>

            <!-- Group Children Nodes -->
            <div
              v-show="!collapsedGroups[dept.id]"
              class="p-2 space-y-1.5 border-t border-border/50 bg-background/50"
            >
              <div
                v-for="target in dept.targets"
                :key="target.id"
                @click="selectedTarget = target"
                class="group relative flex items-start justify-between gap-3 p-2.5 rounded-xl border transition-all cursor-pointer select-none"
                :class="[
                  selectedTarget?.id === target.id
                    ? 'border-primary bg-primary/10 ring-1 ring-primary shadow-xs'
                    : 'border-border/60 hover:border-border hover:bg-muted/30'
                ]"
              >
                <!-- Left Details -->
                <div class="flex items-start gap-2.5 min-w-0">
                  <!-- Person Avatar with Presence Dot -->
                  <div v-if="target.type === 'person'" class="relative shrink-0 mt-0.5">
                    <div class="size-8 rounded-full bg-indigo-500/10 text-indigo-600 dark:text-indigo-300 font-bold flex items-center justify-center text-xs border border-indigo-500/20">
                      {{ target.name.split(' ').map(n => n[0]).join('').slice(0, 2) }}
                    </div>
                    <!-- Presence Dot -->
                    <span
                      class="absolute -bottom-0.5 -right-0.5 size-3 rounded-full border-2 border-card"
                      :class="getPresenceBadge(target.presence).dotClass"
                      :title="getPresenceBadge(target.presence).label"
                    />
                  </div>

                  <!-- Channel Icon (#) -->
                  <div
                    v-else
                    class="size-8 rounded-xl bg-violet-500/15 text-violet-600 dark:text-violet-400 font-black flex items-center justify-center text-sm shrink-0 border border-violet-500/20"
                  >
                    <Hash class="size-4" />
                  </div>

                  <!-- Details -->
                  <div class="min-w-0 space-y-0.5">
                    <div class="flex items-center gap-1.5 flex-wrap">
                      <span class="font-bold text-foreground text-xs leading-none">
                        {{ target.name }}
                      </span>

                      <!-- Presence Pill for People -->
                      <span
                        v-if="target.type === 'person'"
                        class="text-[9px] font-bold uppercase px-1.5 py-0.2 rounded-md border"
                        :class="getPresenceBadge(target.presence).badgeClass"
                      >
                        {{ getPresenceBadge(target.presence).label }}
                      </span>

                      <!-- Channel Tag -->
                      <span
                        v-else
                        class="text-[9px] font-bold uppercase px-1.5 py-0.2 rounded-md bg-violet-500/10 text-violet-700 dark:text-violet-400 border border-violet-500/30 flex items-center gap-1"
                      >
                        <Users class="size-2.5" />
                        <span>{{ target.membersCount }} members</span>
                      </span>

                      <span
                        v-if="target.level"
                        class="text-[9px] font-mono text-muted-foreground px-1 rounded bg-muted/60"
                      >
                        L{{ target.level }}
                      </span>
                    </div>

                    <!-- Role or Channel Purpose -->
                    <p class="text-[11px] text-muted-foreground truncate">
                      {{ target.role || target.channelPurpose }}
                    </p>

                    <!-- Specialization / OOO Alert -->
                    <div v-if="target.specialization" class="text-[10px] text-foreground/80 truncate font-medium">
                      {{ target.specialization }}
                    </div>

                    <!-- Out Of Office Warning Banner -->
                    <div
                      v-if="target.isOutOfOffice"
                      class="mt-1 px-2 py-0.5 rounded-md bg-purple-500/10 border border-purple-500/30 text-[10px] text-purple-700 dark:text-purple-300 font-semibold flex items-center gap-1.5"
                    >
                      <Calendar class="size-3 shrink-0" />
                      <span>OOO until {{ target.oooReturnDate }} &bull; Delegate: <strong>{{ target.oooDelegate }}</strong></span>
                    </div>
                  </div>
                </div>

                <!-- Right Radio Check Indicator -->
                <div class="shrink-0 mt-1">
                  <div
                    class="size-5 rounded-full border flex items-center justify-center transition-colors"
                    :class="[
                      selectedTarget?.id === target.id
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-muted-foreground/40 group-hover:border-foreground/60'
                    ]"
                  >
                    <CheckCircle2 v-if="selectedTarget?.id === target.id" class="size-3.5 stroke-[3]" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Selected Target Confirmation Card -->
      <div
        v-if="selectedTarget"
        class="p-3.5 rounded-2xl bg-primary/5 border border-primary/20 space-y-2"
      >
        <div class="flex items-center justify-between text-[10px] font-bold uppercase tracking-wider text-primary">
          <span class="flex items-center gap-1.5">
            <CheckCircle2 class="size-3.5" />
            <span>Target Recipient Selected</span>
          </span>
          <button
            type="button"
            @click="selectedTarget = null"
            class="text-[10px] underline hover:text-foreground cursor-pointer"
          >
            Change
          </button>
        </div>

        <div class="flex items-center justify-between gap-3">
          <div class="flex items-center gap-2.5 min-w-0">
            <div
              v-if="selectedTarget.type === 'person'"
              class="size-9 rounded-full bg-primary text-primary-foreground font-bold flex items-center justify-center text-xs shrink-0"
            >
              {{ selectedTarget.name.split(' ').map(n => n[0]).join('').slice(0, 2) }}
            </div>
            <div
              v-else
              class="size-9 rounded-xl bg-violet-600 text-white font-black flex items-center justify-center text-sm shrink-0"
            >
              <Hash class="size-4" />
            </div>

            <div class="min-w-0">
              <div class="font-bold text-foreground text-xs truncate">{{ selectedTarget.name }}</div>
              <div class="text-[11px] text-muted-foreground truncate">{{ selectedTarget.role || selectedTarget.channelPurpose }}</div>
              <div class="text-[10px] font-mono text-muted-foreground">{{ selectedTarget.email }}</div>
            </div>
          </div>

          <Badge
            v-if="selectedTarget.presence"
            variant="outline"
            class="text-[10px] font-bold shrink-0"
            :class="getPresenceBadge(selectedTarget.presence).badgeClass"
          >
            {{ selectedTarget.presence }}
          </Badge>
        </div>

        <div
          v-if="selectedTarget.isOutOfOffice"
          class="p-2 rounded-xl bg-purple-500/15 border border-purple-500/30 text-[11px] text-purple-800 dark:text-purple-200"
        >
          <strong>Notice:</strong> {{ selectedTarget.name }} is currently Out of Office. The dispatch notification will also automatically alert designated backup <strong>{{ selectedTarget.oooDelegate }}</strong>.
        </div>
      </div>

      <!-- Handover Protocol (Escalation Handover State) -->
      <div class="space-y-2">
        <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground block">
          Handover Protocol & Ownership
        </label>
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-2">
          <!-- Notification Option -->
          <div
            @click="handoverState = 'Notification'"
            class="p-2.5 rounded-xl border text-left transition-all cursor-pointer flex flex-col justify-between gap-1 select-none"
            :class="[
              handoverState === 'Notification'
                ? 'border-indigo-500 bg-indigo-500/10 ring-1 ring-indigo-500 shadow-xs'
                : 'border-border bg-card hover:bg-muted/30'
            ]"
          >
            <div class="flex items-center justify-between">
              <span class="font-bold text-xs text-foreground flex items-center gap-1.5">
                <Share2 class="size-3.5 text-indigo-500" />
                <span>Notification</span>
              </span>
              <div
                class="size-3.5 rounded-full border flex items-center justify-center shrink-0"
                :class="handoverState === 'Notification' ? 'border-indigo-500 bg-indigo-500' : 'border-muted-foreground/40'"
              >
                <div v-if="handoverState === 'Notification'" class="size-1.5 rounded-full bg-white" />
              </div>
            </div>
            <p class="text-[10px] text-muted-foreground leading-snug">
              Alert only. Maintain current technician as primary investigator.
            </p>
          </div>

          <!-- Complete HandOff Option -->
          <div
            @click="handoverState = 'HandOff'"
            class="p-2.5 rounded-xl border text-left transition-all cursor-pointer flex flex-col justify-between gap-1 select-none"
            :class="[
              handoverState === 'HandOff'
                ? 'border-rose-500 bg-rose-500/10 ring-1 ring-rose-500 shadow-xs'
                : 'border-border bg-card hover:bg-muted/30'
            ]"
          >
            <div class="flex items-center justify-between">
              <span class="font-bold text-xs text-foreground flex items-center gap-1.5">
                <ArrowRight class="size-3.5 text-rose-500" />
                <span>Hand-Off</span>
              </span>
              <div
                class="size-3.5 rounded-full border flex items-center justify-center shrink-0"
                :class="handoverState === 'HandOff' ? 'border-rose-500 bg-rose-500' : 'border-muted-foreground/40'"
              >
                <div v-if="handoverState === 'HandOff'" class="size-1.5 rounded-full bg-white" />
              </div>
            </div>
            <p class="text-[10px] text-muted-foreground leading-snug">
              Transfer ticket ownership and triage directly to target.
            </p>
          </div>

          <!-- Parallel Work Option -->
          <div
            @click="handoverState = 'ParallelWork'"
            class="p-2.5 rounded-xl border text-left transition-all cursor-pointer flex flex-col justify-between gap-1 select-none"
            :class="[
              handoverState === 'ParallelWork'
                ? 'border-blue-500 bg-blue-500/10 ring-1 ring-blue-500 shadow-xs'
                : 'border-border bg-card hover:bg-muted/30'
            ]"
          >
            <div class="flex items-center justify-between">
              <span class="font-bold text-xs text-foreground flex items-center gap-1.5">
                <Users class="size-3.5 text-blue-500" />
                <span>Parallel Work</span>
              </span>
              <div
                class="size-3.5 rounded-full border flex items-center justify-center shrink-0"
                :class="handoverState === 'ParallelWork' ? 'border-blue-500 bg-blue-500' : 'border-muted-foreground/40'"
              >
                <div v-if="handoverState === 'ParallelWork'" class="size-1.5 rounded-full bg-white" />
              </div>
            </div>
            <p class="text-[10px] text-muted-foreground leading-snug">
              Establish dual investigation bridge across incident channels.
            </p>
          </div>
        </div>
      </div>

      <!-- Escalation Reason & 1-Click Templates -->
      <div class="space-y-2">
        <div class="flex items-center justify-between">
          <label class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
            Escalation Justification & Symptoms *
          </label>
          <span class="text-[10px] text-muted-foreground">Required</span>
        </div>

        <!-- 1-Click Quick Template Pills -->
        <div class="space-y-1">
          <div class="text-[10px] text-muted-foreground flex items-center gap-1">
            <Sparkles class="size-3 text-amber-500" />
            <span>1-Click Presets:</span>
          </div>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="(tpl, idx) in quickTemplates"
              :key="idx"
              type="button"
              @click="applyTemplate(tpl)"
              class="px-2 py-1 rounded-lg bg-muted/60 hover:bg-muted text-[10px] text-foreground border border-border/80 transition-colors cursor-pointer text-left leading-tight"
            >
              {{ tpl.slice(0, 38) }}...
            </button>
          </div>
        </div>

        <textarea
          v-model="escalationReason"
          rows="3"
          placeholder="Specify exact symptoms, failure mode, or PLC block requiring specialist access..."
          class="w-full px-3 py-2 bg-background border border-border rounded-xl text-xs text-foreground placeholder:text-muted-foreground focus:outline-hidden focus:ring-2 focus:ring-primary/50 resize-none transition-all"
        />
      </div>

      <!-- Incident Dispatch Card Preview Accordion -->
      <div class="border border-border rounded-xl overflow-hidden">
        <button
          type="button"
          @click="showAdaptiveCardPreview = !showAdaptiveCardPreview"
          class="w-full px-3 py-2 bg-muted/20 hover:bg-muted/40 flex items-center justify-between text-left transition-colors cursor-pointer"
        >
          <span class="flex items-center gap-2 font-bold text-xs text-foreground">
            <MessageSquare class="size-3.5 text-primary" />
            <span>Incident Dispatch Card Preview</span>
          </span>
          <span class="text-[10px] font-mono text-muted-foreground flex items-center gap-1">
            {{ showAdaptiveCardPreview ? 'Hide Preview' : 'Show Card Preview' }}
            <ChevronDown v-if="!showAdaptiveCardPreview" class="size-3.5" />
            <ChevronRight v-else class="size-3.5" />
          </span>
        </button>

        <div v-show="showAdaptiveCardPreview" class="p-3 bg-muted/5 border-t border-border">
          <!-- Incident Dispatch Card Mockup -->
          <div class="max-w-md mx-auto rounded-xl border border-border bg-card shadow-md p-4 space-y-3 font-sans">
            <!-- Dispatch Header -->
            <div class="flex items-center justify-between border-b border-border/60 pb-2">
              <div class="flex items-center gap-2">
                <div class="size-5 rounded-md bg-primary text-primary-foreground flex items-center justify-center text-[10px] font-bold">
                  H
                </div>
                <div>
                  <div class="font-bold text-[11px] text-foreground leading-none">Heimdall Incident Command</div>
                  <div class="text-[9px] text-muted-foreground">Automated Escalation Dispatcher &bull; Priority Broadcast</div>
                </div>
              </div>
              <span class="text-[9px] text-muted-foreground font-mono">Just now</span>
            </div>

            <!-- Card Body -->
            <div class="space-y-2">
              <div class="flex items-center gap-2">
                <span class="size-2 rounded-full bg-rose-500 animate-pulse" />
                <h4 class="font-bold text-xs text-rose-600 dark:text-rose-400">
                  Priority Escalation &bull; {{ ticket.ticketNumber }}
                </h4>
              </div>

              <div class="font-bold text-xs text-foreground">
                {{ ticket.title }}
              </div>

              <!-- Key Value Table -->
              <div class="grid grid-cols-2 gap-1.5 text-[10px] bg-muted/30 p-2 rounded-lg border border-border/60">
                <div>
                  <span class="text-muted-foreground block text-[9px] uppercase">Target Recipient</span>
                  <span class="font-semibold text-foreground">
                    {{ selectedTarget?.name || 'Unselected' }}
                  </span>
                </div>
                <div>
                  <span class="text-muted-foreground block text-[9px] uppercase">Protocol</span>
                  <span class="font-semibold text-foreground">{{ handoverState }}</span>
                </div>
                <div>
                  <span class="text-muted-foreground block text-[9px] uppercase">Station / Cell</span>
                  <span class="font-semibold text-foreground">{{ ticket.stationName || ticket.stationId || 'Production' }}</span>
                </div>
                <div>
                  <span class="text-muted-foreground block text-[9px] uppercase">Escalated By</span>
                  <span class="font-semibold text-foreground">{{ currentUser }}</span>
                </div>
              </div>

              <div class="text-[11px] text-muted-foreground bg-background p-2 rounded-lg border border-border/60">
                <span class="font-bold text-foreground block text-[10px] uppercase mb-0.5">Reason:</span>
                <p class="italic text-foreground">
                  "{{ escalationReason || 'Specialist intervention requested.' }}"
                </p>
              </div>

              <!-- Card Action Buttons -->
              <div class="flex items-center gap-2 pt-1">
                <div class="flex-1 py-1.5 text-center bg-primary text-primary-foreground rounded-lg font-bold text-[10px] shadow-xs cursor-default">
                  Acknowledge & Join Bridge
                </div>
                <div class="flex-1 py-1.5 text-center bg-muted border border-border text-foreground rounded-lg font-bold text-[10px] cursor-default">
                  View in Heimdall
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Action Footer (Non-blocking) -->
    <div class="p-4 border-t border-border bg-muted/20 flex items-center justify-between gap-3 shrink-0">
      <div class="flex items-center gap-2 text-xs text-muted-foreground min-w-0">
        <Info class="size-4 shrink-0 text-indigo-500" />
        <span class="truncate">
          Notification will be dispatched to <strong>{{ selectedTarget?.name || 'Selected Target' }}</strong>
        </span>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <Button
          variant="outline"
          size="sm"
          class="text-xs border-border cursor-pointer"
          @click="emit('close')"
        >
          Cancel
        </Button>
        <Button
          size="sm"
          :disabled="!selectedTarget || !escalationReason.trim() || isSubmitting"
          class="bg-primary hover:bg-primary/90 text-primary-foreground font-bold text-xs px-4 cursor-pointer shadow-xs disabled:opacity-50 gap-1.5"
          @click="handleDispatchEscalation"
        >
          <Send class="size-3.5" />
          <span>{{ isSubmitting ? 'Dispatching...' : 'Dispatch Escalation' }}</span>
        </Button>
      </div>
    </div>
  </div>
</template>
