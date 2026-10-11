<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { 
  ChevronLeft, Package, Monitor, Cpu, History, Tag, Box, 
  Info, Loader2, ArrowUpCircle, Link2Off, Eye, QrCode,
  Send, Wrench, AlertTriangle, CheckCircle2, ArrowRightLeft,
  Building, MapPin, DollarSign, Edit3, Trash2
} from 'lucide-vue-next'
import RemoteQuickViewModal from '~/components/controllers/RemoteQuickViewModal.vue'
import DashboardInventoryEditModal from '~/components/dashboard/InventoryEditModal.vue'
import PartAuditLogDrawer from '~/components/inventory/PartAuditLogDrawer.vue'
import UsePartModal from '~/components/inventory/UsePartModal.vue'
import { generateQrDataUrl } from '~/utils/qrSvgRenderer'
import { useInventoryLive } from '~/composables/useInventoryLive'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const props = defineProps<{
  itemId?: string
}>()

const route = useRoute()
const router = useRouter()
const id = computed(() => props.itemId || (route?.params?.id as string) || '')

const loading = ref(true)
const component = ref<any>(null)
const auditLogs = ref<any[]>([])

// Modals state
const isRemoteModalOpen = ref(false)
const remoteTargetController = ref<any>(null)
const showEditModal = ref(false)
const showAuditDrawer = ref(false)
const isUsePartModalOpen = ref(false)

const openRemoteForPc = (pc: any) => {
  remoteTargetController.value = {
    id: pc.id || pc.hostname,
    name: pc.name || pc.hostname,
    hostname: pc.hostname,
    ipAddress: pc.ipAddress || (typeof window !== 'undefined' ? window.location.hostname : '127.0.0.1'),
    isOnline: pc.isOnline ?? true,
    macAddress: pc.macAddress || '00:00:00:00:00:00',
    osVersion: pc.osVersion || 'Windows 10 IoT Enterprise'
  }
  isRemoteModalOpen.value = true
}

const qrCodeDataUrl = computed(() => {
  const payload = component.value?.customIdentifier || component.value?.serialNumber || component.value?.id || ''
  return payload ? generateQrDataUrl(payload) : ''
})

