<script setup lang="ts">
import {
  HelpCircle,
  Search,
  Cpu,
  AlertOctagon,
  ShieldCheck,
  Terminal,
  BookOpen,
  ExternalLink,
  Keyboard,
  Clock,
  Radio,
  FileCode2,
  CheckCircle2,
  ArrowRight
} from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

definePageMeta({
  layout: 'shadcn-dashboard'
})
</script>

<template>
  <div class="space-y-6 max-w-6xl mx-auto pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
          <HelpCircle class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-100">
            Documentation & Operational Knowledge Base
          </h1>
          <p class="text-sm text-slate-400 mt-0.5">
            Comprehensive operator guidelines, industrial protocols, and emergency line-stop procedures
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2.5">
        <NuxtLink to="/dashboard/telemetry/templates">
          <Button variant="outline" size="sm" class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-lg h-8 text-xs font-medium gap-1.5 transition-colors">
            <Radio class="size-3.5 text-indigo-400" />
            <span>Telemetry Studio</span>
          </Button>
        </NuxtLink>
        <NuxtLink to="/dashboard/tickets">
          <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg h-8 px-3.5 text-xs font-medium gap-1.5 shadow-sm transition-colors">
            <AlertOctagon class="size-3.5" />
            <span>Incident Log</span>
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Section 1: FMFD (Find My Field Data) & Global Navigation -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardHeader class="p-5 border-b border-slate-800">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Search class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-slate-100">
              FMFD (Find My Field Data) & Global Navigation
            </CardTitle>
            <CardDescription class="text-xs text-slate-400 mt-0.5">
              High-throughput universal indexing across all fleet nodes, machine stations, and work orders
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <p class="text-xs text-slate-300 leading-relaxed">
          Heimdall provides FMFD ("Find My Field Data"), an instant fuzzy-search indexing engine accessible throughout the application. Operators and automation engineers can locate any plant asset in milliseconds using either physical hardware IDs, network attributes, or Active Directory organizational units.
        </p>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-3 pt-1">
          <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-200">Global Shortcut</span>
              <kbd class="px-2 py-0.5 rounded bg-slate-900 border border-slate-700 text-slate-300 font-mono text-[11px]">Ctrl + K</kbd>
            </div>
            <p class="text-[11px] text-slate-400">
              Opens the global modal search palette from any dashboard view or terminal session.
            </p>
          </div>

          <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-200">Host & MAC Syntax</span>
              <Badge variant="outline" class="bg-indigo-500/10 text-indigo-400 border-indigo-500/30 text-[10px] font-mono">00:01:05:*</Badge>
            </div>
            <p class="text-[11px] text-slate-400">
              Filter industrial IPCs by partial MAC address or hostname prefix (e.g., <code class="text-slate-300 font-mono">BKHF-IPC-01</code>).
            </p>
          </div>

          <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-200">OU Tag Filters</span>
              <Badge variant="outline" class="bg-purple-500/10 text-purple-400 border-purple-500/30 text-[10px] font-mono">*Line-A*</Badge>
            </div>
            <p class="text-[11px] text-slate-400">
              Type OU patterns or asset tags (<code class="text-slate-300 font-mono">Beckhoff</code>, <code class="text-slate-300 font-mono">Vision</code>) to isolate clusters.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 2: OT Telemetry & Industrial Fieldbus Protocols -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardHeader class="p-5 border-b border-slate-800">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Cpu class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-slate-100">
              OT Telemetry & Industrial Fieldbus Protocols
            </CardTitle>
            <CardDescription class="text-xs text-slate-400 mt-0.5">
              Protocol specifications, delta evaluation algorithms, and local spool encryption
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <!-- Beckhoff ADS -->
          <div class="p-4 rounded-lg bg-slate-950/60 border border-slate-800 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-slate-100">Beckhoff TwinCAT ADS</span>
              <Badge class="bg-indigo-500/10 text-indigo-400 border border-indigo-500/30 text-[10px]">High-Frequency</Badge>
            </div>
            <p class="text-xs text-slate-400">
              Direct symbol-based memory access using TwinCAT ADS protocol. Polls PLC cycle variables with sub-millisecond deadband filtering.
            </p>
            <div class="text-[11px] text-slate-500 font-mono pt-1">Default Port: 48898 (TCP)</div>
          </div>

          <!-- OPC UA -->
          <div class="p-4 rounded-lg bg-slate-950/60 border border-slate-800 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-slate-100">OPC UA Client / Gateway</span>
              <Badge class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px]">Standardized</Badge>
            </div>
            <p class="text-xs text-slate-400">
              IEC 62541 compliant edge communication with mutual certificate exchange (mTLS) and binary node subscriptions.
            </p>
            <div class="text-[11px] text-slate-500 font-mono pt-1">Default Port: 4840 (TCP)</div>
          </div>

          <!-- Modbus TCP -->
          <div class="p-4 rounded-lg bg-slate-950/60 border border-slate-800 space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-slate-100">Modbus TCP & Couplers</span>
              <Badge class="bg-amber-500/10 text-amber-400 border border-amber-500/30 text-[10px]">Legacy Support</Badge>
            </div>
            <p class="text-xs text-slate-400">
              Polling of holding registers and digital inputs from legacy fieldbus bus couplers and remote I/O blocks.
            </p>
            <div class="text-[11px] text-slate-500 font-mono pt-1">Default Port: 502 (TCP)</div>
          </div>
        </div>

        <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800 flex items-center justify-between text-xs text-slate-400">
          <div class="flex items-center gap-2">
            <ShieldCheck class="size-4 text-indigo-400 shrink-0" />
            <span><strong>Cryptographic Tamper Protection:</strong> Telemetry packets are sealed with AES-256-GCM hardware binding and verified with xxHash64 delta evaluation.</span>
          </div>
          <NuxtLink to="/dashboard/telemetry/configure" class="text-indigo-400 hover:text-indigo-300 font-medium whitespace-nowrap ml-4 flex items-center gap-1">
            Configure Policy <ArrowRight class="size-3" />
          </NuxtLink>
        </div>
      </CardContent>
    </Card>

    <!-- Section 3: Incident Management & Line Stop SOP -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardHeader class="p-5 border-b border-slate-800">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-rose-500/10 border border-rose-500/20 text-rose-400">
            <AlertOctagon class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-slate-100">
              Incident Management & Line Stop Standard Operating Procedure (SOP)
            </CardTitle>
            <CardDescription class="text-xs text-slate-400 mt-0.5">
              4-tier severity matrix, automated escalation triggers, and technician dispatch workflow
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
          <!-- Critical -->
          <div class="p-3.5 rounded-lg bg-rose-500/5 border border-rose-500/20 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-rose-400">Critical</span>
              <span class="text-[10px] font-mono text-rose-400 font-semibold">SLA: 15 min</span>
            </div>
            <p class="text-xs text-slate-300 font-medium">Immediate Line Halt</p>
            <p class="text-[11px] text-slate-400">Spindle seizure, E-stop trip, safety gate interlock failure. Triggers automated shift broadcast.</p>
          </div>

          <!-- High -->
          <div class="p-3.5 rounded-lg bg-amber-500/5 border border-amber-500/20 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-amber-400">High</span>
              <span class="text-[10px] font-mono text-amber-400 font-semibold">SLA: 1 hour</span>
            </div>
            <p class="text-xs text-slate-300 font-medium">Degraded Output</p>
            <p class="text-[11px] text-slate-400">Robot servo thermal warning, cycle time divergence > 20%, continuous minor jams.</p>
          </div>

          <!-- Medium -->
          <div class="p-3.5 rounded-lg bg-indigo-500/5 border border-indigo-500/20 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-indigo-400">Medium</span>
              <span class="text-[10px] font-mono text-indigo-400 font-semibold">SLA: 4 hours</span>
            </div>
            <p class="text-xs text-slate-300 font-medium">Preventative Maintenance</p>
            <p class="text-[11px] text-slate-400">Filter replacement due, lubrication level low, camera lens calibration required.</p>
          </div>

          <!-- Low -->
          <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-slate-400">Low</span>
              <span class="text-[10px] font-mono text-slate-400 font-semibold">SLA: 24 hours</span>
            </div>
            <p class="text-xs text-slate-300 font-medium">General Inspection</p>
            <p class="text-[11px] text-slate-400">Minor cosmetic fixture scratch, non-blocking label print smudge, cable dressing.</p>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 4: Security, mTLS & Active Directory Architecture -->
    <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm">
      <CardHeader class="p-5 border-b border-slate-800">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-400">
            <ShieldCheck class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-slate-100">
              Security Governance, mTLS & Active Directory Architecture
            </CardTitle>
            <CardDescription class="text-xs text-slate-400 mt-0.5">
              Zero-trust identity, hardware sealing, and directory boundary enforcement
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="p-4 rounded-lg bg-slate-950 border border-slate-800 space-y-2">
            <div class="text-xs font-semibold text-slate-200">Active Directory & Entra ID Ingestion</div>
            <p class="text-xs text-slate-400 leading-relaxed">
              Industrial controllers are automatically mapped to security zones based on their Active Directory Organizational Unit (OU). Read/write access requires dual-approval and an assigned technical owner.
            </p>
          </div>

          <div class="p-4 rounded-lg bg-slate-950 border border-slate-800 space-y-2">
            <div class="text-xs font-semibold text-slate-200">Cryptographic Ed25519 Dispatch</div>
            <p class="text-xs text-slate-400 leading-relaxed">
              All telemetry recipe updates and remote diagnostic commands dispatched from the central platform are signed using Ed25519 digital signatures, preventing replay and man-in-the-middle attacks on the OT floor.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
