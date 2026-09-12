<script setup lang="ts">
definePageMeta({
  layout: false
})

if (import.meta.client) {
  // If navigating via client-side router, bypass vue-router to hit Nitro server handler
  window.location.href = '/admin/studio'
}
</script>

<template>
  <div class="flex h-screen w-full items-center justify-center bg-slate-950 text-slate-100">
    <div class="text-center space-y-3">
      <div class="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-purple-500 border-r-transparent"></div>
      <p class="text-sm font-semibold text-slate-400">Redirecting to Better Auth Identity Studio...</p>
    </div>
  </div>
</template>
