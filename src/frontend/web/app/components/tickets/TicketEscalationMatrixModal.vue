<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  AlertTriangle,
  ShieldAlert,
  UserCheck,
  User,
  Users,
  CheckCircle2,
  X,
  ChevronRight,
  ArrowRight,
  Sparkles,
  Layers,
  Wrench,
  Building2,
  Clock,
  Radio
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { resolvePreferredTechnician } from '~/utils/technicianInheritance'
import { useAuthSession } from '~/composables/useAuthSession'

export interface EscalationMatrixResult {
  level: number
  levelTitle: string
  targetMode: 'auto_dedicated' | 'manual_override'
  targetName: string
  targetRole?: string
  handoverState: 'Notification' | 'HandOff' | 'ParallelWork'
  reason: string
  notes?: string
}

const props = defineProps<{
  ticket: any | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'escalate', result: EscalationMatrixResult): void
}>()

const { user } = useAuthSession()

// Escalation Levels in the Industrial Matrix
const ESCALATION_LEVELS = [
  {
    level: 1,
    title: 'Level 1: Shift Response',
    role: 'Shift Leader',
    slaMinutes: 15,
    description: 'Line micro-stops, repeatable alarms, operator cycle delays',
    badgeColor: 'border-cyan-500/30 bg-cyan-500/10 text-cyan-400'
  },
  {
    level: 2,
    title: 'Level 2: Dedicated Station Engineer',
    role: 'Automation & Hardware Specialist',
    slaMinutes: 30,
    description: 'Component malfunction, sensor calibration fault, tooling wear',
    badgeColor: 'border-indigo-500/30 bg-indigo-500/10 text-indigo-400'
  },
  {
    level: 3,
    title: 'Level 3: Plant Operations Management',
    role: 'Maintenance Head / Plant Manager',
    slaMinutes: 60,
    description: 'Safety envelope breach, stoppage > 1hr, high scrap rate',
    badgeColor: 'border-amber-500/30 bg-amber-500/10 text-amber-500'
  },
  {
    level: 4,
    title: 'Level 4: OEM / Vendor Specialist',
    role: 'External Field Specialist',
    slaMinutes: 120,
    description: 'Catastrophic hardware fault, vendor warranty requirement',
    badgeColor: 'border-rose-500/30 bg-rose-500/10 text-rose-400'
  }
]

// Modal State
const selectedLevel = ref(2)
const targetMode = ref<'auto_dedicated' | 'manual_override'>('auto_dedicated')
const manualOverrideTarget = ref('')
const handoverState = ref<'Notification' | 'HandOff' | 'ParallelWork'>('HandOff')
const reason = ref('')
const notes = ref('')
const isSubmitting = ref(false)

// Candidate technicians for manual override
const candidateTechnicians = ref<any[]>([
  { id: 'usr-sally', name: 'Engineer Sally', role: 'Automation Lead', department: 'Controls' },
  { id: 'usr-orwell', name: 'Engineer Orwell', role: 'Robotics Specialist', department: 'Automation' },
  { id: 'usr-istvan', name: 'István Kovács', role: 'Lead Electrical Tech', department: 'Electrical' },
  { id: 'usr-gabor', name: 'Gábor Varga', role: 'Senior Mechanical Tech', department: 'Mechanical' },
  { id: 'usr-ferenc', name: 'Shift Leader Ferenc', role: 'Shift Supervisor', department: 'Operations' },
  { id: 'usr-plant-mgr', name: 'Plant Manager Kovács', role: 'Plant Operations Head', department: 'Executive' }
])

// Auto-detected Dedicated Technician via inheritance
const autoDedicatedTarget = computed(() => {
  if (!props.ticket) return null

  // Check if ticket already resolved a preferred technician
  if (props.ticket.preferredTechnicianName) {
    return {
      name: props.ticket.preferredTechnicianName,
      role: 'Dedicated Station Engineer',
      source: 'Station Dedication'
    }
  }

  // Resolve via inheritance
  const stationId = props.ticket.machineId || props.ticket.stationId || props.ticket.stationName || ''
  const techType = props.ticket.machineType || props.ticket.category || 'Assembly'
  const groupId = props.ticket.groupId || props.ticket.line || 'Line 1'

  const resolved = resolvePreferredTechnician(stationId, techType, groupId, [], [])
  if (resolved?.technicianName) {
    return {
      name: resolved.technicianName,
      role: (resolved as any).role || 'Dedicated Automation Specialist',
      source: resolved.sourceLabel || (resolved as any).source || 'Station Dedication'
    }
  }

  return {
    name: 'Engineer Sally',
    role: 'Dedicated Automation Lead',
    source: 'Cell Default'
  }
})

