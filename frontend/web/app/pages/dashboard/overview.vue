<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useAuthSession } from '~/composables/useAuthSession'
import { useDashboard } from '~/composables/useDashboard'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import type { PinnedTelemetryWidget } from '~/types/telemetryMetrics'
import HeimdallSearchBar from '~/components/search/HeimdallSearchBar.vue'
import type { SearchInstanceConfig } from '~/types/search'
import { Pin, Activity, ExternalLink, X } from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Button } from '~/components/ui/button'

definePageMeta({
  layout: 'shadcn-dashboard',
  alias: ['/dashboard']
})

const { user, userRole } = useAuthSession()
const { stats, recentClients, securityEvents } = useDashboard()
const { getPinnedWidgets, unpinFromOverview } = useTelemetryMetrics()

const pinnedTelemetry = ref<PinnedTelemetryWidget[]>([])

const loadPinned = () => {
  pinnedTelemetry.value = getPinnedWidgets()
}

onMounted(() => {
  loadPinned()
  if (typeof window !== 'undefined') {
    window.addEventListener('heimdall:pinned-telemetry-changed', loadPinned)
  }
})

onUnmounted(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('heimdall:pinned-telemetry-changed', loadPinned)
  }
})

const dashboardSearchConfig: SearchInstanceConfig = {
  instanceId: 'dashboard',
  placeholder: 'Search stations, controllers, assets, telemetry, or incident numbers...',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  enableAutoTagging: true,
  showGlobalShortcut: true
}
</script>

<template>
  <div class="space-y-8 pb-12">
    <!-- Hero Section -->
    <DashboardHero 
      :user-name="user?.name || 'Operator'" 
      :user-role="userRole"
    />

    <!-- Quick Search Bar -->
    <div class="max-w-4xl mx-auto w-full">
      <HeimdallSearchBar :config="dashboardSearchConfig" />
    </div>

    <!-- Pinned Critical Telemetry Section -->
    <div v-if="pinnedTelemetry.length > 0" class="space-y-3">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Pin class="w-4 h-4 text-primary" />
          <h3 class="text-xs font-bold uppercase tracking-wider text-foreground">Pinned Critical Telemetry</h3>
          <Badge variant="outline" class="text-[10px] px-2 py-0.5 font-mono">
            {{ pinnedTelemetry.length }} Pinned
          </Badge>
        </div>
        <NuxtLink to="/dashboard/telemetry?tab=live" class="text-xs text-primary hover:underline flex items-center gap-1">
          <span>Manage Telemetry Hub</span>
          <ExternalLink class="w-3 h-3" />
        </NuxtLink>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card
          v-for="p in pinnedTelemetry"
          :key="p.id"
          class="border border-border bg-card shadow-xs group relative"
        >
          <CardHeader class="pb-1 pt-3 px-4">
            <div class="flex items-start justify-between">
              <div>
                <span class="text-[10px] font-mono text-muted-foreground block truncate max-w-[140px]">{{ p.machineName }}</span>
                <CardTitle class="text-xs font-bold text-foreground mt-0.5 truncate max-w-[150px]">{{ p.metricName }}</CardTitle>
              </div>
              <button
                type="button"
                @click="unpinFromOverview(p.id)"
                class="opacity-0 group-hover:opacity-100 p-1 text-muted-foreground hover:text-rose-500 rounded transition-opacity cursor-pointer"
                title="Unpin widget"
              >
                <X class="w-3 h-3" />
              </button>
            </div>
          </CardHeader>
          <CardContent class="pb-3 px-4">
            <div class="flex items-baseline justify-between">
              <span class="font-mono text-xl font-bold text-foreground">{{ p.currentValue }} <span class="text-[11px] font-normal text-muted-foreground">{{ p.unit }}</span></span>
              <Badge variant="outline" class="text-[9px] px-1.5 py-0 border-emerald-500/30 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10">
                Live
              </Badge>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- Stats Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <DashboardStatCard v-for="stat in stats" :key="stat.title" v-bind="stat">
        <template #icon>
          <component :is="stat.icon" class="h-6 w-6" />
        </template>
      </DashboardStatCard>
    </div>

    <!-- Main Content -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
      <div class="lg:col-span-2">
        <DashboardClientPreview :clients="recentClients" />
      </div>
      <div>
        <DashboardActivityFeed :events="securityEvents" />
      </div>
    </div>
  </div>
</template>
