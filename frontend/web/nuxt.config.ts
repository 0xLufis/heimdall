import tailwindcss from '@tailwindcss/vite'

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  future: {
    compatibilityVersion: 4
  },
  devtools: { enabled: true },
  ssr: true,
  css: ['~/assets/css/tailwind.css'],
  vite: {
    plugins: [
      tailwindcss(),
    ],
    build: {
      modulePreload: {
        polyfill: false
      }
    },
    server: {
      hmr: {
        clientPort: 3000,
        protocol: 'ws'
      },
      allowedHosts: [
        'localhost',
        '127.0.0.1'
      ]
    }
  },
  experimental: {
    payloadExtraction: false
  },
  modules: [
    'shadcn-nuxt',
    '@nuxt/icon',
    '@vueuse/nuxt',
    '@nuxtjs/color-mode'
  ],
  colorMode: {
    classSuffix: '',
    preference: 'dark',
    fallback: 'dark'
  },
  shadcn: {
    prefix: '',
    componentDir: './app/components/ui'
  },
  imports: {
    dirs: [
      './app/lib'
    ]
  },
  runtimeConfig: {
    databaseUrl: process.env.DATABASE_URL,
    public: {
      signalrHubUrl: process.env.SIGNALR_HUB_URL || ''
    }
  },
  nitro: {
    // Ensure the pg module is externalized correctly for the server to avoid driver issues
    externals: {
      external: ['pg']
    },
    routeRules: {
      '/hubs/**': {
        proxy: `${process.env.BACKEND_API_URL || 'http://localhost:5099'}/hubs/**`
      }
    }
  },
  hooks: {
    'vite:extendConfig'(config) {
      config.plugins = config.plugins || []
      config.plugins.push({
        name: 'legacy-host-redirect',
        configureServer(server) {
          server.middlewares.use((req, res, next) => {
            if (!req.url) return next()

            // Catch legacy host-path requests for the Nuxt entry bundle
            if (
              req.url.includes('node_modules/nuxt/dist/app/entry.async.js') &&
              !req.url.startsWith('/_nuxt/app/')
            ) {
              res.writeHead(302, { Location: '/_nuxt/app/node_modules/nuxt/dist/app/entry.async.js' })
              return res.end()
            }

            // Catch any host-prefixed Vite/Nuxt resource paths (e.g. /_nuxt/home/lufis/.../frontend/...)
            if (req.url.startsWith('/_nuxt/home/')) {
              const correctedPath = req.url.replace(/^\/_nuxt\/home\/[^/]+\/Projects\/[^/]+\/[^/]+\/frontend\/[^/]+/, '/_nuxt/app')
              if (correctedPath !== req.url) {
                res.writeHead(302, { Location: correctedPath })
                return res.end()
              }
            }

            // Allow container bridge IP for DevTools virtual file system
            if (req.url && (req.url === '/_vfs.json' || req.url.startsWith('/_vfs'))) {
              if (req.socket) {
                try {
                  Object.defineProperty(req.socket, 'remoteAddress', {
                    value: '127.0.0.1',
                    configurable: true,
                    writable: true
                  })
                } catch {}
              }
            }
            next()
          })
        }
      })
    },
    'nitro:init'(nitro: any) {
      nitro.options.devHandlers = nitro.options.devHandlers || []
      nitro.options.devHandlers.unshift({
        route: '/_vfs',
        handler: (event: any) => {
          if (event.node?.req?.socket) {
            try {
              Object.defineProperty(event.node.req.socket, 'remoteAddress', {
                value: '127.0.0.1',
                configurable: true,
                writable: true
              })
            } catch {}
          }
        }
      })
    }
  }
})