const fetchData = async () => {
  const targetId = id.value
  if (!targetId) {
    loading.value = false
    return
  }
  loading.value = true
  try {
    // 1. Try Parts Inventory Store by ID / customIdentifier
    const partRes = await $fetch<any>(`/api/inventory/parts/${targetId}`).catch(() => null)
    const part = partRes?.part || (partRes?.id ? partRes : null)

    if (part) {
      component.value = {
        id: part.id,
        name: part.name,
        displayName: part.displayName || part.name,
        customIdentifier: part.customIdentifier || part.name,
        serialNumber: part.serialNumber,
        category: part.category || 'Hardware',
        subCategory: part.subCategory || 'Automation Equipment',
        trackingType: part.trackingType || 'serialized',
        condition: part.condition || 'new',
        operationalState: part.operationalState || 'working',
        stockAlertStatus: part.stockAlertStatus || 'optimal',
        manufacturer: part.manufacturer || { name: 'Industrial OEM' },
        supplier: part.supplier || { name: 'Central Facility Store' },
        owner: part.owner || { type: 'organization', name: 'Heimdall Manufacturing Org' },
        location: part.location || 'Warehouse Central / Shelf A',
        priceEur: part.priceEur ?? 0,
        costInHUF: part.priceEur ? Math.round(part.priceEur * 400) : (part.costInHUF || 0),
        quantity: part.quantity ?? 1,
        minQuantity: part.minQuantity ?? 1,
        technology: part.category === 'Hardware' ? 'Electrical & Automation' : 'Software',
        topLevelFlags: { type: (part.trackingType === 'serialized' ? 'hardware' : 'peripherals') },
        isMachineLinked: part.isMachineLinked || !!part.linkedMachineId,
        linkedMachineId: part.linkedMachineId || part.machineId,
        linkedMachineName: part.linkedMachineName || part.machineName,
        alternateParts: part.alternateParts || [],
        tags: part.tags || [],
        data: {
          Condition: part.condition || 'new',
          OperationalState: part.operationalState || 'working',
          TrackingType: part.trackingType || 'serialized',
          Location: part.location || 'Warehouse Central',
          SerialNumber: part.serialNumber || 'N/A',
          CostInHUF: part.priceEur ? Math.round(part.priceEur * 400) : '-'
        },
        clientPc: part.clientPc,
        machine: part.linkedMachineName ? { customIdentifier: part.linkedMachineName, id: part.linkedMachineId } : undefined
      }

      // Load part audit logs
      const auditRes = await $fetch<any>(`/api/inventory/parts/${part.id}/audit`).catch(() => null)
      if (auditRes?.auditLogs) {
        auditLogs.value = auditRes.auditLogs
      } else if (part.auditTrail) {
        auditLogs.value = part.auditTrail
      }
      return
    }

    // 2. Try Master / Seed Filter Inventory
    const filterRes = await $fetch<any>('/api/inventory/filter', {
      params: { classification: 'all', tracking: 'all' }
    }).catch(() => null)

    const found = filterRes?.items?.find((i: any) => 
      i.id === targetId || 
      i.customIdentifier === targetId || 
      i.serialNumber === targetId ||
      i.name === targetId
    )

    if (found) {
      component.value = {
        id: found.id,
        name: found.name,
        displayName: found.displayName || found.name,
        customIdentifier: found.customIdentifier || found.name,
        serialNumber: found.serialNumber,
        category: found.itemType === 'software' ? 'Software' : 'Hardware',
        trackingType: found.isStockItem ? 'bulk' : 'serialized',
        condition: found.condition || 'new',
        operationalState: found.operationalState || 'working',
        stockAlertStatus: (found.stockQuantity ?? 1) <= 2 ? 'low_stock' : 'optimal',
        manufacturer: found.manufacturer || { name: 'Industrial OEM' },
        supplier: found.supplier || { name: 'Central Facility Store' },
        owner: { type: 'organization', name: 'Heimdall Manufacturing Org' },
        location: found.storageLocation || 'Main Plant Warehouse',
        priceEur: found.costInHUF ? Math.round(found.costInHUF / 400) : 0,
        costInHUF: found.costInHUF || 0,
        quantity: found.stockQuantity ?? (found.isStockItem ? 10 : 1),
        minQuantity: 2,
        technology: found.technology || 'Assembly',
        topLevelFlags: { type: found.itemType || 'hardware' },
        isMachineLinked: !!found.station || !!found.metadata?.Station,
        linkedMachineName: found.station || found.metadata?.Station,
        tags: found.tags || ['#Asset', `#${found.technology || 'Industrial'}`],
        data: {
          Condition: 'new',
          OperationalState: 'working',
          StorageLocation: found.storageLocation || 'Main Plant Warehouse',
          EquipmentStatus: found.equipmentStatus || 'InMachine',
          SerialNumber: found.serialNumber || 'N/A',
          CostInHUF: found.costInHUF ? `${new Intl.NumberFormat('hu-HU').format(found.costInHUF)} HUF` : '-',
          ...(found.metadata || {})
        },
        clientPc: found.clientPc,
        machine: found.station ? { customIdentifier: found.station } : undefined
      }
      return
    }

    // 3. Fallback generic proxy endpoint
    const proxyData = await $fetch<any>(`/api/proxy/inventory/${targetId}`).catch(() => null)
    if (proxyData && (proxyData.id || proxyData.name)) {
      component.value = proxyData
      return
    }
  } catch (e) {
    console.error('Error fetching component details:', e)
  } finally {
    loading.value = false
  }
}

const { onInventoryUpdate } = useInventoryLive()
onInventoryUpdate(() => {
  fetchData()
})

onMounted(fetchData)

const getConditionColor = (cond: string) => {
  switch ((cond || '').toLowerCase()) {
    case 'new': return 'border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
    case 'used': return 'border-blue-500/30 bg-blue-500/10 text-blue-600 dark:text-blue-400'
    case 'donor': return 'border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400'
    case 'obsolete': return 'border-orange-500/30 bg-orange-500/10 text-orange-600 dark:text-orange-400'
    case 'scrap': return 'border-rose-500/30 bg-rose-500/10 text-rose-600 dark:text-rose-400'
    default: return 'border-border bg-muted text-muted-foreground'
  }
}

const getOperationalColor = (state: string) => {
  switch ((state || '').toLowerCase()) {
    case 'working': return 'border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
    case 'in_service': return 'border-indigo-500/30 bg-indigo-500/10 text-indigo-600 dark:text-indigo-400'
    case 'evaluation': return 'border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400'
    case 'broken': return 'border-rose-500/30 bg-rose-500/10 text-rose-600 dark:text-rose-400'
    default: return 'border-border bg-muted text-muted-foreground'
  }
}

