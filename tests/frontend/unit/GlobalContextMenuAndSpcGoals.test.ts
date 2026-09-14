import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useGlobalContextMenu } from '../../../frontend/web/app/composables/useGlobalContextMenu'
import GlobalContextMenu from '../../../frontend/web/app/components/common/GlobalContextMenu.vue'
import GrafanaMassTelemetryEmbed from '../../../frontend/web/app/components/analytics/GrafanaMassTelemetryEmbed.vue'

describe('Global Context Menu & Grafana Integration Test Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', {
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
      clear: vi.fn()
    })
    const { closeContextMenu } = useGlobalContextMenu()
    closeContextMenu()
  })

  describe('useGlobalContextMenu Composable', () => {
    it('manages singleton open/close state and stores context payload', () => {
      const { isOpen, x, y, contextData, openContextMenu, closeContextMenu } = useGlobalContextMenu()

      expect(isOpen.value).toBe(false)
      expect(contextData.value).toBeNull()

      const fakeEvent = {
        clientX: 150,
        clientY: 200,
        shiftKey: false,
        preventDefault: vi.fn(),
        stopPropagation: vi.fn()
      } as unknown as MouseEvent

      openContextMenu(fakeEvent, {
        entityType: 'map-node',
        handle: 'LINE1_OP10',
        machineId: 'm-op10',
        machineName: 'OP10 Infeed',
        ownerTeam: { name: 'Line 1 Maintenance' },
        ownerPerson: { name: 'Alex controls' }
      })

      expect(fakeEvent.preventDefault).toHaveBeenCalled()
      expect(isOpen.value).toBe(true)
      expect(x.value).toBe(150)
      expect(y.value).toBe(200)
      expect(contextData.value?.handle).toBe('LINE1_OP10')
      expect(contextData.value?.ownerTeam?.name).toBe('Line 1 Maintenance')

      closeContextMenu()
      expect(isOpen.value).toBe(false)
    })

    it('bypasses custom context menu when Shift key is pressed', () => {
      const { isOpen, openContextMenu } = useGlobalContextMenu()

      const fakeShiftEvent = {
        clientX: 150,
        clientY: 200,
        shiftKey: true,
        preventDefault: vi.fn(),
        stopPropagation: vi.fn()
      } as unknown as MouseEvent

      openContextMenu(fakeShiftEvent, {
        entityType: 'controller',
        controllerId: 'c-1'
      })

      // Must NOT preventDefault so browser native context menu opens
      expect(fakeShiftEvent.preventDefault).not.toHaveBeenCalled()
      expect(isOpen.value).toBe(false)
    })

    it('unlockNativeMenu enables native context menu pass-through', () => {
      const { isOpen, isNativeMenuBypassed, openContextMenu, unlockNativeMenu } = useGlobalContextMenu()

      unlockNativeMenu()
      expect(isNativeMenuBypassed.value).toBe(true)
      expect(isOpen.value).toBe(false)

      const normalEvent = {
        clientX: 100,
        clientY: 100,
        shiftKey: false,
        preventDefault: vi.fn(),
        stopPropagation: vi.fn()
      } as unknown as MouseEvent

      // Next call should pass through and reset bypass
      openContextMenu(normalEvent, { entityType: 'general' })
      expect(normalEvent.preventDefault).not.toHaveBeenCalled()
      expect(isOpen.value).toBe(false)
      expect(isNativeMenuBypassed.value).toBe(false)
    })
  })

  describe('GlobalContextMenu Component', () => {
    it('renders domain action buttons when context menu is active', async () => {
      const { openContextMenu } = useGlobalContextMenu()

      const fakeEvent = {
        clientX: 50,
        clientY: 50,
        shiftKey: false,
        preventDefault: vi.fn(),
        stopPropagation: vi.fn()
      } as unknown as MouseEvent

      openContextMenu(fakeEvent, {
        entityType: 'map-node',
        handle: 'OP20_WELD',
        machineId: 'm-op20',
        machineName: 'OP20 Laser Cell',
        controllerId: 'c-2',
        controllerHostname: 'IPC-L1-02',
        ownerTeam: { name: 'Robotics Team' },
        ownerPerson: { name: 'Elena Engineer' },
        tickets: [{ id: 't-1', title: 'Optic lens dirty', status: 'In_Progress' }]
      })

      const wrapper = mount(GlobalContextMenu, {
        global: {
          mocks: {
            $router: {
              push: vi.fn()
            }
          }
        }
      })

      expect(wrapper.text()).toContain('OP20 Laser Cell')
      expect(wrapper.text()).toContain('CAD NODE')
      expect(wrapper.text()).toContain('Go to Machine')
      expect(wrapper.text()).toContain('Go to Node / Controller')
      expect(wrapper.text()).toContain('Owner Team: Robotics Team')
      expect(wrapper.text()).toContain('Owner Person: Elena Engineer')
      expect(wrapper.text()).toContain('Go to Maintenance Tickets')
      expect(wrapper.text()).toContain('Create Incident Ticket')
      expect(wrapper.text()).toContain('Live Telemetry & Diagnostics')
      expect(wrapper.text()).toContain('Inspect Component Tree')
      expect(wrapper.text()).toContain('Copy Handle / ID')
      expect(wrapper.text()).toContain('Open Browser Context Menu')
    })
  })

  describe('GrafanaMassTelemetryEmbed Component', () => {
    it('renders dashboard presets and Heimdall metrics endpoints', () => {
      const wrapper = mount(GrafanaMassTelemetryEmbed)

      expect(wrapper.text()).toContain('Grafana OT Telemetry & Mass Visualization Workspace')
      expect(wrapper.text()).toContain('Spindle Vibration & High-Frequency FFT')
      expect(wrapper.text()).toContain('Statistical Process Control & Cpk Heatmaps')
      expect(wrapper.text()).toContain('Multi-Axis Motor Thermal & Current Draw')
      expect(wrapper.text()).toContain('Prometheus OT Edge Scrape Metrics')
      expect(wrapper.text()).toContain('/api/v1/ReportExport/grafana/metrics')
      expect(wrapper.text()).toContain('/api/v1/ReportExport/odata/telemetry')
    })

    it('updates active preset when clicked', async () => {
      const wrapper = mount(GrafanaMassTelemetryEmbed)

      const presetCards = wrapper.findAll('[role="button"]')
      expect(presetCards.length).toBeGreaterThanOrEqual(4)

      // Click second preset (SPC & Cpk)
      await presetCards[1].trigger('click')

      const iframe = wrapper.find('iframe')
      expect(iframe.attributes('src')).toContain('spc-capability')
    })

    it('renders interactive demo panels by default and toggles between demo and iframe modes', async () => {
      const wrapper = mount(GrafanaMassTelemetryEmbed)

      // Verify interactive demo is active
      expect(wrapper.text()).toContain('Interactive Demo')
      expect(wrapper.text()).toContain('Infinity Engine: Online')
      expect(wrapper.text()).toContain('High-Frequency FFT Spectral Spectrum')
      expect(wrapper.text()).toContain('ISO 10816-3 Spindle Velocity RMS')

      // Switch to External Server Iframe mode
      const iframeBtn = wrapper.findAll('button').find(b => b.text().includes('External Server (Iframe)'))
      expect(iframeBtn).toBeDefined()
      await iframeBtn?.trigger('click')

      // Now iframe controls are shown
      expect(wrapper.text()).toContain('Targeting Grafana Instance')
      expect(wrapper.text()).toContain('Reload')

      // Switch back to Interactive Demo
      const demoBtn = wrapper.findAll('button').find(b => b.text().includes('Interactive Demo'))
      expect(demoBtn).toBeDefined()
      await demoBtn?.trigger('click')

      expect(wrapper.text()).toContain('Inject Telemetry Spike')
    })

    it('renders SPC capability panel when SPC preset is chosen in demo mode', async () => {
      const wrapper = mount(GrafanaMassTelemetryEmbed)

      const presetCards = wrapper.findAll('[role="button"]')
      await presetCards[1].trigger('click')

      expect(wrapper.text()).toContain('Real-Time X-Bar Chart')
      expect(wrapper.text()).toContain('Capability Indices (Cp / Cpk)')
      expect(wrapper.text()).toContain('Six-Sigma Compliant')
    })
  })
})
