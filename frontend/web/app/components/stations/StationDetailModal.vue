<script setup lang="ts">
import type { ProductionStation } from '~/types/domain'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Cpu, Monitor, MapPin, Package, Shield, AlertTriangle, X } from 'lucide-vue-next'

const props = defineProps<{
  station: ProductionStation | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent v-if="station" :show-close="false" class="max-w-3xl bg-card border-border text-foreground p-0 overflow-hidden rounded-2xl shadow-xl">
      <DialogHeader class="bg-muted/40 p-6 sm:p-8 border-b border-border">
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-4">
            <div class="p-3 bg-primary/10 rounded-xl text-primary border border-primary/20">
              <Cpu class="w-7 h-7" />
            </div>
            <div>
              <DialogTitle class="text-xl sm:text-2xl font-bold uppercase tracking-tight text-foreground flex items-center gap-3">
                {{ station.name }}
                <Badge class="bg-primary text-primary-foreground font-mono text-[10px] uppercase">{{ station.customIdentifier }}</Badge>
              </DialogTitle>
              <DialogDescription class="text-muted-foreground text-xs font-medium uppercase tracking-wider mt-1">
                Station Graph Node Identity & Physical Interconnects
              </DialogDescription>
            </div>
          </div>
          <Button
            variant="ghost"
            size="icon"
            class="h-8 w-8 rounded-lg text-muted-foreground hover:text-foreground shrink-0 cursor-pointer"
            @click="emit('update:open', false)"
          >
            <X class="w-4 h-4" />
          </Button>
        </div>
      </DialogHeader>

      <div class="p-6 sm:p-8 space-y-6 max-h-[70vh] overflow-y-auto">
        <!-- Key Metrics Grid -->
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <div class="p-4 rounded-xl bg-muted/30 border border-border/80">
            <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Status</div>
            <div class="text-sm font-semibold text-emerald-400 mt-1 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
              Operational
            </div>
          </div>

          <div class="p-4 rounded-xl bg-muted/30 border border-border/80">
            <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">CAD Spatial Ref</div>
            <div class="text-sm font-mono font-medium text-foreground mt-1 truncate">
              {{ station.pinnedObjectHandle || 'Unpinned' }}
            </div>
          </div>

          <div class="p-4 rounded-xl bg-muted/30 border border-border/80">
            <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Controllers</div>
            <div class="text-sm font-semibold text-primary mt-1">
              {{ station.controllers?.length || 0 }} IPCs/PLCs
            </div>
          </div>

          <div class="p-4 rounded-xl bg-muted/30 border border-border/80">
            <div class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Organization</div>
            <div class="text-sm font-medium text-foreground mt-1 truncate">
              {{ station.organizationId || 'Heimdall Root' }}
            </div>
          </div>
        </div>

        <!-- Associated Controllers Section -->
        <div class="space-y-3">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
            <Monitor class="w-4 h-4 text-primary" />
            Associated Industrial Controllers (IPCs / PLCs)
          </h4>

          <div v-if="!station.controllers || station.controllers.length === 0" class="p-6 text-center bg-muted/20 rounded-xl border border-dashed border-border text-muted-foreground text-xs font-medium uppercase tracking-wider">
            No controllers currently assigned to this production station.
          </div>

          <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div
              v-for="c in station.controllers"
              :key="c.id"
              class="p-3.5 bg-muted/30 border border-border rounded-xl flex items-center justify-between"
            >
              <div>
                <div class="text-xs font-semibold text-foreground">{{ c.hostname || c.name || 'Controller Node' }}</div>
                <div class="text-[10px] font-mono text-muted-foreground mt-0.5">{{ c.ipAddress || '192.168.1.xxx' }}</div>
              </div>
              <Badge variant="outline" class="border-primary/30 text-primary text-[9px] uppercase font-mono">
                {{ c.role || 'Primary' }}
              </Badge>
            </div>
          </div>
        </div>

        <!-- Hardware Components List -->
        <div class="space-y-3">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
            <Package class="w-4 h-4 text-emerald-400" />
            Station Hardware Assets & Sensors
          </h4>

          <div v-if="!station.hardwareComponents || station.hardwareComponents.length === 0" class="p-6 text-center bg-muted/20 rounded-xl border border-dashed border-border text-muted-foreground text-xs font-medium uppercase tracking-wider">
            No nested hardware components registered.
          </div>

          <div v-else class="divide-y divide-border/60 border border-border rounded-xl bg-muted/30 overflow-hidden">
            <div
              v-for="comp in station.hardwareComponents"
              :key="comp.id"
              class="p-3.5 flex items-center justify-between"
            >
              <div>
                <div class="text-xs font-semibold text-foreground">{{ comp.name }}</div>
                <div class="text-[10px] text-muted-foreground font-mono">{{ comp.serialNumber || comp.id }}</div>
              </div>
              <span class="text-[10px] font-semibold text-primary uppercase">{{ comp.itemType }}</span>
            </div>
          </div>
        </div>
      </div>

      <div class="p-5 bg-muted/20 border-t border-border flex justify-end gap-3">
        <Button variant="outline" @click="emit('update:open', false)" class="rounded-xl border-border text-xs font-semibold uppercase tracking-wider">
          Close
        </Button>
      </div>
    </DialogContent>
  </Dialog>
</template>
