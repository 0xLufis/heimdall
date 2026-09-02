<script setup lang="ts">
import { computed } from 'vue'
import { useAuthSession } from '~/composables/useAuthSession'
import { useDashboard } from '~/composables/useDashboard'
import OmniSearchBar from '~/components/search/OmniSearchBar.vue'
import type { SearchInstanceConfig } from '~/types/search'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { user, userRole } = useAuthSession()
const { stats, recentClients, securityEvents } = useDashboard()

const dashboardSearchConfig: SearchInstanceConfig = {
  instanceId: 'dashboard',
  placeholder: 'OmniSearch: Search stations, IPCs, assets, telemetry, or incident numbers...',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  enableAutoTagging: true,
  showGlobalShortcut: true
}
</script>

<template>
  <div class="space-y-8 pb-12">
    <!-- Hero Section -->
    <DashboardHero 
      :user-name="user?.name || 'Operator'" 
      :user-role="userRole"
    />

    <!-- Quick Search Bar -->
    <div class="max-w-4xl mx-auto w-full">
      <OmniSearchBar :config="dashboardSearchConfig" />
    </div>

    <!-- Stats Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <DashboardStatCard v-for="stat in stats" :key="stat.title" v-bind="stat">
        <template #icon>
          <component :is="stat.icon" class="h-6 w-6" />
        </template>
      </DashboardStatCard>
    </div>

    <!-- Main Content -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
      <div class="lg:col-span-2">
        <DashboardClientPreview :clients="recentClients" />
      </div>
      <div>
        <DashboardActivityFeed :events="securityEvents" />
      </div>
    </div>
  </div>
</template>
