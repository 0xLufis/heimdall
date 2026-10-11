<script setup lang="ts">
import {
  X,
  Sliders,
  Check,
  Layout,
  Eye,
  EyeOff,
  Palette,
  Maximize,
  RefreshCw,
  Play,
  Pause
} from 'lucide-vue-next'
import type {
  DigitalTwinLayoutPreset,
  DigitalTwinDensity,
  DigitalTwinThemeAccent,
  DigitalTwinWidgetConfig
} from '~/composables/useDigitalTwin'

const props = defineProps<{
  isOpen: boolean
  widgets: DigitalTwinWidgetConfig[]
  layoutPreset: DigitalTwinLayoutPreset
  density: DigitalTwinDensity
  themeAccent: DigitalTwinThemeAccent
  autoRefreshSeconds: number
  isSimulationActive: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'toggle-widget', id: string): void
  (e: 'set-preset', preset: DigitalTwinLayoutPreset): void
  (e: 'set-density', density: DigitalTwinDensity): void
  (e: 'set-theme', theme: DigitalTwinThemeAccent): void
  (e: 'update:autoRefreshSeconds', sec: number): void
  (e: 'update:isSimulationActive', val: boolean): void
}>()

const presets: Array<{ id: DigitalTwinLayoutPreset; title: string; desc: string }> = [
  { id: 'command-center', title: 'Command Center', desc: 'Full multi-widget plant overview with all telemetry, incidents, and topography.' },
  { id: 'spatial-focus', title: 'Spatial Topography Focus', desc: 'Maximized factory floor canvas with machine fleet matrix.' },
  { id: 'maintenance-lead', title: 'Maintenance Lead', desc: 'Prioritizes line stop radar, emergency incident queue, and downtime metrics.' },
  { id: 'controls-engineer', title: 'Controls & Telemetry Engineer', desc: 'Real-time SPC sparklines, upper control limits, and Edge IPC status.' },
  { id: 'minimalist', title: 'Minimalist Operator', desc: 'Clean, distraction-free spatial canvas with high-level KPI indicators.' }
]

const densityOptions: Array<{ id: DigitalTwinDensity; title: string; desc: string }> = [
  { id: 'compact', title: 'Compact', desc: 'High information density for multi-monitor operations.' },
  { id: 'comfortable', title: 'Comfortable', desc: 'Balanced padding and standard visual hierarchy.' },
  { id: 'spacious', title: 'Spacious', desc: 'Large touch-ready targets for shopfloor tablets.' }
]

const colorThemes: Array<{ id: DigitalTwinThemeAccent; label: string; class: string }> = [
  { id: 'graphite', label: 'Graphite', class: 'bg-[#495464]' },
  { id: 'teal', label: 'Muted Teal', class: 'bg-teal-500' },
  { id: 'emerald', label: 'Sage Emerald', class: 'bg-emerald-500' },
  { id: 'amber', label: 'Amber Alert', class: 'bg-amber-500' },
  { id: 'rose', label: 'Laser Rose', class: 'bg-rose-500' },
  { id: 'blue', label: 'Industrial Cadet', class: 'bg-sky-500' }
]
</script>

