<script setup lang="ts">
import { ref } from 'vue'
import {
  Activity,
  Cpu,
  Factory,
  Layers,
  CheckCircle2,
  AlertTriangle,
  ShieldAlert,
  ArrowRight,
  ExternalLink,
  RotateCw,
  Gauge,
  Thermometer,
  Wrench,
  Radio
} from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

interface PlantStation {
  id: string
  code: string
  name: string
  lineName: string
  status: 'normal' | 'warning' | 'critical' | 'idle'
  cycleTimeMs: number
  spindleRpm?: number
  vibrationMmS?: number
  temperatureC?: number
  activeIncidentCount: number
  controllerHostname: string
}

const lines = [
  {
    name: 'Line 1 - Pre-Assembly Cells',
    stations: [
      {
        id: 'stn-op10',
        code: 'OP-10',
        name: 'Robotic Laser Welding & Clamping Cell',
        lineName: 'Line 1',
        status: 'normal',
        cycleTimeMs: 840,
        spindleRpm: 12450,
        vibrationMmS: 0.42,
        temperatureC: 43.5,
        activeIncidentCount: 0,
        controllerHostname: 'IPC-LINE1-OP10'
      },
      {
        id: 'stn-op20',
        code: 'OP-20',
        name: 'Automatic Multi-Spindle Screwing Nest',
        lineName: 'Line 1',
        status: 'normal',
        cycleTimeMs: 1120,
        spindleRpm: 4800,
        vibrationMmS: 0.28,
        temperatureC: 38.1,
        activeIncidentCount: 0,
        controllerHostname: 'IPC-LINE1-OP20'
      },
      {
        id: 'stn-op30',
        code: 'OP-30',
        name: 'Optical Geometry & Vision Station',
        lineName: 'Line 1',
        status: 'warning',
        cycleTimeMs: 650,
        vibrationMmS: 0.85,
        temperatureC: 51.2,
        activeIncidentCount: 1,
        controllerHostname: 'IPC-LINE1-OP30'
      }
    ] as PlantStation[]
  },
  {
    name: 'Line 2 - High Voltage Battery & Assembly',
    stations: [
      {
        id: 'stn-op40',
        code: 'OP-40',
        name: 'Dispensing & Thermal Adhesive Gasketing',
        lineName: 'Line 2',
        status: 'normal',
        cycleTimeMs: 2400,
        temperatureC: 36.4,
        activeIncidentCount: 0,
        controllerHostname: 'IPC-LINE2-OP40'
      },
      {
        id: 'stn-op50',
        code: 'OP-50',
        name: 'Cell Module Press & Busbar Fastener',
        lineName: 'Line 2',
        status: 'normal',
        cycleTimeMs: 1850,
        spindleRpm: 6200,
        vibrationMmS: 0.35,
        temperatureC: 40.8,
        activeIncidentCount: 0,
        controllerHostname: 'IPC-LINE2-OP50'
      }
    ] as PlantStation[]
  },
  {
    name: 'Line 3 - EOL Quality & Final Diagnostics',
    stations: [
      {
        id: 'stn-op60',
        code: 'OP-60',
        name: 'Automated Optical Inspection (AOI)',
        lineName: 'Line 3',
        status: 'normal',
        cycleTimeMs: 420,
        temperatureC: 32.0,
        activeIncidentCount: 0,
        controllerHostname: 'IPC-LINE3-OP60'
      },
      {
        id: 'stn-op70',
        code: 'OP-70',
        name: 'High Voltage Dielectric & Leak Tester',
        lineName: 'Line 3',
        status: 'normal',
        cycleTimeMs: 3100,
        temperatureC: 34.5,
        activeIncidentCount: 0,
        controllerHostname: 'IPC-LINE3-OP70'
      }
    ] as PlantStation[]
  }
]

const selectedStation = ref<PlantStation | null>(null)

const getStationBadge = (status: PlantStation['status']) => {
  switch (status) {
    case 'normal':
      return { label: 'RUNNING', class: 'border-emerald-500/40 text-emerald-400 bg-emerald-500/10' }
    case 'warning':
      return { label: 'TOLERANCE DRIFT', class: 'border-amber-500/40 text-amber-400 bg-amber-500/10' }
    case 'critical':
      return { label: 'HALTED', class: 'border-rose-500/40 text-rose-400 bg-rose-500/10' }
    default:
      return { label: 'IDLE', class: 'border-border text-muted-foreground' }
  }
}
</script>

