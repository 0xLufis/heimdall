<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { toast } from 'vue-sonner'
import {
  X,
  Boxes,
  Cpu,
  Wrench,
  History,
  Edit3,
  Send,
  AlertTriangle,
  CheckCircle2,
  DollarSign,
  Building,
  MapPin,
  Barcode,
  Layers,
  ArrowRight,
  ShieldCheck,
  Zap,
  Gauge
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import type { InventoryPart, InventoryAuditLog } from '~/types/inventory'

const props = defineProps<{
  part: InventoryPart | null
  currency: 'EUR' | 'USD' | 'HUF'
  exchangeRate: number
  machines?: Array<{ id: string; name: string }>
  costCenters?: Array<{ id: string; code: string; name: string }>
}>()

const emit = defineEmits<{
  'close': []
  'usePart': [data: any]
  'editPart': [updated: any]
  'selectStation': [stationId: string]
}>()

type InspectorTab = 'overview' | 'dispatch' | 'edit' | 'audit'
const activeTab = ref<InspectorTab>('overview')

// Format currency
const formatPrice = (val?: number) => {
  if (val === undefined || val === null || isNaN(val)) return '0 ' + props.currency
  const converted = val * (props.exchangeRate || 1.0)
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: props.currency,
    maximumFractionDigits: props.currency === 'HUF' ? 0 : 2
  }).format(converted)
}

// ── Dispatch / Use Part State ───────────────────────────────────────────────
const dispatchQty = ref(1)
const dispatchCostCenter = ref('')
const dispatchMachineId = ref('')
const dispatchReason = ref('Preventive Maintenance replacement')
const dispatching = ref(false)
const dispatchSuccess = ref(false)

const maxDispatchQty = computed(() => {
  if (!props.part) return 1
  return Math.max(1, props.part.quantity || 1)
})

const handleExecuteDispatch = async () => {
  if (!props.part) return
  dispatching.value = true
  try {
    emit('usePart', {
      partId: props.part.id,
      quantity: dispatchQty.value,
      costCenter: dispatchCostCenter.value,
      machineId: dispatchMachineId.value,
      reason: dispatchReason.value,
      timestamp: new Date().toISOString()
    })
    dispatchSuccess.value = true
    toast.success(`Dispatched ${dispatchQty.value}x ${props.part.displayName || props.part.name || props.part.customIdentifier} to ${dispatchCostCenter.value}`)
    setTimeout(() => {
      dispatchSuccess.value = false
      activeTab.value = 'overview'
    }, 1200)
  } catch (err: any) {
    toast.error(err?.message || 'Failed to dispatch part.')
  } finally {
    dispatching.value = false
  }
}

// ── Inline Edit State ──────────────────────────────────────────────────────
const editForm = ref({
  location: '',
  minQuantity: 1,
  resellPriceOverride: 0,
  operationalState: 'working' as any,
  condition: 'new' as any
})
const savingEdit = ref(false)
const editSaved = ref(false)

const initEditForm = () => {
  if (!props.part) return
  editForm.value = {
    location: props.part.location || '',
    minQuantity: props.part.minQuantity ?? 1,
    resellPriceOverride: props.part.estimatedResellPriceEur ?? props.part.priceEur ?? 0,
    operationalState: props.part.operationalState || 'working',
    condition: props.part.condition || 'new'
  }
  editSaved.value = false
}

const handleSaveInlineEdit = async () => {
  if (!props.part) return
  savingEdit.value = true
  try {
    emit('editPart', {
      ...props.part,
      location: editForm.value.location,
      minQuantity: Number(editForm.value.minQuantity),
      estimatedResellPriceEur: Number(editForm.value.resellPriceOverride),
      operationalState: editForm.value.operationalState,
      condition: editForm.value.condition,
      updatedAt: new Date().toISOString()
    })
    editSaved.value = true
    toast.success(`Updated settings for ${props.part.displayName || props.part.name || props.part.customIdentifier}`)
    setTimeout(() => {
      editSaved.value = false
      activeTab.value = 'overview'
    }, 1000)
  } catch (err: any) {
    toast.error(err?.message || 'Failed to save component settings.')
  } finally {
    savingEdit.value = false
  }
}

// ── Inline Audit Stream State ──────────────────────────────────────────────
const localAuditLogs = ref<InventoryAuditLog[]>([])
const loadingAudit = ref(false)

