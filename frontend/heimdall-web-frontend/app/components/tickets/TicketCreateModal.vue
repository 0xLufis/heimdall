<script setup lang="ts">
import { ref } from 'vue'
import { Plus, Camera, AlertTriangle, Check, X, ShieldAlert } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { QrScanner } from '~/components/ui/qr-scanner'
import { useOfflineTickets } from '~/composables/useOfflineTickets'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'created', ticket: any): void
}>()

const { queueOfflineTicket } = useOfflineTickets()

const showScanner = ref(false)
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)

const form = ref({
  stationId: 'STATION-OP10-01',
  stationName: 'OP10 Machining Cell',
  controllerId: '',
  title: '',
  description: '',
  priority: 'Medium' as 'Low' | 'Medium' | 'High' | 'Critical',
  assignedTechnicianName: 'Gábor Varga (Lead Tech)'
})

function onQrScanned(code: string) {
  showScanner.value = false
  form.value.stationId = code
  form.value.stationName = `Station ${code}`
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
    controllerId: form.value.controllerId || undefined,
    title: form.value.title.trim(),
    description: form.value.description.trim(),
    priority: form.value.priority,
    assignedTechnicianName: form.value.assignedTechnicianName,
    reportedByUserName: 'Operator (PWA)'
  }

  try {
    // Check if browser is online
    if (typeof navigator !== 'undefined' && !navigator.onLine) {
      // Offline mode: enqueue ticket to IndexedDB / local queue
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
      emit('created', res.ticket)
      emit('update:open', false)
      resetForm()
    }
  } catch (err: any) {
    console.warn('Network request failed, queueing ticket offline:', err)
    // Fallback queue offline ticket
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
    stationId: 'STATION-OP10-01',
    stationName: 'OP10 Machining Cell',
    controllerId: '',
    title: '',
    description: '',
    priority: 'Medium',
    assignedTechnicianName: 'Gábor Varga (Lead Tech)'
  }
  errorMessage.value = null
}

function handleClose() {
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
    <div class="bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl w-full max-w-xl overflow-hidden animate-in fade-in zoom-in-95 duration-200">
      
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
              Industrial Equipment Failure Incident Report
            </p>
          </div>
        </div>

        <Button variant="ghost" size="icon" @click="handleClose" class="text-slate-400 hover:text-white rounded-xl">
          <X class="h-5 w-5" />
        </Button>
      </div>

      <!-- QR Scanner Overlay Modal -->
      <div v-if="showScanner" class="p-4 bg-slate-950 border-b border-slate-800">
        <QrScanner @scanned="onQrScanned" @close="showScanner = false" />
      </div>

      <!-- Form Body -->
      <div class="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
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
            <Input
              v-model="form.stationId"
              placeholder="Station ID (e.g. STATION-OP10-01)"
              class="bg-slate-950 border-slate-800 rounded-xl text-xs flex-1"
            />
            <Button
              type="button"
              variant="outline"
              @click="showScanner = !showScanner"
              class="border-indigo-500/30 bg-indigo-500/10 text-indigo-300 hover:bg-indigo-500/20 rounded-xl text-xs font-black uppercase tracking-wider flex items-center gap-2"
            >
              <Camera class="h-4 w-4" />
              <span>Scan QR</span>
            </Button>
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

        <!-- Priority Selection -->
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

        <!-- Technician Assignment -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Assign Technician (Optional)
          </label>
          <Input
            v-model="form.assignedTechnicianName"
            placeholder="Technician name or leave blank for auto-assignment"
            class="bg-slate-950 border-slate-800 rounded-xl text-xs"
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
