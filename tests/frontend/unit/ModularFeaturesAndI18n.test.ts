import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { useAppI18n, SUPPORTED_LOCALES } from '~/composables/useAppI18n'
import { useFeatureFlags, getAppFeatureFlags } from '~/composables/useFeatureFlags'
import { usePlugins } from '~/composables/usePlugins'
import { PLATFORM_MODULE_MANIFESTS } from '~/types/modules'
import { assertModuleEnabled, getFeatureFlags } from '~~/server/utils/featureFlags'
import LanguageSwitcher from '~/components/common/LanguageSwitcher.vue'
import ModularFeaturesManager from '~/components/admin/ModularFeaturesManager.vue'
import { navMenu } from '~/constants/menus'

describe('Modular Features Architecture & Frontend i18n Suite', () => {
  const originalEnv = { ...process.env }
  const storageMock = new Map<string, string>()

  beforeEach(() => {
    process.env = { ...originalEnv }
    storageMock.clear()
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => storageMock.get(key) || null,
      setItem: (key: string, value: string) => storageMock.set(key, value),
      removeItem: (key: string) => storageMock.delete(key),
      clear: () => storageMock.clear()
    })
  })

  afterEach(() => {
    process.env = { ...originalEnv }
    vi.unstubAllGlobals()
  })

  describe('Internationalization Composable (useAppI18n)', () => {
    it('initializes with English by default and translates standard keys', () => {
      const { locale, t } = useAppI18n()
      expect(locale.value).toBe('en')
      expect(t('common.save')).toBe('Save')
      expect(t('common.status')).toBe('Status')
      expect(t('nav.dashboard')).toBe('Dashboard')
      expect(t('machines.statusActive')).toBe('Active')
      expect(t('tickets.title')).toBe('Maintenance & Repair Tickets')
    })

    it('switches locale to Hungarian and provides industrial terminology', () => {
      const { locale, setLocale, t } = useAppI18n()
      setLocale('hu')
      expect(locale.value).toBe('hu')

      // Industrial Hungarian translations
      expect(t('common.save')).toBe('Mentés')
      expect(t('common.status')).toBe('Állapot')
      expect(t('nav.dashboard')).toBe('Vezérlőpult')
      expect(t('nav.inventory')).toBe('Raktárkészlet')
      expect(t('machines.oee')).toBe('Teljes Eszközhatékonyság (OEE)')
      expect(t('machines.statusDecommissioned')).toBe('Kiselejtezve')
      expect(t('inventory.inStock')).toBe('Raktáron')
      expect(t('tickets.newTicket')).toBe('Új Hibajegy')
      expect(t('telemetry.liveTelemetry')).toBe('Élő Jelek')

      // Switch back to English
      setLocale('en')
      expect(locale.value).toBe('en')
      expect(t('common.save')).toBe('Save')
    })

    it('falls back to English when a translation key is missing in the target locale', () => {
      const { setLocale, t } = useAppI18n()
      setLocale('hu')
      // Non-existent key should return fallback or key itself
      expect(t('nonexistent.path.key', 'Fallback Label')).toBe('Fallback Label')
      expect(t('common.save')).toBe('Mentés')
      setLocale('en')
    })

    it('formats Hungarian and European dates, currencies, and numbers adhering to industrial standards', () => {
      const { setLocale, formatDate, formatCurrency, formatNumber } = useAppI18n()

      const testDate = new Date('2026-10-02T14:30:00Z')

      // Hungarian standard formatting
      setLocale('hu')
      const huDate = formatDate(testDate)
      expect(huDate).toBeTruthy()
      expect(huDate.includes('2026') || huDate.includes('10')).toBe(true)

      const huHuf = formatCurrency(250000, 'HUF')
      expect(huHuf).toContain('Ft')

      const huEur = formatCurrency(1250.5, 'EUR')
      expect(huEur.includes('€') || huEur.includes('EUR')).toBe(true)

      const huNumber = formatNumber(1250000.5, 2)
      expect(huNumber).toBeTruthy()

      // English formatting
      setLocale('en')
      const enEur = formatCurrency(1250.5, 'EUR')
      expect(enEur).toContain('€')
    })

    it('lists supported locales correctly', () => {
      expect(SUPPORTED_LOCALES).toHaveLength(2)
      expect(SUPPORTED_LOCALES.map(l => l.code)).toEqual(['en', 'hu'])
    })
  })

  describe('Modular Feature Architecture & Flags (useFeatureFlags)', () => {
    it('has all four core platform module manifests defined', () => {
      expect(PLATFORM_MODULE_MANIFESTS.core).toBeDefined()
      expect(PLATFORM_MODULE_MANIFESTS.core.isRequired).toBe(true)
      expect(PLATFORM_MODULE_MANIFESTS.ticketing).toBeDefined()
      expect(PLATFORM_MODULE_MANIFESTS.inventory).toBeDefined()
      expect(PLATFORM_MODULE_MANIFESTS.telemetry).toBeDefined()

      // Dependencies check: addons depend on Core
      expect(PLATFORM_MODULE_MANIFESTS.ticketing.dependencies).toContain('core')
      expect(PLATFORM_MODULE_MANIFESTS.inventory.dependencies).toContain('core')
      expect(PLATFORM_MODULE_MANIFESTS.telemetry.dependencies).toContain('core')
    })

    it('enables all modules by default in full monolith configuration', () => {
      const { enableTicketing, enableInventory, enableTelemetry, isMinimalMode, isModuleEnabled } = useFeatureFlags()
      expect(enableTicketing.value).toBe(true)
      expect(enableInventory.value).toBe(true)
      expect(enableTelemetry.value).toBe(true)
      expect(isMinimalMode.value).toBe(false)
      expect(isModuleEnabled('core')).toBe(true)
      expect(isModuleEnabled('ticketing')).toBe(true)
      expect(isModuleEnabled('inventory')).toBe(true)
      expect(isModuleEnabled('telemetry')).toBe(true)
    })

    it('dynamically toggles modules on the client and flags minimal mode', () => {
      const { setModuleEnabled, isModuleEnabled, isMinimalMode, resetModulesToDefault } = useFeatureFlags()

      // Disabling inventory activates minimal mode
      setModuleEnabled('inventory', false)
      expect(isModuleEnabled('inventory')).toBe(false)
      expect(isMinimalMode.value).toBe(true)

      // Core cannot be disabled
      setModuleEnabled('core', false)
      expect(isModuleEnabled('core')).toBe(true)

      // Re-enabling inventory restores monolith mode
      setModuleEnabled('inventory', true)
      expect(isModuleEnabled('inventory')).toBe(true)
      expect(isMinimalMode.value).toBe(false)

      resetModulesToDefault()
    })

    it('persists client module overrides across reloads via localStorage', () => {
      const { setModuleEnabled } = useFeatureFlags()
      setModuleEnabled('telemetry', false)

      const stored = storageMock.get('heimdall_modules_config')
      expect(stored).toBeDefined()
      expect(JSON.parse(stored!)).toEqual({ telemetry: false })

      const { resetModulesToDefault, isModuleEnabled } = useFeatureFlags()
      resetModulesToDefault()
      expect(storageMock.has('heimdall_modules_config')).toBe(false)
    })
  })

  describe('Server Feature Flags & Module Gating (featureFlags.ts)', () => {
    it('evaluates server module flags as enabled by default', () => {
      delete process.env.HEIMDALL_MODULE_TICKETING
      delete process.env.HEIMDALL_MODULE_INVENTORY
      delete process.env.HEIMDALL_MODULE_TELEMETRY

      const flags = getFeatureFlags()
      expect(flags.enableTicketing).toBe(true)
      expect(flags.enableInventory).toBe(true)
      expect(flags.enableTelemetry).toBe(true)
    })

    it('assertModuleEnabled does not throw for core module regardless of env', () => {
      expect(() => assertModuleEnabled('core')).not.toThrow()
    })

    it('assertModuleEnabled throws 503 when a module is disabled via environment variable', () => {
      process.env.HEIMDALL_MODULE_TICKETING = 'false'
      expect(() => assertModuleEnabled('ticketing')).toThrowError(/Service Unavailable: Module 'ticketing' is currently disabled/)

      process.env.HEIMDALL_MODULE_INVENTORY = 'false'
      expect(() => assertModuleEnabled('inventory')).toThrowError(/Service Unavailable: Module 'inventory' is currently disabled/)

      process.env.HEIMDALL_MODULE_TELEMETRY = 'false'
      expect(() => assertModuleEnabled('telemetry')).toThrowError(/Service Unavailable: Module 'telemetry' is currently disabled/)
    })
  })

  describe('Navigation Menu Module Tagging', () => {
    it('tags appropriate menu items and groups with requiredModule', () => {
      // Find Inventory item
      const managementGroup = navMenu.find(g => g.heading === 'Management')
      expect(managementGroup).toBeDefined()
      const invItem = managementGroup?.items.find((i: any) => i.title === 'Inventory') as any
      expect(invItem).toBeDefined()
      expect(invItem.requiredModule).toBe('inventory')

      // Find Data group
      const dataGroup = navMenu.find(g => g.heading === 'Data')
      expect(dataGroup).toBeDefined()
      expect(dataGroup?.requiredModule).toBe('telemetry')
      for (const item of (dataGroup?.items || []) as any[]) {
        expect(item.requiredModule).toBe('telemetry')
      }

      // Find Maintenance group
      const maintGroup = navMenu.find(g => g.heading === 'Maintenance')
      expect(maintGroup).toBeDefined()
      expect(maintGroup?.requiredModule).toBe('ticketing')
      for (const item of (maintGroup?.items || []) as any[]) {
        expect(item.requiredModule).toBe('ticketing')
      }
    })
  })

  describe('UI Component Verification', () => {
    it('mounts LanguageSwitcher component and switches locales via inline variant', async () => {
      const wrapper = mount(LanguageSwitcher, {
        props: {
          variant: 'inline'
        }
      })

      expect(wrapper.exists()).toBe(true)
      const huButton = wrapper.find('[data-testid="locale-btn-hu"]')
      expect(huButton.exists()).toBe(true)

      await huButton.trigger('click')
      const { locale } = useAppI18n()
      expect(locale.value).toBe('hu')

      const enButton = wrapper.find('[data-testid="locale-btn-en"]')
      await enButton.trigger('click')
      expect(locale.value).toBe('en')
    })

    it('mounts ModularFeaturesManager component and renders module cards and presets', async () => {
      const wrapper = mount(ModularFeaturesManager)
      expect(wrapper.exists()).toBe(true)

      // Should render preset buttons
      expect(wrapper.text()).toContain('Quick Deployment Presets')
      expect(wrapper.text()).toContain('Standard Full Monolith')
      expect(wrapper.text()).toContain('Telemetry + Ticketing (No Inventory)')

      // Should render module cards
      expect(wrapper.text()).toContain('Core Platform')
      expect(wrapper.text()).toContain('Maintenance Ticketing')
      expect(wrapper.text()).toContain('Inventory & Spare Parts')
      expect(wrapper.text()).toContain('Industrial Telemetry')

      // Test toggling inventory module
      const invToggle = wrapper.find('[data-testid="toggle-inventory"]')
      expect(invToggle.exists()).toBe(true)
      await invToggle.trigger('click')

      const notification = wrapper.find('[data-testid="module-notification"]')
      expect(notification.exists()).toBe(true)

      const { isMinimalMode, resetModulesToDefault } = useFeatureFlags()
      expect(isMinimalMode.value).toBe(true)

      resetModulesToDefault()
    })
  })

  describe('Plugin Architecture & Addon System (usePlugins)', () => {
    it('initializes with built-in native plugins and reports status', () => {
      const { plugins, enabledPlugins, isPluginEnabled, getPlugin } = usePlugins()
      expect(plugins.value.length).toBeGreaterThanOrEqual(5)
      expect(isPluginEnabled('core-platform')).toBe(true)
      expect(isPluginEnabled('maintenance-ticketing')).toBe(true)
      expect(isPluginEnabled('inventory-assets')).toBe(true)
      expect(isPluginEnabled('industrial-telemetry')).toBe(true)

      const corePlugin = getPlugin('core-platform')
      expect(corePlugin).toBeDefined()
      expect(corePlugin?.isRequired).toBe(true)
    })

    it('enables, disables, and toggles plugins', async () => {
      const { isPluginEnabled, disablePlugin, enablePlugin, togglePlugin } = usePlugins()
      
      // Disable opc-ua-gateway if enabled, or toggle it
      expect(isPluginEnabled('opc-ua-gateway')).toBe(false)
      const enabled = await enablePlugin('opc-ua-gateway')
      expect(enabled).toBe(true)
      expect(isPluginEnabled('opc-ua-gateway')).toBe(true)

      await togglePlugin('opc-ua-gateway')
      expect(isPluginEnabled('opc-ua-gateway')).toBe(false)
    })

    it('prevents disabling required core plugin', async () => {
      const { disablePlugin, isPluginEnabled } = usePlugins()
      const result = await disablePlugin('core-platform')
      expect(result).toBe(false)
      expect(isPluginEnabled('core-platform')).toBe(true)
    })

    it('registers and unregisters custom third-party plugins with lifecycle hooks', () => {
      const { registerPlugin, unregisterPlugin, getPlugin, isPluginEnabled } = usePlugins()
      let installCalled = false
      let uninstallCalled = false

      registerPlugin({
        id: 'custom-scada-bridge',
        name: 'SCADA Bridge',
        version: '1.0.0',
        description: 'OPC-UA and MQTT integration connector',
        category: 'integration',
        enabled: true,
        hooks: {
          onInstall: () => { installCalled = true },
          onUninstall: () => { uninstallCalled = true }
        }
      })

      expect(installCalled).toBe(true)
      expect(isPluginEnabled('custom-scada-bridge')).toBe(true)
      expect(getPlugin('custom-scada-bridge')?.name).toBe('SCADA Bridge')

      const removed = unregisterPlugin('custom-scada-bridge')
      expect(removed).toBe(true)
      expect(uninstallCalled).toBe(true)
      expect(isPluginEnabled('custom-scada-bridge')).toBe(false)
    })
  })

  describe('Language Hot-Swap & Event Synchronization', () => {
    it('dispatches custom locale-changed event and updates DOM document language', () => {
      const { setLocale, locale } = useAppI18n()
      let eventDispatched = false
      let dispatchedLocale = ''

      const listener = (event: any) => {
        eventDispatched = true
        dispatchedLocale = event.detail?.locale
      }

      window.addEventListener('heimdall:locale-changed', listener)

      setLocale('hu')
      expect(locale.value).toBe('hu')
      expect(eventDispatched).toBe(true)
      expect(dispatchedLocale).toBe('hu')
      expect(document.documentElement.lang).toBe('hu')

      setLocale('en')
      expect(locale.value).toBe('en')
      expect(dispatchedLocale).toBe('en')
      expect(document.documentElement.lang).toBe('en')

      window.removeEventListener('heimdall:locale-changed', listener)
    })
  })
})

