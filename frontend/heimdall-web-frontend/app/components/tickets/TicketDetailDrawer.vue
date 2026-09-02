<script setup lang="ts">
import { ref, watch } from 'vue'
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
  Archive 
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import type { MaintenanceTicket } from '~/server/utils/ticketsStore'

const props = defineProps<{
  ticket: MaintenanceTicket | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'updated', ticket: MaintenanceTicket): void
}>()

const commentText = ref('')
const isSubmittingComment = ref(false)
const localTicket = ref<MaintenanceTicket | null>(null)

watch(() => props.ticket, (newVal) => {
  localTicket.value = newVal ? JSON.parse(JSON.stringify(newVal)) : null
}, { immediate: true, deep: true })

async function updateStatus(newStatus: string) {
  if (!localTicket.value) return
  try {
    const res = await $fetch<{ success: boolean; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}`, {
      method: 'PATCH',
      body: { status: newStatus }
    })
    if (res && res.success) {
      localTicket.value = res.ticket
      emit('updated', res.ticket)
    }
  } catch (err) {
    console.error('Error updating ticket status:', err)
  }
}

async function addComment() {
  if (!commentText.value.trim() || !localTicket.value) return
  isSubmittingComment.value = true

  try {
    const res = await $fetch<{ success: boolean; comment: any; ticket: MaintenanceTicket }>(`/api/tickets/${localTicket.value.id}/comments`, {
      method: 'POST',
      body: {
        authorName: 'Technician (PWA)',
        content: commentText.value.trim()
      }
    })

    if (res && res.success) {
      localTicket.value = res.ticket
      commentText.value = ''
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
    <div class="bg-slate-900 border-l border-slate-800 w-full max-w-xl h-full flex flex-col shadow-2xl animate-in slide-in-from-right duration-300">
      
      <!-- Header -->
      <div class="p-6 border-b border-slate-800 flex items-center justify-between bg-slate-900/60">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-slate-800 border border-slate-700 text-indigo-400">
            <Cpu class="h-5 w-5" />
          </div>
          <div>
            <span class="text-xs font-mono font-black text-indigo-400">{{ localTicket.ticketNumber }}</span>
            <h3 class="text-sm font-black uppercase text-slate-100 tracking-tight leading-tight">
              {{ localTicket.stationName }}
            </h3>
          </div>
        </div>

        <Button variant="ghost" size="icon" @click="handleClose" class="text-slate-400 hover:text-white rounded-xl">
          <X class="h-5 w-5" />
        </Button>
      </div>

      <!-- Main Scrollable Content -->
      <div class="flex-1 overflow-y-auto p-6 space-y-6">
        
        <!-- Status & Priority Banner -->
        <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-3">
          <div class="flex items-center justify-between">
            <Badge variant="outline" class="text-xs font-black uppercase tracking-widest px-3 py-1 bg-indigo-500/10 text-indigo-400 border-indigo-500/30">
              Status: {{ localTicket.status }}
            </Badge>

            <Badge variant="outline" class="text-xs font-black uppercase tracking-widest px-3 py-1 bg-rose-500/10 text-rose-400 border-rose-500/30">
              Priority: {{ localTicket.priority }}
            </Badge>
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
              @click="updateStatus('Closed')"
              class="bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-[10px] font-black uppercase tracking-wider h-8"
            >
              <Archive class="h-3.5 w-3.5 mr-1" />
              Close Ticket
            </Button>
          </div>
        </div>

        <!-- Incident Details -->
        <div>
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-2">
            Incident Details
          </h4>
          <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-2">
            <h5 class="text-sm font-bold text-slate-200">
              {{ localTicket.title }}
            </h5>
            <p class="text-xs text-slate-400 leading-relaxed">
              {{ localTicket.description }}
            </p>
          </div>
        </div>

        <!-- Metadata Info Grid -->
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

        <!-- Comments & Activity Log -->
        <div>
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 mb-3">
            Activity Timeline & Technician Notes
          </h4>

          <div class="space-y-3 mb-4 max-h-60 overflow-y-auto pr-1">
            <template v-if="localTicket.comments.length === 0">
              <div class="p-4 text-center text-slate-600 text-xs uppercase font-bold bg-slate-950/40 rounded-xl border border-slate-900">
                No technician notes recorded yet.
              </div>
            </template>
            <template v-else>
              <div
                v-for="cmt in localTicket.comments"
                :key="cmt.id"
                class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1"
              >
                <div class="flex items-center justify-between text-[10px]">
                  <span class="font-bold text-indigo-400 uppercase">{{ cmt.authorName }}</span>
                  <span class="text-slate-500 font-mono">{{ new Date(cmt.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}</span>
                </div>
                <p class="text-xs text-slate-300">
                  {{ cmt.content }}
                </p>
              </div>
            </template>
          </div>

          <!-- Add Comment Input -->
          <div class="flex gap-2">
            <Input
              v-model="commentText"
              placeholder="Add technician observation or note..."
              class="bg-slate-950 border-slate-800 rounded-xl text-xs flex-1"
              @keyup.enter="addComment"
            />
            <Button
              @click="addComment"
              :disabled="isSubmittingComment || !commentText.trim()"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-4 text-xs font-black uppercase"
            >
              <Send class="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>

      </div>

    </div>
  </div>
</template>