// Effective Escalation Target
const effectiveTargetName = computed(() => {
  if (targetMode.value === 'auto_dedicated') {
    return autoDedicatedTarget.value?.name || 'Dedicated Station Engineer'
  }
  return manualOverrideTarget.value || 'Unassigned Override'
})

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    selectedLevel.value = 2
    targetMode.value = 'auto_dedicated'
    manualOverrideTarget.value = candidateTechnicians.value[0]?.name || ''
    handoverState.value = 'HandOff'
    reason.value = props.ticket?.title ? `Critical resolution required: ${props.ticket.title}` : ''
    notes.value = ''
  }
})

function handleConfirm() {
  if (!reason.value.trim()) return

  isSubmitting.value = true
  const levelObj = ESCALATION_LEVELS.find(l => l.level === selectedLevel.value) || ESCALATION_LEVELS[1]

  const result: EscalationMatrixResult = {
    level: selectedLevel.value,
    levelTitle: levelObj.title,
    targetMode: targetMode.value,
    targetName: effectiveTargetName.value,
    targetRole: levelObj.role,
    handoverState: handoverState.value,
    reason: reason.value.trim(),
    notes: notes.value.trim() || undefined
  }

  emit('escalate', result)
  isSubmitting.value = false
  emit('update:open', false)
}
</script>

