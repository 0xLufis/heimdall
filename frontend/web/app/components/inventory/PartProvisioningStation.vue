<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Wrench,
  Search,
  PackageCheck,
  QrCode,
  ShieldCheck,
  AlertTriangle,
  CheckCircle2,
  Building,
  FolderGit2,
  Layers,
  User,
  Hash,
  FileText,
  Boxes,
  Cpu,
  ArrowRight,
  Printer,
  Sparkles,
  Info,
  Check
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { InventoryPart, PartUsageCostCenter, PartAuditRecord } from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  parts: InventoryPart[]
  preselectedPartId?: string
}>()

const emit = defineEmits<{
  (e: 'partProvisioned', record: PartAuditRecord): void
  (e: 'scanRequested'): void
}>()

const { usePart, formatCurrency, activeCurrency, fetchParts, canUsePart } = usePartsInventory()

// State
const searchQuery = ref('')
const selectedTrackingFilter = ref<'all' | 'serialized' | 'bulk'>('all')
const selectedPart = ref<InventoryPart | null>(null)
const quantityToUse = ref(1)

// Cost Center Fields (Strict OR relation: at least one of prodLine, project, department)
const prodLine = ref('')
const project = ref('')
const department = ref('')
const technician = ref('')
const ticketNumber = ref('')
const notes = ref('')

const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const lastAuditRecord = ref<PartAuditRecord | null>(null)
const showSuccessBanner = ref(false)

const selectPart = (part: InventoryPart) => {
  selectedPart.value = part
  quantityToUse.value = 1
  errorMessage.value = null
  showSuccessBanner.value = false
  lastAuditRecord.value = null
}

// Initialize or react to preselectedPartId
watch(() => props.preselectedPartId, (id) => {
  if (id) {
    const found = props.parts.find(p => p.id === id)
    if (found) selectPart(found)
  }
}, { immediate: true })

// Filtered parts catalog
const filteredCatalog = computed(() => {
  let list = props.parts.filter(p => p.condition !== 'scrap')

  if (selectedTrackingFilter.value !== 'all') {
    list = list.filter(p => p.trackingType === selectedTrackingFilter.value)
  }

  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase().trim()
    list = list.filter(p =>
      p.name.toLowerCase().includes(q) ||
      p.customIdentifier.toLowerCase().includes(q) ||
      (p.serialNumber && p.serialNumber.toLowerCase().includes(q)) ||
      (p.location && p.location.toLowerCase().includes(q)) ||
      (p.manufacturer?.name && p.manufacturer.name.toLowerCase().includes(q)) ||
      (p.category && p.category.toLowerCase().includes(q))
    )
  }

  return list
})

// Cost center OR condition check
const isCostCenterValid = computed(() => {
  const hasLine = Boolean(prodLine.value && prodLine.value.trim())
  const hasProject = Boolean(project.value && project.value.trim())
  const hasDept = Boolean(department.value && department.value.trim())
  return hasLine || hasProject || hasDept
})

const activeCostCenterTarget = computed(() => {
  const parts: string[] = []
  if (prodLine.value.trim()) parts.push(`Line: ${prodLine.value.trim()}`)
  if (project.value.trim()) parts.push(`Project: ${project.value.trim()}`)
  if (department.value.trim()) parts.push(`Dept: ${department.value.trim()}`)
  return parts.join(' | ')
})

const canSubmit = computed(() => {
  if (!selectedPart.value) return false
  if (selectedPart.value.quantity <= 0) return false
  if (quantityToUse.value <= 0 || quantityToUse.value > selectedPart.value.quantity) return false
  if (!isCostCenterValid.value) return false
  return true
})

const applyQuickLine = (line: string) => {
  prodLine.value = line
}

const applyQuickProject = (prj: string) => {
  project.value = prj
}

const applyQuickDept = (dept: string) => {
  department.value = dept
}

