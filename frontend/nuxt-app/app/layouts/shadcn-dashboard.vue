<script setup lang="ts">
import { authClient } from "~/utils/auth-client"

const session = authClient.useSession()
const route = useRoute()

const userEmail = computed(() => session.data?.value?.user?.email ?? '...')
const userName = computed(() => session.data?.value?.user?.name ?? 'User')
const userInitials = computed(() => userName.value.charAt(0).toUpperCase())

const pageTitle = computed(() => {
  if (route.path.includes('/users')) return 'Users'
  if (route.path.includes('/organizations')) return 'Organizations'
  if (route.path.includes('/clients')) return 'Client PCs'
  if (route.path.includes('/inventory')) return 'Inventory'
  if (route.path.includes('/map')) return 'Plant Map'
  return 'Dashboard'
})
</script>

<template>
  <SidebarProvider>
    <LayoutAppSidebar />
    <SidebarInset>
      <LayoutHeader />
      <div class="flex flex-col flex-1">
        <div class="@container/main p-4 lg:p-6 grow">
          <slot />
        </div>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template>

<style scoped>
</style>
