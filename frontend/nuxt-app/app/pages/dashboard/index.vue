<template>
  <div class="space-y-10 pb-12">
    <!-- Hero Section -->
    <DashboardHero 
      :user-name="session?.user.name" 
      :user-role="session?.user.role"
    />

    <!-- Stats Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <DashboardStatCard v-for="stat in stats" :key="stat.title" v-bind="stat">
        <template #icon>
          <component :is="stat.icon" class="h-6 w-6" />
        </template>
      </DashboardStatCard>
    </div>

    <!-- Main Content -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-10">
      <div class="lg:col-span-2">
        <DashboardClientPreview :clients="recentClients" />
      </div>
      <div>
        <DashboardActivityFeed :events="securityEvents" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"
import { useDashboard } from "@/composables/useDashboard"

const { data: session } = authClient.useSession()
const { stats, recentClients, securityEvents } = useDashboard()

definePageMeta({
  layout: 'shadcn-dashboard'
})
</script>