const loadAuditLogs = async () => {
  if (!props.part?.id) return
  loadingAudit.value = true
  try {
    const res = await $fetch<InventoryAuditLog[]>(`/api/proxy/v1/Inventory/${props.part.id}/audit`).catch(() => null)
    if (Array.isArray(res) && res.length > 0) {
      localAuditLogs.value = res
    } else {
      // Synthesize baseline logs if fresh item
      localAuditLogs.value = [
        {
          id: `aud-${props.part.id}-init`,
          partId: props.part.id,
          action: 'intake',
          performedBy: 'System Automation',
          previousQuantity: 0,
          newQuantity: props.part.quantity || 1,
          details: `Initial part intake at ${props.part.location || 'Central Crib'}`,
          timestamp: props.part.createdAt || new Date().toISOString()
        }
      ]
    }
  } catch (err: any) {
    toast.error('Unable to retrieve audit history for this item.')
  } finally {
    loadingAudit.value = false
  }
}

// ── Lifecycle & Sync ───────────────────────────────────────────────────────
watch(() => props.part, (newP) => {
  if (newP) {
    dispatchQty.value = 1
    dispatchCostCenter.value = 'CC-PROD-LINE-01'
    dispatchMachineId.value = newP.stationId || ''
    dispatchSuccess.value = false
    activeTab.value = 'overview'
    initEditForm()
    loadAuditLogs()
  }
}, { immediate: true })
</script>

