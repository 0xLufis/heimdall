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
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-800 pb-5">
      <div>
        <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-indigo-400 mb-1">
          <NuxtLink to="/dashboard" class="hover:underline">Dashboard</NuxtLink>
          <span class="text-slate-600">/</span>
          <NuxtLink to="/dashboard/machines" class="hover:underline">Machines</NuxtLink>
          <span class="text-slate-600">/</span>
          <span class="text-slate-400">Machine Groups</span>
        </div>
        <h1 class="text-2xl font-black tracking-tight text-white flex items-center gap-3">
          <div class="p-2 rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
            <FolderTree class="h-6 w-6" />
          </div>
          <span>Machine Groups & Envelope Hierarchy</span>
        </h1>
        <p class="text-sm text-slate-400 mt-1">
          Configure production lines, automation cells, technology type clusters, and dedicated engineering ownership.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <NuxtLink to="/dashboard/machines">
          <Button variant="outline" size="sm" class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-xl text-xs">
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
