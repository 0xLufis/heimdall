<script setup lang="ts">
import {
  Activity,
  Cpu,
  Layers,
  Wrench,
  Boxes,
  MapPin,
  ArrowRight,
  ShieldCheck,
  TrendingUp,
  Sparkles
} from 'lucide-vue-next'
</script>

<template>
  <div class="min-h-screen bg-[#0c0e12] text-zinc-100 font-sans selection:bg-teal-500 selection:text-black">
    <!-- Top Navigation -->
    <nav class="bg-[#15181e]/90 backdrop-blur-md border-b border-[#232730] sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between h-16 items-center">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 bg-teal-500/10 border border-teal-500/30 rounded-lg flex items-center justify-center font-mono font-bold text-teal-300 shadow-sm">
              H
            </div>
            <div class="flex items-center gap-2">
              <span class="text-base font-bold tracking-tight text-zinc-100">Heimdall</span>
              <span class="text-[9px] font-mono px-1.5 py-0.5 rounded bg-zinc-800 text-teal-300 border border-zinc-700">DIGITAL TWIN OS</span>
            </div>
          </div>

          <div class="flex items-center space-x-3 text-xs font-medium">
            <NuxtLink
              to="/dashboard"
              class="hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-zinc-300 hover:text-white hover:bg-zinc-800 transition-colors"
            >
              <Activity class="w-3.5 h-3.5 text-teal-400" />
              <span>Live Twin</span>
            </NuxtLink>
            <NuxtLink
              to="/dashboard"
              class="bg-teal-500 hover:bg-teal-400 text-black px-4 py-2 rounded-lg font-bold transition-all shadow-md flex items-center gap-1.5"
            >
              <span>Launch Twin</span>
              <ArrowRight class="w-3.5 h-3.5" />
            </NuxtLink>
          </div>
        </div>
      </div>
    </nav>

    <main>
      <!-- Hero Section -->
      <section class="py-20 lg:py-28 relative overflow-hidden">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center relative z-10">
          <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-teal-500/10 border border-teal-500/30 text-teal-300 text-xs font-mono mb-6">
            <span class="w-2 h-2 rounded-full bg-teal-400 animate-pulse"></span>
            <span>Next-Generation Industrial Plant Representation</span>
          </div>

          <h1 class="text-4xl sm:text-6xl font-extrabold text-zinc-100 mb-6 tracking-tight max-w-4xl mx-auto">
            The Canonical Factory <span class="text-teal-400">Digital Twin</span> &amp; Fleet Operations OS
          </h1>

          <p class="text-base sm:text-lg text-zinc-400 max-w-2xl mx-auto mb-10 leading-relaxed font-sans">
            Visual spatial floor topography, live machine shadows, vibration &amp; thermal telemetry heatmaps, and sub-minute line stop incident response.
          </p>

          <div class="flex flex-col sm:flex-row justify-center gap-3">
            <NuxtLink
              to="/dashboard"
              class="bg-teal-500 hover:bg-teal-400 text-black font-bold px-8 py-3.5 rounded-xl text-sm transition-all shadow-xl flex items-center justify-center gap-2"
            >
              <span>Open Digital Twin Command Center</span>
              <ArrowRight class="w-4 h-4" />
            </NuxtLink>
            <NuxtLink
              to="/dashboard/machines"
              class="bg-[#15181e] text-zinc-200 border border-[#232730] hover:border-zinc-700 hover:text-white px-8 py-3.5 rounded-xl font-semibold text-sm transition-colors flex items-center justify-center gap-2"
            >
              <Layers class="w-4 h-4 text-zinc-400" />
              <span>Explore Machine Fleet</span>
            </NuxtLink>
          </div>

          <!-- Live Digital Twin Metric Preview Bar -->
          <div class="mt-16 max-w-4xl mx-auto bg-[#15181e]/80 border border-[#232730] rounded-2xl p-4 sm:p-6 backdrop-blur-md shadow-2xl grid grid-cols-2 md:grid-cols-4 gap-4 text-left">
            <div>
              <div class="text-[10px] font-mono uppercase text-zinc-500">Connected Nodes</div>
              <div class="text-xl sm:text-2xl font-bold font-mono text-white mt-1">138 Stations</div>
              <div class="text-[10px] text-emerald-400 font-mono mt-0.5">● 100% Edge Monitored</div>
            </div>

            <div>
              <div class="text-[10px] font-mono uppercase text-zinc-500">Overall OEE</div>
              <div class="text-xl sm:text-2xl font-bold font-mono text-teal-300 mt-1">99.8%</div>
              <div class="text-[10px] text-zinc-400 font-mono mt-0.5">Avail: 98.4% • Qual: 99.2%</div>
            </div>

            <div>
              <div class="text-[10px] font-mono uppercase text-zinc-500">Live Vibration</div>
              <div class="text-xl sm:text-2xl font-bold font-mono text-white mt-1">1.18 mm/s</div>
              <div class="text-[10px] text-teal-400 font-mono mt-0.5">Nominal ISO 10816</div>
            </div>

            <div>
              <div class="text-[10px] font-mono uppercase text-zinc-500">Reaction Time</div>
              <div class="text-xl sm:text-2xl font-bold font-mono text-white mt-1">4.2 min</div>
              <div class="text-[10px] text-sky-400 font-mono mt-0.5">QR Instant Pickup</div>
            </div>
          </div>
        </div>
      </section>

      <!-- Core Capabilities Grid -->
      <section class="py-16 bg-[#0e1116] border-t border-[#232730]">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="text-center mb-12">
            <h2 class="text-2xl sm:text-3xl font-bold text-zinc-100 tracking-tight">
              State-of-the-Art Digital Twin Architecture
            </h2>
            <p class="text-zinc-400 text-xs sm:text-sm mt-2">Built specifically for high-throughput automotive and electronics assembly lines.</p>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div class="p-6 bg-[#15181e] rounded-2xl border border-[#232730] space-y-3">
              <div class="w-10 h-10 rounded-xl bg-teal-500/10 text-teal-300 border border-teal-500/20 flex items-center justify-center">
                <MapPin class="w-5 h-5" />
              </div>
              <h3 class="text-sm font-bold text-white">Spatial Plant Topography</h3>
              <p class="text-xs text-zinc-400 leading-relaxed">
                Interactive multi-line floor canvas with CAD spatial anchors, live process flow tracks, and multi-layer heatmap overlays.
              </p>
            </div>

            <div class="p-6 bg-[#15181e] rounded-2xl border border-[#232730] space-y-3">
              <div class="w-10 h-10 rounded-xl bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 flex items-center justify-center">
                <Activity class="w-5 h-5" />
              </div>
              <h3 class="text-sm font-bold text-white">Real-Time Waveforms &amp; SPC</h3>
              <p class="text-xs text-zinc-400 leading-relaxed">
                Continuous high-frequency sensor streams with Upper/Lower Control Limits, harmonic envelope analysis, and statistical process control.
              </p>
            </div>

            <div class="p-6 bg-[#15181e] rounded-2xl border border-[#232730] space-y-3">
              <div class="w-10 h-10 rounded-xl bg-rose-500/10 text-rose-300 border border-rose-500/20 flex items-center justify-center">
                <Wrench class="w-5 h-5" />
              </div>
              <h3 class="text-sm font-bold text-white">Line Stop Radar &amp; Downtime</h3>
              <p class="text-xs text-zinc-400 leading-relaxed">
                Emergency line stop countdowns, action QR scanning for field technicians, and automated watchdog ticket clearing.
              </p>
            </div>
          </div>
        </div>
      </section>
    </main>
  </div>
</template>
