import type { AppSettings } from '~/types/appSettings'

import { createDefu } from 'defu'

/**
 * Custom defu instance for merging app settings.
 * It handles merging of arrays of strings specifically.
 */
const customDefu = createDefu((obj, key, value) => {
  if (Array.isArray(value) && value.every((x: any) => typeof x === 'string')) {
    obj[key] = value
    return true
  }
})

/**
 * Default application settings.
 * @type {AppSettings}
 */
const defaultAppSettings: AppSettings = {
  sidebar: {
    collapsible: 'offcanvas',
    side: 'left',
    variant: 'sidebar',
  },
  theme: {
    color: 'default',
    type: 'default',
  },
}

/**
 * Composable for managing and accessing application settings.
 * It merges default settings with app configuration and stores them in a cookie.
 * 
 * @returns {object} An object containing:
 *   - `updateAppSettings`: A function to update application settings.
 *   - `sidebar`: A computed property for sidebar-related settings.
 *   - `theme`: A computed property for theme-related settings.
 */
export function useAppSettings() {
  const { appSettings } = useAppConfig()

  const processedConfig = customDefu(appSettings, defaultAppSettings)

  /**
   * Reactive cookie that stores and synchronizes application settings.
   * @type {Ref<AppSettings>}
   */
  const cookieAppSettings = useCookie<AppSettings>('app_settings', {
    default: () => processedConfig,
  })

  /**
   * Updates the application settings.
   * Merges the provided settings with the current settings.
   * @param {AppSettings} settings - The settings to update.
   */
  const updateAppSettings = (settings: AppSettings) => {
    cookieAppSettings.value = customDefu(settings, cookieAppSettings.value)
  }

  return {
    updateAppSettings,
    /**
     * Computed property for sidebar settings.
     * @type {ComputedRef<AppSettings['sidebar']>}
     */
    sidebar: computed(() => cookieAppSettings.value.sidebar),
    /**
     * Computed property for theme settings.
     * @type {ComputedRef<AppSettings['theme']>}
     */
    theme: computed(() => cookieAppSettings.value.theme),
  }
}
