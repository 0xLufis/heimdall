<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  ChevronRight,
  Cpu,
  Layers,
  HardDrive,
  Edit3,
  ArrowUpRight,
  Send,
  History,
  Building,
  MapPin,
  ExternalLink
} from 'lucide-vue-next'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '~/components/ui/table'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'

const props = defineProps<{
  items: any[]
  type?: string
  classification?: string
  tracking?: string
  loading?: boolean
  columns?: Record<string, boolean>
  isChild?: boolean
  activeCurrency?: string
  exchangeRate?: number
  selectedId?: string
}>()

const emit = defineEmits<{
  (e: 'select', item: any): void
  (e: 'edit', item: any): void
  (e: 'use', item: any): void
  (e: 'audit', item: any): void
}>()

const formatCurrency = (val: any) => {
  if (!val && val !== 0) return '-'
  const currency = props.activeCurrency || 'EUR'
  const rate = props.exchangeRate || 1
  const converted = Number(val) * rate

  if (currency === 'HUF') {
    return `${new Intl.NumberFormat('hu-HU').format(Math.round(converted))} Ft`
  }
  if (currency === 'USD') {
    return `$${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2 }).format(converted)}`
  }
  return `€${new Intl.NumberFormat('de-DE', { minimumFractionDigits: 2 }).format(converted)}`
}

const getStockBadge = (quantity: number = 0, minQuantity: number = 1) => {
  if (quantity <= 0) {
    return { label: 'Depleted (0)', class: 'border-rose-500/40 text-rose-500 bg-rose-500/10' }
  }
  if (quantity <= minQuantity) {
    return { label: `Low (${quantity})`, class: 'border-amber-500/40 text-amber-500 bg-amber-500/10' }
  }
  return { label: `In Stock (${quantity})`, class: 'border-emerald-500/40 text-emerald-500 bg-emerald-500/10' }
}
</script>

<template>
  <Card class="border-border shadow-xs overflow-hidden bg-card" :class="{'!bg-transparent !shadow-none !border-none': isChild}">
    <CardContent class="p-0">
      <!-- Loading Skeleton -->
      <div v-if="loading" class="p-4 space-y-3">
        <div v-for="i in 5" :key="i" class="h-10 rounded-lg bg-muted/40 animate-pulse" />
      </div>

      <!-- Empty State -->
      <div v-else-if="items.length === 0" class="text-center py-12 text-xs font-mono text-muted-foreground">
        No inventory items match the current criteria.
      </div>

      <!-- Responsive Data Table -->
      <div v-else class="overflow-x-auto">
        <Table>
          <TableHeader class="bg-muted/40">
            <TableRow class="border-b border-border hover:bg-transparent">
              <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs min-w-[220px]">
                Asset Identity & Class
              </TableHead>
              <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs min-w-[140px]">
                Manufacturer
              </TableHead>
              <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs min-w-[130px]">
                Location / Storage
              </TableHead>
              <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs min-w-[110px]">
                Stock Level
              </TableHead>
              <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs min-w-[100px] text-right">
                Unit Value
              </TableHead>
              <TableHead class="px-4 py-3 uppercase tracking-wider font-semibold text-muted-foreground text-xs min-w-[130px] text-right">
                Actions
              </TableHead>
            </TableRow>
          </TableHeader>

          <TableBody>
            <TableRow
              v-for="item in items"
              :key="item.id"
              v-ctx:inventory="item"
              @click="emit('select', item)"
              class="border-b border-border/50 hover:bg-muted/40 transition-colors group text-xs cursor-pointer"
              :class="item.id === selectedId ? 'bg-primary/10 border-l-2 border-l-primary' : ''"
            >
              <!-- Asset Identity -->
              <TableCell class="px-4 py-3">
                <div class="flex items-center gap-2.5">
                  <div class="p-1.5 rounded-lg bg-primary/10 text-primary border border-primary/20 shrink-0">
                    <component :is="(item.category || item.itemType || '').toLowerCase().includes('soft') ? HardDrive : Cpu" class="w-4 h-4" />
                  </div>
                  <div>
                    <div class="flex items-center gap-1.5">
                      <span class="font-mono text-[10px] text-cyan-400 font-bold uppercase">
                        {{ item.customIdentifier || item.id?.substring(0, 8) }}
                      </span>
                      <Badge variant="outline" class="text-[9px] px-1 py-0 font-mono text-muted-foreground">
                        {{ item.category || item.itemType || 'Hardware' }}
                      </Badge>
                    </div>
                    <div class="font-semibold text-foreground mt-0.5 line-clamp-1 group-hover:text-primary transition-colors">
                      {{ item.displayName || item.name }}
                    </div>
                  </div>
                </div>
              </TableCell>

              <!-- Manufacturer -->
              <TableCell class="px-4 py-3 font-mono text-muted-foreground">
                <div class="flex items-center gap-1.5 truncate max-w-[160px]">
                  <Building class="w-3.5 h-3.5 text-muted-foreground shrink-0" />
                  <span class="truncate">{{ item.manufacturer?.name || item.manufacturer || 'OEM Standard' }}</span>
                </div>
              </TableCell>

              <!-- Storage Location -->
              <TableCell class="px-4 py-3 font-mono text-muted-foreground">
                <div class="flex items-center gap-1.5 truncate max-w-[150px]">
                  <MapPin class="w-3.5 h-3.5 text-muted-foreground shrink-0" />
                  <span class="truncate">{{ item.location || item.storageLocation || 'Warehouse Central' }}</span>
                </div>
              </TableCell>

              <!-- Stock Level -->
              <TableCell class="px-4 py-3">
                <Badge
                  variant="outline"
                  :class="getStockBadge(item.quantity ?? item.stockQuantity ?? 1, item.minQuantity || 1).class"
                  class="font-mono text-[10px] px-2 py-0.5"
                >
                  {{ getStockBadge(item.quantity ?? item.stockQuantity ?? 1, item.minQuantity || 1).label }}
                </Badge>
              </TableCell>

              <!-- Valuation -->
              <TableCell class="px-4 py-3 font-mono text-foreground font-semibold text-right">
                {{ formatCurrency(item.priceEur ?? item.unitPrice ?? 0) }}
              </TableCell>

              <!-- Actions -->
              <TableCell class="px-4 py-3 text-right">
                <div class="flex items-center justify-end gap-1">
                  <Button
                    variant="ghost"
                    size="sm"
                    @click="emit('use', item)"
                    class="h-7 w-7 p-0 text-muted-foreground hover:text-emerald-400 cursor-pointer"
                    title="Dispatch part"
                  >
                    <Send class="w-3.5 h-3.5" />
                  </Button>

                  <Button
                    variant="ghost"
                    size="sm"
                    @click="emit('audit', item)"
                    class="h-7 w-7 p-0 text-muted-foreground hover:text-cyan-400 cursor-pointer"
                    title="Audit history"
                  >
                    <History class="w-3.5 h-3.5" />
                  </Button>

                  <Button
                    variant="ghost"
                    size="sm"
                    @click="emit('edit', item)"
                    class="h-7 w-7 p-0 text-muted-foreground hover:text-foreground cursor-pointer"
                    title="Edit item"
                  >
                    <Edit3 class="w-3.5 h-3.5" />
                  </Button>

                  <NuxtLink
                    :to="`/dashboard/inventory/${item.id}`"
                    class="inline-flex items-center justify-center h-7 w-7 text-muted-foreground hover:text-primary transition-colors"
                    title="Inspect item"
                  >
                    <ArrowUpRight class="w-3.5 h-3.5" />
                  </NuxtLink>
                </div>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </CardContent>
  </Card>
</template>
