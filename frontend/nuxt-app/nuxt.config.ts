// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
   compatibilityDate: '2025-07-15',
   devtools: { enabled: true },
   ssr: false,
   modules: ['@nuxtjs/tailwindcss', 'shadcn-nuxt'],
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
