import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useGlobalContextMenu } from '../../../frontend/web/app/composables/useGlobalContextMenu'
import GlobalContextMenu from '../../../frontend/web/app/components/common/GlobalContextMenu.vue'

describe('Global Context Menu Test Suite', () => {
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
})
