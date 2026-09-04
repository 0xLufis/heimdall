import { defineEventHandler, getRequestURL, sendRedirect } from 'h3'

export default defineEventHandler((event) => {
  const url = getRequestURL(event)
  const pathname = url.pathname

  // 1. Catch legacy host-path requests for the Nuxt entry bundle
  if (
    pathname.includes('node_modules/nuxt/dist/app/entry.async.js') &&
    !pathname.startsWith('/_nuxt/app/')
  ) {
    return sendRedirect(event, '/_nuxt/app/node_modules/nuxt/dist/app/entry.async.js', 302)
  }

  // 2. Catch any host-prefixed Vite/Nuxt resource paths (e.g. /_nuxt/home/lufis/.../frontend/...)
  if (pathname.startsWith('/_nuxt/home/')) {
    const correctedPath = pathname.replace(/^\/_nuxt\/home\/[^/]+\/Projects\/[^/]+\/[^/]+\/frontend\/[^/]+/, '/_nuxt/app')
    if (correctedPath !== pathname) {
      return sendRedirect(event, correctedPath, 302)
    }
  }

  // 3. Redirect /swagger and /api-docs to backend Swagger UI
  if (
    pathname === '/swagger' ||
    pathname.startsWith('/swagger/') ||
    pathname === '/api-docs' ||
    pathname.startsWith('/api-docs/')
  ) {
    const backendPort = '5099'
    const backendUrl = `http://${url.hostname}:${backendPort}/swagger`
    return sendRedirect(event, backendUrl, 302)
  }

  // 4. Allow Docker container bridge IP for DevTools Virtual File System (VFS) inspection
  if (pathname === '/_vfs.json' || pathname.startsWith('/_vfs/')) {
    event.context.clientAddress = '127.0.0.1'
    if (event.node?.req?.socket) {
      try {
        Object.defineProperty(event.node.req.socket, 'remoteAddress', {
          value: '127.0.0.1',
          configurable: true,
          writable: true
        })
      } catch {
        // Continue if socket cannot be modified
      }
    }
  }
})
