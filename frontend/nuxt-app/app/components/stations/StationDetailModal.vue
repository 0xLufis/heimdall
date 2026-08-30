<script setup lang="ts">
import type { ProductionStation } from '~/types/domain'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Cpu, Monitor, MapPin, Package, Shield, AlertTriangle, X } from 'lucide-vue-next'

const props = defineProps<{
  station: ProductionStation | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent v-if="station" class="max-w-3xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
      <DialogHeader class="bg-indigo-950/50 p-8 border-b border-slate-800">
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-4">
            <div class="p-3 bg-indigo-500/20 rounded-2xl text-indigo-400 border border-indigo-500/30">
              <Cpu class="w-8 h-8" />
            </div>
            <div>
              <DialogTitle class="text-2xl font-black uppercase tracking-tight text-white flex items-center gap-3">
                {{ station.name }}
                <Badge class="bg-indigo-600 text-white font-mono text-[10px] uppercase">{{ station.customIdentifier }}</Badge>
              </DialogTitle>
              <DialogDescription class="text-indigo-300/70 text-xs font-bold uppercase tracking-widest mt-1">
                Station Graph Node Identity & Physical Interconnects
              </DialogDescription>
            </div>
          </div>
        </div>
      </DialogHeader>

      <div class="p-8 space-y-6 max-h-[70vh] overflow-y-auto">
        <!-- Key Metrics Grid -->
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <div class="p-4 rounded-2xl bg-slate-900 border border-slate-800">
            <div class="text-[10px] font-black uppercase tracking-widest text-slate-500">Status</div>
            <div class="text-sm font-black text-emerald-400 mt-1 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
              Operational
            </div>
          </div>

          <div class="p-4 rounded-2xl bg-slate-900 border border-slate-800">
            <div class="text-[10px] font-black uppercase tracking-widest text-slate-500">CAD Spatial Ref</div>
            <div class="text-sm font-mono font-bold text-slate-200 mt-1 truncate">
              {{ station.pinnedObjectHandle || 'Unpinned' }}
            </div>
          </div>

          <div class="p-4 rounded-2xl bg-slate-900 border border-slate-800">
            <div class="text-[10px] font-black uppercase tracking-widest text-slate-500">Controllers</div>
            <div class="text-sm font-black text-indigo-400 mt-1">
              {{ station.controllers?.length || 0 }} IPCs/PLCs
            </div>
          </div>

          <div class="p-4 rounded-2xl bg-slate-900 border border-slate-800">
            <div class="text-[10px] font-black uppercase tracking-widest text-slate-500">Organization</div>
            <div class="text-sm font-bold text-slate-200 mt-1 truncate">
              {{ station.organizationId || 'Heimdall Root' }}
            </div>
          </div>
        </div>

        <!-- Associated Controllers Section -->
        <div class="space-y-3">
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 flex items-center gap-2">
            <Monitor class="w-4 h-4 text-indigo-400" />
            Associated Industrial Controllers (IPCs / PLCs)
          </h4>

          <div v-if="!station.controllers || station.controllers.length === 0" class="p-6 text-center bg-slate-900/50 rounded-2xl border border-dashed border-slate-800 text-slate-500 text-xs font-bold uppercase tracking-wider">
            No controllers currently assigned to this production station.
          </div>

          <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div
              v-for="c in station.controllers"
              :key="c.id"
              class="p-4 bg-slate-900 border border-slate-800 rounded-2xl flex items-center justify-between"
            >
              <div>
                <div class="text-xs font-bold text-slate-200">{{ c.hostname || c.name || 'Controller Node' }}</div>
                <div class="text-[10px] font-mono text-slate-500 mt-0.5">{{ c.ipAddress || '192.168.1.xxx' }}</div>
              </div>
              <Badge variant="outline" class="border-indigo-500/30 text-indigo-400 text-[9px] uppercase font-mono">
                {{ c.role || 'Primary' }}
              </Badge>
            </div>
          </div>
        </div>

        <!-- Hardware Components List -->
        <div class="space-y-3">
          <h4 class="text-xs font-black uppercase tracking-widest text-slate-400 flex items-center gap-2">
            <Package class="w-4 h-4 text-emerald-400" />
            Station Hardware Assets & Sensors
          </h4>

          <div v-if="!station.hardwareComponents || station.hardwareComponents.length === 0" class="p-6 text-center bg-slate-900/50 rounded-2xl border border-dashed border-slate-800 text-slate-500 text-xs font-bold uppercase tracking-wider">
            No nested hardware components registered.
          </div>

          <div v-else class="divide-y divide-slate-900 border border-slate-800 rounded-2xl bg-slate-900 overflow-hidden">
            <div
              v-for="comp in station.hardwareComponents"
              :key="comp.id"
              class="p-4 flex items-center justify-between"
            >
              <div>
                <div class="text-xs font-bold text-slate-200">{{ comp.name }}</div>
                <div class="text-[10px] text-slate-500 font-mono">{{ comp.serialNumber || comp.id }}</div>
              </div>
              <span class="text-[10px] font-bold text-indigo-400 uppercase">{{ comp.itemType }}</span>
            </div>
          </div>
        </div>
      </div>

      <div class="p-6 bg-slate-900/50 border-t border-slate-800 flex justify-end gap-3">
        <Button variant="outline" @click="emit('update:open', false)" class="rounded-xl border-slate-800 text-xs font-bold uppercase tracking-wider">
          Close
        </Button>
      </div>
    </DialogContent>
  </Dialog>
</template>
