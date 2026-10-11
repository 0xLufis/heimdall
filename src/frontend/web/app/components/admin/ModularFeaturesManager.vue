<script setup lang="ts">
import { ref } from 'vue'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  LayersIcon,
  WrenchIcon,
  PackageIcon,
  ActivityIcon,
  CheckCircle2Icon,
  AlertCircleIcon,
  RotateCcwIcon,
  SparklesIcon,
  ShieldCheckIcon,
  ArrowRightIcon,
  PuzzleIcon,
  CpuIcon,
  DatabaseIcon,
  PlusIcon
} from 'lucide-vue-next'
import { useFeatureFlags } from '~/composables/useFeatureFlags'
import { usePlugins, type HeimdallPlugin } from '~/composables/usePlugins'
import { PLATFORM_MODULE_MANIFESTS, type PlatformModuleId } from '~/types/modules'
import { useAppI18n } from '~/composables/useAppI18n'

const {
  enableTicketing,
  enableInventory,
  enableTelemetry,
  isMinimalMode,
  isModuleEnabled,
  setModuleEnabled,
  resetModulesToDefault
} = useFeatureFlags()

const {
  plugins,
  enabledPlugins,
  togglePlugin,
  isPluginEnabled,
  resetPluginsToDefault
} = usePlugins()

const { t } = useAppI18n()
const notification = ref<string | null>(null)

function notify(text: string) {
  notification.value = text
  setTimeout(() => {
    notification.value = null
  }, 4000)
}

function handleToggle(moduleId: PlatformModuleId, currentVal: boolean) {
  if (moduleId === 'core') return
  const nextVal = !currentVal
  setModuleEnabled(moduleId, nextVal)
  const name = PLATFORM_MODULE_MANIFESTS[moduleId]?.name || moduleId
  notify(`${name} module ${nextVal ? 'enabled' : 'disabled'}. Navigation updated.`)
}

function applyPreset(preset: 'all' | 'telemetry-ticketing' | 'ticketing-only' | 'telemetry-only' | 'core-only') {
  if (preset === 'all') {
    setModuleEnabled('ticketing', true)
    setModuleEnabled('inventory', true)
    setModuleEnabled('telemetry', true)
    notify('Standard Monolith preset applied: All modules active.')
  } else if (preset === 'telemetry-ticketing') {
    setModuleEnabled('ticketing', true)
    setModuleEnabled('inventory', false)
    setModuleEnabled('telemetry', true)
    notify('Minimal preset applied: Telemetry + Ticketing active (Inventory disabled).')
  } else if (preset === 'ticketing-only') {
    setModuleEnabled('ticketing', true)
    setModuleEnabled('inventory', false)
    setModuleEnabled('telemetry', false)
    notify('Minimal preset applied: Maintenance Ticketing only.')
  } else if (preset === 'telemetry-only') {
    setModuleEnabled('ticketing', false)
    setModuleEnabled('inventory', false)
    setModuleEnabled('telemetry', true)
    notify('Minimal preset applied: Industrial Telemetry only.')
  } else if (preset === 'core-only') {
    setModuleEnabled('ticketing', false)
    setModuleEnabled('inventory', false)
    setModuleEnabled('telemetry', false)
    notify('Strict Lean preset applied: Core Platform only.')
  }
}

function handleReset() {
  resetModulesToDefault()
  notify('Module configuration reset to default environment configuration.')
}
</script>

