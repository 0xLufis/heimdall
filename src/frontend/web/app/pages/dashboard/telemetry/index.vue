<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Activity,
  Cpu,
  Layers,
  SlidersHorizontal,
  Bell,
  RefreshCw,
  Search,
  CheckCircle2,
  AlertTriangle
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import { Input } from '~/components/ui/input'
import AlertRulesManager from '~/components/analytics/AlertRulesManager.vue'
import LiveTelemetryGrid from '~/components/telemetry/LiveTelemetryGrid.vue'
import { useControllers } from '~/composables/useControllers'
import { useTelemetryStream } from '~/composables/useTelemetryStream'
import { useTelemetryMetrics } from '~/composables/useTelemetryMetrics'
import { useTelemetryTemplates } from '~/composables/useTelemetryTemplates'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const router = useRouter()

type TelemetryTab = 'live' | 'templates' | 'configurations' | 'alerts'
const activeTab = ref<TelemetryTab>('live')

watch(() => route.query.tab, (newTab) => {
  if (newTab && ['live', 'templates', 'configurations', 'alerts'].includes(newTab as string)) {
    activeTab.value = newTab as TelemetryTab
  }
}, { immediate: true })

const setTab = (tab: TelemetryTab) => {
  activeTab.value = tab
  router.push({ query: { ...route.query, tab } })
}

const { controllers, isLoading: controllersLoading, fetchControllers } = useControllers()
const { metrics, catalogItems, isCatalogLoading, fetchCatalog } = useTelemetryMetrics()
const { templates, activeTemplate, setActiveTemplate } = useTelemetryTemplates()

// Controller selection
const selectedControllerId = ref<string>('')
const activeController = computed(() => {
  if (selectedControllerId.value) {
    const found = controllers.value.find(c => c.id === selectedControllerId.value)
    if (found) return found
  }
  return controllers.value[0] || null
})

// Bind real SignalR telemetry stream
const stream = useTelemetryStream({
  targetHostname: activeController.value?.hostname,
  bufferCapacity: 120
})

// Watch controller selection changes and re-connect stream
watch(() => activeController.value?.hostname, (newHost) => {
  if (newHost) {
    stream.disconnect()
    stream.connect()
  }
})

