<script setup lang="ts">
import {
  Sliders,
  Check,
  Eye,
  EyeOff,
  Palette,
  Play,
  Pause,
  RefreshCw,
  Layout,
  Gauge
} from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { useDigitalTwin, type DigitalTwinLayoutPreset, type DigitalTwinDensity, type DigitalTwinThemeAccent } from '~/composables/useDigitalTwin'

const {
  layoutPreset,
  density,
  themeAccent,
  autoRefreshSeconds,
  isSimulationActive,
  widgetsConfig,
  setLayoutPreset,
  toggleWidget,
  setDensity,
  setThemeAccent
} = useDigitalTwin()

const presets: Array<{ id: DigitalTwinLayoutPreset; title: string; desc: string }> = [
  { id: 'command-center', title: 'Command Center', desc: 'Full multi-widget plant overview with all telemetry, incidents, and topography.' },
  { id: 'spatial-focus', title: 'Spatial Topography Focus', desc: 'Maximized factory floor canvas with machine fleet matrix.' },
  { id: 'maintenance-lead', title: 'Maintenance Lead', desc: 'Prioritizes line stop radar, emergency incident queue, and downtime metrics.' },
  { id: 'controls-engineer', title: 'Controls & Telemetry Engineer', desc: 'Real-time SPC sparklines, upper control limits, and Edge IPC status.' },
  { id: 'minimalist', title: 'Minimalist Operator', desc: 'Clean, distraction-free spatial canvas with high-level KPI indicators.' }
]

const densityOptions: Array<{ id: DigitalTwinDensity; title: string; desc: string }> = [
  { id: 'compact', title: 'Compact Density', desc: 'High information density suited for multi-monitor command rooms.' },
  { id: 'comfortable', title: 'Comfortable Density', desc: 'Standard balanced industrial UI layout with natural spacing.' },
  { id: 'spacious', title: 'Spacious Density', desc: 'Enlarged touchscreen target standards for plant floor tablets.' }
]

const colorThemes: Array<{ id: DigitalTwinThemeAccent; label: string; class: string }> = [
  { id: 'graphite', label: 'Graphite Tool Steel', class: 'bg-[#495464]' },
  { id: 'teal', label: 'Muted Industrial Teal', class: 'bg-teal-500' },
  { id: 'emerald', label: 'Sage Emerald', class: 'bg-emerald-500' },
  { id: 'amber', label: 'Amber Warning', class: 'bg-amber-500' },
  { id: 'rose', label: 'Laser Rose', class: 'bg-rose-500' },
  { id: 'blue', label: 'Cadet Slate Blue', class: 'bg-sky-500' }
]
</script>

