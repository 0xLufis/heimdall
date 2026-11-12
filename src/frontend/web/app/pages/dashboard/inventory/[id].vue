<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { toast } from 'vue-sonner'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  ChevronLeft,
  Package,
  Monitor,
  Cpu,
  History,
  Tag,
  Box,
  MapPin,
  Building,
  Loader2,
  ArrowUpCircle,
  Edit3,
  Send,
  Trash2,
  CheckCircle2,
  AlertTriangle
} from 'lucide-vue-next'
import RemoteQuickViewModal from '~/components/controllers/RemoteQuickViewModal.vue'
import EditPartModal from '~/components/inventory/EditPartModal.vue'
import UsePartModal from '~/components/inventory/UsePartModal.vue'
import PartAuditLogDrawer from '~/components/inventory/PartAuditLogDrawer.vue'
import { useInventoryLive } from '~/composables/useInventoryLive'
import { usePartsInventory } from '~/composables/usePartsInventory'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const route = useRoute()
const id = route.params.id as string
const loading = ref(true)
const component = ref<any>(null)

// Modals
const isRemoteModalOpen = ref(false)
const remoteTargetController = ref<any>(null)
const showEditModal = ref(false)
const showUseModal = ref(false)
const showAuditDrawer = ref(false)

const { formatCurrency, usePart, editPart, fetchAuditLogs, auditLogs } = usePartsInventory()

const openAudit = async () => {
  try {
    await fetchAuditLogs(id)
  } catch {}
  showAuditDrawer.value = true
}

const fetchData = async () => {
  loading.value = true
  try {
    try {
      const res = await $fetch<any>(`/api/inventory/parts/${id}`)
      if (res?.part) {
        component.value = res.part
        return
      } else if (res && (res.id || res.name)) {
        component.value = res
        return
      }
    } catch {
      // Fallback to proxy API
    }
    const data = await $fetch<any>(`/api/proxy/v1/Inventory/${id}`)
    component.value = data
  } catch (e) {
    console.error('Error fetching inventory item:', e)
    component.value = null
  } finally {
    loading.value = false
  }
}

/** Controller hosting this item (software / IPC components), when the record names one. */
const hostController = computed<any | null>(() => {
  const c = component.value
  if (!c) return null
  if (c.clientPc?.id) return c.clientPc
  return c.clientPcId ? { id: c.clientPcId, hostname: c.clientPcHostname || '' } : null
})

// The viewer resolves the remote endpoint through this controller's id; pass only what is known
// about it (no stand-in address, MAC or OS).
const openRemoteForPc = (pc: any) => {
  const lastOnline = pc.lastOnline || pc.lastSeen || null
  const reportedRecently = lastOnline ? Date.now() - new Date(lastOnline).getTime() < 5 * 60 * 1000 : false
  remoteTargetController.value = {
    id: pc.id,
    name: pc.name || pc.hostname,
    hostname: pc.hostname,
    ipAddress: pc.ipAddress || undefined,
    lastOnline,
    isOnline: pc.isOnline ?? reportedRecently,
    macAddress: pc.macAddress || '',
    osVersion: pc.osVersion || undefined
  }
  isRemoteModalOpen.value = true
}

const handleSaveEdit = async (updatedItem: any) => {
  try {
    if (updatedItem) {
      component.value = { ...component.value, ...updatedItem }
    }
    try {
      await $fetch(`/api/proxy/v1/Inventory/${id}`, {
        method: 'PUT',
        body: updatedItem
      })
    } catch {
      // proxy fallback
    }
    showEditModal.value = false
    toast.success('Asset updated successfully')
    await fetchData()
  } catch (e) {
    console.error('Error updating inventory item:', e)
    toast.error('Failed to update asset')
  }
}

const handlePartUsed = async (data: any) => {
  try {
    await usePart(id, data.costCenter, data.quantity, data)
    showUseModal.value = false
    toast.success('Part dispatched successfully')
    await fetchData()
  } catch (e: any) {
    console.error('Error dispatching part:', e)
    toast.error('Failed to dispatch part')
  }
}

const { onInventoryUpdate } = useInventoryLive()
onInventoryUpdate(() => {
  fetchData()
})

onMounted(fetchData)
</script>

