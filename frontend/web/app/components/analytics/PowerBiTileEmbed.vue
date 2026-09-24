<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import {
  ExternalLink,
  Copy,
  Check,
  Maximize2,
  RefreshCw,
  Share2,
  Database,
  FileSpreadsheet,
  Layers,
  ShieldCheck,
  Terminal
} from 'lucide-vue-next'

export interface PowerBiConfig {
  embedUrl: string
  reportId: string
  datasetId: string
  workspaceId: string
  isConfigured: boolean
  authStatus: string
}

const config = ref<PowerBiConfig>({
  embedUrl: 'https://app.powerbi.com/reportEmbed?reportId=71c0490e-b812-4cf4-916b-70678d781bcf&groupId=a840e39b-7e61-4191-bb21-98782f93bc01',
  reportId: '71c0490e-b812-4cf4-916b-70678d781bcf',
  datasetId: '54b98df0-1011-477b-8911-39870198ad23',
  workspaceId: 'a840e39b-7e61-4191-bb21-98782f93bc01',
  isConfigured: false,
  authStatus: 'Demonstration Mock (Local Development)'
})

const copiedField = ref<string | null>(null)

const grafanaUrl = ref('http://localhost:5099/api/v1/ReportExport/grafana/metrics')
const excelODataUrl = ref('http://localhost:5099/api/v1/ReportExport/odata/telemetry')
const machinesODataUrl = ref('http://localhost:5099/api/v1/ReportExport/odata/machines')

onMounted(async () => {
  try {
    const res = await $fetch<PowerBiConfig>('/api/proxy/v1/analytics/powerbi/config')
    if (res) config.value = res
  } catch {
    try {
      const fallback = await $fetch<PowerBiConfig>('/api/analytics/powerbi')
      if (fallback) config.value = fallback
    } catch {
      // Retain defaults
    }
  }
})

