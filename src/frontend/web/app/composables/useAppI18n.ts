import { ref, computed } from 'vue'
import enMessages from '~/../locales/en.json'
import huMessages from '~/../locales/hu.json'

export type SupportedLocale = 'en' | 'hu'

export interface LocaleDefinition {
  code: SupportedLocale
  name: string
  nativeName: string
  flag: string
}

export const SUPPORTED_LOCALES: LocaleDefinition[] = [
  { code: 'en', name: 'English', nativeName: 'English', flag: '🇬🇧' },
  { code: 'hu', name: 'Hungarian', nativeName: 'Magyar', flag: '🇭🇺' }
]

const messages: Record<SupportedLocale, any> = {
  en: enMessages,
  hu: huMessages
}

// Fallback ref for environments without Nuxt useState/useCookie (e.g. Vitest unit tests)
const fallbackLocale = ref<SupportedLocale>('en')

function getNestedValue(obj: any, path: string): string | undefined {
  if (!obj) return undefined
  const parts = path.split('.')
  let current = obj
  for (const part of parts) {
    if (current === undefined || current === null) return undefined
    current = current[part]
  }
  return typeof current === 'string' ? current : undefined
}

export function useAppI18n() {
  const currentLocale = typeof useState !== 'undefined'
    ? useState<SupportedLocale>('app_locale', () => {
        if (typeof useCookie !== 'undefined') {
          const cookie = useCookie<SupportedLocale>('heimdall_locale')
          if (cookie.value === 'en' || cookie.value === 'hu') {
            return cookie.value
          }
        }
        if (typeof useRequestHeaders !== 'undefined') {
          try {
            const headers = useRequestHeaders(['accept-language'])
            const acceptLang = headers['accept-language']?.toLowerCase()
            if (acceptLang && (acceptLang.startsWith('hu') || acceptLang.includes(',hu') || acceptLang.includes('hu-hu'))) {
              return 'hu'
            }
          } catch {}
        }
        return 'en'
      })
    : fallbackLocale

  const setLocale = (locale: SupportedLocale) => {
    if (locale !== 'en' && locale !== 'hu') return
    currentLocale.value = locale
    fallbackLocale.value = locale

    if (typeof useCookie !== 'undefined') {
      const cookie = useCookie<SupportedLocale>('heimdall_locale', {
        sameSite: 'lax',
        path: '/',
        maxAge: 31536000
      })
      cookie.value = locale
    }

    if (typeof window !== 'undefined') {
      try {
        window.localStorage?.setItem('heimdall_locale', locale)
        document.cookie = `heimdall_locale=${locale}; path=/; max-age=31536000; SameSite=Lax`
        if (typeof document !== 'undefined' && document.documentElement) {
          document.documentElement.lang = locale === 'hu' ? 'hu' : 'en'
        }
        window.dispatchEvent(new CustomEvent('heimdall:locale-changed', { detail: { locale } }))
      } catch {
        // Safe fallback in test contexts
      }

      try {
        const nuxt = (window as any).__NUXT__
        if (nuxt && typeof (window as any).$nuxt?.i18n?.setLocale === 'function') {
          (window as any).$nuxt.i18n.setLocale(locale)
        }
      } catch {
        // Safe fallback outside Nuxt runtime context
      }
    }
  }

  const t = (key: string, fallback?: string): string => {
    const localeObj = messages[currentLocale.value]
    const translated = getNestedValue(localeObj, key)
    if (translated !== undefined) {
      return translated
    }

    // Fallback to English if current locale translation is missing
    if (currentLocale.value !== 'en') {
      const enVal = getNestedValue(messages.en, key)
      if (enVal !== undefined) return enVal
    }

    return fallback ?? key
  }

  const formatDate = (date: string | Date | number, options?: Intl.DateTimeFormatOptions): string => {
    const d = new Date(date)
    if (isNaN(d.getTime())) return ''
    const localeCode = currentLocale.value === 'hu' ? 'hu-HU' : 'en-US'
    const defaultOptions: Intl.DateTimeFormatOptions = options || {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }
    return new Intl.DateTimeFormat(localeCode, defaultOptions).format(d)
  }

  const formatDateTime = (date: string | Date | number): string => {
    const d = new Date(date)
    if (isNaN(d.getTime())) return ''
    const localeCode = currentLocale.value === 'hu' ? 'hu-HU' : 'en-US'
    return new Intl.DateTimeFormat(localeCode, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: currentLocale.value !== 'hu'
    }).format(d)
  }

  const formatCurrency = (amount: number, currency: 'EUR' | 'HUF' = 'EUR'): string => {
    const localeCode = currentLocale.value === 'hu' ? 'hu-HU' : 'en-US'
    try {
      return new Intl.NumberFormat(localeCode, {
        style: 'currency',
        currency,
        currencyDisplay: 'narrowSymbol',
        maximumFractionDigits: currency === 'HUF' ? 0 : 2
      }).format(amount)
    } catch {
      return `${amount} ${currency}`
    }
  }

  const formatNumber = (value: number, decimals: number = 2): string => {
    const localeCode = currentLocale.value === 'hu' ? 'hu-HU' : 'en-US'
    return new Intl.NumberFormat(localeCode, {
      minimumFractionDigits: 0,
      maximumFractionDigits: decimals
    }).format(value)
  }

  return {
    locale: computed(() => currentLocale.value),
    setLocale,
    t,
    formatDate,
    formatDateTime,
    formatCurrency,
    formatNumber,
    supportedLocales: SUPPORTED_LOCALES
  }
}
