<script setup lang="ts">
definePageMeta({
  layout: false,
})
</script>

<template>
  <LayoutAuth reverse>
    <div class="grid mx-auto max-w-sm gap-6">
      <div class="grid gap-2 text-center">
        <h1 class="text-3xl font-bold tracking-tight uppercase">
          Welcome back
        </h1>
        <p class="text-balance text-xs font-bold uppercase tracking-widest text-muted-foreground opacity-70">
          Identify yourself to access the terminal
        </p>
      </div>
      <AuthSignIn />
    </div>
  </LayoutAuth>
</template>
