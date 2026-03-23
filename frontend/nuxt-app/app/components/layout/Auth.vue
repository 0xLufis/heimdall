<script setup lang="ts">
defineProps<{
  reverse?: boolean
}>()
</script>

<template>
  <div
    class="relative flex items-center justify-center px-4 h-dvh lg:max-w-none lg:px-0"
    :class="{ 'flex-row-reverse': reverse }"
  >
    <div class="relative hidden h-full flex-1 flex-col bg-muted p-10 text-white lg:flex dark:border-r">
      <div class="absolute inset-0 bg-slate-950" />
      <div class="relative z-20 flex items-center text-xl font-bold tracking-tight">
        <div class="w-8 h-8 bg-primary rounded-lg flex items-center justify-center text-primary-foreground font-black mr-3 shadow-lg">H</div>
        Heimdall
      </div>
      <div class="relative z-20 mt-auto">
        <blockquote class="space-y-2">
          <p class="text-lg font-medium italic opacity-80">
            &ldquo;Unified industrial PC management and monitoring system for modern production environments.&rdquo;
          </p>
          <footer class="text-sm font-bold uppercase tracking-widest opacity-40 text-slate-500">
            Security Protocol v1.0
          </footer>
        </blockquote>
      </div>
    </div>
    <div class="mx-auto flex-1 lg:p-8">
      <slot />
    </div>
  </div>
</template>

<style scoped>

</style>
