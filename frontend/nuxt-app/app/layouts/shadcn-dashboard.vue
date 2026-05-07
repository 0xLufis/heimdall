<script setup lang="ts">
import { authClient } from "~/utils/auth-client"

const authSession = authClient.useSession()

const userEmail = computed(() => authSession?.data?.value?.user?.email ?? '...')
const userName = computed(() => authSession?.data?.value?.user?.name ?? 'User')
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
