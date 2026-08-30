<script setup lang="ts">
import { onMounted } from 'vue'

useHead({
  title: 'Heimdall - Maintenance & Telemetry',
  link: [
    { rel: 'manifest', href: '/manifest.json' },
    { rel: 'apple-touch-icon', href: '/icons/icon-192x192.png' }
  ],
  meta: [
    { name: 'theme-color', content: '#4f46e5' },
    { name: 'apple-mobile-web-app-capable', content: 'yes' },
    { name: 'apple-mobile-web-app-status-bar-style', content: 'black-translucent' }
  ]
})

onMounted(() => {
  if (typeof window !== 'undefined' && 'serviceWorker' in navigator && process.env.NODE_ENV !== 'test') {
    navigator.serviceWorker.register('/sw.js').then((reg) => {
      console.log('Heimdall Service Worker registered with scope:', reg.scope)
    }).catch((err) => {
      console.warn('Service worker registration failed:', err)
    })
  }
})
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
