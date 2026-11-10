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
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-border">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-primary/10 border border-primary/20 text-primary">
          <HelpCircle class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-foreground">
            Documentation & Operational Knowledge Base
          </h1>
          <p class="text-sm text-muted-foreground mt-0.5">
            Comprehensive operator guidelines, industrial protocols, and emergency line-stop procedures
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2.5">
        <NuxtLink to="/dashboard/telemetry/templates">
          <Button variant="outline" size="sm" class="border-border bg-card hover:bg-accent text-foreground rounded-lg h-8 text-xs font-medium gap-1.5 transition-colors cursor-pointer">
            <Radio class="size-3.5 text-primary" />
            <span>Telemetry Studio</span>
          </Button>
        </NuxtLink>
        <NuxtLink to="/dashboard/tickets">
          <Button size="sm" class="bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg h-8 px-3.5 text-xs font-medium gap-1.5 shadow-xs transition-colors cursor-pointer">
            <AlertOctagon class="size-3.5" />
            <span>Incident Log</span>
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Section 1: Heimdall Search Heimdall Search & Global Navigation Global Navigation -->
    <Card class="bg-card border-border rounded-xl shadow-xs">
      <CardHeader class="p-5 border-b border-border">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <Search class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-foreground">
              Heimdall Search Heimdall Search & Global Navigation Global Navigation
            </CardTitle>
            <CardDescription class="text-xs text-muted-foreground mt-0.5">
              High-throughput universal indexing across all fleet nodes, machine stations, and work orders
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <p class="text-xs text-foreground/90 leading-relaxed">
          Heimdall Search provides an instant fuzzy-search indexing engine accessible throughout the application. Operators and automation engineers can locate any plant asset in milliseconds using either physical hardware IDs, network attributes, or Active Directory organizational units.
        </p>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-3 pt-1">
          <div class="p-3.5 rounded-lg bg-muted/40 border border-border space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground">Global Shortcut</span>
              <kbd class="px-2 py-0.5 rounded bg-muted border border-border text-foreground font-mono text-[11px]">Ctrl + K</kbd>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Opens the global modal search palette from any dashboard view or terminal session.
            </p>
          </div>

          <div class="p-3.5 rounded-lg bg-muted/40 border border-border space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground">Host & MAC Syntax</span>
              <Badge variant="outline" class="bg-primary/10 text-primary border-primary/30 text-[10px] font-mono">00:01:05:*</Badge>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Filter industrial IPCs by partial MAC address or hostname prefix (e.g., <code class="text-foreground font-mono">BKHF-IPC-01</code>).
            </p>
          </div>

          <div class="p-3.5 rounded-lg bg-muted/40 border border-border space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground">OU Tag Filters</span>
              <Badge variant="outline" class="bg-purple-500/10 text-purple-700 dark:text-purple-300 border-purple-500/30 text-[10px] font-mono">*Line-A*</Badge>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Type OU patterns or asset tags (<code class="text-foreground font-mono">Beckhoff</code>, <code class="text-foreground font-mono">Vision</code>) to isolate clusters.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 2: OT Telemetry & Industrial Fieldbus Protocols -->
    <Card class="bg-card border-border rounded-xl shadow-xs">
      <CardHeader class="p-5 border-b border-border">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <Cpu class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-foreground">
              OT Telemetry & Industrial Fieldbus Protocols
            </CardTitle>
            <CardDescription class="text-xs text-muted-foreground mt-0.5">
              Protocol specifications, delta evaluation algorithms, and local spool encryption
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <!-- Beckhoff ADS -->
          <div class="p-4 rounded-lg bg-muted/30 border border-border space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-foreground">Beckhoff TwinCAT ADS</span>
              <Badge class="bg-primary/10 text-primary border border-primary/30 text-[10px]">High-Frequency</Badge>
            </div>
            <p class="text-xs text-muted-foreground">
              Direct symbol-based memory access using TwinCAT ADS protocol. Polls PLC cycle variables with sub-millisecond deadband filtering.
            </p>
            <div class="text-[11px] text-muted-foreground/70 font-mono pt-1">Default Port: 48898 (TCP)</div>
          </div>

          <!-- OPC UA -->
          <div class="p-4 rounded-lg bg-muted/30 border border-border space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-foreground">OPC UA Client / Gateway</span>
              <Badge class="bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border border-emerald-500/30 text-[10px]">Standardized</Badge>
            </div>
            <p class="text-xs text-muted-foreground">
              IEC 62541 compliant edge communication with mutual certificate exchange (mTLS) and binary node subscriptions.
            </p>
            <div class="text-[11px] text-muted-foreground/70 font-mono pt-1">Default Port: 4840 (TCP)</div>
          </div>

          <!-- Modbus TCP -->
          <div class="p-4 rounded-lg bg-muted/30 border border-border space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-foreground">Modbus TCP & Couplers</span>
              <Badge class="bg-amber-500/10 text-amber-700 dark:text-amber-300 border border-amber-500/30 text-[10px]">Legacy Support</Badge>
            </div>
            <p class="text-xs text-muted-foreground">
              Polling of holding registers and digital inputs from legacy fieldbus bus couplers and remote I/O blocks.
            </p>
            <div class="text-[11px] text-muted-foreground/70 font-mono pt-1">Default Port: 502 (TCP)</div>
          </div>
        </div>

        <div class="p-3.5 rounded-lg bg-muted/30 border border-border flex items-center justify-between text-xs text-muted-foreground">
          <div class="flex items-center gap-2">
            <ShieldCheck class="size-4 text-primary shrink-0" />
            <span><strong class="text-foreground">Cryptographic Tamper Protection:</strong> Telemetry packets are sealed with AES-256-GCM hardware binding and verified with xxHash64 delta evaluation.</span>
          </div>
          <NuxtLink to="/dashboard/telemetry/configure" class="text-primary hover:underline font-medium whitespace-nowrap ml-4 flex items-center gap-1">
            Configure Policy <ArrowRight class="size-3" />
          </NuxtLink>
        </div>
      </CardContent>
    </Card>

    <!-- Section 3: Incident Management & Line Stop SOP -->
    <Card class="bg-card border-border rounded-xl shadow-xs">
      <CardHeader class="p-5 border-b border-border">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-rose-500/10 border border-rose-500/20 text-rose-600 dark:text-rose-400">
            <AlertOctagon class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-foreground">
              Incident Management & Line Stop Standard Operating Procedure (SOP)
            </CardTitle>
            <CardDescription class="text-xs text-muted-foreground mt-0.5">
              4-tier severity matrix, automated escalation triggers, and technician dispatch workflow
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
          <!-- Critical -->
          <div class="p-3.5 rounded-lg bg-rose-500/10 border border-rose-500/30 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-rose-700 dark:text-rose-400">Critical</span>
              <span class="text-[10px] font-mono text-rose-700 dark:text-rose-400 font-semibold">SLA: 15 min</span>
            </div>
            <p class="text-xs text-foreground font-medium">Immediate Line Halt</p>
            <p class="text-[11px] text-muted-foreground">Spindle seizure, E-stop trip, safety gate interlock failure. Triggers automated shift broadcast.</p>
          </div>

          <!-- High -->
          <div class="p-3.5 rounded-lg bg-amber-500/10 border border-amber-500/30 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-amber-700 dark:text-amber-400">High</span>
              <span class="text-[10px] font-mono text-amber-700 dark:text-amber-400 font-semibold">SLA: 1 hour</span>
            </div>
            <p class="text-xs text-foreground font-medium">Degraded Output</p>
            <p class="text-[11px] text-muted-foreground">Robot servo thermal warning, cycle time divergence > 20%, continuous minor jams.</p>
          </div>

          <!-- Medium -->
          <div class="p-3.5 rounded-lg bg-primary/10 border border-primary/30 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-primary">Medium</span>
              <span class="text-[10px] font-mono text-primary font-semibold">SLA: 4 hours</span>
            </div>
            <p class="text-xs text-foreground font-medium">Preventative Maintenance</p>
            <p class="text-[11px] text-muted-foreground">Filter replacement due, lubrication level low, camera lens calibration required.</p>
          </div>

          <!-- Low -->
          <div class="p-3.5 rounded-lg bg-muted/40 border border-border space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-muted-foreground">Low</span>
              <span class="text-[10px] font-mono text-muted-foreground font-semibold">SLA: 24 hours</span>
            </div>
            <p class="text-xs text-foreground font-medium">General Inspection</p>
            <p class="text-[11px] text-muted-foreground">Minor cosmetic fixture scratch, non-blocking label print smudge, cable dressing.</p>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 4: Security, mTLS & Active Directory Architecture -->
    <Card class="bg-card border-border rounded-xl shadow-xs">
      <CardHeader class="p-5 border-b border-border">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400">
            <ShieldCheck class="size-4" />
          </div>
          <div>
            <CardTitle class="text-base font-semibold text-foreground">
              Security Governance, mTLS & Active Directory Architecture
            </CardTitle>
            <CardDescription class="text-xs text-muted-foreground mt-0.5">
              Zero-trust identity, hardware sealing, and directory boundary enforcement
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent class="p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="p-4 rounded-lg bg-muted/30 border border-border space-y-2">
            <div class="text-xs font-semibold text-foreground">Active Directory & Entra ID Ingestion</div>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Industrial controllers are automatically mapped to security zones based on their Active Directory Organizational Unit (OU). Read/write access requires dual-approval and an assigned technical owner.
            </p>
          </div>

          <div class="p-4 rounded-lg bg-muted/30 border border-border space-y-2">
            <div class="text-xs font-semibold text-foreground">Cryptographic Ed25519 Dispatch</div>
            <p class="text-xs text-muted-foreground leading-relaxed">
              All telemetry recipe updates and remote diagnostic commands dispatched from the central platform are signed using Ed25519 digital signatures, preventing replay and man-in-the-middle attacks on the OT floor.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
