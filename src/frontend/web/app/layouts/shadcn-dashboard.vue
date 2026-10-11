<script setup lang="ts">
import { useAuthSession } from '~/composables/useAuthSession'
import GlobalHeimdallSearchModal from '~/components/search/GlobalHeimdallSearchModal.vue'
import NotificationToastOverlay from '~/components/notifications/NotificationToastOverlay.vue'
import AppMobileBottomNav from '~/components/layout/AppMobileBottomNav.vue'

const { user } = useAuthSession()
</script>

<template>
  <SidebarProvider>
    <LayoutAppSidebar />
    <SidebarInset>
      <LayoutHeader />
      <div class="flex flex-col flex-1 pb-16 md:pb-0">
        <div class="@container/main p-4 lg:p-6 grow">
          <slot />
        </div>
      </div>
      <AppMobileBottomNav />
      <GlobalHeimdallSearchModal />
      <NotificationToastOverlay />
    </SidebarInset>
  </SidebarProvider>
</template>

<style scoped>
</style>
