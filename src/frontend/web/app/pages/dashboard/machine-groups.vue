<script setup lang="ts">
import { FolderTree, ArrowLeft, Plus } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import MachineGroupManager from '~/components/tickets/MachineGroupManager.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Page Header & Breadcrumbs -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border pb-5">
      <div>
        <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-primary mb-1">
          <NuxtLink to="/dashboard" class="hover:underline">Dashboard</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <NuxtLink to="/dashboard/machines" class="hover:underline">Machines</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <span class="text-muted-foreground">Machine Groups</span>
        </div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-3">
          <div class="p-2 rounded-xl bg-primary/10 text-primary border border-primary/20">
            <FolderTree class="h-6 w-6" />
          </div>
          <span>Machine Groups & Envelope Hierarchy</span>
        </h1>
        <p class="text-sm text-muted-foreground mt-1">
          Configure production lines, automation cells, technology type clusters, and dedicated engineering ownership.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <NuxtLink to="/dashboard/machines">
          <Button variant="outline" size="sm" class="border-border bg-card hover:bg-accent text-foreground rounded-xl text-xs cursor-pointer">
            <ArrowLeft class="w-3.5 h-3.5 mr-1.5" />
            Back to Machines
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Machine Group Manager Full View -->
    <MachineGroupManager :is-modal="false" />
  </div>
</template>