<template>
  <div
    v-if="isOpen"
    class="fixed inset-0 bg-black/75 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200"
  >
    <div
      class="bg-[#15181e] border border-[#232730] rounded-2xl w-full max-w-2xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden"
    >
      <!-- Modal Header -->
      <div class="px-6 py-4 border-b border-[#232730] flex items-center justify-between bg-[#11141a]">
        <div class="flex items-center gap-2.5">
          <Sliders class="w-5 h-5 text-teal-400" />
          <div>
            <h3 class="text-sm font-bold text-white tracking-tight">Digital Twin Studio & Customizer</h3>
            <p class="text-xs text-zinc-400">Configure dashboard layout, widget visibility, density, and live sensor simulations.</p>
          </div>
        </div>
        <button
          @click="emit('close')"
          class="p-1.5 rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 overflow-y-auto space-y-6 text-xs">
        <!-- 1. Layout Presets -->
        <div>
          <label class="block font-mono uppercase text-[10px] text-zinc-400 font-bold mb-2.5">
            Role & View Presets
          </label>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div
              v-for="p in presets"
              :key="p.id"
              @click="emit('set-preset', p.id)"
              class="p-3 rounded-xl border cursor-pointer transition-all flex flex-col justify-between"
              :class="layoutPreset === p.id ? 'bg-teal-950/20 border-teal-400 text-teal-200 ring-1 ring-teal-400' : 'bg-[#0c0e12] border-[#232730] text-zinc-300 hover:border-zinc-700'"
            >
              <div class="flex items-center justify-between mb-1">
                <span class="font-bold text-xs">{{ p.title }}</span>
                <Check v-if="layoutPreset === p.id" class="w-3.5 h-3.5 text-teal-400" />
              </div>
              <p class="text-[11px] text-zinc-500 leading-relaxed">{{ p.desc }}</p>
            </div>
          </div>
        </div>

        <!-- 2. Modular Widget Visibility Toggles -->
        <div>
          <label class="block font-mono uppercase text-[10px] text-zinc-400 font-bold mb-2.5">
            Modular Widgets Visibility
          </label>
          <div class="space-y-2">
            <div
              v-for="w in widgets"
              :key="w.id"
              class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] flex items-center justify-between hover:border-zinc-700 transition-colors"
            >
              <div>
                <div class="font-semibold text-xs text-zinc-200">{{ w.title }}</div>
                <div class="text-[11px] text-zinc-500">{{ w.description }}</div>
              </div>
              <button
                @click="emit('toggle-widget', w.id)"
                class="px-2.5 py-1 rounded-lg text-[11px] font-mono font-medium transition-colors flex items-center gap-1.5"
                :class="w.visible ? 'bg-teal-500/10 text-teal-300 border border-teal-500/30' : 'bg-zinc-800 text-zinc-500 border border-zinc-700'"
              >
                <Eye v-if="w.visible" class="w-3 h-3 text-teal-400" />
                <EyeOff v-else class="w-3 h-3" />
                <span>{{ w.visible ? 'Visible' : 'Hidden' }}</span>
              </button>
            </div>
          </div>
        </div>

        <!-- 3. Interface Density -->
        <div>
          <label class="block font-mono uppercase text-[10px] text-zinc-400 font-bold mb-2.5">
            Interface Density & Target Standards
          </label>
          <div class="grid grid-cols-3 gap-2">
            <div
              v-for="opt in densityOptions"
              :key="opt.id"
              @click="emit('set-density', opt.id)"
              class="p-2.5 rounded-xl border cursor-pointer text-center transition-all"
              :class="density === opt.id ? 'bg-teal-950/20 border-teal-400 text-teal-200' : 'bg-[#0c0e12] border-[#232730] text-zinc-400 hover:border-zinc-700'"
            >
              <div class="font-bold text-xs">{{ opt.title }}</div>
              <div class="text-[10px] text-zinc-500 mt-0.5">{{ opt.desc }}</div>
            </div>
          </div>
        </div>

        <!-- 4. Theme Accent Color -->
        <div>
          <label class="block font-mono uppercase text-[10px] text-zinc-400 font-bold mb-2.5">
            Accent Palette
          </label>
          <div class="flex items-center gap-3">
            <button
              v-for="th in colorThemes"
              :key="th.id"
              @click="emit('set-theme', th.id)"
              class="flex items-center gap-2 px-3 py-1.5 rounded-lg border text-xs transition-all"
              :class="themeAccent === th.id ? 'border-white text-white bg-zinc-800 ring-1 ring-white/30' : 'border-[#232730] text-zinc-400 hover:text-zinc-200 bg-[#0c0e12]'"
            >
              <span class="w-2.5 h-2.5 rounded-full" :class="th.class"></span>
              <span>{{ th.label }}</span>
            </button>
          </div>
        </div>

        <!-- 5. Simulation & Telemetry Refresh -->
        <div class="pt-2 border-t border-[#232730] flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div>
            <label class="block font-mono uppercase text-[10px] text-zinc-400 font-bold">
              Digital Twin Live Stream Simulation
            </label>
            <p class="text-[11px] text-zinc-500">Injects real-time fluctuating sensor noise into machine telemetry.</p>
          </div>
          <button
            @click="emit('update:isSimulationActive', !isSimulationActive)"
            class="px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-2 border transition-colors"
            :class="isSimulationActive ? 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30' : 'bg-zinc-800 text-zinc-400 border-zinc-700'"
          >
            <Play v-if="isSimulationActive" class="w-3.5 h-3.5 text-emerald-400" />
            <Pause v-else class="w-3.5 h-3.5" />
            <span>{{ isSimulationActive ? 'Streaming Active' : 'Simulation Paused' }}</span>
          </button>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="px-6 py-3.5 border-t border-[#232730] bg-[#11141a] flex items-center justify-between">
        <span class="text-[10px] font-mono text-zinc-500">Preferences automatically persisted to client local storage.</span>
        <button
          @click="emit('close')"
          class="px-4 py-2 rounded-lg bg-teal-500 hover:bg-teal-400 text-black text-xs font-bold transition-colors"
        >
          Save & Apply
        </button>
      </div>
    </div>
  </div>
</template>
