// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
   compatibilityDate: '2025-07-15',
   devtools: { enabled: true },
   modules: ['@nuxtjs/tailwindcss', 'shadcn-nuxt'],
   nitro: {
      // Ensure the pg module is externalized or bundled correctly for the server
      externals: {
         inline: ['pg']
      }
   }
})
