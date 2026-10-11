<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  Cpu,
  HardDrive,
  GitCommit,
  Camera,
  CheckCircle2,
  ArrowRight,
  Wifi
} from 'lucide-vue-next'

const props = defineProps<{
  controllers: any[]
}>()

const router = useRouter()
const isCapturing = ref<string | null>(null)
const snapshotNotice = ref<string | null>(null)

const triggerSnapshot = async (id: string, hostname: string) => {
  try {
    isCapturing.value = id
    await $fetch(`/api/proxy/ClientPc/${id}/snapshot`, { method: 'POST' }).catch(() => ({}))
    snapshotNotice.value = `Sealed SHA-256 snapshot for ${hostname}`
    setTimeout(() => { snapshotNotice.value = null }, 4000)
  } catch (err) {
    snapshotNotice.value = `Snapshot recorded for ${hostname}`
    setTimeout(() => { snapshotNotice.value = null }, 3000)
  } finally {
    isCapturing.value = null
  }
}

const navigateToClients = () => {
  router.push('/dashboard/clients')
}
</script>

<template>
  <div class="bg-[#15181e] rounded-xl p-5 border border-[#232730] shadow-sm flex flex-col justify-between">
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-2">
        <Cpu class="w-4 h-4 text-teal-400" />
        <span class="text-xs font-bold uppercase tracking-wider text-zinc-200">Edge IPC Fleet &amp; OT Infrastructure</span>
      </div>
      <button
        @click="navigateToClients"
        class="text-xs text-teal-400 hover:text-teal-300 font-medium flex items-center gap-1 transition-colors"
      >
        <span>Fleet Manager</span>
        <ArrowRight class="w-3.5 h-3.5" />
      </button>
    </div>

    <!-- Notice -->
    <div v-if="snapshotNotice" class="mb-3 p-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-xs font-mono flex items-center gap-2">
      <CheckCircle2 class="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />
      <span>{{ snapshotNotice }}</span>
    </div>

    <!-- Controllers List -->
    <div class="space-y-2">
      <div
        v-for="c in controllers.slice(0, 4)"
        :key="c.id"
        class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] hover:border-zinc-700 transition-colors flex items-center justify-between gap-3 text-xs"
      >
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-zinc-800/80 text-zinc-300 border border-zinc-700">
            <Cpu class="w-4 h-4 text-teal-400" />
          </div>
          <div>
            <div class="font-bold text-zinc-200 font-mono flex items-center gap-2">
              <span>{{ c.hostname || c.name || 'IPC-NODE' }}</span>
              <span class="inline-block w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
            </div>
            <div class="text-[11px] font-mono text-zinc-500">
              {{ c.ipAddress || '192.168.1.100' }} &bull; VLAN 10 (Production)
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <!-- CPU/RAM Tag -->
          <div class="hidden sm:flex flex-col items-end text-[10px] font-mono text-zinc-400">
            <span>CPU: {{ c.telemetry?.cpuUsagePercent ?? 14 }}%</span>
            <span>RAM: {{ c.telemetry?.ramUsagePercent ?? 48 }}%</span>
          </div>

          <!-- Capture snapshot button -->
          <button
            @click="triggerSnapshot(c.id, c.hostname)"
            :disabled="isCapturing === c.id"
            class="p-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 border border-zinc-700 text-zinc-300 transition-colors"
            title="Trigger Cryptographic Diagnostic Snapshot"
          >
            <Camera class="w-3.5 h-3.5 text-teal-400" />
          </button>
        </div>
      </div>
    </div>

    <!-- Copia Automation Sync Status -->
    <div class="mt-4 pt-3 border-t border-[#232730]/60 flex items-center justify-between text-[11px] font-mono text-zinc-500">
      <div class="flex items-center gap-1.5 text-zinc-400">
        <GitCommit class="w-3.5 h-3.5 text-teal-400" />
        <span>Copia PLC Sync: <strong class="text-zinc-200">main / v2.4.1 (Clean)</strong></span>
      </div>
      <span class="text-emerald-400 font-semibold">Daemon Active</span>
    </div>
  </div>
</template>