<template>
  <Card class="bg-card border-border shadow-xs overflow-hidden rounded-2xl">
    <CardHeader class="px-5 py-4 border-b border-border bg-muted/20 flex flex-row items-center justify-between space-y-0">
      <div>
        <div class="flex items-center gap-2">
          <Factory class="w-5 h-5 text-primary" />
          <CardTitle class="text-base font-bold text-foreground">
            Plant Physical Hierarchy & Cell Topology
          </CardTitle>
          <Badge variant="outline" class="text-[10px] font-mono px-2 py-0 border-primary/30 text-primary">
            7 Production Cells
          </Badge>
        </div>
        <p class="text-xs text-muted-foreground mt-0.5">
          Real-time physical telemetry sync across manufacturing lines and kinematic cells
        </p>
      </div>

      <NuxtLink to="/dashboard/machines" class="text-xs text-primary hover:underline flex items-center gap-1 font-medium">
        <span>Machine Directory</span>
        <ArrowRight class="w-3.5 h-3.5" />
      </NuxtLink>
    </CardHeader>

    <CardContent class="p-5 space-y-6">
      <!-- Line by Line Topology Rollup -->
      <div v-for="line in lines" :key="line.name" class="space-y-2.5">
        <div class="flex items-center justify-between text-xs">
          <span class="font-bold font-mono text-muted-foreground uppercase tracking-wider flex items-center gap-2">
            <span class="w-1.5 h-1.5 rounded-full bg-primary" />
            {{ line.name }}
          </span>
          <span class="text-[11px] font-mono text-muted-foreground">
            {{ line.stations.filter(s => s.status === 'normal').length }} / {{ line.stations.length }} In-Spec
          </span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
          <div
            v-for="stn in line.stations"
            :key="stn.id"
            @click="selectedStation = stn"
            class="p-3.5 rounded-xl border bg-card/60 hover:bg-muted/30 transition-all cursor-pointer group flex flex-col justify-between space-y-2.5 shadow-xs"
            :class="[
              selectedStation?.id === stn.id
                ? 'border-primary ring-1 ring-primary/40 bg-primary/5'
                : 'border-border hover:border-border/80'
            ]"
          >
            <!-- Card Header -->
            <div class="flex items-start justify-between gap-2">
              <div class="space-y-0.5 min-w-0">
                <div class="flex items-center gap-1.5">
                  <span class="font-mono text-xs font-black text-cyan-400">{{ stn.code }}</span>
                  <span class="text-muted-foreground text-[10px]">•</span>
                  <span class="font-mono text-[10px] text-muted-foreground truncate">{{ stn.controllerHostname }}</span>
                </div>
                <div class="text-xs font-bold text-foreground group-hover:text-primary transition-colors truncate" :title="stn.name">
                  {{ stn.name }}
                </div>
              </div>

              <Badge
                variant="outline"
                :class="getStationBadge(stn.status).class"
                class="text-[9px] font-mono px-1.5 py-0 shrink-0 uppercase"
              >
                {{ getStationBadge(stn.status).label }}
              </Badge>
            </div>

            <!-- Live Physical Parameters Bento -->
            <div class="grid grid-cols-3 gap-1.5 p-2 rounded-lg bg-muted/30 border border-border/40 font-mono text-[10px]">
              <div>
                <span class="text-muted-foreground block text-[9px]">Cycle</span>
                <span class="font-bold text-foreground">{{ stn.cycleTimeMs }} ms</span>
              </div>
              <div>
                <span class="text-muted-foreground block text-[9px]">Spindle</span>
                <span class="font-bold text-cyan-400">{{ stn.spindleRpm ? `${(stn.spindleRpm / 1000).toFixed(1)}k` : 'N/A' }}</span>
              </div>
              <div>
                <span class="text-muted-foreground block text-[9px]">Thermal</span>
                <span class="font-bold" :class="stn.temperatureC && stn.temperatureC > 50 ? 'text-rose-400' : 'text-emerald-400'">
                  {{ stn.temperatureC ? `${stn.temperatureC}°C` : 'N/A' }}
                </span>
              </div>
            </div>

            <!-- Footer Quick Launch Links -->
            <div class="flex items-center justify-between text-[11px] pt-1 border-t border-border/40">
              <span class="text-muted-foreground font-mono text-[10px]">
                {{ stn.activeIncidentCount > 0 ? `${stn.activeIncidentCount} Open Ticket` : '0 Active Incidents' }}
              </span>

              <div class="flex items-center gap-2">
                <NuxtLink
                  :to="`/terminal/machine/${stn.id}`"
                  class="text-[10px] font-bold text-cyan-400 hover:underline flex items-center gap-0.5"
                  title="Open Shopfloor Kiosk Terminal"
                >
                  <span>Kiosk</span>
                  <ExternalLink class="w-2.5 h-2.5" />
                </NuxtLink>

                <NuxtLink
                  :to="`/dashboard/telemetry?tab=live`"
                  class="text-[10px] font-bold text-primary hover:underline flex items-center gap-0.5"
                  title="Inspect Real-Time Signal Stream"
                >
                  <span>Signals</span>
                  <ArrowRight class="w-2.5 h-2.5" />
                </NuxtLink>
              </div>
            </div>
          </div>
        </div>
      </div>
    </CardContent>
  </Card>
</template>
