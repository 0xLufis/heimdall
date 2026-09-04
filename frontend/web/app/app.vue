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
    { name: 'mobile-web-app-capable', content: 'yes' },
    { name: 'apple-mobile-web-app-capable', content: 'yes' },
    { name: 'apple-mobile-web-app-status-bar-style', content: 'black-translucent' }
  ]
})

onMounted(() => {
  if (typeof window !== 'undefined' && 'serviceWorker' in navigator) {
    if (import.meta.dev) {
      // In development mode, unregister active service workers and purge stale caches
      navigator.serviceWorker.getRegistrations().then((registrations) => {
        for (const reg of registrations) {
          reg.unregister().then(() => {
            console.log('Unregistered development service worker:', reg.scope)
          })
        }
      })
      if (typeof caches !== 'undefined') {
        caches.keys().then((keys) => {
          for (const key of keys) {
            caches.delete(key)
          }
        })
      }
    } else if (process.env.NODE_ENV !== 'test') {
      navigator.serviceWorker.register('/sw.js').then((reg) => {
        console.log('Heimdall Service Worker registered with scope:', reg.scope)
      }).catch((err) => {
        console.warn('Service worker registration failed:', err)
      })
    }
  }
})
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
