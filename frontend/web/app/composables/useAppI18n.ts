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

// Global shared state across components
const currentLocale = ref<SupportedLocale>('en')
let isInitialized = false

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
  if (!isInitialized && typeof window !== 'undefined') {
    try {
      const saved = window.localStorage?.getItem('heimdall_locale') as SupportedLocale | null
      if (saved && (saved === 'en' || saved === 'hu')) {
        currentLocale.value = saved
      } else {
        const browserLang = navigator.language?.toLowerCase()
        if (browserLang && browserLang.startsWith('hu')) {
          currentLocale.value = 'hu'
        }
      }
    } catch {
      // Safe fallback when localStorage is restricted or in test environments
    }
    isInitialized = true
  }

  const setLocale = (locale: SupportedLocale) => {
    if (locale !== 'en' && locale !== 'hu') return
    currentLocale.value = locale

    if (typeof window !== 'undefined') {
      try {
        window.localStorage?.setItem('heimdall_locale', locale)
        document.cookie = `heimdall_locale=${locale}; path=/; max-age=31536000; SameSite=Lax`
      } catch {
        // Safe fallback in test contexts
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
