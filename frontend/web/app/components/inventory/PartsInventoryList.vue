<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Boxes,
  Cpu,
  PackagePlus,
  Send,
  QrCode,
  History,
  Layers,
  Search,
  DollarSign,
  AlertTriangle,
  CheckCircle2,
  MapPin,
  Building,
  User,
  ShieldAlert,
  ArrowRightLeft
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type {
  InventoryPart,
  PartTrackingType,
  PartLifecycleCondition,
  PartOperationalState
} from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  parts: InventoryPart[]
  loading: boolean
  kpis: any
}>()

const emit = defineEmits<{
  (e: 'logPart'): void
  (e: 'bulkIntake'): void
  (e: 'usePart', part: InventoryPart): void
  (e: 'scanCode'): void
  (e: 'viewAudit', partId?: string): void
  (e: 'filterChange', filters: any): void
}>()

const { formatCurrency, activeCurrency, canUsePart, canLogPart } = usePartsInventory()

const searchQuery = ref('')
const filterTracking = ref<string>('all')
const filterCondition = ref<string>('all')
const filterOperational = ref<string>('all')
const filterAlert = ref<string>('all')

const applyFilters = () => {
  emit('filterChange', {
    search: searchQuery.value,
    trackingType: filterTracking.value,
    condition: filterCondition.value,
    operationalState: filterOperational.value,
    stockAlert: filterAlert.value
  })
}

const onFilterClick = (type: 'tracking' | 'condition' | 'operational' | 'alert', val: string) => {
  if (type === 'tracking') filterTracking.value = filterTracking.value === val ? 'all' : val
  if (type === 'condition') filterCondition.value = filterCondition.value === val ? 'all' : val
  if (type === 'operational') filterOperational.value = filterOperational.value === val ? 'all' : val
  if (type === 'alert') filterAlert.value = filterAlert.value === val ? 'all' : val
  applyFilters()
}
</script>