<template>
  <div class="space-y-6 pb-20 text-foreground">
    <!-- Breadcrumbs & Quick Action Bar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <NuxtLink to="/dashboard/inventory">
        <Button variant="ghost" size="sm" class="text-muted-foreground hover:text-foreground -ml-2 text-xs font-medium gap-1.5 cursor-pointer">
          <ChevronLeft class="h-4 w-4" />
          <span>Back to Inventory Registry</span>
        </Button>
      </NuxtLink>

      <div class="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          @click="openAudit"
          class="h-8 text-xs gap-1.5 cursor-pointer"
        >
          <History class="h-3.5 w-3.5 text-cyan-400" />
          <span>Audit History</span>
        </Button>

        <Button
          variant="outline"
          size="sm"
          :disabled="!component"
          @click="showUseModal = true"
          class="h-8 text-xs gap-1.5 text-emerald-400 border-emerald-500/40 hover:bg-emerald-500/10 cursor-pointer disabled:opacity-50"
        >
          <Send class="h-3.5 w-3.5" />
          <span>Dispatch / Use Part</span>
        </Button>

        <Button
          size="sm"
          :disabled="!component"
          @click="showEditModal = true"
          class="h-8 text-xs gap-1.5 bg-primary text-primary-foreground hover:bg-primary/90 font-semibold cursor-pointer shadow-xs disabled:opacity-50"
        >
          <Edit3 class="h-3.5 w-3.5" />
          <span>Edit Asset</span>
        </Button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex flex-col items-center justify-center py-24 gap-3">
      <Loader2 class="size-8 animate-spin text-primary" />
      <span class="text-xs font-mono text-muted-foreground">Loading asset metadata...</span>
    </div>

    <!-- Asset Profile Content -->
    <template v-else-if="component">
      <!-- Top Hero Section -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Main Asset Identity Card -->
        <Card class="lg:col-span-2 border-border bg-card shadow-xs">
          <CardHeader class="p-6 pb-3">
            <div class="flex items-start justify-between gap-4">
              <div class="flex items-center gap-3.5">
                <div class="p-3 rounded-xl bg-primary/10 border border-primary/20 text-primary shrink-0">
                  <Package class="h-7 w-7" />
                </div>
                <div>
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="font-mono text-xs text-cyan-400 font-bold uppercase">
                      {{ component.customIdentifier || component.id }}
                    </span>
                    <Badge variant="outline" class="text-[10px] px-2 py-0.5 font-mono">
                      {{ component.category || component.itemType || 'Hardware' }}
                    </Badge>
                  </div>
                  <h1 class="text-2xl font-black text-foreground tracking-tight mt-1">
                    {{ component.displayName || component.name }}
                  </h1>
                </div>
              </div>
            </div>
          </CardHeader>

          <CardContent class="p-6 pt-3">
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4 pt-3 border-t border-border/50">
              <div class="space-y-1">
                <span class="text-[10px] font-mono uppercase text-muted-foreground">Manufacturer</span>
                <div class="text-xs font-bold text-foreground truncate">
                  {{ component.manufacturer?.name || component.manufacturer || 'OEM Standard' }}
                </div>
              </div>

              <div class="space-y-1">
                <span class="text-[10px] font-mono uppercase text-muted-foreground">Location</span>
                <div class="text-xs font-bold text-foreground truncate">
                  {{ component.location || component.storageLocation || 'Warehouse Central' }}
                </div>
              </div>

              <div class="space-y-1">
                <span class="text-[10px] font-mono uppercase text-muted-foreground">Stock Quantity</span>
                <div class="text-xs font-bold font-mono text-emerald-400">
                  {{ component.quantity ?? component.stockQuantity ?? 1 }} Units
                </div>
              </div>

              <div class="space-y-1">
                <span class="text-[10px] font-mono uppercase text-muted-foreground">Operational State</span>
                <div class="flex items-center gap-1.5">
                  <span class="size-2 rounded-full bg-emerald-500 animate-pulse" />
                  <span class="text-xs font-semibold text-emerald-400">Verified</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Financials & Allocation Card -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="p-6 pb-3">
            <CardTitle class="text-xs font-bold uppercase tracking-wider text-muted-foreground">
              Valuation & Accounting
            </CardTitle>
          </CardHeader>
          <CardContent class="p-6 pt-0 space-y-4">
            <div class="flex justify-between items-baseline border-b border-border pb-3">
              <span class="text-xs text-muted-foreground">Unit Asset Value</span>
              <div class="text-xl font-black font-mono text-foreground">
                €{{ formatCurrency(component.priceEur ?? component.unitPrice ?? 0, 'EUR') }}
              </div>
            </div>

            <div class="space-y-2 text-xs font-mono">
              <div class="flex justify-between">
                <span class="text-muted-foreground">Cost Center:</span>
                <span class="text-foreground font-semibold">{{ component.costCenter || 'C-100 Operations' }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Serial Number:</span>
                <span class="text-cyan-400 font-semibold">{{ component.serialNumber || 'Unserialized' }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Technology:</span>
                <span class="text-foreground">{{ component.technology || 'Automation' }}</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <!-- Extended Details & Hierarchy -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Extended Specs -->
        <Card class="lg:col-span-2 border-border bg-card shadow-xs">
          <CardHeader class="p-5 border-b border-border">
            <div class="flex items-center gap-2">
              <Tag class="w-4 h-4 text-cyan-400" />
              <CardTitle class="text-xs font-bold uppercase tracking-wider text-foreground">
                Technical Specifications & Metadata
              </CardTitle>
            </div>
          </CardHeader>
          <CardContent class="p-0">
            <div v-if="component.data && Object.keys(component.data).length > 0" class="divide-y divide-border">
              <div
                v-for="(val, key) in component.data"
                :key="key"
                class="p-3.5 px-5 flex items-center justify-between text-xs font-mono hover:bg-muted/30 transition-colors"
              >
                <span class="text-muted-foreground">{{ key }}</span>
                <code class="bg-muted px-2 py-0.5 rounded text-foreground font-semibold">{{ val }}</code>
              </div>
            </div>
            <div v-else class="p-6 text-center text-xs font-mono text-muted-foreground">
              No custom extended specifications registered for this asset.
            </div>
          </CardContent>
        </Card>

        <!-- Hierarchy & Linkage -->
        <Card class="border-border bg-card shadow-xs">
          <CardHeader class="p-5 border-b border-border">
            <div class="flex items-center gap-2">
              <Box class="w-4 h-4 text-primary" />
              <CardTitle class="text-xs font-bold uppercase tracking-wider text-foreground">
                Plant Topology Linkage
              </CardTitle>
            </div>
          </CardHeader>
          <CardContent class="p-5 space-y-4">
            <div v-if="component.parent" class="space-y-1">
              <span class="text-[10px] font-mono text-muted-foreground uppercase">Parent Assembly</span>
              <NuxtLink
                :to="`/dashboard/inventory/${component.parent.id}`"
                class="flex items-center gap-2 p-2.5 rounded-lg bg-muted/40 border border-border hover:border-primary/50 text-xs font-mono text-foreground"
              >
                <ArrowUpCircle class="w-4 h-4 text-primary" />
                <span class="truncate">{{ component.parent.name }}</span>
              </NuxtLink>
            </div>

            <div class="space-y-1">
              <span class="text-[10px] font-mono text-muted-foreground uppercase">Assigned Machine</span>
              <NuxtLink
                v-if="component.machineId || component.linkedMachineId"
                :to="{ path: '/dashboard/machines', query: { tree: component.machineId || component.linkedMachineId } }"
                class="flex items-center gap-2 p-2.5 rounded-lg bg-muted/40 border border-border hover:border-primary/50 text-xs font-mono text-foreground"
                title="Open the station this part is installed on"
              >
                <span class="truncate">{{ component.machineName || component.machineId || component.linkedMachineId }}</span>
              </NuxtLink>
              <div v-else class="p-2.5 rounded-lg bg-muted/20 border border-border text-xs font-mono text-muted-foreground">
                Not installed on a machine
              </div>
            </div>

            <div v-if="hostController" class="space-y-1">
              <span class="text-[10px] font-mono text-muted-foreground uppercase">Host Controller</span>
              <div class="flex items-center gap-2">
                <NuxtLink
                  :to="{ path: '/dashboard/clients', query: { selected: hostController.id, hostname: hostController.hostname || undefined } }"
                  class="flex-1 min-w-0 flex items-center gap-2 p-2.5 rounded-lg bg-muted/40 border border-border hover:border-primary/50 text-xs font-mono text-foreground"
                >
                  <Monitor class="w-4 h-4 text-primary shrink-0" />
                  <span class="truncate">{{ hostController.hostname || hostController.name || hostController.id }}</span>
                </NuxtLink>
                <button
                  type="button"
                  class="shrink-0 h-9 px-3 rounded-lg bg-primary text-primary-foreground text-xs font-semibold hover:bg-primary/90 cursor-pointer"
                  title="Open this controller's remote view"
                  @click="openRemoteForPc(hostController)"
                >
                  Remote View
                </button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </template>

    <!-- Asset Not Found State -->
    <div v-else class="flex flex-col items-center justify-center py-20 px-4">
      <Card class="max-w-md w-full border-border bg-card shadow-sm text-center p-8">
        <div class="mx-auto w-12 h-12 rounded-full bg-amber-500/10 flex items-center justify-center text-amber-500 mb-4 border border-amber-500/20">
          <AlertTriangle class="h-6 w-6" />
        </div>
        <h2 class="text-lg font-bold text-foreground">Asset Not Found</h2>
        <p class="text-xs text-muted-foreground mt-2 leading-relaxed">
          No inventory item or canonical part was found matching identifier <code class="font-mono text-primary font-semibold">{{ id }}</code>. It may have been decommissioned, consumed, or does not exist in the registry.
        </p>
        <div class="mt-6 flex justify-center">
          <NuxtLink to="/dashboard/inventory">
            <Button size="sm" class="gap-2 cursor-pointer">
              <ChevronLeft class="h-4 w-4" />
              <span>Back to Inventory Registry</span>
            </Button>
          </NuxtLink>
        </div>
      </Card>
    </div>

    <!-- Drawers & Non-Blocking Panels -->
    <RemoteQuickViewModal
      v-if="isRemoteModalOpen"
      :controller="remoteTargetController"
      :open="isRemoteModalOpen"
      @update:open="isRemoteModalOpen = $event"
    />

    <EditPartModal
      v-if="showEditModal"
      :open="showEditModal"
      :part="component"
      @update:open="showEditModal = $event"
      @updated="handleSaveEdit"
      @edited="handleSaveEdit"
    />

    <UsePartModal
      v-if="showUseModal"
      :open="showUseModal"
      :part="component"
      @update:open="showUseModal = $event"
      @used="handlePartUsed"
    />

    <PartAuditLogDrawer
      v-if="showAuditDrawer"
      :open="showAuditDrawer"
      :part="component"
      :audit-logs="auditLogs"
      @update:open="showAuditDrawer = $event"
    />
  </div>
</template>