const handleProvision = async () => {
  if (!canSubmit.value || !selectedPart.value) return

  isSubmitting.value = true
  errorMessage.value = null

  try {
    const costCenterPayload: PartUsageCostCenter = {
      prodLine: prodLine.value.trim() || undefined,
      project: project.value.trim() || undefined,
      department: department.value.trim() || undefined,
      technician: technician.value.trim() || undefined,
      notes: notes.value.trim() ? (ticketNumber.value.trim() ? `[${ticketNumber.value.trim()}] ${notes.value.trim()}` : notes.value.trim()) : (ticketNumber.value.trim() ? `Ticket: ${ticketNumber.value.trim()}` : undefined)
    }

    const res = await usePart(selectedPart.value.id, costCenterPayload, quantityToUse.value)
    lastAuditRecord.value = res.auditRecord
    showSuccessBanner.value = true
    emit('partProvisioned', res.auditRecord)
    await fetchParts()

    // Update selected part reference with newly fetched data
    const updated = props.parts.find(p => p.id === selectedPart.value?.id)
    if (updated) {
      selectedPart.value = updated
      if (updated.quantity === 0) {
        quantityToUse.value = 0
      }
    }
  } catch (err: any) {
    errorMessage.value = err.data?.statusMessage || err.message || 'Failed to provision part.'
  } finally {
    isSubmitting.value = false
  }
}

const printDispatchSlip = () => {
  window.print()
}

const resetWorkstation = () => {
  selectedPart.value = null
  prodLine.value = ''
  project.value = ''
  department.value = ''
  ticketNumber.value = ''
  notes.value = ''
  showSuccessBanner.value = false
  lastAuditRecord.value = null
}
</script>