<template>
  <div
    v-if="open && ticket"
    class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 animate-in fade-in"
    @click.self="emit('update:open', false)"
  >
    <div class="bg-card border border-border rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-5 max-h-[90vh] overflow-y-auto">
      <!-- Modal Header -->
      <div class="flex items-start justify-between border-b border-border pb-4">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-500">
            <ShieldAlert class="size-6" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h3 class="text-lg font-bold text-foreground">Incident Escalation Matrix</h3>
              <Badge variant="outline" class="text-[10px] uppercase font-mono border-rose-500/30 bg-rose-500/10 text-rose-400">
                Priority Tiering
              </Badge>
            </div>
            <p class="text-xs text-muted-foreground mt-0.5">
              Escalate <strong>{{ ticket.ticketNumber }}</strong> ({{ ticket.stationName || ticket.machineId || 'Cell' }}) according to standard operational escalation matrix.
            </p>
          </div>
        </div>

        <button
          type="button"
          @click="emit('update:open', false)"
          class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
        >
          <X class="size-4" />
        </button>
      </div>

      <!-- 1. ESCALATION MATRIX LEVEL SELECTION -->
      <div class="space-y-2">
        <label class="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
          <Layers class="size-3.5 text-primary" />
          <span>1. Select Escalation Level</span>
        </label>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
          <button
            type="button"
            v-for="lvl in ESCALATION_LEVELS"
            :key="lvl.level"
            @click="selectedLevel = lvl.level"
            class="p-3 rounded-xl border transition-all cursor-pointer flex flex-col justify-between gap-2 text-left"
            :class="selectedLevel === lvl.level
              ? 'border-primary ring-2 ring-primary/30 bg-primary/5 shadow-2xs'
              : 'border-border bg-card hover:border-primary/40 hover:bg-muted/20'"
          >
            <div>
              <div class="flex items-center justify-between">
                <span class="text-xs font-bold text-foreground">{{ lvl.title }}</span>
                <span class="text-[10px] font-mono px-1.5 py-0.2 rounded border" :class="lvl.badgeColor">
                  SLA: {{ lvl.slaMinutes }}m
                </span>
              </div>
              <p class="text-[11px] text-muted-foreground mt-1 line-clamp-2">
                {{ lvl.description }}
              </p>
            </div>
            <div class="text-[10px] text-primary font-medium flex items-center gap-1">
              <UserCheck class="size-3" />
              <span>Role: {{ lvl.role }}</span>
            </div>
          </button>
        </div>
      </div>

      <!-- 2. TARGET RESOLUTION: AUTO-DEDICATED VS MANUAL OVERRIDE -->
      <div class="space-y-2.5 p-4 rounded-xl border border-border bg-muted/20">
        <label class="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center justify-between">
          <div class="flex items-center gap-1.5">
            <UserCheck class="size-3.5 text-indigo-500" />
            <span>2. Escalation Target Resolution</span>
          </div>
        </label>

        <!-- Mode Toggle -->
        <div class="flex items-center bg-card p-1 rounded-xl border border-border w-fit shadow-2xs">
          <button
            type="button"
            @click="targetMode = 'auto_dedicated'"
            class="px-3 py-1 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
            :class="targetMode === 'auto_dedicated' ? 'bg-indigo-600 text-white font-bold shadow-2xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Sparkles class="size-3" />
            <span>Auto Dedicated Target</span>
          </button>

          <button
            type="button"
            @click="targetMode = 'manual_override'"
            class="px-3 py-1 rounded-lg text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer"
            :class="targetMode === 'manual_override' ? 'bg-indigo-600 text-white font-bold shadow-2xs' : 'text-muted-foreground hover:text-foreground'"
          >
            <Wrench class="size-3" />
            <span>Manual Override</span>
          </button>
        </div>

        <!-- Auto-resolved Target Details -->
        <div v-if="targetMode === 'auto_dedicated'" class="p-3 rounded-lg border border-indigo-500/30 bg-indigo-500/5 flex items-center justify-between">
          <div class="flex items-center gap-2.5">
            <div class="size-8 rounded-full bg-indigo-500/20 text-indigo-400 font-bold flex items-center justify-center text-xs">
              {{ autoDedicatedTarget?.name.charAt(0) || 'E' }}
            </div>
            <div>
              <div class="text-xs font-bold text-foreground">{{ autoDedicatedTarget?.name }}</div>
              <div class="text-[11px] text-muted-foreground">{{ autoDedicatedTarget?.role }}</div>
            </div>
          </div>
          <Badge variant="outline" class="text-[10px] font-mono border-indigo-500/30 text-indigo-400">
            {{ autoDedicatedTarget?.source }}
          </Badge>
        </div>

        <!-- Manual Override Selector -->
        <div v-else class="space-y-1.5">
          <span class="text-[11px] text-muted-foreground">Select technician or lead to override auto-escalation target:</span>
          <select
            v-model="manualOverrideTarget"
            class="w-full h-9 rounded-xl bg-card border border-border text-foreground text-xs px-3 focus:ring-1 focus:ring-primary"
          >
            <option
              v-for="cand in candidateTechnicians"
              :key="cand.id"
              :value="cand.name"
            >
              {{ cand.name }} — {{ cand.role }} ({{ cand.department }})
            </option>
          </select>
        </div>
      </div>

      <!-- 3. HANDOVER PROTOCOL -->
      <div class="space-y-2">
        <label class="text-xs font-bold uppercase tracking-wider text-muted-foreground">
          3. Handover Protocol
        </label>
        <div class="grid grid-cols-3 gap-2">
          <button
            type="button"
            @click="handoverState = 'Notification'"
            class="p-2.5 rounded-xl border text-left transition-all cursor-pointer"
            :class="handoverState === 'Notification' ? 'border-primary bg-primary/10 text-primary font-bold' : 'border-border bg-card text-muted-foreground hover:text-foreground'"
          >
            <div class="text-xs">Notification Only</div>
            <div class="text-[10px] opacity-80 mt-0.5">Keep current owner, alert target</div>
          </button>

          <button
            type="button"
            @click="handoverState = 'HandOff'"
            class="p-2.5 rounded-xl border text-left transition-all cursor-pointer"
            :class="handoverState === 'HandOff' ? 'border-primary bg-primary/10 text-primary font-bold' : 'border-border bg-card text-muted-foreground hover:text-foreground'"
          >
            <div class="text-xs">Direct Hand-off</div>
            <div class="text-[10px] opacity-80 mt-0.5">Transfer ticket ownership to target</div>
          </button>

          <button
            type="button"
            @click="handoverState = 'ParallelWork'"
            class="p-2.5 rounded-xl border text-left transition-all cursor-pointer"
            :class="handoverState === 'ParallelWork' ? 'border-primary bg-primary/10 text-primary font-bold' : 'border-border bg-card text-muted-foreground hover:text-foreground'"
          >
            <div class="text-xs">Parallel Work</div>
            <div class="text-[10px] opacity-80 mt-0.5">Co-assigned collaboration</div>
          </button>
        </div>
      </div>

      <!-- 4. REASON & URGENCY NOTES -->
      <div class="space-y-3">
        <div>
          <label class="text-xs font-bold uppercase tracking-wider text-muted-foreground block mb-1">
            4. Escalation Reason *
          </label>
          <Input
            v-model="reason"
            placeholder="e.g. Robot axis 4 harmonic vibration exceeded 0.8g tolerance..."
            class="h-9 text-xs bg-card"
          />
        </div>

        <div>
          <label class="text-xs font-bold uppercase tracking-wider text-muted-foreground block mb-1">
            Additional Diagnostics & Context
          </label>
          <textarea
            v-model="notes"
            placeholder="Describe immediate actions taken, safety status, parts attempted..."
            class="w-full rounded-md border border-input bg-card px-3 py-2 text-xs ring-offset-background placeholder:text-muted-foreground focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 min-h-[60px] resize-none"
          />
        </div>
      </div>

      <!-- Modal Footer Actions -->
      <div class="flex items-center justify-between pt-3 border-t border-border">
        <div class="text-xs text-muted-foreground">
          Target: <strong class="text-foreground">{{ effectiveTargetName }}</strong>
        </div>

        <div class="flex items-center gap-2">
          <Button variant="ghost" size="sm" class="text-xs" @click="emit('update:open', false)">
            Cancel
          </Button>
          <Button
            size="sm"
            class="text-xs bg-rose-600 hover:bg-rose-700 text-white font-bold px-4 gap-1.5"
            :disabled="!reason.trim() || isSubmitting"
            @click="handleConfirm"
          >
            <ShieldAlert class="size-3.5" />
            <span>Confirm Escalation</span>
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