<template>
  <div class="space-y-6">
    <!-- 1. Layout Presets Card -->
    <Card class="border border-border bg-card">
      <CardHeader>
        <CardTitle class="text-base font-bold text-foreground flex items-center gap-2">
          <Layout class="w-4 h-4 text-teal-400" />
          <span>Digital Twin Layout Presets</span>
        </CardTitle>
        <CardDescription class="text-xs text-muted-foreground">
          Select a tailored view optimized for your role on the plant floor.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          <div
            v-for="p in presets"
            :key="p.id"
            @click="setLayoutPreset(p.id)"
            class="p-4 rounded-xl border cursor-pointer transition-all flex flex-col justify-between"
            :class="layoutPreset === p.id ? 'bg-teal-950/20 border-teal-400 text-teal-200 ring-1 ring-teal-400' : 'bg-muted/40 border-border text-foreground hover:border-muted-foreground'"
          >
            <div class="flex items-center justify-between mb-1.5">
              <span class="font-bold text-xs">{{ p.title }}</span>
              <Check v-if="layoutPreset === p.id" class="w-4 h-4 text-teal-400" />
            </div>
            <p class="text-[11px] text-muted-foreground leading-relaxed">{{ p.desc }}</p>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- 2. Widget Visibility Toggles -->
    <Card class="border border-border bg-card">
      <CardHeader>
        <CardTitle class="text-base font-bold text-foreground flex items-center gap-2">
          <Sliders class="w-4 h-4 text-teal-400" />
          <span>Modular Widget Controls</span>
        </CardTitle>
        <CardDescription class="text-xs text-muted-foreground">
          Toggle and customize individual panels rendered on the main Digital Twin dashboard.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div
            v-for="w in widgetsConfig"
            :key="w.id"
            class="p-3.5 bg-muted/30 rounded-xl border border-border flex items-center justify-between hover:border-muted-foreground transition-colors"
          >
            <div>
              <div class="font-semibold text-xs text-foreground">{{ w.title }}</div>
              <div class="text-[11px] text-muted-foreground">{{ w.description }}</div>
            </div>
            <button
              @click="toggleWidget(w.id)"
              class="px-2.5 py-1 rounded-lg text-xs font-mono font-medium transition-colors flex items-center gap-1.5"
              :class="w.visible ? 'bg-teal-500/10 text-teal-300 border border-teal-500/30' : 'bg-muted text-muted-foreground border border-border'"
            >
              <Eye v-if="w.visible" class="w-3.5 h-3.5 text-teal-400" />
              <EyeOff v-else class="w-3.5 h-3.5" />
              <span>{{ w.visible ? 'Visible' : 'Hidden' }}</span>
            </button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- 3. Interface Density & Theme Palette -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
      <Card class="border border-border bg-card">
        <CardHeader>
          <CardTitle class="text-base font-bold text-foreground">
            Display Density &amp; Touch Standards
          </CardTitle>
          <CardDescription class="text-xs text-muted-foreground">
            Set information density standards for desktops or floor kiosks.
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-2">
          <div
            v-for="opt in densityOptions"
            :key="opt.id"
            @click="setDensity(opt.id)"
            class="p-3 rounded-xl border cursor-pointer transition-all flex items-center justify-between"
            :class="density === opt.id ? 'bg-teal-950/20 border-teal-400 text-teal-200 ring-1 ring-teal-400' : 'bg-muted/30 border-border text-foreground hover:border-muted-foreground'"
          >
            <div>
              <div class="font-bold text-xs">{{ opt.title }}</div>
              <div class="text-[11px] text-muted-foreground">{{ opt.desc }}</div>
            </div>
            <Check v-if="density === opt.id" class="w-4 h-4 text-teal-400" />
          </div>
        </CardContent>
      </Card>

      <Card class="border border-border bg-card">
        <CardHeader>
          <CardTitle class="text-base font-bold text-foreground">
            Accent Palette Personalization
          </CardTitle>
          <CardDescription class="text-xs text-muted-foreground">
            Choose your preferred industrial accent tone.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div class="grid grid-cols-2 gap-2.5">
            <button
              v-for="th in colorThemes"
              :key="th.id"
              @click="setThemeAccent(th.id)"
              class="flex items-center gap-2 p-2.5 rounded-xl border text-xs transition-all text-left"
              :class="themeAccent === th.id ? 'border-primary bg-muted font-bold text-foreground ring-1 ring-primary' : 'border-border text-muted-foreground hover:text-foreground bg-muted/20'"
            >
              <span class="w-3 h-3 rounded-full flex-shrink-0" :class="th.class"></span>
              <span class="truncate">{{ th.label }}</span>
            </button>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- 4. Real-Time Simulation Control -->
    <Card class="border border-border bg-card">
      <CardHeader>
        <CardTitle class="text-base font-bold text-foreground flex items-center gap-2">
          <Gauge class="w-4 h-4 text-teal-400" />
          <span>Digital Twin Live Telemetry Simulation Engine</span>
        </CardTitle>
        <CardDescription class="text-xs text-muted-foreground">
          Injects realistic fluctuating sensor readings (vibration velocity, bearing temperature, cycle times) into the digital twin nodes.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 p-4 rounded-xl bg-muted/30 border border-border">
          <div>
            <div class="text-xs font-semibold text-foreground">Live Telemetry Simulator</div>
            <div class="text-[11px] text-muted-foreground mt-0.5">
              Status: {{ isSimulationActive ? 'Broadcasting live sensor telemetry' : 'Simulation paused' }}
            </div>
          </div>
          <button
            @click="isSimulationActive = !isSimulationActive"
            class="px-4 py-2 rounded-xl text-xs font-bold flex items-center gap-2 transition-colors border"
            :class="isSimulationActive ? 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30' : 'bg-muted text-muted-foreground border-border'"
          >
            <Play v-if="isSimulationActive" class="w-3.5 h-3.5 text-emerald-400" />
            <Pause v-else class="w-3.5 h-3.5" />
            <span>{{ isSimulationActive ? 'Streaming Active' : 'Simulation Paused' }}</span>
          </button>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