<template>
  <div class="space-y-4">
    <!-- Workstation Top Header Banner -->
    <div class="p-4 rounded-xl bg-card border border-border flex flex-col md:flex-row md:items-center justify-between gap-3 shadow-xs">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400">
          <Wrench class="size-5" />
        </div>
        <div>
          <h2 class="text-base font-bold text-foreground flex items-center gap-2">
            <span>Part Provisioning & Floor Workstation</span>
            <span class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase bg-primary/10 text-primary border border-primary/20">
              Live Terminal
            </span>
          </h2>
          <p class="text-xs text-muted-foreground mt-0.5">
            Consume warehouse parts, verify condition & depreciation, and enforce mandatory cost center tagging (OR relation)
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          @click="$emit('scanRequested')"
          class="h-8 px-3 text-xs gap-1.5 border-primary/30 text-primary hover:bg-primary/10 cursor-pointer shadow-xs"
        >
          <QrCode class="size-3.5" />
          <span>Scan Barcode / QR</span>
        </Button>

        <Button
          v-if="selectedPart"
          variant="ghost"
          size="sm"
          @click="resetWorkstation"
          class="h-8 px-2.5 text-xs text-muted-foreground hover:text-foreground cursor-pointer"
        >
          Clear Workspace
        </Button>
      </div>
    </div>

    <!-- Main Two-Column Layout -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-5">
      <!-- LEFT COLUMN: Search & Select On-Hand Inventory (5 Cols) -->
      <div class="lg:col-span-5 space-y-3">
        <div class="p-3.5 rounded-xl bg-card border border-border space-y-3 shadow-xs">
          <!-- Search & Filter Header -->
          <div class="relative">
            <Search class="absolute left-2.5 top-2.5 size-4 text-muted-foreground" />
            <Input
              v-model="searchQuery"
              placeholder="Search catalog by name, identifier, serial..."
              class="pl-9 text-xs h-9"
            />
          </div>

          <!-- Tracking Type Switcher -->
          <div class="flex items-center gap-1 p-1 bg-muted/60 rounded-lg border border-border text-xs">
            <button
              type="button"
              @click="selectedTrackingFilter = 'all'"
              class="flex-1 py-1 rounded-md font-medium text-center transition-all cursor-pointer"
              :class="selectedTrackingFilter === 'all' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
            >
              All Parts ({{ parts.length }})
            </button>
            <button
              type="button"
              @click="selectedTrackingFilter = 'serialized'"
              class="flex-1 py-1 rounded-md font-medium text-center transition-all cursor-pointer"
              :class="selectedTrackingFilter === 'serialized' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
            >
              Serialized
            </button>
            <button
              type="button"
              @click="selectedTrackingFilter = 'bulk'"
              class="flex-1 py-1 rounded-md font-medium text-center transition-all cursor-pointer"
              :class="selectedTrackingFilter === 'bulk' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
            >
              Bulk Stock
            </button>
          </div>
        </div>

        <!-- Catalog List -->
        <div class="space-y-2 max-h-[640px] overflow-y-auto custom-scrollbar pr-1">
          <div
            v-for="part in filteredCatalog"
            :key="part.id"
            @click="selectPart(part)"
            class="p-3 rounded-xl border transition-all cursor-pointer select-none space-y-2"
            :class="selectedPart?.id === part.id 
              ? 'border-primary bg-primary/5 ring-1 ring-primary shadow-xs' 
              : 'border-border bg-card hover:bg-muted/30'"
          >
            <div class="flex items-start justify-between gap-2">
              <div class="space-y-0.5">
                <div class="flex items-center gap-1.5 flex-wrap">
                  <span class="font-mono text-xs font-bold text-primary">{{ part.customIdentifier }}</span>
                  <span
                    class="px-1.5 py-0.2 rounded text-[10px] font-semibold uppercase"
                    :class="part.trackingType === 'serialized' ? 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border border-purple-500/20' : 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-500/20'"
                  >
                    {{ part.trackingType }}
                  </span>
                  <span
                    v-if="part.stockAlertStatus === 'low_stock'"
                    class="px-1.5 py-0.2 rounded text-[10px] font-semibold bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20"
                  >
                    Low Stock
                  </span>
                  <span
                    v-else-if="part.stockAlertStatus === 'out_of_stock' || part.quantity === 0"
                    class="px-1.5 py-0.2 rounded text-[10px] font-semibold bg-destructive/10 text-destructive border border-destructive/20"
                  >
                    Out of Stock
                  </span>
                </div>
                <div class="text-xs font-semibold text-foreground leading-tight line-clamp-1">
                  {{ part.name }}
                </div>
              </div>

              <!-- Available Stock Count Pill -->
              <div class="text-right shrink-0">
                <span class="font-mono text-xs font-bold text-foreground">
                  {{ part.quantity }}
                </span>
                <span class="text-[10px] text-muted-foreground ml-1">in stock</span>
              </div>
            </div>

            <div class="flex items-center justify-between text-[11px] text-muted-foreground pt-1 border-t border-border/60">
              <span class="truncate">{{ part.location || 'Warehouse Bin' }}</span>
              <span class="font-mono font-medium text-foreground">€{{ formatCurrency(part.priceEur, 'EUR') }}</span>
            </div>
          </div>

          <div v-if="filteredCatalog.length === 0" class="p-8 text-center border border-dashed border-border rounded-xl bg-card text-muted-foreground text-xs">
            No matching inventory items found in warehouse stock.
          </div>
        </div>
      </div>

      <!-- RIGHT COLUMN: Active Provisioning Station Form (7 Cols) -->
      <div class="lg:col-span-7">
        <!-- Case 1: No Part Selected -->
        <div
          v-if="!selectedPart"
          class="h-full min-h-[420px] p-8 rounded-xl border border-dashed border-border bg-card flex flex-col items-center justify-center text-center space-y-3 text-muted-foreground"
        >
          <div class="p-4 rounded-2xl bg-muted/60 border border-border text-muted-foreground">
            <PackageCheck class="size-8" />
          </div>
          <div class="space-y-1">
            <h3 class="text-sm font-bold text-foreground">No Part Selected for Provisioning</h3>
            <p class="text-xs max-w-sm text-muted-foreground leading-relaxed">
              Select an item from the catalog on the left or scan a barcode/QR code to start provisioning.
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            @click="$emit('scanRequested')"
            class="text-xs gap-1.5 cursor-pointer mt-2"
          >
            <QrCode class="size-3.5" />
            <span>Open Quick Scanner</span>
          </Button>
        </div>

        <!-- Case 2: Part Selected & Form Active -->
        <div v-else class="space-y-4">
          <!-- Success Confirmation Banner -->
          <div
            v-if="showSuccessBanner && lastAuditRecord"
            class="p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-800 dark:text-emerald-200 space-y-2 shadow-xs"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <CheckCircle2 class="size-4 text-emerald-600 dark:text-emerald-400" />
                <span class="text-xs font-bold">Successfully Provisioned & Dispatched!</span>
              </div>
              <span class="font-mono text-[10px] px-2 py-0.5 rounded bg-emerald-500/20 font-semibold">
                {{ lastAuditRecord.id }}
              </span>
            </div>
            <p class="text-xs text-muted-foreground">
              Part <strong>{{ lastAuditRecord.partIdentifier }}</strong> ({{ lastAuditRecord.partName }}) has been logged as deployed to <strong>{{ activeCostCenterTarget }}</strong>.
            </p>
            <div class="flex items-center gap-2 pt-1">
              <Button
                variant="outline"
                size="sm"
                @click="printDispatchSlip"
                class="h-7 text-xs gap-1 border-emerald-500/40 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-500/20 cursor-pointer"
              >
                <Printer class="size-3" />
                <span>Print Dispatch Slip</span>
              </Button>
              <Button
                variant="ghost"
                size="sm"
                @click="showSuccessBanner = false"
                class="h-7 text-xs text-muted-foreground hover:text-foreground cursor-pointer"
              >
                Dismiss
              </Button>
            </div>
          </div>

          <!-- Error Alert Banner -->
          <div
            v-if="errorMessage"
            class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2"
          >
            <AlertTriangle class="size-4 shrink-0" />
            <span>{{ errorMessage }}</span>
          </div>

          <!-- Selected Part Inspection Card -->
          <div class="p-4 rounded-xl bg-card border border-border space-y-3 shadow-xs">
            <div class="flex items-start justify-between gap-3">
              <div>
                <div class="flex items-center gap-2">
                  <span class="font-mono text-sm font-bold text-primary">{{ selectedPart.customIdentifier }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase bg-muted text-muted-foreground border border-border">
                    {{ selectedPart.category }}
                  </span>
                  <span
                    class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase"
                    :class="selectedPart.condition === 'new' ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20' : 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20'"
                  >
                    Condition: {{ selectedPart.condition }}
                  </span>
                </div>
                <h3 class="text-sm font-bold text-foreground mt-1">{{ selectedPart.name }}</h3>
                <p class="text-xs text-muted-foreground">
                  Location: <span class="font-medium text-foreground">{{ selectedPart.location || 'Warehouse Bin' }}</span>
                </p>
              </div>

              <!-- Available Stock & Buffer Gauge -->
              <div class="p-3 rounded-xl bg-muted/30 border border-border text-right shrink-0">
                <div class="text-[10px] text-muted-foreground uppercase tracking-wider font-semibold">Available Stock</div>
                <div class="font-mono text-lg font-bold" :class="selectedPart.quantity > 0 ? 'text-foreground' : 'text-destructive'">
                  {{ selectedPart.quantity }}
                </div>
                <div class="text-[10px] text-muted-foreground">
                  Buffer Min: {{ selectedPart.effectiveMinQuantity ?? selectedPart.minQuantity }} (x{{ selectedPart.minQuantityScalar ?? 1.0 }})
                </div>
              </div>
            </div>

            <!-- Valuation & Depreciation Grid -->
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-2 border-t border-border text-xs">
              <div>
                <span class="text-[10px] text-muted-foreground">Tracking Mode</span>
                <div class="font-semibold text-foreground uppercase text-[11px]">{{ selectedPart.trackingType }}</div>
              </div>
              <div>
                <span class="text-[10px] text-muted-foreground">Base Price</span>
                <div class="font-mono font-semibold text-foreground">€{{ formatCurrency(selectedPart.priceEur, 'EUR') }}</div>
              </div>
              <div>
                <span class="text-[10px] text-muted-foreground">Wear Factor</span>
                <div class="font-mono font-semibold text-amber-600 dark:text-amber-400">
                  {{ selectedPart.wearDepreciationPercentage || 0 }}%
                </div>
              </div>
              <div>
                <span class="text-[10px] text-muted-foreground">Estimated Resell</span>
                <div class="font-mono font-bold text-emerald-600 dark:text-emerald-400">
                  €{{ formatCurrency(selectedPart.estimatedResellPriceEur || selectedPart.priceEur, 'EUR') }}
                </div>
              </div>
            </div>
          </div>

          <!-- Quantity to Provision -->
          <div class="p-4 rounded-xl bg-card border border-border space-y-2 shadow-xs">
            <div class="flex items-center justify-between">
              <Label class="text-xs font-bold text-foreground">Quantity to Use / Dispatch</Label>
              <span class="text-[11px] text-muted-foreground">
                Max available: <strong class="text-foreground">{{ selectedPart.quantity }}</strong>
              </span>
            </div>

            <div v-if="selectedPart.trackingType === 'bulk'" class="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                @click="quantityToUse = Math.max(1, quantityToUse - 1)"
                class="h-8 w-8 p-0 cursor-pointer"
              >
                -
              </Button>
              <Input
                v-model.number="quantityToUse"
                type="number"
                min="1"
                :max="selectedPart.quantity"
                class="w-24 text-center font-mono text-xs h-8"
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                @click="quantityToUse = Math.min(selectedPart.quantity, quantityToUse + 1)"
                class="h-8 w-8 p-0 cursor-pointer"
              >
                +
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                @click="quantityToUse = selectedPart.quantity"
                class="text-xs text-primary hover:bg-primary/10 h-8 px-2 cursor-pointer"
              >
                Max All ({{ selectedPart.quantity }})
              </Button>
            </div>

            <div v-else class="text-xs p-2 rounded-lg bg-muted/40 border border-border flex items-center gap-2 text-muted-foreground">
              <Cpu class="size-4 text-primary shrink-0" />
              <span>
                <strong>1 Serialized Asset:</strong> Deploying will consume this specific physical unit and transition its state to <code class="font-mono text-foreground font-bold">in_service</code>.
              </span>
            </div>
          </div>

          <!-- Mandatory Cost Center Tagging Section (Strict OR Relation Enforced) -->
          <div class="p-4 rounded-xl bg-card border border-border space-y-4 shadow-xs">
            <div>
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <Building class="size-4 text-primary" />
                  <span class="text-xs font-bold text-foreground">Mandatory Cost Center Allocation</span>
                </div>

                <!-- Live OR validation indicator badge -->
                <span
                  class="px-2.5 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1"
                  :class="isCostCenterValid 
                    ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30' 
                    : 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/30'"
                >
                  <Check v-if="isCostCenterValid" class="size-3" />
                  <AlertTriangle v-else class="size-3" />
                  <span>{{ isCostCenterValid ? 'Cost Center Tagged' : 'Tag Required (OR)' }}</span>
                </span>
              </div>

              <p class="text-[11px] text-muted-foreground mt-1">
                Policy requirement: At least <strong>one</strong> allocation target below is required (<span class="font-semibold text-foreground">Production Line OR Project OR Department</span>).
              </p>
            </div>

            <!-- Cost Center OR Inputs -->
            <div class="space-y-3 pt-1">
              <!-- 1. Production Line -->
              <div class="space-y-1.5">
                <div class="flex items-center justify-between">
                  <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
                    <Layers class="size-3.5 text-muted-foreground" />
                    <span>Production Line (prodLine)</span>
                  </Label>
                  <div class="flex items-center gap-1 text-[10px]">
                    <span class="text-muted-foreground">Presets:</span>
                    <button type="button" @click="applyQuickLine('Line 1 - Battery Pack')" class="text-primary hover:underline cursor-pointer">Line 1</button>
                    <span class="text-muted-foreground">|</span>
                    <button type="button" @click="applyQuickLine('Line 2 - Stamping')" class="text-primary hover:underline cursor-pointer">Line 2</button>
                    <span class="text-muted-foreground">|</span>
                    <button type="button" @click="applyQuickLine('Line 3 - Packaging')" class="text-primary hover:underline cursor-pointer">Line 3</button>
                  </div>
                </div>
                <Input
                  v-model="prodLine"
                  placeholder="e.g. Line 1 - Battery Pack Assembly"
                  class="text-xs h-8"
                />
              </div>

              <!-- 2. Project ID -->
              <div class="space-y-1.5">
                <div class="flex items-center justify-between">
                  <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
                    <FolderGit2 class="size-3.5 text-muted-foreground" />
                    <span>Project / Work Breakdown Structure (project)</span>
                  </Label>
                  <div class="flex items-center gap-1 text-[10px]">
                    <span class="text-muted-foreground">Presets:</span>
                    <button type="button" @click="applyQuickProject('PRJ-TESLA-MOD3')" class="text-primary hover:underline cursor-pointer">Tesla Mod3</button>
                    <span class="text-muted-foreground">|</span>
                    <button type="button" @click="applyQuickProject('PRJ-AUDI-E-TRON')" class="text-primary hover:underline cursor-pointer">Audi e-tron</button>
                  </div>
                </div>
                <Input
                  v-model="project"
                  placeholder="e.g. PRJ-TESLA-MOD3 or WBS-8821"
                  class="text-xs h-8 font-mono"
                />
              </div>

              <!-- 3. Department -->
              <div class="space-y-1.5">
                <div class="flex items-center justify-between">
                  <Label class="text-xs font-semibold text-foreground flex items-center gap-1.5">
                    <Building class="size-3.5 text-muted-foreground" />
                    <span>Department / Cost Center (department)</span>
                  </Label>
                  <div class="flex items-center gap-1 text-[10px]">
                    <span class="text-muted-foreground">Presets:</span>
                    <button type="button" @click="applyQuickDept('Automation & Robotics')" class="text-primary hover:underline cursor-pointer">Robotics</button>
                    <span class="text-muted-foreground">|</span>
                    <button type="button" @click="applyQuickDept('Pneumatics & Fluid Maintenance')" class="text-primary hover:underline cursor-pointer">Pneumatics</button>
                  </div>
                </div>
                <Input
                  v-model="department"
                  placeholder="e.g. Automation & Robotics, Pneumatics Maintenance"
                  class="text-xs h-8"
                />
              </div>
            </div>

            <!-- Supplementary Details (Technician, Ticket, Reason) -->
            <div class="pt-3 border-t border-border grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
              <div class="space-y-1">
                <Label class="text-[11px] text-muted-foreground flex items-center gap-1">
                  <User class="size-3" />
                  <span>Technician Name / Staff ID</span>
                </Label>
                <Input
                  v-model="technician"
                  placeholder="e.g. Elena Rostova / T-402"
                  class="text-xs h-8"
                />
              </div>

              <div class="space-y-1">
                <Label class="text-[11px] text-muted-foreground flex items-center gap-1">
                  <Hash class="size-3" />
                  <span>Work Order / Ticket # (Optional)</span>
                </Label>
                <Input
                  v-model="ticketNumber"
                  placeholder="e.g. WO-88491 / TKT-2026-09"
                  class="text-xs h-8 font-mono"
                />
              </div>

              <div class="sm:col-span-2 space-y-1">
                <Label class="text-[11px] text-muted-foreground flex items-center gap-1">
                  <FileText class="size-3" />
                  <span>Deployment Reason & Maintenance Notes</span>
                </Label>
                <textarea
                  v-model="notes"
                  rows="2"
                  placeholder="Reason for part replacement or installation..."
                  class="w-full p-2 bg-background border border-border rounded-md text-xs text-foreground focus:outline-hidden focus:border-primary"
                />
              </div>
            </div>
          </div>

          <!-- Bottom Action Bar -->
          <div class="p-4 rounded-xl bg-card border border-border flex flex-col sm:flex-row items-center justify-between gap-3 shadow-xs">
            <div class="text-xs text-muted-foreground">
              <div v-if="isCostCenterValid" class="flex items-center gap-1.5 text-emerald-600 dark:text-emerald-400 font-medium">
                <CheckCircle2 class="size-3.5" />
                <span>Ready: {{ activeCostCenterTarget }}</span>
              </div>
              <div v-else class="text-amber-600 dark:text-amber-400 flex items-center gap-1.5">
                <AlertTriangle class="size-3.5" />
                <span>Specify at least one cost center target above</span>
              </div>
            </div>

            <Button
              type="button"
              :disabled="!canSubmit || isSubmitting"
              @click="handleProvision"
              class="w-full sm:w-auto h-9 px-5 text-xs font-semibold gap-2 bg-primary text-primary-foreground hover:bg-primary/90 shadow-xs cursor-pointer disabled:opacity-40"
            >
              <PackageCheck class="size-4" />
              <span>Provision & Deploy Part</span>
              <ArrowRight class="size-3.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