const handleSaveEdit = async (updatedItem: any) => {
  try {
    if (updatedItem.id) {
      // 1. Try editing part via parts store
      const editRes = await $fetch(`/api/inventory/parts/${updatedItem.id}/edit`, {
        method: 'PUT',
        body: {
          name: updatedItem.displayName || updatedItem.name,
          customIdentifier: updatedItem.customIdentifier,
          manufacturer: updatedItem.manufacturer,
          quantity: updatedItem.quantity ?? updatedItem.stockQuantity,
          location: updatedItem.location || updatedItem.storageLocation,
          costCenter: updatedItem.costCenter ? { department: updatedItem.costCenter } : undefined,
          tags: updatedItem.tags,
          actor: { id: 'usr-current', name: 'Technician' }
        }
      }).catch(() => null)

      // 2. Also update condition & operational state if provided
      await $fetch(`/api/inventory/parts/${updatedItem.id}`, {
        method: 'PATCH',
        body: {
          condition: updatedItem.condition,
          operationalState: updatedItem.operationalState,
          quantity: updatedItem.quantity ?? updatedItem.stockQuantity,
          actor: { id: 'usr-current', name: 'Technician' }
        }
      }).catch(() => null)
    }
    showEditModal.value = false
  } catch (e) {
    console.error('Error updating inventory item:', e)
  } finally {
    await fetchData()
  }
}

const handlePartUsed = async (usageData: any) => {
  try {
    await $fetch(`/api/inventory/parts/${usageData.partId}/use`, {
      method: 'POST',
      body: usageData
    })
    isUsePartModalOpen.value = false
  } catch (err) {
    console.error('Error consuming part:', err)
  } finally {
    await fetchData()
  }
}
</script>

