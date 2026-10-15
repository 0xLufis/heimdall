<script setup lang="ts">
import { computed } from 'vue'
import {
  Boxes,
  Cpu,
  HardDrive,
  MapPin,
  Building,
  Wrench,
  AlertTriangle,
  CheckCircle2,
  ExternalLink,
  Edit3,
  Send,
  History
} from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

const props = defineProps<{
  items: any[]
  loading?: boolean
  activeCurrency?: string
  exchangeRate?: number
  selectedId?: string
}>()

const emit = defineEmits<{
  'selectItem': [item: any]
  'editItem': [item: any]
  'useItem': [item: any]
  'auditItem': [item: any]
}>()

const getConditionBadge = (condition: string) => {
  switch (condition?.toLowerCase()) {
    case 'new':
      return { label: 'NEW', class: 'border-emerald-500/40 text-emerald-400 bg-emerald-500/10' }
    case 'used':
      return { label: 'USED', class: 'border-cyan-500/40 text-cyan-400 bg-cyan-500/10' }
    case 'donor':
      return { label: 'DONOR', class: 'border-purple-500/40 text-purple-400 bg-purple-500/10' }
    case 'obsolete':
      return { label: 'OBSOLETE', class: 'border-amber-500/40 text-amber-400 bg-amber-500/10' }
    case 'scrap':
      return { label: 'SCRAP', class: 'border-rose-500/40 text-rose-500 bg-rose-500/10' }
    default:
      return { label: condition || 'UNKNOWN', class: 'border-border text-muted-foreground' }
  }
}

const getStockStatusClass = (quantity: number, minQuantity: number = 1) => {
  if (quantity <= 0) return 'text-rose-500 border-rose-500/40 bg-rose-500/10'
  if (quantity <= minQuantity) return 'text-amber-500 border-amber-500/40 bg-amber-500/10'
  return 'text-emerald-500 border-emerald-500/40 bg-emerald-500/10'
}

const formatPrice = (priceEur: number) => {
  if (!priceEur && priceEur !== 0) return '-'
  const currency = props.activeCurrency || 'EUR'
  const rate = props.exchangeRate || 1
  const converted = priceEur * rate

  if (currency === 'HUF') {
    return `${new Intl.NumberFormat('hu-HU').format(Math.round(converted))} Ft`
  }
  if (currency === 'USD') {
    return `$${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2 }).format(converted)}`
  }
  return `€${new Intl.NumberFormat('de-DE', { minimumFractionDigits: 2 }).format(converted)}`
}
</script>

<template>
  <div>
    <!-- Skeleton loader -->
    <div v-if="loading" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      <div v-for="i in 8" :key="i" class="h-56 rounded-xl border border-border bg-card animate-pulse" />
    </div>

    <!-- Empty State -->
    <div v-else-if="items.length === 0" class="text-center py-16 border border-dashed border-border rounded-xl">
      <Boxes class="w-10 h-10 text-muted-foreground/40 mx-auto mb-3" />
      <h3 class="text-sm font-semibold text-foreground">No inventory items matched</h3>
      <p class="text-xs text-muted-foreground mt-1">Try modifying the search term or adjusting filter tags.</p>
    </div>

    <!-- Card Grid -->
    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      <Card
        v-for="item in items"
        :key="item.id"
        @click="emit('selectItem', item)"
        class="border bg-card shadow-xs hover:border-primary/50 transition-all flex flex-col justify-between group overflow-hidden cursor-pointer"
        :class="item.id === selectedId ? 'border-primary ring-1 ring-primary/40 bg-primary/5' : 'border-border'"
      >
        <CardHeader class="pb-2 pt-4 px-4">
          <div class="flex items-start justify-between gap-2">
            <div>
              <div class="flex items-center gap-1.5 flex-wrap">
                <span class="text-[10px] font-mono text-cyan-400 font-bold uppercase tracking-wider">
                  {{ item.customIdentifier || item.id?.substring(0, 8) }}
                </span>
                <span class="text-[10px] text-muted-foreground">•</span>
                <span class="text-[10px] font-mono text-muted-foreground uppercase">
                  {{ item.category || item.itemType || 'Hardware' }}
                </span>
              </div>
              <CardTitle class="text-sm font-bold text-foreground mt-1 group-hover:text-primary transition-colors line-clamp-1">
                {{ item.displayName || item.name }}
              </CardTitle>
            </div>

            <Badge
              variant="outline"
              :class="getConditionBadge(item.condition || 'new').class"
              class="text-[9px] px-1.5 py-0 font-mono shrink-0"
            >
              {{ getConditionBadge(item.condition || 'new').label }}
            </Badge>
          </div>
        </CardHeader>

        <CardContent class="pt-0 pb-4 px-4 space-y-3">
          <!-- Metadata details -->
          <div class="space-y-1.5 text-xs text-muted-foreground bg-muted/20 p-2.5 rounded-lg border border-border/50 font-mono">
            <div class="flex items-center justify-between">
              <span class="flex items-center gap-1 text-[11px] truncate max-w-[140px]">
                <Building class="w-3 h-3 text-muted-foreground shrink-0" />
                <span class="truncate">{{ item.manufacturer?.name || item.manufacturer || 'OEM Standard' }}</span>
              </span>
              <span class="text-foreground font-bold text-[11px]">
                {{ formatPrice(item.priceEur ?? item.unitPrice ?? 0) }}
              </span>
            </div>

            <div class="flex items-center justify-between text-[10px]">
              <span class="flex items-center gap-1 truncate max-w-[140px]">
                <MapPin class="w-3 h-3 text-muted-foreground shrink-0" />
                <span class="truncate">{{ item.location || item.storageLocation || 'Warehouse Central' }}</span>
              </span>
              <Badge
                variant="outline"
                :class="getStockStatusClass(item.quantity ?? item.stockQuantity ?? 1, item.minQuantity || 1)"
                class="text-[9px] px-1.5 py-0 font-mono"
              >
                Qty: {{ item.quantity ?? item.stockQuantity ?? 1 }}
              </Badge>
            </div>
          </div>

          <!-- Bottom Action Buttons -->
          <div class="flex items-center justify-between pt-1 border-t border-border/40 text-xs">
            <div class="flex items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                @click="emit('useItem', item)"
                class="h-7 px-2 text-[11px] text-muted-foreground hover:text-emerald-400 gap-1 cursor-pointer"
                title="Dispatch or use part on station"
              >
                <Send class="w-3 h-3 text-emerald-400" />
                <span>Dispatch</span>
              </Button>

              <Button
                variant="ghost"
                size="sm"
                @click="emit('auditItem', item)"
                class="h-7 px-2 text-[11px] text-muted-foreground hover:text-cyan-400 gap-1 cursor-pointer"
                title="View movement audit history"
              >
                <History class="w-3 h-3 text-cyan-400" />
                <span>History</span>
              </Button>
            </div>

            <div class="flex items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                @click="emit('editItem', item)"
                class="h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground cursor-pointer"
                title="Edit item attributes"
              >
                <Edit3 class="w-3 h-3" />
              </Button>

              <NuxtLink
                :to="`/dashboard/inventory/${item.id}`"
                class="inline-flex items-center justify-center h-7 px-2 text-[11px] text-muted-foreground hover:text-primary transition-colors"
                title="Open full asset inspector"
              >
                <ExternalLink class="w-3 h-3" />
              </NuxtLink>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>
