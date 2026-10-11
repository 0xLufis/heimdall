<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import {
  Sparkles,
  X,
  Play,
  ShieldCheck,
  UserCheck,
  AlertOctagon,
  Clock,
  ArrowRight,
  CheckCircle2
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'

interface Props {
  open: boolean
  machines?: any[]
}

const props = withDefaults(defineProps<Props>(), {
  machines: () => []
})

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()

const internalMachines = ref<any[]>([])

const availableMachines = computed(() => {
  if (props.machines && props.machines.length > 0) {
    return props.machines
  }
  return internalMachines.value
})

const selectedMachineId = ref('')
const selectedDiscipline = ref('Controls')
const selectedSchedule = ref<'Shift' | 'OfficeTime' | 'OnCall'>('Shift')
const resolving = ref(false)
const result = ref<any>(null)

const handleKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Escape' && props.open) {
    close()
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
})

watch(
  () => props.open,
  async (isOpen) => {
    if (isOpen) {
      result.value = null
      if ((!props.machines || props.machines.length === 0) && internalMachines.value.length === 0) {
        try {
          const list = await $fetch<any[]>('/api/machines', {
            params: { includeDecommissioned: true }
          }).catch(() => [])
          if (Array.isArray(list) && list.length > 0) {
            internalMachines.value = list
          }
        } catch {
          // fallback
        }
      }
      const list = availableMachines.value
      if (list.length > 0 && !selectedMachineId.value) {
        selectedMachineId.value = list[0].id
      } else if (!selectedMachineId.value) {
        selectedMachineId.value = 'L02-OP070'
      }
    }
  }
)

const runResolution = async () => {
  if (!selectedMachineId.value) return
  resolving.value = true
  try {
    const res = await $fetch('/api/delegations/resolve', {
      params: {
        machineId: selectedMachineId.value,
        discipline: selectedDiscipline.value,
        scheduleType: selectedSchedule.value
      }
    })
    result.value = res
  } catch (err: any) {
    console.error('Error resolving delegation:', err)
  } finally {
    resolving.value = false
  }
}

const close = () => {
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open" @click.self="close" class="fixed inset-0 z-50 overflow-y-auto bg-background/80 backdrop-blur-xs flex items-center justify-center p-4">
    <div class="w-full max-w-xl bg-card border border-border shadow-2xl rounded-2xl overflow-hidden flex flex-col">
      <!-- Modal Header -->
      <div class="p-6 border-b border-border flex items-center justify-between bg-muted/20">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-primary/10 text-primary border border-primary/20">
            <Sparkles class="size-5" />
          </div>
          <div>
            <h2 class="text-base font-bold text-foreground">
              Delegation Recipe Router Simulator
            </h2>
            <p class="text-xs text-muted-foreground">
              Simulate responsibility routing across disciplines and worker schedules
            </p>
          </div>
        </div>
        <Button variant="ghost" size="icon" @click="close" class="cursor-pointer text-muted-foreground hover:text-foreground">
          <X class="size-4" />
        </Button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 space-y-5">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Target Machine</label>
            <select
              v-model="selectedMachineId"
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
            >
              <option v-for="m in availableMachines" :key="m.id" :value="m.id">
                {{ m.customIdentifier || m.name }}
              </option>
              <option v-if="availableMachines.length === 0" value="L02-OP070">
                L02-OP070 (Production Cell)
              </option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Discipline Context</label>
            <select
              v-model="selectedDiscipline"
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
            >
              <option value="Mechanical">Mechanical</option>
              <option value="Controls">Controls</option>
              <option value="Electrical">Electrical</option>
              <option value="Safety">Safety</option>
              <option value="Quality">Quality</option>
              <option value="General">General</option>
            </select>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium text-foreground">Schedule Context</label>
            <select
              v-model="selectedSchedule"
              class="w-full px-3 py-2 rounded-lg bg-background border border-border text-xs focus:outline-none focus:ring-1 focus:ring-primary"
            >
              <option value="Shift">Active Shift Roster</option>
              <option value="OfficeTime">Office-Time Workers</option>
              <option value="OnCall">On-Call Off-Hours</option>
            </select>
          </div>
        </div>

        <Button @click="runResolution" :disabled="resolving" class="w-full text-xs font-bold gap-2 cursor-pointer">
          <Play class="size-3.5" />
          <span>{{ resolving ? 'Evaluating Rules...' : 'Execute Delegation Resolution' }}</span>
        </Button>

        <!-- Simulated Outcome -->
        <div v-if="result" class="p-4 rounded-xl border border-primary/20 bg-primary/5 space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-primary uppercase flex items-center gap-1.5">
              <CheckCircle2 class="size-4 text-emerald-500" />
              Resolution Outcome
            </span>
            <Badge :variant="result.isOnCallFallback ? 'destructive' : 'default'" class="text-[10px]">
              {{ result.isOnCallFallback ? 'On-Call Fallback Triggered' : 'Shift Roster Assigned' }}
            </Badge>
          </div>

          <div class="p-3 rounded-lg bg-background/80 border border-border space-y-1.5">
            <div class="flex items-center justify-between text-xs">
              <span class="text-muted-foreground">Matching Recipe:</span>
              <span class="font-bold text-foreground">{{ result.recipeName }}</span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-muted-foreground">Effective Assignee:</span>
              <span class="font-mono font-bold text-primary text-sm">
                {{ result.assigneeName || result.assigneeId || 'Unassigned' }}
              </span>
            </div>
            <div v-if="result.groupName" class="flex items-center justify-between text-xs">
              <span class="text-muted-foreground">Assigned Crew:</span>
              <span class="text-foreground font-medium">{{ result.groupName }}</span>
            </div>
          </div>

          <div class="text-xs text-muted-foreground bg-muted/40 p-2.5 rounded-lg border border-border/50">
            <strong>Decision Rationale:</strong> {{ result.reason }}
          </div>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="p-4 border-t border-border bg-muted/10 flex justify-end">
        <Button variant="outline" size="sm" @click="close" class="cursor-pointer text-xs">
          Close
        </Button>
      </div>
    </div>
  </div>
</template>