<template>
  <div v-if="part" class="h-full flex flex-col bg-card border-l border-border select-none animate-in slide-in-from-right-4 duration-200">
    <!-- Header: Identity & Close Button -->
    <div class="p-4 border-b border-border bg-muted/20">
      <div class="flex items-start justify-between gap-3">
        <div class="space-y-1 min-w-0">
          <div class="flex items-center gap-2">
            <Badge variant="outline" class="font-mono text-[10px] uppercase px-1.5 py-0 border-primary/30 text-primary bg-primary/5">
              {{ part.customIdentifier || part.id }}
            </Badge>
            <Badge
              variant="outline"
              class="text-[10px] capitalize px-1.5 py-0"
              :class="[
                part.condition === 'new' ? 'border-emerald-500/40 text-emerald-400 bg-emerald-500/10' :
                part.condition === 'used' ? 'border-blue-500/40 text-blue-400 bg-blue-500/10' :
                part.condition === 'donor' ? 'border-amber-500/40 text-amber-400 bg-amber-500/10' :
                'border-rose-500/40 text-rose-400 bg-rose-500/10'
              ]"
            >
              {{ part.condition || 'new' }}
            </Badge>
            <span v-if="part.trackingType === 'serialized'" class="text-[10px] font-mono text-muted-foreground">
              SN: {{ part.serialNumber || 'N/A' }}
            </span>
          </div>

          <h3 class="text-base font-bold text-foreground truncate" :title="part.displayName || part.name">
            {{ part.displayName || part.name }}
          </h3>
          <p class="text-xs text-muted-foreground font-mono truncate">
            {{ typeof part.manufacturer === 'object' ? part.manufacturer?.name : part.manufacturer || 'OEM Component' }}
          </p>
        </div>

        <button
          type="button"
          @click="emit('close')"
          class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
          title="Close inspector"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <!-- Compact Navigation Tabs -->
      <div class="flex items-center gap-1 mt-4 p-1 bg-muted/50 rounded-lg border border-border">
        <button
          type="button"
          @click="activeTab = 'overview'"
          class="flex-1 py-1 px-2 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeTab === 'overview' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <Boxes class="w-3.5 h-3.5 text-primary" />
          <span>Specs</span>
        </button>

        <button
          type="button"
          @click="activeTab = 'dispatch'"
          class="flex-1 py-1 px-2 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeTab === 'dispatch' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <Send class="w-3.5 h-3.5 text-emerald-400" />
          <span>Dispatch</span>
        </button>

        <button
          type="button"
          @click="activeTab = 'edit'"
          class="flex-1 py-1 px-2 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeTab === 'edit' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <Edit3 class="w-3.5 h-3.5 text-cyan-400" />
          <span>Quick Edit</span>
        </button>

        <button
          type="button"
          @click="activeTab = 'audit'"
          class="flex-1 py-1 px-2 text-xs font-semibold rounded-md transition-all cursor-pointer flex items-center justify-center gap-1.5"
          :class="activeTab === 'audit' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
        >
          <History class="w-3.5 h-3.5 text-amber-400" />
          <span>Audit Log</span>
        </button>
      </div>
    </div>

    <!-- Tab 1: Overview & Physical Node Specs -->
    <div v-if="activeTab === 'overview'" class="flex-1 overflow-y-auto p-4 space-y-4">
      <!-- Top Metrics Bento -->
      <div class="grid grid-cols-2 gap-2.5">
        <div class="p-3 rounded-xl bg-muted/30 border border-border space-y-1">
          <div class="text-[10px] font-mono text-muted-foreground uppercase flex items-center justify-between">
            <span>In-Stock Stock</span>
            <span :class="part.quantity <= (part.minQuantity || 1) ? 'text-rose-400 font-bold' : 'text-emerald-400'">
              {{ part.quantity <= (part.minQuantity || 1) ? 'Depleted' : 'Optimal' }}
            </span>
          </div>
          <div class="font-mono text-xl font-bold text-foreground">
            {{ part.quantity ?? 1 }} <span class="text-xs text-muted-foreground font-normal">units</span>
          </div>
          <div class="text-[10px] text-muted-foreground">
            Threshold Min: {{ part.minQuantity ?? 1 }}
          </div>
        </div>

        <div class="p-3 rounded-xl bg-muted/30 border border-border space-y-1">
          <div class="text-[10px] font-mono text-muted-foreground uppercase flex items-center justify-between">
            <span>Unit Valuation</span>
            <span class="text-primary font-mono">{{ currency }}</span>
          </div>
          <div class="font-mono text-xl font-bold text-foreground">
            {{ formatPrice(part.estimatedResellPriceEur ?? part.priceEur) }}
          </div>
          <div class="text-[10px] text-muted-foreground">
            Cost Basis: {{ formatPrice(part.priceEur) }}
          </div>
        </div>
      </div>

      <!-- Physical Placement & Topology Link -->
      <div class="p-3 rounded-xl bg-muted/20 border border-border space-y-2">
        <h4 class="text-xs font-bold text-foreground uppercase tracking-wider flex items-center gap-1.5">
          <MapPin class="w-3.5 h-3.5 text-primary" />
          <span>Physical Location & Cell Binding</span>
        </h4>

        <div class="grid grid-cols-2 gap-2 text-xs">
          <div>
            <span class="text-muted-foreground text-[10px] block">Warehouse Crib / Slot:</span>
            <span class="font-mono font-medium text-foreground">{{ part.location || 'Storage Bay / Central Crib' }}</span>
          </div>
          <div>
            <span class="text-muted-foreground text-[10px] block">Assigned Station Cell:</span>
            <button
              v-if="part.stationId"
              type="button"
              @click="emit('selectStation', part.stationId)"
              class="font-mono font-medium text-primary hover:underline text-left truncate block cursor-pointer"
            >
              {{ part.stationId }}
            </button>
            <span v-else class="text-muted-foreground italic font-mono">Unassigned Spares Stock</span>
          </div>
        </div>
      </div>

      <!-- Technical Specifications List -->
      <div class="p-3 rounded-xl bg-muted/20 border border-border space-y-2 text-xs">
        <h4 class="text-xs font-bold text-foreground uppercase tracking-wider flex items-center gap-1.5">
          <Cpu class="w-3.5 h-3.5 text-cyan-400" />
          <span>Hardware Architecture Specs</span>
        </h4>

        <dl class="divide-y divide-border/60">
          <div class="py-1.5 flex justify-between">
            <dt class="text-muted-foreground">Classification</dt>
            <dd class="font-mono font-semibold text-foreground">{{ part.category || 'Hardware' }}</dd>
          </div>
          <div class="py-1.5 flex justify-between">
            <dt class="text-muted-foreground">Tracking Paradigm</dt>
            <dd class="font-mono uppercase text-foreground">{{ part.trackingType || 'serialized' }}</dd>
          </div>
          <div class="py-1.5 flex justify-between">
            <dt class="text-muted-foreground">Operational State</dt>
            <dd class="font-mono font-semibold" :class="part.operationalState === 'working' ? 'text-emerald-400' : 'text-amber-400'">
              {{ part.operationalState || 'working' }}
            </dd>
          </div>
          <div class="py-1.5 flex justify-between">
            <dt class="text-muted-foreground">Owner Department</dt>
            <dd class="font-mono text-foreground">{{ typeof part.owner === 'object' ? part.owner?.name : 'Manufacturing Operations' }}</dd>
          </div>
          <div class="py-1.5 flex justify-between">
            <dt class="text-muted-foreground">Last Recorded Sync</dt>
            <dd class="font-mono text-muted-foreground">{{ part.updatedAt ? new Date(part.updatedAt).toLocaleDateString() : 'Active' }}</dd>
          </div>
        </dl>
      </div>

      <!-- Fast Action Shortcut Buttons -->
      <div class="pt-2 flex gap-2">
        <Button
          size="sm"
          @click="activeTab = 'dispatch'"
          class="flex-1 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs gap-1.5 cursor-pointer shadow-xs"
        >
          <Send class="w-3.5 h-3.5" />
          <span>Dispatch Part</span>
        </Button>
        <Button
          size="sm"
          variant="outline"
          @click="activeTab = 'edit'"
          class="flex-1 text-xs gap-1.5 cursor-pointer"
        >
          <Edit3 class="w-3.5 h-3.5 text-cyan-400" />
          <span>Modify Settings</span>
        </Button>
      </div>
    </div>

    <!-- Tab 2: Inline Dispatch / Consumption (No Modal!) -->
    <div v-else-if="activeTab === 'dispatch'" class="flex-1 overflow-y-auto p-4 space-y-4">
      <div class="p-3 rounded-xl bg-emerald-950/20 border border-emerald-500/30 text-emerald-400 text-xs flex items-start gap-2">
        <ShieldCheck class="w-4 h-4 shrink-0 mt-0.5 text-emerald-400" />
        <div>
          <strong class="font-bold">Direct Shopfloor Dispatch</strong>
          <p class="text-[11px] text-emerald-300/80 mt-0.5">
            Deduct quantity immediately from warehouse crib and assign to designated cell or work order.
          </p>
        </div>
      </div>

      <div class="space-y-3 text-xs">
        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Dispatch Quantity (Max: {{ maxDispatchQty }})
          </label>
          <div class="flex items-center gap-2">
            <input
              v-model.number="dispatchQty"
              type="number"
              :min="1"
              :max="maxDispatchQty"
              class="w-24 bg-background border border-border rounded-lg px-3 py-2 font-mono font-bold text-foreground focus:outline-none focus:border-primary"
            />
            <span class="text-muted-foreground">unit(s) to consume</span>
          </div>
        </div>

        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Target Cost Center / Accounting Unit
          </label>
          <select
            v-model="dispatchCostCenter"
            class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono text-foreground focus:outline-none focus:border-primary cursor-pointer"
          >
            <option value="CC-PROD-LINE-01">Line 1 - Pre-Assembly Cell (CC-PROD-01)</option>
            <option value="CC-PROD-LINE-02">Line 2 - Screwing & Fastening (CC-PROD-02)</option>
            <option value="CC-MAINT-EMERGENCY">Emergency Line-Stop Maintenance (CC-MAINT-99)</option>
            <option value="CC-TOOLING-RND">Engineering & Tooling Calibration (CC-TOOL-04)</option>
          </select>
        </div>

        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Target Machine Station (Optional)
          </label>
          <select
            v-model="dispatchMachineId"
            class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono text-foreground focus:outline-none focus:border-primary cursor-pointer"
          >
            <option value="">-- Generic Floor Spares Pool --</option>
            <option v-for="m in machines" :key="m.id" :value="m.id">
              {{ m.name }} ({{ m.id }})
            </option>
          </select>
        </div>

        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Justification / Work Order Note
          </label>
          <Input
            v-model="dispatchReason"
            placeholder="e.g. Broken spindle bearing replacement on shift 2"
            class="text-xs"
          />
        </div>
      </div>

      <div class="pt-3">
        <Button
          @click="handleExecuteDispatch"
          :disabled="dispatching || dispatchQty < 1 || dispatchQty > maxDispatchQty"
          class="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs gap-2 cursor-pointer shadow-md shadow-emerald-950/40"
        >
          <CheckCircle2 v-if="dispatchSuccess" class="w-4 h-4 text-white" />
          <Send v-else class="w-4 h-4" />
          <span>{{ dispatchSuccess ? 'Dispatched Successfully!' : (dispatching ? 'Processing...' : 'Confirm Dispatch Allocation') }}</span>
        </Button>
      </div>
    </div>

    <!-- Tab 3: Inline Quick Edit (No Modal!) -->
    <div v-else-if="activeTab === 'edit'" class="flex-1 overflow-y-auto p-4 space-y-4 text-xs">
      <div class="space-y-3">
        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Storage Location Bin / Crib
          </label>
          <Input
            v-model="editForm.location"
            placeholder="e.g. Warehouse Central / Bin 4B"
            class="text-xs font-mono"
          />
        </div>

        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Effective Minimum Stock Alert Threshold
          </label>
          <input
            v-model.number="editForm.minQuantity"
            type="number"
            min="0"
            class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono font-bold text-foreground focus:outline-none focus:border-primary"
          />
        </div>

        <div>
          <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
            Estimated Resell / Fair Value Override (EUR)
          </label>
          <input
            v-model.number="editForm.resellPriceOverride"
            type="number"
            min="0"
            step="0.01"
            class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono font-bold text-foreground focus:outline-none focus:border-primary"
          />
        </div>

        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
              Operational State
            </label>
            <select
              v-model="editForm.operationalState"
              class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono text-foreground focus:outline-none focus:border-primary cursor-pointer"
            >
              <option value="working">Working</option>
              <option value="degraded">Degraded</option>
              <option value="failed">Failed / Jammed</option>
            </select>
          </div>

          <div>
            <label class="font-bold text-muted-foreground uppercase text-[10px] block mb-1">
              Condition Grade
            </label>
            <select
              v-model="editForm.condition"
              class="w-full bg-background border border-border rounded-lg px-3 py-2 font-mono text-foreground focus:outline-none focus:border-primary cursor-pointer"
            >
              <option value="new">New (Factory OEM)</option>
              <option value="used">Used (Refurbished)</option>
              <option value="donor">Donor (Harvested)</option>
              <option value="obsolete">Obsolete</option>
              <option value="scrap">Scrap</option>
            </select>
          </div>
        </div>
      </div>

      <div class="pt-3">
        <Button
          @click="handleSaveInlineEdit"
          :disabled="savingEdit"
          class="w-full bg-primary text-primary-foreground font-bold text-xs gap-2 cursor-pointer shadow-xs"
        >
          <CheckCircle2 v-if="editSaved" class="w-4 h-4 text-emerald-400" />
          <Edit3 v-else class="w-4 h-4" />
          <span>{{ editSaved ? 'Changes Saved!' : (savingEdit ? 'Saving...' : 'Save Component Settings') }}</span>
        </Button>
      </div>
    </div>

    <!-- Tab 4: Audit History Log Stream (No Modal!) -->
    <div v-else-if="activeTab === 'audit'" class="flex-1 overflow-y-auto p-4 space-y-3">
      <div class="flex items-center justify-between text-xs text-muted-foreground font-mono">
        <span>Recorded Lifecycle Transactions</span>
        <span>{{ localAuditLogs.length }} events</span>
      </div>

      <div v-if="localAuditLogs.length === 0" class="text-center py-8 text-muted-foreground text-xs">
        No transaction history logged for this component yet.
      </div>

      <div v-else class="space-y-2">
        <div
          v-for="log in localAuditLogs"
          :key="log.id"
          class="p-2.5 rounded-lg bg-muted/30 border border-border text-xs space-y-1"
        >
          <div class="flex items-center justify-between">
            <Badge variant="outline" class="text-[9px] uppercase font-mono px-1.5 py-0 border-primary/40 text-primary">
              {{ log.action }}
            </Badge>
            <span class="text-[10px] font-mono text-muted-foreground">
              {{ new Date(log.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }) }}
            </span>
          </div>

          <p class="text-foreground text-xs font-medium">
            {{ log.details || `Quantity shifted from ${log.previousQuantity} to ${log.newQuantity}` }}
          </p>

          <div class="flex items-center justify-between text-[10px] text-muted-foreground font-mono pt-1 border-t border-border/40">
            <span>By: {{ log.performedBy || 'Operator' }}</span>
            <span v-if="log.costCenter" class="text-emerald-400">{{ log.costCenter }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