<template>
  <div class="space-y-6 pb-20 text-foreground">
    <!-- Breadcrumbs & Action Toolbar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <NuxtLink to="/dashboard/inventory">
        <Button variant="ghost" size="sm" class="text-muted-foreground hover:text-foreground -ml-2 text-xs font-medium cursor-pointer">
          <ChevronLeft class="h-4 w-4 mr-1.5" />
          Back to Inventory
        </Button>
      </NuxtLink>
      
      <div class="flex flex-wrap items-center gap-2">
        <!-- Audit Trail Drawer Trigger -->
        <Button
          variant="outline"
          size="sm"
          @click="showAuditDrawer = true"
          class="border-border bg-card text-foreground hover:bg-muted rounded-xl text-xs font-bold h-8 px-3 cursor-pointer"
        >
          <History class="h-3.5 w-3.5 mr-1.5 text-muted-foreground" />
          Audit Trail
        </Button>

        <!-- Use / Consume Part Modal Trigger -->
        <Button
          v-if="component && component.quantity > 0"
          size="sm"
          @click="isUsePartModalOpen = true"
          class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl px-3.5 h-8 shadow-xs border-0 cursor-pointer text-xs font-bold gap-1.5"
        >
          <Send class="h-3.5 w-3.5" />
          Use / Issue Part
        </Button>

        <!-- Edit Component Modal Trigger -->
        <Button
          size="sm"
          @click="showEditModal = true"
          class="bg-primary hover:bg-primary/90 text-primary-foreground rounded-xl px-3.5 h-8 shadow-xs border-0 cursor-pointer text-xs font-bold gap-1.5"
        >
          <Edit3 class="h-3.5 w-3.5" />
          Edit Entity
        </Button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex flex-col items-center justify-center py-20 gap-3">
      <Loader2 class="size-8 animate-spin text-primary" />
      <span class="text-xs font-medium text-muted-foreground">Loading Asset Entity...</span>
    </div>

    <!-- Entity Content -->
    <template v-else-if="component">
      <!-- Top Cards Grid: Identity & Financials -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Main Asset Identity Card -->
        <Card class="lg:col-span-2 border-border bg-card shadow-sm rounded-2xl text-foreground">
          <CardHeader class="p-6 pb-3">
            <div class="flex items-start justify-between gap-4">
              <div class="flex items-center gap-3">
                <div class="p-3 rounded-xl bg-primary/10 border border-primary/20 text-primary">
                  <Package class="h-6 w-6" />
                </div>
                <div>
                  <h1 class="text-2xl font-bold text-foreground tracking-tight">
                    {{ component.displayName || component.name }}
                  </h1>
                  <div class="flex flex-wrap items-center gap-2 mt-1">
                    <span class="text-xs font-mono font-bold text-primary">
                      {{ component.customIdentifier }}
                    </span>
                    <span class="text-xs text-muted-foreground">&bull;</span>
                    <span class="text-xs font-mono text-muted-foreground">
                      {{ component.id }}
                    </span>
                  </div>
                </div>
              </div>

              <!-- Status Badges -->
              <div class="flex flex-col items-end gap-1.5 shrink-0">
                <div class="flex items-center gap-1.5">
                  <Badge
                    variant="outline"
                    class="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-lg border"
                    :class="component.trackingType === 'serialized' ? 'border-teal-500/30 bg-teal-500/10 text-teal-600 dark:text-teal-400' : 'border-purple-500/30 bg-purple-500/10 text-purple-600 dark:text-purple-400'"
                  >
                    {{ component.trackingType }}
                  </Badge>

                  <Badge
                    variant="outline"
                    class="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-lg border"
                    :class="getConditionColor(component.condition)"
                  >
                    {{ component.condition }}
                  </Badge>
                </div>

                <div v-if="component.operationalState" class="flex items-center gap-1.5">
                  <Badge
                    variant="outline"
                    class="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-lg border"
                    :class="getOperationalColor(component.operationalState)"
                  >
                    {{ component.operationalState.replace('_', ' ') }}
                  </Badge>
                </div>
              </div>
            </div>
          </CardHeader>

          <CardContent class="p-6 pt-3">
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 pt-2 border-t border-border">
              <div class="space-y-0.5">
                <span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Category</span>
                <div class="text-xs font-semibold text-foreground">{{ component.category || 'Hardware' }}</div>
              </div>
              <div class="space-y-0.5">
                <span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Manufacturer</span>
                <div class="text-xs font-semibold text-primary">{{ component.manufacturer?.name || 'Generic OEM' }}</div>
              </div>
              <div class="space-y-0.5">
                <span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">In Stock</span>
                <div class="text-xs font-mono font-bold text-foreground">
                  {{ component.quantity }} Units
                  <span v-if="component.minQuantity" class="text-[10px] text-muted-foreground font-normal">(min: {{ component.minQuantity }})</span>
                </div>
              </div>
              <div class="space-y-0.5">
                <span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Serial Number</span>
                <div class="text-xs font-mono font-medium text-foreground truncate">{{ component.serialNumber || 'Batch / Non-Serialized' }}</div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Ownership & Financials Card -->
        <Card class="border-border bg-card shadow-sm rounded-2xl text-foreground">
          <CardHeader class="p-6 pb-3 border-b border-border">
            <CardTitle class="text-xs font-bold text-muted-foreground uppercase tracking-wider flex items-center justify-between">
              <span>Financials &amp; Sourcing</span>
              <DollarSign class="size-4 text-emerald-500" />
            </CardTitle>
          </CardHeader>
          <CardContent class="p-6 pt-4 space-y-3">
            <div class="flex justify-between items-baseline border-b border-border pb-3">
              <span class="text-xs font-medium text-muted-foreground">Base Valuation</span>
              <div class="text-xl font-bold font-mono text-foreground">
                {{ component.priceEur }} &euro;
                <span v-if="component.costInHUF" class="text-xs text-muted-foreground font-normal ml-1">
                  ({{ new Intl.NumberFormat('hu-HU').format(component.costInHUF) }} HUF)
                </span>
              </div>
            </div>
            <div class="space-y-2 text-xs">
              <div class="flex justify-between">
                <span class="font-medium text-muted-foreground">Storage Location:</span>
                <span class="font-semibold text-foreground flex items-center gap-1">
                  <MapPin class="size-3 text-primary" />
                  {{ component.location }}
                </span>
              </div>
              <div class="flex justify-between">
                <span class="font-medium text-muted-foreground">Supplier:</span>
                <span class="font-semibold text-primary">{{ component.supplier?.name || 'Direct Facility Store' }}</span>
              </div>
              <div class="flex justify-between">
                <span class="font-medium text-muted-foreground">Owner / Org:</span>
                <span class="font-semibold text-foreground">{{ component.owner?.name || 'Manufacturing Operations' }}</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <!-- Main Specifications & Connectivity Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Left: Technical Specs, Extended Metadata & Alternates -->
        <div class="lg:col-span-2 space-y-6">
          <!-- Extended Specifications Card -->
          <Card class="border-border bg-card shadow-sm rounded-2xl text-foreground">
            <CardHeader class="p-5 border-b border-border">
              <div class="flex items-center gap-2">
                <Tag class="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <CardTitle class="text-xs font-bold text-foreground uppercase tracking-wider">
                  Technical Specifications &amp; Attributes
                </CardTitle>
              </div>
            </CardHeader>
            <CardContent class="p-0">
              <div class="divide-y divide-border">
                <div
                  v-for="(val, key) in component.data"
                  :key="key"
                  class="p-4 flex items-center justify-between group hover:bg-muted/20 transition-colors"
                >
                  <span class="text-xs font-medium text-muted-foreground group-hover:text-foreground transition-colors">
                    {{ key }}
                  </span>
                  <div class="flex items-center gap-2">
                    <code class="text-xs bg-muted border border-border px-2.5 py-1 rounded-lg text-foreground font-mono">
                      {{ typeof val === 'object' ? JSON.stringify(val) : val }}
                    </code>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Drop-in Replacements & Alternate Parts -->
          <Card v-if="component.alternateParts && component.alternateParts.length > 0" class="border-border bg-card shadow-sm rounded-2xl text-foreground">
            <CardHeader class="p-5 border-b border-border">
              <div class="flex items-center gap-2">
                <ArrowRightLeft class="h-4 w-4 text-teal-500" />
                <CardTitle class="text-xs font-bold text-foreground uppercase tracking-wider">
                  Approved Drop-in Alternates ({{ component.alternateParts.length }})
                </CardTitle>
              </div>
            </CardHeader>
            <CardContent class="p-4">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <NuxtLink
                  v-for="alt in component.alternateParts"
                  :key="alt.id"
                  :to="`/dashboard/inventory/${alt.id}`"
                  class="p-3 rounded-xl border border-border bg-muted/20 hover:border-teal-500/50 hover:bg-muted/40 transition-all flex items-center justify-between group"
                >
                  <div>
                    <span class="font-mono text-xs font-bold text-primary group-hover:underline">{{ alt.customIdentifier }}</span>
                    <p class="text-xs text-foreground font-medium mt-0.5">{{ alt.name }}</p>
                  </div>
                  <Badge variant="outline" class="font-mono text-[10px] text-teal-600 dark:text-teal-400 border-teal-500/30">
                    {{ alt.quantity }} in stock
                  </Badge>
                </NuxtLink>
              </div>
            </CardContent>
          </Card>

          <!-- Tags Card -->
          <Card v-if="component.tags && component.tags.length > 0" class="border-border bg-card shadow-sm rounded-2xl text-foreground">
            <CardHeader class="p-5 border-b border-border">
              <CardTitle class="text-xs font-bold text-muted-foreground uppercase tracking-wider">
                Classification Tags
              </CardTitle>
            </CardHeader>
            <CardContent class="p-5 flex flex-wrap gap-2">
              <span
                v-for="t in component.tags"
                :key="t"
                class="px-2.5 py-1 rounded-lg bg-muted border border-border text-xs font-mono text-foreground font-medium"
              >
                {{ t.startsWith('#') ? t : `#${t}` }}
              </span>
            </CardContent>
          </Card>
        </div>

        <!-- Right: Connected Machine, PC & Scannable QR Code -->
        <div class="space-y-6">
          <!-- Station Assignment Card -->
          <Card class="border-border bg-card shadow-sm rounded-2xl text-foreground">
            <CardHeader class="p-5 pb-3 border-b border-border">
              <div class="flex items-center justify-between">
                <CardTitle class="text-xs font-bold text-foreground uppercase tracking-wider flex items-center gap-2">
                  <Building class="size-4 text-amber-500" />
                  <span>Workcell Assignment</span>
                </CardTitle>
                <Badge
                  v-if="component.isMachineLinked || component.linkedMachineName"
                  variant="outline"
                  class="text-[10px] border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400"
                >
                  Installed
                </Badge>
                <Badge v-else variant="outline" class="text-[10px] border-border text-muted-foreground">
                  Unassigned Stock
                </Badge>
              </div>
            </CardHeader>
            <CardContent class="p-5">
              <div v-if="component.isMachineLinked || component.linkedMachineName" class="space-y-3">
                <div class="text-xs text-muted-foreground font-medium">Assigned to Production Workcell:</div>
                <div class="p-3 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-center justify-between">
                  <div class="font-bold text-xs text-foreground font-mono">
                    {{ component.linkedMachineName || component.machine?.customIdentifier }}
                  </div>
                  <NuxtLink
                    v-if="component.linkedMachineId"
                    :to="`/dashboard/machines/${component.linkedMachineId}`"
                    class="text-xs text-amber-600 dark:text-amber-400 hover:underline font-bold"
                  >
                    View Machine &rarr;
                  </NuxtLink>
                </div>
              </div>
              <div v-else class="text-xs text-muted-foreground py-2 text-center">
                Currently stored in warehouse inventory. No active workcell installation.
              </div>
            </CardContent>
          </Card>

          <!-- Scannable Industrial QR Code Card -->
          <Card class="border-border bg-card shadow-sm rounded-2xl text-foreground">
            <CardHeader class="p-5 pb-3 border-b border-border">
              <CardTitle class="text-xs font-bold text-foreground uppercase tracking-wider flex items-center gap-2">
                <QrCode class="size-4 text-indigo-500" />
                <span>Physical QR Asset Identifier</span>
              </CardTitle>
            </CardHeader>
            <CardContent class="p-5 flex flex-col items-center gap-3">
              <div class="p-2.5 bg-white rounded-xl border border-border shadow-2xs">
                <img
                  v-if="qrCodeDataUrl"
                  :src="qrCodeDataUrl"
                  alt="Asset QR Code"
                  class="size-36 rounded-lg"
                />
              </div>
              <div class="text-center space-y-0.5">
                <div class="font-mono font-bold text-xs text-foreground">
                  {{ component.customIdentifier }}
                </div>
                <div class="text-[10px] text-muted-foreground">
                  Ready for handheld laser scanner / mobile app intake
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Connected Client PC / Remote View Card -->
          <Card v-if="component.clientPc" class="border-border bg-card shadow-sm rounded-2xl text-foreground">
            <CardHeader class="p-5 pb-3 border-b border-border">
              <div class="flex items-center justify-between">
                <CardTitle class="text-xs font-bold text-foreground uppercase tracking-wider flex items-center gap-2">
                  <Monitor class="size-4 text-blue-500" />
                  <span>Host Controller PC</span>
                </CardTitle>
                <Button
                  size="sm"
                  variant="outline"
                  @click="openRemoteForPc(component.clientPc)"
                  class="h-7 px-2.5 bg-blue-500/10 hover:bg-blue-500/20 border-blue-500/30 text-blue-700 dark:text-blue-300 text-[10px] font-bold rounded-lg flex items-center gap-1 cursor-pointer"
                >
                  <Eye class="size-3" />
                  <span>Remote View</span>
                </Button>
              </div>
            </CardHeader>
            <CardContent class="p-5 space-y-2">
              <div class="font-mono font-bold text-xs text-foreground">{{ component.clientPc.hostname }}</div>
              <div class="text-[11px] text-muted-foreground font-mono">{{ component.clientPc.ipAddress || '192.168.1.100' }}</div>
            </CardContent>
          </Card>
        </div>
      </div>
    </template>

    <!-- Component Not Found Card -->
    <Card v-else class="border-border bg-card p-12 text-center rounded-2xl shadow-sm">
      <Package class="size-12 text-muted-foreground mx-auto mb-3 opacity-40" />
      <h2 class="text-base font-bold text-foreground">Asset Entity Not Found</h2>
      <p class="text-xs text-muted-foreground mt-1 max-w-sm mx-auto">
        The requested component ("{{ id }}") could not be located in parts inventory or plant equipment registry.
      </p>
      <NuxtLink to="/dashboard/inventory" class="mt-4 inline-block">
        <Button variant="outline" size="sm" class="text-xs font-bold cursor-pointer">
          Return to Inventory
        </Button>
      </NuxtLink>
    </Card>

    <!-- Modals -->
    <DashboardInventoryEditModal
      :open="showEditModal"
      :item="component"
      @update:open="showEditModal = $event"
      @save="handleSaveEdit"
    />

    <UsePartModal
      :open="isUsePartModalOpen"
      :part="component"
      @update:open="isUsePartModalOpen = $event"
      @used="handlePartUsed"
    />

    <PartAuditLogDrawer
      :open="showAuditDrawer"
      :audit-logs="auditLogs"
      @update:open="showAuditDrawer = $event"
    />

    <RemoteQuickViewModal
      :controller="remoteTargetController"
      :open="isRemoteModalOpen"
      @update:open="isRemoteModalOpen = $event"
    />
  </div>
</template>
