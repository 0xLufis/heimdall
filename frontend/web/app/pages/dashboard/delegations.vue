<script setup lang="ts">
import { Users, ArrowLeft, Plus, FolderTree } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import DelegationsManager from '~/components/tickets/DelegationsManager.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Page Header & Breadcrumbs -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-800 pb-5">
      <div>
        <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-violet-400 mb-1">
          <NuxtLink to="/dashboard" class="hover:underline">Dashboard</NuxtLink>
          <span class="text-slate-600">/</span>
          <NuxtLink to="/dashboard/tickets" class="hover:underline">Maintenance</NuxtLink>
          <span class="text-slate-600">/</span>
          <span class="text-slate-400">Delegations</span>
        </div>
        <h1 class="text-2xl font-black tracking-tight text-white flex items-center gap-3">
          <div class="p-2 rounded-xl bg-violet-500/10 text-violet-400 border border-violet-500/20">
            <Users class="h-6 w-6" />
          </div>
          <span>Technician Delegations & Shift Roster</span>
        </h1>
        <p class="text-sm text-slate-400 mt-1">
          Manage shift attendance, engineer dedication rules, machine group clusters, and on-call availability.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <NuxtLink to="/dashboard/tickets">
          <Button variant="outline" size="sm" class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-xl text-xs">
            <ArrowLeft class="w-3.5 h-3.5 mr-1.5" />
            Back to Tickets
          </Button>
        </NuxtLink>

        <NuxtLink to="/dashboard/machine-groups">
          <Button variant="outline" size="sm" class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 rounded-xl text-xs">
            <FolderTree class="w-3.5 h-3.5 mr-1.5 text-indigo-400" />
            Machine Groups
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Delegations Full View -->
    <DelegationsManager :is-modal="false" />
  </div>
</template>
