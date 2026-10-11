<script setup lang="ts">
import { ref } from 'vue'
import {
  Wrench,
  ShieldCheck,
  AlertTriangle,
  DollarSign,
  Edit2,
  X,
  Sliders,
  Layers,
  ArrowRight,
  TrendingUp,
  Cpu
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { MachineSparePartItem, SparePartsReportingSummary } from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  spareParts: MachineSparePartItem[]
  reporting: SparePartsReportingSummary | null
}>()

const emit = defineEmits<{
  (e: 'refresh'): void
}>()

const { formatCurrency, activeCurrency, updateSparePartPolicy } = usePartsInventory()

const editingPolicy = ref<MachineSparePartItem | null>(null)
const editFractionalRatio = ref<number>(0.333)
const editUpperBoundQty = ref<number | undefined>(undefined)
const editUpperBoundCost = ref<number | undefined>(undefined)
const editActiveMachines = ref<number>(6)

const startEdit = (item: MachineSparePartItem) => {
  editingPolicy.value = item
  editFractionalRatio.value = item.fractionalRatio
  editUpperBoundQty.value = item.upperBoundQuantity
  editUpperBoundCost.value = item.upperBoundCostEur
  editActiveMachines.value = item.activeMachinesInProduction
}

const cancelEdit = () => {
  editingPolicy.value = null
}

const savePolicy = async () => {
  if (!editingPolicy.value) return
  await updateSparePartPolicy(editingPolicy.value.id, {
    fractionalRatio: editFractionalRatio.value,
    upperBoundQuantity: editUpperBoundQty.value,
    upperBoundCostEur: editUpperBoundCost.value,
    activeMachinesInProduction: editActiveMachines.value
  })
  editingPolicy.value = null
  emit('refresh')
}
</script>

