<script setup lang="ts">
import { useAuthSession } from '~/composables/useAuthSession'
import GlobalHeimdallSearchModal from '~/components/search/GlobalHeimdallSearchModal.vue'

const { user } = useAuthSession()
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
      <GlobalHeimdallSearchModal />
    </SidebarInset>
  </SidebarProvider>
</template>

<style scoped>
</style>
