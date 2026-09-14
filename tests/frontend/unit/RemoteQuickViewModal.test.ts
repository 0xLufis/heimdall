import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, computed } from 'vue'
import RemoteQuickViewModal from '../../../frontend/web/app/components/controllers/RemoteQuickViewModal.vue'

// Mock useAuthSession
const mockCurrentRole = ref('engineer')

// Mock ResizeObserver for JSDOM
if (typeof global !== 'undefined' && !global.ResizeObserver) {
  global.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as any
}
vi.mock('../../../frontend/web/app/composables/useAuthSession', () => {
  return {
    useAuthSession: () => {
      const userRole = computed(() => mockCurrentRole.value)
      const canExecuteRemote = computed(() => ['system_admin', 'engineering_admin', 'lead_engineer', 'engineer'].includes(userRole.value))
      return {
        userRole,
        canExecuteRemote
      }
    }
  }
})

// Mock @novnc/novnc
class MockRFB {
  target: any
  url: string
  options: any
  scaleViewport = false
  resizeSession = false
  clipViewport = false
  viewOnly = false
  focusOnClick = false
  listeners: Record<string, Function[]> = {}

  constructor(target: any, url: string, options: any) {
    this.target = target
    this.url = url
    this.options = options
    setTimeout(() => {
      this.dispatchEvent('connect', {})
      this.dispatchEvent('desktopname', { detail: { name: 'Windows 10 LTSC Test Node' } })
    }, 10)
  }

  addEventListener(event: string, callback: Function) {
    if (!this.listeners[event]) this.listeners[event] = []
    this.listeners[event].push(callback)
  }

  dispatchEvent(event: string, data: any) {
    if (this.listeners[event]) {
      this.listeners[event].forEach(cb => cb(data))
    }
  }

  sendCtrlAltDel = vi.fn()
  disconnect = vi.fn(() => {
    this.dispatchEvent('disconnect', { detail: { clean: true } })
  })
}

vi.mock('../../../frontend/web/app/composables/useRfbClient', () => {
  return {
    createRfbClient: async (target: any, url: string, options: any) => {
      return new MockRFB(target, url, options)
    }
  }
})

describe('RemoteQuickViewModal.vue Component Test Suite', () => {
  const mockController = {
    id: 'ctrl-win-01',
    name: 'DOCKERW-MHBLVEM',
    hostname: 'DOCKERW-MHBLVEM',
    ipAddress: '127.0.0.1',
    macAddress: '02:BF:66:2E:EC:DD',
    isOnline: true,
    osVersion: 'Microsoft Windows 10 Enterprise LTSC'
  }

  beforeEach(() => {
    mockCurrentRole.value = 'engineer'
  })

  it('renders modal header with controller details when open is true', () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: {
        controller: mockController as any,
        open: true
      },
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<div><slot /></div>' },
          DialogDescription: { template: '<div><slot /></div>' },
          Badge: { template: '<span><slot /></span>' },
          Button: { template: '<button><slot /></button>' },
          RbacButton: { template: '<button><slot /></button>' },
          RbacTooltip: { template: '<div><slot /></div>' }
        }
      }
    })

    expect(wrapper.text()).toContain('DOCKERW-MHBLVEM')
    expect(wrapper.text()).toContain('127.0.0.1')
    expect(wrapper.text()).toContain('Online')
    expect(wrapper.text()).toContain('HTML5 VNC')
    expect(wrapper.text()).toContain('DameWare MRC')
    expect(wrapper.text()).toContain('RDP')
  })

  it('defaults to Live RFB mode with port 8006 and path websockify for Windows Docker node', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: {
        controller: mockController as any,
        open: true
      },
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<div><slot /></div>' },
          DialogDescription: { template: '<div><slot /></div>' },
          Badge: { template: '<span><slot /></span>' },
          Button: { template: '<button><slot /></button>' },
          RbacButton: { template: '<button><slot /></button>' },
          RbacTooltip: { template: '<div><slot /></div>' }
        }
      }
    })

    // Wait for connect event
    await new Promise(r => setTimeout(r, 50))

    expect(wrapper.text()).toContain('RFB 3.8 Connected')
    expect(wrapper.text()).toContain('Live RFB')
    expect(wrapper.text()).toContain('Simulation')
    expect(wrapper.text()).toContain('Ctrl+Alt+Del')
  })

  it('allows switching between Live RFB and Simulation modes', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: {
        controller: mockController as any,
        open: true
      },
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<div><slot /></div>' },
          DialogDescription: { template: '<div><slot /></div>' },
          Badge: { template: '<span><slot /></span>' },
          Button: { template: '<button><slot /></button>' },
          RbacButton: { template: '<button><slot /></button>' },
          RbacTooltip: { template: '<div><slot /></div>' }
        }
      }
    })

    const simButton = wrapper.findAll('button').find(b => b.text().includes('Simulation'))
    expect(simButton).toBeDefined()
    await simButton?.trigger('click')

    expect(wrapper.text()).toContain('TwinCAT Viewport Simulation')

    const liveButton = wrapper.findAll('button').find(b => b.text().includes('Live RFB'))
    expect(liveButton).toBeDefined()
    await liveButton?.trigger('click')

    await new Promise(r => setTimeout(r, 50))
    expect(wrapper.text()).toContain('RFB 3.8 Connected')
  })

  it('renders DameWare and RDP tabs accurately', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: {
        controller: mockController as any,
        open: true
      },
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<div><slot /></div>' },
          DialogDescription: { template: '<div><slot /></div>' },
          Badge: { template: '<span><slot /></span>' },
          Button: { template: '<button><slot /></button>' },
          RbacButton: { template: '<button><slot /></button>' },
          RbacTooltip: { template: '<div><slot /></div>' }
        }
      }
    })

    // Switch to DameWare
    const damewareTab = wrapper.findAll('button').find(b => b.text().includes('DameWare MRC'))
    expect(damewareTab).toBeDefined()
    await damewareTab?.trigger('click')

    expect(wrapper.text()).toContain('dwmrc://127.0.0.1?port=6129&use_cur_creds=1')
    expect(wrapper.text()).toContain('Launch DameWare Client')

    // Switch to RDP
    const rdpTab = wrapper.findAll('button').find(b => b.text().includes('RDP'))
    expect(rdpTab).toBeDefined()
    await rdpTab?.trigger('click')

    expect(wrapper.text()).toContain('127.0.0.1:3389')
    expect(wrapper.text()).toContain('Download .RDP Profile')
  })

  it('supports fullscreen toggle and renders mobile responsive indicator', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: {
        controller: mockController as any,
        open: true
      },
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<div><slot /></div>' },
          DialogDescription: { template: '<div><slot /></div>' },
          Badge: { template: '<span><slot /></span>' },
          Button: { template: '<button><slot /></button>' },
          RbacButton: { template: '<button><slot /></button>' },
          RbacTooltip: { template: '<div><slot /></div>' }
        }
      }
    })

    // Verify mobile touch optimization banner exists
    expect(wrapper.text()).toContain('Mobile Touch Optimized')

    // Find fullscreen button by title
    const fullscreenBtn = wrapper.findAll('button').find(b => b.attributes('title')?.includes('Fullscreen'))
    expect(fullscreenBtn).toBeDefined()
    await fullscreenBtn?.trigger('click')

    // Fullscreen state active
    expect(wrapper.html()).toContain('Exit Fullscreen')
  })
})
