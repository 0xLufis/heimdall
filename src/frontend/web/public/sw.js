// Heimdall PWA Service Worker for Offline Caching & Background Sync
const CACHE_NAME = 'heimdall-maintenance-v2'
const STATIC_ASSETS = [
  '/manifest.json',
  '/favicon.ico',
  '/icons/icon-192x192.png',
  '/icons/icon-512x512.png'
]

// Service Worker Installation: Pre-cache core shell static assets
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(STATIC_ASSETS).catch((err) => {
        console.warn('Pre-caching static assets non-critical failure:', err)
      })
    }).then(() => self.skipWaiting())
  )
})

// Service Worker Activation: Clean up old cache versions
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((name) => {
          if (name !== CACHE_NAME) {
            return caches.delete(name)
          }
        })
      )
    }).then(() => self.clients.claim())
  )
})

// Fetch event handler: Offline-first with Network Fallback for API reads & cache for static assets
self.addEventListener('fetch', (event) => {
  const { request } = event
  const url = new URL(request.url)

  // Bypass Vite dev server resources, HMR, DevTools, and source maps
  if (
    url.pathname.startsWith('/_nuxt/') ||
    url.pathname.startsWith('/@vite') ||
    url.pathname.startsWith('/@fs') ||
    url.pathname.startsWith('/@id') ||
    url.pathname.includes('hot-update') ||
    url.pathname.startsWith('/__nuxt_devtools__') ||
    url.pathname.startsWith('/__vite_ping')
  ) {
    return
  }

  // Handle API requests with Network-first & Cache fallback
  if (url.pathname.startsWith('/api/')) {
    if (request.method === 'GET') {
      event.respondWith(
        fetch(request)
          .then((networkResponse) => {
            if (networkResponse && networkResponse.status === 200) {
              const responseClone = networkResponse.clone()
              caches.open(CACHE_NAME).then((cache) => cache.put(request, responseClone))
            }
            return networkResponse
          })
          .catch(async () => {
            const cachedResponse = await caches.match(request)
            if (cachedResponse) return cachedResponse

            // Empty JSON fallback if offline and no cache match
            return new Response(JSON.stringify({ offline: true, tickets: [], items: [] }), {
              headers: { 'Content-Type': 'application/json' }
            })
          })
      )
    }
    return
  }

  // Handle HTML document navigations: Network-First with Cache fallback
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((networkResponse) => {
          if (networkResponse && networkResponse.status === 200) {
            const responseClone = networkResponse.clone()
            caches.open(CACHE_NAME).then((cache) => cache.put(request, responseClone))
          }
          return networkResponse
        })
        .catch(async () => {
          const cached = await caches.match(request)
          if (cached) return cached
          return caches.match('/')
        })
    )
    return
  }

  // Handle static assets (Cache-first with Network Fallback)
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached
      return fetch(request).then((networkResponse) => {
        if (networkResponse && networkResponse.status === 200 && request.method === 'GET') {
          const responseClone = networkResponse.clone()
          caches.open(CACHE_NAME).then((cache) => cache.put(request, responseClone))
        }
        return networkResponse
      })
    })
  )
})

// Background Sync Handler for Offline Ticket Submissions
self.addEventListener('sync', (event) => {
  if (event.tag === 'sync-tickets') {
    event.waitUntil(syncOfflineTickets())
  }
})

async function syncOfflineTickets() {
  try {
    const clients = await self.clients.matchAll()
    for (const client of clients) {
      client.postMessage({ type: 'SYNC_OFFLINE_TICKETS' })
    }
  } catch (err) {
    console.error('Error during Service Worker background sync:', err)
  }
}
