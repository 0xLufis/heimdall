import { defineNuxtPlugin } from '#app'
import { useAppI18n, type SupportedLocale } from '~/composables/useAppI18n'

export default defineNuxtPlugin((nuxtApp) => {
  const { locale, setLocale, t, formatDate, formatDateTime, formatCurrency, formatNumber } = useAppI18n()

  // Ensure DOM document language reflects current locale
  if (typeof document !== 'undefined') {
    document.documentElement.lang = locale.value === 'hu' ? 'hu' : 'en'
  }

  // Listen for locale changes across windows or components
  if (typeof window !== 'undefined') {
    window.addEventListener('heimdall:locale-changed', (event: any) => {
      const nextLocale = event.detail?.locale as SupportedLocale
      if (nextLocale && nextLocale !== locale.value) {
        setLocale(nextLocale)
      }
    })
  }

  // Inject helpers globally into Nuxt context and templates
  return {
    provide: {
      appLocale: locale,
      appT: t,
      appFormatDate: formatDate,
      appFormatDateTime: formatDateTime,
      appFormatCurrency: formatCurrency,
      appFormatNumber: formatNumber
    }
  }
})