<template>
  <div class="space-y-4">
    <!-- Action Controls Bar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-xl bg-card border border-border shadow-xs">
      <div class="flex flex-wrap items-center gap-2">
        <Button
          v-if="canLogPart"
          size="sm"
          @click="emit('logPart')"
          class="bg-primary text-primary-foreground hover:bg-primary/90 text-xs h-8 gap-1.5 cursor-pointer"
        >
          <PackagePlus class="size-3.5" />
          <span>Log Part</span>
        </Button>

        <Button
          v-if="canLogPart"
          variant="outline"
          size="sm"
          @click="emit('bulkIntake')"
          class="text-xs h-8 gap-1.5 cursor-pointer"
        >
          <Layers class="size-3.5 text-primary" />
          <span>Bulk Intake</span>
        </Button>

        <Button
          variant="outline"
          size="sm"
          @click="emit('scanCode')"
          class="text-xs h-8 gap-1.5 border-primary/30 hover:border-primary text-primary cursor-pointer"
        >
          <QrCode class="size-3.5" />
          <span>Scan Code (QR / RFID)</span>
        </Button>

        <Button
          variant="ghost"
          size="sm"
          @click="emit('viewAudit')"
          class="text-xs h-8 gap-1.5 text-muted-foreground hover:text-foreground cursor-pointer"
        >
          <History class="size-3.5" />
          <span>Audit Log</span>
        </Button>
      </div>

      <!-- Currency Switcher -->
      <div class="flex items-center gap-1.5 self-end sm:self-auto">
        <span class="text-xs text-muted-foreground font-medium">Currency:</span>
        <div class="flex p-0.5 bg-muted/60 rounded-lg border border-border gap-1">
          <button
            v-for="curr in ['EUR', 'HUF', 'USD', 'GBP'] as const"
            :key="curr"
            type="button"
            @click="activeCurrency = curr; applyFilters()"
            class="px-2 py-1 rounded text-xs font-mono font-semibold transition-colors cursor-pointer"
            :class="activeCurrency === curr ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          >
            {{ curr }}
          </button>
        </div>
      </div>
    </div>

    <!-- KPI Metric Filter Pills -->
    <div class="flex flex-wrap items-center gap-2">
      <button
        type="button"
        @click="filterTracking = 'all'; filterCondition = 'all'; filterOperational = 'all'; filterAlert = 'all'; applyFilters()"
        class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
        :class="filterTracking === 'all' && filterCondition === 'all' && filterOperational === 'all' && filterAlert === 'all' ? 'bg-primary/15 border-primary text-primary ring-1 ring-primary/40' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
      >
        <Boxes class="size-3.5 text-primary" />
        <span>Total:</span>
        <span class="font-mono font-bold text-foreground">{{ kpis.totalPartsCount }}</span>
      </button>

      <button
        type="button"
        @click="onFilterClick('tracking', 'serialized')"
        class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
        :class="filterTracking === 'serialized' ? 'bg-teal-500/15 border-teal-500 text-teal-600 ring-1 ring-teal-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
      >
        <Cpu class="size-3.5 text-teal-600" />
        <span>Serialized:</span>
        <span class="font-mono font-bold text-foreground">{{ kpis.serializedCount }}</span>
      </button>

      <button
        type="button"
        @click="onFilterClick('tracking', 'bulk')"
        class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
        :class="filterTracking === 'bulk' ? 'bg-purple-500/15 border-purple-500 text-purple-600 ring-1 ring-purple-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
      >
        <Boxes class="size-3.5 text-purple-600" />
        <span>Bulk Stock Units:</span>
        <span class="font-mono font-bold text-foreground">{{ kpis.bulkCount }}</span>
      </button>

      <button
        type="button"
        @click="onFilterClick('operational', 'evaluation')"
        class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
        :class="filterOperational === 'evaluation' ? 'bg-amber-500/15 border-amber-500 text-amber-600 ring-1 ring-amber-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
      >
        <span>Evaluation Pending:</span>
        <span class="font-mono font-bold text-foreground">{{ kpis.evaluationCount }}</span>
      </button>

      <button
        type="button"
        @click="onFilterClick('condition', 'scrap')"
        class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
        :class="filterCondition === 'scrap' ? 'bg-rose-500/15 border-rose-500 text-rose-600 ring-1 ring-rose-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
      >
        <span>Scrap / Can Dispose:</span>
        <span class="font-mono font-bold text-foreground">{{ kpis.scrapConditionCount }}</span>
      </button>

      <button
        type="button"
        @click="onFilterClick('alert', 'low_stock')"
        class="flex items-center gap-2 px-3 py-1 rounded-lg border text-xs font-medium transition-all cursor-pointer"
        :class="filterAlert === 'low_stock' ? 'bg-amber-500/15 border-amber-500 text-amber-600 ring-1 ring-amber-500/40' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
      >
        <AlertTriangle class="size-3.5 text-amber-500" />
        <span>Low Stock:</span>
        <span class="font-mono font-bold text-foreground">{{ kpis.lowStockAlertCount }}</span>
      </button>

      <div class="flex items-center gap-2 px-3 py-1 rounded-lg bg-card border border-border text-xs font-medium">
        <DollarSign class="size-3.5 text-amber-500" />
        <span class="text-muted-foreground">Warehouse Valuation:</span>
        <span class="font-mono font-bold text-foreground">
          {{ formatCurrency(kpis.totalWarehouseValuationConverted, activeCurrency) }} {{ activeCurrency }}
        </span>
      </div>
    </div>

    <!-- Search Input & Quick Keyword Filter -->
    <div class="relative w-full">
      <Search class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
      <Input
        v-model="searchQuery"
        placeholder="Filter parts by name, custom identifier (IPC-1001), serial, manufacturer, or location..."
        class="pl-9 h-9 text-xs"
        @input="applyFilters"
      />
    </div>

    <!-- Parts Table -->
    <div class="rounded-xl border border-border bg-card overflow-hidden shadow-xs">
      <div class="overflow-x-auto">
        <table class="w-full text-xs text-left">
          <thead class="bg-muted/40 text-muted-foreground border-b border-border font-medium">
            <tr>
              <th class="p-3 min-w-[200px]">Part Identifier & Name</th>
              <th class="p-3 w-28">Tracking</th>
              <th class="p-3 w-32">Condition</th>
              <th class="p-3 w-36">Operational State</th>
              <th class="p-3 w-28 text-center">Stock / Alert</th>
              <th class="p-3 min-w-[140px]">Location</th>
              <th class="p-3 w-32 text-right">Valuation ({{ activeCurrency }})</th>
              <th class="p-3 w-28 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-if="parts.length === 0" class="text-center">
              <td colspan="8" class="p-8 text-muted-foreground text-xs">
                No parts matching current filters. Try resetting search or log a new part.
              </td>
            </tr>

            <tr v-for="part in parts" :key="part.id" class="hover:bg-muted/20 transition-colors">
              <!-- Name & Identifier -->
              <td class="p-3">
                <div class="flex items-center gap-2">
                  <span class="font-mono font-bold text-primary">{{ part.customIdentifier }}</span>
                  <span class="font-semibold text-foreground">{{ part.name }}</span>
                </div>
                <div class="flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground mt-0.5">
                  <span>{{ part.manufacturer.name }}</span>
                  <span>·</span>
                  <span class="font-mono text-[10px]">{{ part.serialNumber || 'Batch stock' }}</span>
                  <span v-if="part.alternateParts && part.alternateParts.length > 0" class="flex items-center gap-1 text-teal-600 dark:text-teal-400 font-mono text-[10px]">
                    <ArrowRightLeft class="size-3" />
                    <span>{{ part.alternateParts.length }} alt</span>
                  </span>
                </div>
              </td>

              <!-- Tracking Type -->
              <td class="p-3">
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wider"
                  :class="part.trackingType === 'serialized' ? 'bg-teal-500/15 text-teal-600 dark:text-teal-400' : 'bg-purple-500/15 text-purple-600 dark:text-purple-400'"
                >
                  {{ part.trackingType }}
                </span>
              </td>

              <!-- Lifecycle Condition -->
              <td class="p-3">
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wider"
                  :class="{
                    'bg-emerald-500/15 text-emerald-600': part.condition === 'new',
                    'bg-blue-500/15 text-blue-600': part.condition === 'used',
                    'bg-amber-500/15 text-amber-600': part.condition === 'donor',
                    'bg-neutral-500/15 text-neutral-600': part.condition === 'obsolete',
                    'bg-rose-500/15 text-rose-600 font-bold': part.condition === 'scrap'
                  }"
                >
                  {{ part.condition }}
                </span>
              </td>

              <!-- Operational State -->
              <td class="p-3">
                <span
                  v-if="part.operationalState"
                  class="px-2 py-0.5 rounded text-[10px] font-medium"
                  :class="{
                    'bg-emerald-500/15 text-emerald-600': part.operationalState === 'working',
                    'bg-purple-500/15 text-purple-600': part.operationalState === 'in_service',
                    'bg-amber-500/15 text-amber-600': part.operationalState === 'evaluation',
                    'bg-destructive/15 text-destructive font-bold': part.operationalState === 'broken'
                  }"
                >
                  {{ part.operationalState.replace('_', ' ') }}
                </span>
                <span v-else class="text-[11px] text-muted-foreground">Bulk Item</span>
              </td>

              <!-- Stock & Alert Status -->
              <td class="p-3 text-center">
                <div class="font-mono font-bold text-foreground">
                  {{ part.quantity }}
                  <span v-if="part.trackingType === 'bulk'" class="text-[10px] text-muted-foreground font-normal">
                    (min {{ part.effectiveMinQuantity !== undefined && part.effectiveMinQuantity !== part.minQuantity ? `${part.minQuantity} [eff: ${part.effectiveMinQuantity}]` : part.minQuantity }})
                  </span>
                </div>
                <div class="mt-0.5">
                  <span
                    class="px-1.5 py-0.5 rounded text-[9px] font-semibold uppercase tracking-wider inline-flex items-center gap-0.5"
                    :class="{
                      'text-emerald-600 bg-emerald-500/10': part.stockAlertStatus === 'optimal',
                      'text-amber-600 bg-amber-500/10 font-bold': part.stockAlertStatus === 'low_stock',
                      'text-destructive bg-destructive/10 font-bold': part.stockAlertStatus === 'out_of_stock'
                    }"
                  >
                    {{ part.stockAlertStatus.replace('_', ' ') }}
                  </span>
                </div>
              </td>

              <!-- Location -->
              <td class="p-3 text-[11px] text-muted-foreground">
                <div class="flex items-center gap-1">
                  <MapPin class="size-3 text-muted-foreground shrink-0" />
                  <span class="truncate max-w-[150px]">{{ part.location }}</span>
                </div>
              </td>

              <!-- Valuation -->
              <td class="p-3 text-right">
                <div class="font-mono font-semibold text-foreground">
                  {{ formatCurrency(part.priceCustomCurrency || part.priceEur, activeCurrency) }}
                </div>
                <div class="text-[10px] font-mono text-muted-foreground">
                  Resell: €{{ part.estimatedResellPriceEur }}
                </div>
              </td>

              <!-- Actions -->
              <td class="p-3 text-right">
                <Button
                  v-if="part.quantity > 0 && part.condition !== 'scrap' && canUsePart"
                  size="sm"
                  @click="emit('usePart', part)"
                  class="h-7 px-2.5 text-xs bg-amber-600 hover:bg-amber-700 text-white gap-1 cursor-pointer"
                >
                  <Send class="size-3" />
                  <span>Use</span>
                </Button>
                <Button
                  v-else
                  variant="ghost"
                  size="sm"
                  @click="emit('viewAudit', part.id)"
                  class="h-7 px-2 text-xs text-muted-foreground hover:text-foreground cursor-pointer"
                >
                  Audit
                </Button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