const copyText = async (text: string, field: string) => {
  try {
    await navigator.clipboard.writeText(text)
    copiedField.value = field
    setTimeout(() => {
      copiedField.value = null
    }, 2000)
  } catch {
    // Ignore fallback
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Main Power BI Embedded Container -->
    <div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-5">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div class="space-y-1">
          <div class="flex items-center gap-2.5">
            <div class="p-2 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400">
              <Layers class="w-4 h-4" />
            </div>
            <h3 class="text-sm font-semibold text-foreground">Microsoft Power BI Embedded Workspace</h3>
          </div>
          <p class="text-xs text-muted-foreground">
            Real-time executive reporting tiles embedded via Azure AD Service Principal credentials.
          </p>
        </div>

        <div class="flex items-center gap-2">
          <Badge
            :class="config.isConfigured ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30' : 'bg-muted text-muted-foreground border-border'"
            class="text-[10px] border font-mono"
          >
            {{ config.authStatus }}
          </Badge>
          <a
            :href="config.embedUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="p-2 rounded-lg bg-card hover:bg-muted border border-border text-muted-foreground hover:text-foreground transition-colors"
            title="Open Fullscreen in Power BI Portal"
          >
            <ExternalLink class="w-3.5 h-3.5" />
          </a>
        </div>
      </div>

      <!-- Power BI Viewport / Mock Container -->
      <div class="relative w-full aspect-[16/9] max-h-[420px] rounded-xl overflow-hidden border border-border bg-muted/40 flex flex-col justify-between p-6">
        <!-- Watermark / Title Bar -->
        <div class="flex items-center justify-between text-xs text-muted-foreground font-mono">
          <span class="flex items-center gap-2">
            <span class="size-2 rounded-full bg-amber-500 dark:bg-amber-400 animate-pulse"></span>
            Report: Heimdall Executive Plant Telemetry [DirectQuery]
          </span>
          <span>Workspace ID: {{ config.workspaceId }}</span>
        </div>

        <!-- Simulated Power BI Visuals Grid -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4 my-auto">
          <div class="p-4 rounded-lg bg-card border border-border space-y-2">
            <span class="text-[11px] text-muted-foreground uppercase tracking-wider block">Line 1..8 Yield</span>
            <div class="text-2xl font-bold font-mono text-emerald-600 dark:text-emerald-400">99.2%</div>
            <div class="text-[10px] text-muted-foreground/80">First-pass verified inspection yield</div>
          </div>
          <div class="p-4 rounded-lg bg-card border border-border space-y-2">
            <span class="text-[11px] text-muted-foreground uppercase tracking-wider block">Total Fleet Downtime</span>
            <div class="text-2xl font-bold font-mono text-amber-600 dark:text-amber-400">4.2 hrs</div>
            <div class="text-[10px] text-muted-foreground/80">Trailing 7 days across 100 stations</div>
          </div>
          <div class="p-4 rounded-lg bg-card border border-border space-y-2">
            <span class="text-[11px] text-muted-foreground uppercase tracking-wider block">Predictive RUL Alert</span>
            <div class="text-2xl font-bold font-mono text-teal-600 dark:text-teal-300">3 Machines</div>
            <div class="text-[10px] text-muted-foreground/80">Recommended preventive calibration</div>
          </div>
        </div>

        <div class="flex items-center justify-between text-[11px] text-muted-foreground font-mono border-t border-border pt-3">
          <span>Azure Service Principal Token: Auto-Refreshed (TTL: 3600s)</span>
          <span class="text-emerald-600 dark:text-emerald-400">Dataset Push Active</span>
        </div>
      </div>
    </div>

    <!-- Section: External BI Export Integrations (Grafana & Excel PowerQuery) -->
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <!-- 1. Grafana Infinity Plugin Card -->
      <div class="p-5 rounded-2xl bg-card border border-border space-y-3 shadow-sm flex flex-col justify-between">
        <div class="space-y-2">
          <div class="flex items-center gap-2">
            <Terminal class="w-4 h-4 text-orange-600 dark:text-orange-400" />
            <h4 class="text-xs font-semibold text-foreground">Grafana Infinity Plugin</h4>
          </div>
          <p class="text-xs text-muted-foreground">
            Connect Grafana dashboards using the Infinity JSON datasource plugin for sub-second telemetry feeds.
          </p>
        </div>

        <div class="space-y-2 pt-2">
          <div class="p-2 rounded bg-muted/50 border border-border text-[11px] font-mono text-foreground truncate select-all">
            {{ grafanaUrl }}
          </div>
          <Button
            variant="outline"
            size="sm"
            @click="copyText(grafanaUrl, 'grafana')"
            class="w-full bg-card hover:bg-muted/80 border-border text-foreground text-xs h-8 cursor-pointer"
          >
            <Check v-if="copiedField === 'grafana'" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400 mr-1.5" />
            <Copy v-else class="w-3.5 h-3.5 mr-1.5" />
            <span>{{ copiedField === 'grafana' ? 'Endpoint Copied!' : 'Copy Grafana URL' }}</span>
          </Button>
        </div>
      </div>

      <!-- 2. Excel / PowerQuery Live Telemetry -->
      <div class="p-5 rounded-2xl bg-card border border-border space-y-3 shadow-sm flex flex-col justify-between">
        <div class="space-y-2">
          <div class="flex items-center gap-2">
            <FileSpreadsheet class="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
            <h4 class="text-xs font-semibold text-foreground">Excel PowerQuery Live Feed</h4>
          </div>
          <p class="text-xs text-muted-foreground">
            Load live telemetry directly in Microsoft Excel via <code class="text-emerald-600 dark:text-emerald-400 text-[10px]">Data &gt; From Web / OData</code>.
          </p>
        </div>

        <div class="space-y-2 pt-2">
          <div class="p-2 rounded bg-muted/50 border border-border text-[11px] font-mono text-foreground truncate select-all">
            {{ excelODataUrl }}
          </div>
          <Button
            variant="outline"
            size="sm"
            @click="copyText(excelODataUrl, 'excel')"
            class="w-full bg-card hover:bg-muted/80 border-border text-foreground text-xs h-8 cursor-pointer"
          >
            <Check v-if="copiedField === 'excel'" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400 mr-1.5" />
            <Copy v-else class="w-3.5 h-3.5 mr-1.5" />
            <span>{{ copiedField === 'excel' ? 'OData URL Copied!' : 'Copy Telemetry OData' }}</span>
          </Button>
        </div>
      </div>

      <!-- 3. Machine Registry OData Feed -->
      <div class="p-5 rounded-2xl bg-card border border-border space-y-3 shadow-sm flex flex-col justify-between">
        <div class="space-y-2">
          <div class="flex items-center gap-2">
            <Database class="w-4 h-4 text-teal-600 dark:text-teal-400" />
            <h4 class="text-xs font-semibold text-foreground">Machines OData Catalog</h4>
          </div>
          <p class="text-xs text-muted-foreground">
            Query the full 100-machine asset taxonomy with schema definitions in third-party reporting tools.
          </p>
        </div>

        <div class="space-y-2 pt-2">
          <div class="p-2 rounded bg-muted/50 border border-border text-[11px] font-mono text-foreground truncate select-all">
            {{ machinesODataUrl }}
          </div>
          <Button
            variant="outline"
            size="sm"
            @click="copyText(machinesODataUrl, 'machines')"
            class="w-full bg-card hover:bg-muted/80 border-border text-foreground text-xs h-8 cursor-pointer"
          >
            <Check v-if="copiedField === 'machines'" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400 mr-1.5" />
            <Copy v-else class="w-3.5 h-3.5 mr-1.5" />
            <span>{{ copiedField === 'machines' ? 'Catalog URL Copied!' : 'Copy Machines OData' }}</span>
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
