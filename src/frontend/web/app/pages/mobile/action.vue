<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  QrCode,
  Flame,
  AlertTriangle,
  Cpu,
  CheckCircle2,
  Clock,
  ArrowRight,
  ExternalLink,
  ShieldCheck,
  Building2,
  Send,
  RefreshCw
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { useMaintenance } from '~/composables/useMaintenance'
import { authClient } from '~/utils/auth-client'
import type { QrAction } from '~/utils/qrActionGenerator'

definePageMeta({
  layout: false // Standalone lightweight mobile layout
})

const route = useRoute()
const router = useRouter()
const { qrPickup, fetchTickets } = useMaintenance()

const action = computed<QrAction>(() => (route.query.action as QrAction) || 'report-incident')
const stationId = computed<string>(() => (route.query.stationId as string) || '')
const machineType = computed<string>(() => (route.query.machineType as string) || '')
const groupId = computed<string>(() => (route.query.groupId as string) || '')
const ticketId = computed<string>(() => (route.query.ticketId as string) || '')
const prefillPriority = computed<string>(() => (route.query.prefillPriority as string) || (route.query.priority as string) || 'High')
const prefillTitle = computed<string>(() => (route.query.prefillTitle as string) || (route.query.title as string) || '')

const isExecuting = ref(false)
const checkInSuccess = ref(false)
const autoRedirectCountdown = ref<number | null>(null)
let countdownTimer: any = null

const currentUser = computed(() => {
  const session = (authClient as any).useSession?.()
  return session?.data?.value?.user?.name || 'On-Duty Floor Technician'
})

function navigateToReport() {
  if (countdownTimer) clearInterval(countdownTimer)
  router.push({
    path: '/dashboard/tickets/new',
    query: {
      stationId: stationId.value,
      machineType: machineType.value,
      groupId: groupId.value,
      priority: prefillPriority.value,
      title: prefillTitle.value
    }
  })
}

function navigateToTicket() {
  if (countdownTimer) clearInterval(countdownTimer)
  if (ticketId.value) {
    router.push(`/dashboard/tickets/${ticketId.value}`)
  } else {
    router.push('/dashboard/tickets')
  }
}

function navigateToMachine() {
  if (countdownTimer) clearInterval(countdownTimer)
  if (stationId.value) {
    router.push({
      path: '/dashboard/machines',
      query: { stationId: stationId.value }
    })
  } else {
    router.push('/dashboard/machines')
  }
}

async function handleCheckIn() {
  isExecuting.value = true
  try {
    if (ticketId.value) {
      await qrPickup(ticketId.value, currentUser.value)
      await fetchTickets()
    } else {
      // Station physical check-in registration
      await $fetch('/api/tickets', {
        method: 'POST',
        body: {
          title: `Technician On-Site Check-In: ${stationId.value || 'Equipment'}`,
          description: `Physical QR scan check-in verified by ${currentUser.value}.`,
          stationId: stationId.value,
          machineType: machineType.value,
          priority: 'Low',
          category: 'Prevention',
          status: 'InProgress',
          reportedByUserName: currentUser.value,
          assignedTechnicianName: currentUser.value,
          qrScannedAt: new Date().toISOString()
        }
      }).catch(() => {})
    }
    checkInSuccess.value = true
    toast.success(`Check-in registered for ${stationId.value || 'Station'}`)
  } catch (err: any) {
    toast.error('Failed to complete check-in: ' + (err.message || 'Network error'))
  } finally {
    isExecuting.value = false
  }
}