<template>
  <div class="space-y-4">
    <!-- Reporting KPI Overview -->
    <div v-if="reporting" class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <div class="p-3.5 rounded-xl bg-card border border-border space-y-1">
        <div class="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">Total Spare Policies</div>
        <div class="text-xl font-bold font-mono text-foreground">{{ reporting.totalDefinedSpareParts }}</div>
        <div class="text-[11px] text-muted-foreground">Cross-referenced with warehouse</div>
      </div>

      <div class="p-3.5 rounded-xl bg-card border border-border space-y-1">
        <div class="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">Coverage Rate</div>
        <div class="text-xl font-bold font-mono text-emerald-600 dark:text-emerald-400">
          {{ Math.round((reporting.totalCovered / (reporting.totalDefinedSpareParts || 1)) * 100) }}%
        </div>
        <div class="text-[11px] text-emerald-600/80">{{ reporting.totalCovered }} of {{ reporting.totalDefinedSpareParts }} covered</div>
      </div>

      <div class="p-3.5 rounded-xl bg-card border border-border space-y-1">
        <div class="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">Shortages</div>
        <div class="text-xl font-bold font-mono" :class="reporting.totalShortages > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-foreground'">
          {{ reporting.totalShortages }}
        </div>
        <div class="text-[11px] text-destructive font-medium">{{ reporting.criticalShortages }} critical empty</div>
      </div>

      <div class="p-3.5 rounded-xl bg-card border border-border space-y-1">
        <div class="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">Holding Valuation</div>
        <div class="text-xl font-bold font-mono text-foreground">
          €{{ formatCurrency(reporting.totalHoldingValuationEur, 'EUR') }}
        </div>
        <div class="text-[11px] text-muted-foreground">
          ≈ {{ formatCurrency(reporting.currencyConversions.HUF, 'HUF') }} HUF
        </div>
      </div>
    </div>

    <!-- Machine Spare Parts Cross-Reference Table -->
    <div class="rounded-xl border border-border bg-card overflow-hidden shadow-xs">
      <div class="p-4 border-b border-border flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-muted/20">
        <div>
          <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
            <Wrench class="size-4 text-primary" />
            <span>Machine Spare Parts & Policy Requirements</span>
          </h3>
          <p class="text-xs text-muted-foreground mt-0.5">
            Fractional minimums per active machine (e.g. 1 per 3) with upper-bound overrides and alternative parts
          </p>
        </div>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-xs text-left">
          <thead class="bg-muted/40 text-muted-foreground border-b border-border font-medium">
            <tr>
              <th class="p-3">Target Machine & Part</th>
              <th class="p-3 text-center">Active Lines</th>
              <th class="p-3 text-center">Fractional Policy</th>
              <th class="p-3 text-center">Upper Bounds</th>
              <th class="p-3 text-center">Required Min</th>
              <th class="p-3 text-center">On-Hand Stock</th>
              <th class="p-3 text-center">Alternatives</th>
              <th class="p-3 text-center">Coverage Status</th>
              <th class="p-3 text-right">Holding Value</th>
              <th class="p-3 text-right">Policy</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="sp in spareParts" :key="sp.id" class="hover:bg-muted/20">
              <td class="p-3">
                <div class="font-semibold text-foreground">{{ sp.partName }}</div>
                <div class="flex items-center gap-2 text-[11px] text-muted-foreground">
                  <span class="font-mono text-primary">{{ sp.customIdentifier }}</span>
                  <span>·</span>
                  <span>{{ sp.machineName }}</span>
                </div>
              </td>

              <td class="p-3 text-center font-mono font-medium">
                {{ sp.activeMachinesInProduction }}
              </td>

              <td class="p-3 text-center font-mono">
                1 / {{ Math.round(1 / (sp.fractionalRatio || 1)) }}
                <div class="text-[10px] text-muted-foreground font-normal">({{ Math.round(sp.fractionalRatio * 100) }}%)</div>
              </td>

              <td class="p-3 text-center">
                <div v-if="sp.upperBoundQuantity || sp.upperBoundCostEur" class="text-[11px] font-mono text-amber-600 dark:text-amber-400 font-medium">
                  <span v-if="sp.upperBoundQuantity">Max {{ sp.upperBoundQuantity }} qty</span>
                  <span v-if="sp.upperBoundCostEur"> · €{{ sp.upperBoundCostEur }}</span>
                </div>
                <span v-else class="text-[11px] text-muted-foreground">No cap</span>
              </td>

              <td class="p-3 text-center">
                <span class="font-mono font-bold text-foreground bg-muted px-2 py-0.5 rounded border border-border">
                  {{ sp.requiredSparesCalculated }}
                </span>
              </td>

              <td class="p-3 text-center font-mono font-bold" :class="sp.onHandSpares >= sp.requiredSparesCalculated ? 'text-emerald-600' : 'text-amber-600'">
                {{ sp.onHandSpares }}
              </td>

              <td class="p-3 text-center">
                <span v-if="sp.alternativeSparesAvailable > 0" class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold bg-teal-500/10 text-teal-600 dark:text-teal-400">
                  +{{ sp.alternativeSparesAvailable }} avail
                </span>
                <span v-else class="text-[11px] text-muted-foreground">None</span>
              </td>

              <td class="p-3 text-center">
                <span
                  class="px-2.5 py-1 rounded-full text-[10px] font-semibold uppercase tracking-wider inline-flex items-center gap-1"
                  :class="{
                    'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20': sp.coverageStatus === 'covered',
                    'bg-blue-500/15 text-blue-600 dark:text-blue-400 border border-blue-500/20': sp.coverageStatus === 'surplus',
                    'bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/20': sp.coverageStatus === 'shortage',
                    'bg-destructive/15 text-destructive border border-destructive/20': sp.coverageStatus === 'critical'
                  }"
                >
                  <ShieldCheck v-if="sp.coverageStatus === 'covered' || sp.coverageStatus === 'surplus'" class="size-3" />
                  <AlertTriangle v-else class="size-3" />
                  <span>{{ sp.coverageStatus }}</span>
                </span>
              </td>

              <td class="p-3 text-right font-mono font-medium text-foreground">
                €{{ formatCurrency(sp.totalHoldingValuationEur, 'EUR') }}
              </td>

              <td class="p-3 text-right">
                <Button
                  variant="ghost"
                  size="sm"
                  @click="startEdit(sp)"
                  class="h-7 text-[11px] gap-1 cursor-pointer text-muted-foreground hover:text-foreground"
                >
                  <Sliders class="size-3" />
                  <span>Tune</span>
                </Button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Tune Policy Modal Overlay -->
    <div v-if="editingPolicy" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
      <div class="relative w-full max-w-md bg-card border border-border rounded-xl shadow-2xl p-6 space-y-4">
        <div class="flex items-center justify-between pb-3 border-b border-border">
          <div>
            <h4 class="text-sm font-bold text-foreground">Tune Machine Spare Parts Policy</h4>
            <p class="text-xs text-muted-foreground mt-0.5">
              {{ editingPolicy.partName }} ({{ editingPolicy.machineName }})
            </p>
          </div>
          <button type="button" @click="cancelEdit" class="p-1 rounded text-muted-foreground hover:text-foreground cursor-pointer">
            <X class="size-4" />
          </button>
        </div>

        <div class="space-y-3 text-xs">
          <div class="space-y-1">
            <label class="font-semibold text-foreground">Active Machines in Production</label>
            <Input v-model.number="editActiveMachines" type="number" min="1" class="h-9 text-xs font-mono" />
          </div>

          <div class="space-y-1">
            <label class="font-semibold text-foreground">
              Fractional Minimum Ratio (e.g. 0.333 for 1 spare per 3 machines)
            </label>
            <Input v-model.number="editFractionalRatio" type="number" step="0.05" min="0.05" max="2" class="h-9 text-xs font-mono" />
            <p class="text-[11px] text-muted-foreground">
              Formula: ceil({{ editActiveMachines }} × {{ editFractionalRatio }}) = {{ Math.ceil(editActiveMachines * editFractionalRatio) }} units
            </p>
          </div>

          <div class="space-y-1">
            <label class="font-semibold text-foreground">Hard Quantity Upper Bound (Override)</label>
            <Input v-model.number="editUpperBoundQty" type="number" min="1" placeholder="Optional max quantity cap" class="h-9 text-xs font-mono" />
          </div>

          <div class="space-y-1">
            <label class="font-semibold text-foreground">Cost Upper Bound Override in EUR</label>
            <Input v-model.number="editUpperBoundCost" type="number" min="0" placeholder="Optional budget limit cap" class="h-9 text-xs font-mono" />
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-3 border-t border-border">
          <Button variant="outline" size="sm" @click="cancelEdit" class="h-8 text-xs cursor-pointer">
            Cancel
          </Button>
          <Button size="sm" @click="savePolicy" class="h-8 text-xs bg-primary text-primary-foreground hover:bg-primary/90 cursor-pointer">
            Apply Policy
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
