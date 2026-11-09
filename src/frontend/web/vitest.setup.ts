import { config, enableAutoUnmount } from '@vue/test-utils'
import { afterEach } from 'vitest'
import { useAppI18n } from './app/composables/useAppI18n'

// Automatically unmount every mounted component after each test to release DOM nodes, event listeners, and Vue VMs
enableAutoUnmount(afterEach)

const i18n = useAppI18n()

config.global.mocks = {
  ...config.global.mocks,
  $t: (key: string, fallback?: string) => i18n.t(key, fallback),
  t: (key: string, fallback?: string) => i18n.t(key, fallback),
  appLocale: i18n.locale,
  appFormatDate: i18n.formatDate,
  appFormatDateTime: i18n.formatDateTime,
  appFormatCurrency: i18n.formatCurrency,
  appFormatNumber: i18n.formatNumber,
}