<template>
  <div class="space-y-6">
    <!-- Live Notification Banner -->
    <div
      v-if="notification"
      class="p-4 rounded-xl bg-blue-500/15 text-blue-700 dark:text-blue-300 text-sm flex items-center gap-2 border border-blue-500/30 transition-all shadow-sm"
      data-testid="module-notification"
    >
      <CheckCircle2Icon class="h-4 w-4 shrink-0" />
      <span>{{ notification }}</span>
    </div>

    <!-- Governance Header Card -->
    <Card class="border-border/80 shadow-xs">
      <CardHeader>
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div class="space-y-1">
            <CardTitle class="text-lg flex items-center gap-2">
              <LayersIcon class="h-5 w-5 text-blue-500" />
              <span>Modular Feature Architecture &amp; Addon Decoupling</span>
            </CardTitle>
            <CardDescription>
              Control platform modularity and run Heimdall in minimal deployment profiles. Disabling an addon cleanly decouples its navigation, routes, and API endpoints without affecting the core registry.
            </CardDescription>
          </div>
          <div class="flex items-center gap-2">
            <Badge
              :variant="isMinimalMode ? 'outline' : 'default'"
              class="font-mono text-xs px-2.5 py-1"
              :class="isMinimalMode ? 'border-amber-500/50 bg-amber-500/10 text-amber-600 dark:text-amber-400' : 'bg-emerald-600 text-white'"
            >
              {{ isMinimalMode ? 'Minimal Mode' : 'Standard Monolith' }}
            </Badge>
            <Button
              variant="outline"
              size="sm"
              class="h-8 gap-1 text-xs cursor-pointer"
              @click="handleReset"
            >
              <RotateCcwIcon class="h-3.5 w-3.5" />
              {{ $t('common.resetDefaults') }}
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div class="space-y-4">
          <div>
            <div class="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2">{{ $t('modules.quickPresets') }}</div>
            <div class="flex flex-wrap gap-2">
              <Button
                variant="outline"
                size="sm"
                class="text-xs h-7 cursor-pointer"
                :class="{ 'bg-primary/10 border-primary text-primary': !isMinimalMode }"
                @click="applyPreset('all')"
              >
                {{ $t('modules.fullMonolith') }}
              </Button>
              <Button
                variant="outline"
                size="sm"
                class="text-xs h-7 cursor-pointer"
                @click="applyPreset('telemetry-ticketing')"
              >
                {{ $t('modules.noInventoryPreset') }}
              </Button>
              <Button
                variant="outline"
                size="sm"
                class="text-xs h-7 cursor-pointer"
                @click="applyPreset('ticketing-only')"
              >
                {{ $t('modules.presetTicketingOnly') }}
              </Button>
              <Button
                variant="outline"
                size="sm"
                class="text-xs h-7 cursor-pointer"
                @click="applyPreset('telemetry-only')"
              >
                {{ $t('modules.presetTelemetryOnly') }}
              </Button>
              <Button
                variant="outline"
                size="sm"
                class="text-xs h-7 cursor-pointer"
                @click="applyPreset('core-only')"
              >
                {{ $t('modules.presetCoreOnly') }}
              </Button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Module Matrix Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <!-- 1. Core Platform (Foundation) -->
      <Card class="border-border/80 relative overflow-hidden bg-card/60">
        <div class="absolute top-0 left-0 w-1 h-full bg-blue-500" />
        <CardHeader class="pb-3">
          <div class="flex items-start justify-between gap-2">
            <div class="flex items-center gap-2.5">
              <div class="p-2 rounded-lg bg-blue-500/10 text-blue-500 border border-blue-500/20">
                <ShieldCheckIcon class="h-5 w-5" />
              </div>
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <span>{{ PLATFORM_MODULE_MANIFESTS.core.name }}</span>
                  <Badge variant="outline" class="text-[10px] bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/30">
                    Foundation
                  </Badge>
                </CardTitle>
                <div class="text-xs text-muted-foreground mt-0.5 font-mono">v{{ PLATFORM_MODULE_MANIFESTS.core.version }}</div>
              </div>
            </div>
            <Badge class="bg-emerald-600 text-white text-xs">
              Always Active
            </Badge>
          </div>
          <CardDescription class="text-xs mt-2">
            {{ PLATFORM_MODULE_MANIFESTS.core.description }}
          </CardDescription>
        </CardHeader>
        <CardContent class="text-xs space-y-2.5 pt-0">
          <div class="text-[11px] font-semibold text-muted-foreground uppercase">Key Capabilities:</div>
          <ul class="space-y-1 text-muted-foreground list-disc list-inside">
            <li>Machine registry, station controllers &amp; plant maps</li>
            <li>Authentication, user roles, organizations &amp; Active Directory</li>
            <li>Master fleet policies &amp; endpoint PKI certificates</li>
          </ul>
        </CardContent>
      </Card>

      <!-- 2. Maintenance Ticketing -->
      <Card
        class="border-border/80 relative overflow-hidden transition-all"
        :class="enableTicketing ? 'bg-card/90 shadow-xs' : 'bg-muted/30 opacity-75'"
      >
        <div class="absolute top-0 left-0 w-1 h-full" :class="enableTicketing ? 'bg-indigo-500' : 'bg-muted-foreground/30'" />
        <CardHeader class="pb-3">
          <div class="flex items-start justify-between gap-2">
            <div class="flex items-center gap-2.5">
              <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-500 border border-indigo-500/20">
                <WrenchIcon class="h-5 w-5" />
              </div>
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <span>{{ PLATFORM_MODULE_MANIFESTS.ticketing.name }}</span>
                  <Badge variant="outline" class="text-[10px]">
                    Addon
                  </Badge>
                </CardTitle>
                <div class="text-xs text-muted-foreground mt-0.5 font-mono">v{{ PLATFORM_MODULE_MANIFESTS.ticketing.version }}</div>
              </div>
            </div>
            <button
              type="button"
              class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden"
              :class="enableTicketing ? 'bg-indigo-600' : 'bg-muted'"
              :aria-checked="enableTicketing"
              data-testid="toggle-ticketing"
              @click="handleToggle('ticketing', enableTicketing)"
            >
              <span
                class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                :class="enableTicketing ? 'translate-x-5' : 'translate-x-0'"
              />
            </button>
          </div>
          <CardDescription class="text-xs mt-2">
            {{ PLATFORM_MODULE_MANIFESTS.ticketing.description }}
          </CardDescription>
        </CardHeader>
        <CardContent class="text-xs space-y-2.5 pt-0">
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground uppercase">Dependencies:</span>
            <span class="font-mono text-xs">Core Platform</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground uppercase">Managed Routes:</span>
            <span class="font-mono text-xs text-muted-foreground">/dashboard/tickets, /dashboard/delegations</span>
          </div>
        </CardContent>
      </Card>

      <!-- 3. Inventory & Spare Parts -->
      <Card
        class="border-border/80 relative overflow-hidden transition-all"
        :class="enableInventory ? 'bg-card/90 shadow-xs' : 'bg-muted/30 opacity-75'"
      >
        <div class="absolute top-0 left-0 w-1 h-full" :class="enableInventory ? 'bg-amber-500' : 'bg-muted-foreground/30'" />
        <CardHeader class="pb-3">
          <div class="flex items-start justify-between gap-2">
            <div class="flex items-center gap-2.5">
              <div class="p-2 rounded-lg bg-amber-500/10 text-amber-500 border border-amber-500/20">
                <PackageIcon class="h-5 w-5" />
              </div>
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <span>{{ PLATFORM_MODULE_MANIFESTS.inventory.name }}</span>
                  <Badge variant="outline" class="text-[10px]">
                    Addon
                  </Badge>
                </CardTitle>
                <div class="text-xs text-muted-foreground mt-0.5 font-mono">v{{ PLATFORM_MODULE_MANIFESTS.inventory.version }}</div>
              </div>
            </div>
            <button
              type="button"
              class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden"
              :class="enableInventory ? 'bg-amber-600' : 'bg-muted'"
              :aria-checked="enableInventory"
              data-testid="toggle-inventory"
              @click="handleToggle('inventory', enableInventory)"
            >
              <span
                class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                :class="enableInventory ? 'translate-x-5' : 'translate-x-0'"
              />
            </button>
          </div>
          <CardDescription class="text-xs mt-2">
            {{ PLATFORM_MODULE_MANIFESTS.inventory.description }}
          </CardDescription>
        </CardHeader>
        <CardContent class="text-xs space-y-2.5 pt-0">
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground uppercase">Dependencies:</span>
            <span class="font-mono text-xs">Core Platform</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground uppercase">Managed Routes:</span>
            <span class="font-mono text-xs text-muted-foreground">/dashboard/inventory</span>
          </div>
        </CardContent>
      </Card>

      <!-- 4. Industrial Telemetry -->
      <Card
        class="border-border/80 relative overflow-hidden transition-all"
        :class="enableTelemetry ? 'bg-card/90 shadow-xs' : 'bg-muted/30 opacity-75'"
      >
        <div class="absolute top-0 left-0 w-1 h-full" :class="enableTelemetry ? 'bg-emerald-500' : 'bg-muted-foreground/30'" />
        <CardHeader class="pb-3">
          <div class="flex items-start justify-between gap-2">
            <div class="flex items-center gap-2.5">
              <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">
                <ActivityIcon class="h-5 w-5" />
              </div>
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <span>{{ PLATFORM_MODULE_MANIFESTS.telemetry.name }}</span>
                  <Badge variant="outline" class="text-[10px]">
                    Addon
                  </Badge>
                </CardTitle>
                <div class="text-xs text-muted-foreground mt-0.5 font-mono">v{{ PLATFORM_MODULE_MANIFESTS.telemetry.version }}</div>
              </div>
            </div>
            <button
              type="button"
              class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden"
              :class="enableTelemetry ? 'bg-emerald-600' : 'bg-muted'"
              :aria-checked="enableTelemetry"
              data-testid="toggle-telemetry"
              @click="handleToggle('telemetry', enableTelemetry)"
            >
              <span
                class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                :class="enableTelemetry ? 'translate-x-5' : 'translate-x-0'"
              />
            </button>
          </div>
          <CardDescription class="text-xs mt-2">
            {{ PLATFORM_MODULE_MANIFESTS.telemetry.description }}
          </CardDescription>
        </CardHeader>
        <CardContent class="text-xs space-y-2.5 pt-0">
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground uppercase">Dependencies:</span>
            <span class="font-mono text-xs">Core Platform</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-[11px] font-semibold text-muted-foreground uppercase">Managed Routes:</span>
            <span class="font-mono text-xs text-muted-foreground">/dashboard/telemetry, /dashboard/analytics</span>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Plugin Registry & Dynamic Addons -->
    <Card class="border-border/80 shadow-xs" data-testid="plugin-registry-card">
      <CardHeader>
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div class="space-y-1">
            <CardTitle class="text-base flex items-center gap-2">
              <PuzzleIcon class="h-4.5 w-4.5 text-indigo-500" />
              <span>Plugin System & Dynamic Feature Addons</span>
            </CardTitle>
            <CardDescription class="text-xs">
              Extensible plugin architecture for Heimdall. Registered plugins can be independently mounted, hot-toggled, and configured with custom lifecycle hooks.
            </CardDescription>
          </div>
          <div class="flex items-center gap-2">
            <Badge variant="outline" class="font-mono text-xs">
              {{ enabledPlugins.length }} / {{ plugins.length }} Active
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div
            v-for="p in plugins"
            :key="p.id"
            class="p-4 rounded-xl border border-border bg-card/60 space-y-3 flex flex-col justify-between"
            :data-testid="`plugin-card-${p.id}`"
          >
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2 font-semibold text-sm">
                  <span>{{ p.name }}</span>
                </div>
                <Badge
                  :variant="p.enabled ? 'default' : 'outline'"
                  class="text-[10px] uppercase font-mono px-2 py-0.5"
                  :class="p.enabled ? 'bg-primary text-primary-foreground' : 'text-muted-foreground'"
                >
                  {{ p.category }}
                </Badge>
              </div>
              <p class="text-xs text-muted-foreground line-clamp-2">
                {{ p.description }}
              </p>
              <div class="text-[11px] text-muted-foreground flex items-center gap-2 font-mono">
                <span>v{{ p.version }}</span>
                <span>•</span>
                <span>{{ p.author || 'Internal' }}</span>
              </div>
            </div>

            <div class="flex items-center justify-between pt-2 border-t border-border/60">
              <span class="text-xs font-medium" :class="p.enabled ? 'text-emerald-600 dark:text-emerald-400' : 'text-muted-foreground'">
                {{ p.isRequired ? 'Core Engine (Fixed)' : (p.enabled ? 'Active' : 'Disabled') }}
              </span>
              <button
                v-if="!p.isRequired"
                type="button"
                role="switch"
                :class="p.enabled ? 'bg-emerald-600' : 'bg-muted'"
                :aria-checked="p.enabled"
                :data-testid="`toggle-plugin-${p.id}`"
                @click="togglePlugin(p.id)"
                class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden"
              >
                <span
                  class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                  :class="p.enabled ? 'translate-x-5' : 'translate-x-0'"
                />
              </button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Decoupling Contracts & Dependency Graph Summary -->
    <Card class="border-border/80">
      <CardHeader>
        <CardTitle class="text-sm font-semibold flex items-center gap-2">
          <SparklesIcon class="h-4 w-4 text-blue-500" />
          <span>Module Decoupling &amp; Dependency Architecture</span>
        </CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground space-y-3">
        <p>
          Heimdall adheres to strict inward dependency rules: <strong>Core Platform</strong> does not depend on any addon module. Each addon (<span class="text-foreground font-medium">Ticketing</span>, <span class="text-foreground font-medium">Inventory</span>, <span class="text-foreground font-medium">Telemetry</span>) can be compiled, deployed, or toggled off completely without cascading failures or broken references.
        </p>
        <div class="flex flex-wrap items-center gap-2 pt-1 font-mono text-[11px]">
          <span class="px-2 py-1 rounded bg-muted text-foreground border border-border">Core Platform</span>
          <ArrowRightIcon class="h-3.5 w-3.5 text-muted-foreground" />
          <span class="px-2 py-1 rounded bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20">Maintenance Ticketing</span>
          <span class="text-muted-foreground">|</span>
          <span class="px-2 py-1 rounded bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20">Inventory &amp; Parts</span>
          <span class="text-muted-foreground">|</span>
          <span class="px-2 py-1 rounded bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20">Industrial Telemetry</span>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
