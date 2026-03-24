import tailwindcss from '@tailwindcss/vite'

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
   compatibilityDate: '2025-07-15',
   devtools: { enabled: true },
   ssr: false,
   css: ['~/assets/css/tailwind.css'],
   vite: {
      plugins: [
         tailwindcss(),
      ],
      server: {
         allowedHosts: [
            'noncategorical-ludivina-tinglingly.ngrok-free.dev'
         ]
      }
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
         // Add public variables here
      }
   },
   nitro: {
      // Ensure the pg module is externalized correctly for the server to avoid driver issues
      externals: {
         external: ['pg']
      }
   }
})
