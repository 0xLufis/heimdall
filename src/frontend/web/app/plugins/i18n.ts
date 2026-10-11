import { defineNuxtPlugin } from '#app'
import { useAppI18n, type SupportedLocale } from '~/composables/useAppI18n'

export default defineNuxtPlugin((nuxtApp) => {
  const { locale, setLocale, t, formatDate, formatDateTime, formatCurrency, formatNumber } = useAppI18n()

  // Make $t and t available globally in all Vue templates and components (SSR and Client)
  nuxtApp.vueApp.config.globalProperties.$t = t
  nuxtApp.vueApp.config.globalProperties.t = t

  // Client-side DOM and event syncing
  if (import.meta.client) {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = locale.value === 'hu' ? 'hu' : 'en'
    }

    nuxtApp.hook('app:mounted', () => {
      try {
        const saved = window.localStorage?.getItem('heimdall_locale') as SupportedLocale | null
        if (saved && (saved === 'en' || saved === 'hu')) {
          if (saved !== locale.value) {
            setLocale(saved)
          }
        } else {
          const browserLang = navigator.language?.toLowerCase()
          if (browserLang && browserLang.startsWith('hu') && locale.value !== 'hu') {
            setLocale('hu')
          }
        }
      } catch {
        // Safe fallback
      }
    })

    if (typeof window !== 'undefined') {
      window.addEventListener('heimdall:locale-changed', (event: any) => {
        const nextLocale = event.detail?.locale as SupportedLocale
        if (nextLocale && nextLocale !== locale.value) {
          setLocale(nextLocale)
        }
      })
    }
  }

  // Inject helpers globally into Nuxt context and templates
  return {
    provide: {
      t,
      $t: t,
      appLocale: locale,
      appT: t,
      appFormatDate: formatDate,
      appFormatDateTime: formatDateTime,
      appFormatCurrency: formatCurrency,
      appFormatNumber: formatNumber
    }
  }
})