// Catalog Search
const catalogSearch = ref('')
const filteredCatalog = computed(() => {
  if (!catalogSearch.value) return catalogItems.value
  const q = catalogSearch.value.toLowerCase()
  return catalogItems.value.filter(item =>
    item.name.toLowerCase().includes(q) ||
    item.key.toLowerCase().includes(q) ||
    item.pathOrSymbol?.toLowerCase().includes(q)
  )
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-2">
          <Activity class="w-6 h-6 text-primary" />
          <span>Telemetry & Edge Monitoring Hub</span>
        </h1>
        <p class="text-xs text-muted-foreground mt-1">
          Real-time TwinCAT ADS, OPC-UA, and high-frequency soft-PLC stream analytics
        </p>
      </div>

      <div class="flex items-center gap-2">
        <!-- Target Controller Selector -->
        <select
          v-model="selectedControllerId"
          class="h-8 px-2.5 bg-background border border-border rounded-lg text-xs font-mono text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
        >
          <option value="">Default Station</option>
          <option v-for="c in controllers" :key="c.id" :value="c.id">
            {{ c.hostname }} ({{ c.name }})
          </option>
        </select>

        <Button
          variant="outline"
          size="sm"
          @click="fetchControllers(false)"
          :disabled="controllersLoading"
          class="h-8 text-xs gap-1.5"
        >
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': controllersLoading }" />
          <span>Refresh</span>
        </Button>
      </div>
    </div>

    <!-- Tabs Navigation -->
    <div class="flex items-center gap-1 border-b border-border pb-1 overflow-x-auto">
      <button
        type="button"
        @click="setTab('live')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'live' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Activity class="w-3.5 h-3.5" />
        <span>Live Telemetry</span>
      </button>

      <button
        type="button"
        @click="setTab('templates')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'templates' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Layers class="w-3.5 h-3.5" />
        <span>Layout Templates</span>
        <Badge variant="outline" class="text-[9px] px-1 py-0 font-mono">{{ templates.length }}</Badge>
      </button>

      <button
        type="button"
        @click="setTab('configurations')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'configurations' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <SlidersHorizontal class="w-3.5 h-3.5" />
        <span>Data Point Catalog</span>
      </button>

      <button
        type="button"
        @click="setTab('alerts')"
        class="px-3.5 py-1.5 text-xs font-medium rounded-lg transition-colors flex items-center gap-2 cursor-pointer"
        :class="activeTab === 'alerts' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'"
      >
        <Bell class="w-3.5 h-3.5" />
        <span>Alert Rules</span>
      </button>
    </div>

    <!-- Tab 1: Live Streaming -->
    <div v-if="activeTab === 'live'" class="space-y-6">
      <LiveTelemetryGrid
        :stream="stream"
        :station-name="activeController?.name || activeController?.hostname"
      />
    </div>

    <!-- Tab 2: Layout Templates -->
    <div v-else-if="activeTab === 'templates'" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="tmpl in templates"
        :key="tmpl.id"
        class="border border-border bg-card shadow-xs transition-all hover:border-primary/50 flex flex-col justify-between"
      >
        <CardHeader>
          <div class="flex items-start justify-between">
            <div>
              <CardTitle class="text-sm font-bold text-foreground">
                {{ tmpl.name }}
              </CardTitle>
              <CardDescription class="text-xs mt-1">
                {{ tmpl.description }}
              </CardDescription>
            </div>
            <Badge v-if="activeTemplate?.id === tmpl.id" variant="default" class="text-[10px] px-2 py-0.5">
              Active
            </Badge>
          </div>
        </CardHeader>
        <CardContent class="pt-0 space-y-3">
          <div class="text-[11px] text-muted-foreground font-mono flex items-center justify-between">
            <span>Widgets: {{ tmpl.widgets?.length || 4 }}</span>
            <span>Layout: {{ tmpl.layoutType || 'Grid 3x2' }}</span>
          </div>
          <Button
            variant="outline"
            size="sm"
            @click="setActiveTemplate(tmpl)"
            class="w-full text-xs cursor-pointer"
          >
            {{ activeTemplate?.id === tmpl.id ? 'Currently Applied' : 'Apply Layout Template' }}
          </Button>
        </CardContent>
      </Card>
    </div>

    <!-- Tab 3: Data Point Catalog -->
    <div v-else-if="activeTab === 'configurations'" class="space-y-4">
      <div class="flex items-center justify-between gap-3 p-3 bg-muted/20 border border-border rounded-xl">
        <div class="relative flex-1 max-w-sm">
          <Search class="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-muted-foreground" />
          <Input
            v-model="catalogSearch"
            placeholder="Search PLC symbols or telemetry keys..."
            class="pl-8 text-xs h-8 bg-background border-border"
          />
        </div>
        <Badge variant="outline" class="text-[10px] font-mono">
          {{ filteredCatalog.length }} Signals Registered
        </Badge>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <Card v-for="item in filteredCatalog" :key="item.id" class="border border-border bg-card shadow-xs">
          <CardHeader class="pb-2">
            <div class="flex items-start justify-between">
              <div>
                <span class="text-[10px] font-mono text-cyan-500 uppercase">{{ item.category }}</span>
                <CardTitle class="text-xs font-bold text-foreground mt-0.5">{{ item.name }}</CardTitle>
              </div>
              <Badge variant="outline" class="text-[9px] font-mono">{{ item.unit || 'raw' }}</Badge>
            </div>
          </CardHeader>
          <CardContent class="text-xs space-y-1 font-mono text-muted-foreground">
            <div class="truncate text-[10px]">Symbol: {{ item.pathOrSymbol }}</div>
            <div class="text-[10px]">Freq: {{ item.updateFrequencyMs || 100 }}ms</div>
          </CardContent>
        </Card>
      </div>
    </div>

    <!-- Tab 4: Alert Rules -->
    <div v-else-if="activeTab === 'alerts'">
      <AlertRulesManager />
    </div>
  </div>
</template>
