<script setup lang="ts">
definePageMeta({
  layout: 'shadcn-dashboard'
})

await navigateTo('/dashboard/admin/system-settings', { replace: true })
</script>

<template>
  <div class="flex items-center justify-center p-8 text-muted-foreground text-sm">
    Redirecting to System Settings...
  </div>
</template>