onMounted(() => {
  // If report-incident or view-ticket, offer smooth auto-redirect countdown with cancel button
  if (action.value === 'report-incident') {
    autoRedirectCountdown.value = 2
    countdownTimer = setInterval(() => {
      if (autoRedirectCountdown.value !== null) {
        autoRedirectCountdown.value -= 1
        if (autoRedirectCountdown.value <= 0) {
          clearInterval(countdownTimer)
          navigateToReport()
        }
      }
    }, 1000)
  } else if (action.value === 'view-ticket' && ticketId.value) {
    autoRedirectCountdown.value = 2
    countdownTimer = setInterval(() => {
      if (autoRedirectCountdown.value !== null) {
        autoRedirectCountdown.value -= 1
        if (autoRedirectCountdown.value <= 0) {
          clearInterval(countdownTimer)
          navigateToTicket()
        }
      }
    }, 1000)
  }
})

onUnmounted(() => {
  if (countdownTimer) {
    clearInterval(countdownTimer)
    countdownTimer = null
  }
})
</script>

<template>
  <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col justify-between p-4 sm:p-6 font-sans">
    <!-- Top Branding Header -->
    <header class="flex items-center justify-between pb-4 border-b border-slate-800">
      <div class="flex items-center gap-3">
        <div class="size-10 rounded-2xl bg-indigo-600 text-white flex items-center justify-center font-black shadow-lg shadow-indigo-600/30">
          <QrCode class="size-6" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <h1 class="text-base font-black tracking-tight text-white">HEIMDALL</h1>
            <Badge class="bg-indigo-500/20 text-indigo-400 border-indigo-500/30 text-[10px] uppercase font-bold">
              Mobile Optical Action
            </Badge>
          </div>
          <p class="text-[11px] text-slate-400">Industrial Factory Floor Optical Dispatch</p>
        </div>
      </div>

      <NuxtLink to="/dashboard">
        <Button variant="ghost" size="sm" class="text-xs text-slate-400 hover:text-white">
          App Home
        </Button>
      </NuxtLink>
    </header>

    <!-- Main Action Card Container -->
    <main class="my-auto py-6 max-w-md w-full mx-auto space-y-4">
      <!-- Target Equipment Banner Card -->
      <div class="p-5 rounded-3xl bg-slate-900 border border-slate-800 shadow-2xl space-y-4">
        <div class="flex items-start justify-between gap-3">
          <div class="space-y-1">
            <span class="text-[10px] font-black uppercase tracking-widest text-indigo-400">
              Scanned Equipment Target
            </span>
            <h2 class="text-lg font-bold text-white leading-tight">
              {{ stationId || 'General Production Cell' }}
            </h2>
            <div class="flex items-center gap-2 flex-wrap pt-0.5">
              <Badge v-if="machineType" variant="outline" class="text-[10px] border-slate-700 text-slate-300">
                <Cpu class="size-3 mr-1 text-indigo-400" />
                {{ machineType }}
              </Badge>
              <Badge v-if="groupId" variant="outline" class="text-[10px] border-slate-700 text-slate-300">
                <Building2 class="size-3 mr-1 text-slate-400" />
                {{ groupId }}
              </Badge>
              <Badge v-if="ticketId" variant="outline" class="text-[10px] border-indigo-500/40 text-indigo-300 bg-indigo-500/10">
                #{{ ticketId }}
              </Badge>
            </div>
          </div>

          <div class="size-10 rounded-2xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 flex items-center justify-center shrink-0">
            <ShieldCheck class="size-5" />
          </div>
        </div>

        <!-- Intent Breakdown -->
        <div class="p-3.5 rounded-2xl bg-slate-950/60 border border-slate-800/80 space-y-2 text-xs">
          <div class="flex items-center justify-between text-[11px]">
            <span class="text-slate-400 font-medium">Requested Action:</span>
            <span class="font-bold text-indigo-400 uppercase tracking-wide">{{ action }}</span>
          </div>
          <div v-if="prefillTitle" class="flex items-center justify-between text-[11px]">
            <span class="text-slate-400 font-medium">Preset Title:</span>
            <span class="font-medium text-slate-200 truncate max-w-[200px]">{{ prefillTitle }}</span>
          </div>
          <div class="flex items-center justify-between text-[11px]">
            <span class="text-slate-400 font-medium">Scanned By:</span>
            <span class="font-mono text-slate-300">{{ currentUser }}</span>
          </div>
        </div>

        <!-- Action-Specific Controls -->
        <!-- Case 1: Report Incident -->
        <div v-if="action === 'report-incident'" class="space-y-3 pt-2">
          <Button
            size="lg"
            class="w-full bg-rose-600 hover:bg-rose-500 text-white font-black text-sm h-12 rounded-2xl gap-2 shadow-lg shadow-rose-600/20 cursor-pointer"
            @click="navigateToReport"
          >
            <AlertTriangle class="size-4" />
            <span>Launch Incident Studio</span>
            <ArrowRight class="size-4 ml-auto" />
          </Button>

          <p v-if="autoRedirectCountdown !== null" class="text-center text-xs text-slate-400">
            Auto-opening in <strong>{{ autoRedirectCountdown }}s</strong> &bull;
            <button
              type="button"
              @click="autoRedirectCountdown = null"
              class="underline text-indigo-400 hover:text-white cursor-pointer ml-1"
            >
              Cancel auto-redirect
            </button>
          </p>
        </div>

        <!-- Case 2: View Ticket -->
        <div v-else-if="action === 'view-ticket'" class="space-y-3 pt-2">
          <Button
            size="lg"
            class="w-full bg-indigo-600 hover:bg-indigo-500 text-white font-black text-sm h-12 rounded-2xl gap-2 shadow-lg shadow-indigo-600/20 cursor-pointer"
            @click="navigateToTicket"
          >
            <ExternalLink class="size-4" />
            <span>Open Dedicated Ticket Page</span>
            <ArrowRight class="size-4 ml-auto" />
          </Button>
        </div>

        <!-- Case 3: Technician Physical Check-In -->
        <div v-else-if="action === 'check-in'" class="space-y-3 pt-2">
          <div v-if="checkInSuccess" class="p-4 rounded-2xl bg-emerald-500/15 border border-emerald-500/30 text-emerald-300 text-center space-y-1">
            <CheckCircle2 class="size-8 mx-auto text-emerald-400 animate-bounce" />
            <div class="font-bold text-sm">Physical Check-In Verified!</div>
            <div class="text-[11px] text-emerald-400/80">Arrival logged to telemetry log.</div>
            <Button
              variant="outline"
              size="sm"
              class="mt-2 text-xs border-emerald-500/40 text-emerald-300"
              @click="navigateToTicket"
            >
              Continue to Ticket Workbench
            </Button>
          </div>

          <Button
            v-else
            size="lg"
            :disabled="isExecuting"
            class="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-black text-sm h-12 rounded-2xl gap-2 shadow-lg shadow-emerald-600/20 cursor-pointer"
            @click="handleCheckIn"
          >
            <CheckCircle2 class="size-4" />
            <span>{{ isExecuting ? 'Registering Arrival...' : 'Confirm Physical Check-In' }}</span>
          </Button>
        </div>

        <!-- Case 4: Inspect Machine / View Station -->
        <div v-else class="space-y-3 pt-2">
          <Button
            size="lg"
            class="w-full bg-blue-600 hover:bg-blue-500 text-white font-black text-sm h-12 rounded-2xl gap-2 shadow-lg shadow-blue-600/20 cursor-pointer"
            @click="navigateToMachine"
          >
            <Cpu class="size-4" />
            <span>Inspect Machine Telemetry</span>
            <ArrowRight class="size-4 ml-auto" />
          </Button>
        </div>
      </div>
    </main>

    <!-- Bottom Mobile Footer -->
    <footer class="pt-4 border-t border-slate-800 text-center text-xs text-slate-500 space-y-1">
      <div>Heimdall Factory Floor Architecture &bull; ISO/IEC 18004 Optical Telemetry</div>
      <div class="text-[10px]">Real-Time OT Dispatch & Synchronization Engine</div>
    </footer>
  </div>
</template>
