import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, computed } from 'vue'
import RemoteQuickViewModal from '../../../src/frontend/web/app/components/controllers/RemoteQuickViewModal.vue'

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
vi.mock('../../../src/frontend/web/app/composables/useAuthSession', () => {
  return {
    useAuthSession: () => {
      const userRole = computed(() => mockCurrentRole.value)
      const canExecuteRemote = computed(() => ['system_admin', 'engineering_admin', 'lead_engineer', 'engineer'].includes(userRole.value))
      const canManageEndpoints = computed(() => ['system_admin', 'engineering_admin', 'lead_engineer', 'engineer'].includes(userRole.value))
      return {
        userRole,
        canExecuteRemote,
        canManageEndpoints
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

const rfbUrls: string[] = []
vi.mock('../../../src/frontend/web/app/composables/useRfbClient', () => {
  return {
    createRfbClient: async (target: any, url: string, options: any) => {
      rfbUrls.push(url)
      return new MockRFB(target, url, options)
    }
  }
})

// Remote-access links per controller id, as the backend would resolve them.
const remoteAccessByController: Record<string, any> = {
  'ctrl-win-01': { controllerId: 'ctrl-win-01', hostname: 'DOCKERW-MHBLVEM', ipAddress: '127.0.0.1', vnc: { host: null, port: 8006, path: 'websockify', secure: false } },
  'ctrl-win-02': { controllerId: 'ctrl-win-02', hostname: 'IPC-L02', ipAddress: '10.42.2.11', vnc: { host: '10.42.2.11', port: 6080, path: 'vnc/ws', secure: false } },
  'ctrl-unlinked': { controllerId: 'ctrl-unlinked', hostname: 'IPC-BARE', ipAddress: '10.42.9.9', vnc: null }
}
const fetchMock = vi.fn(async (url: string, opts?: any) => {
  const match = /ClientPc\/([^/]+)\/remote-access(\/vnc)?$/.exec(url)
  if (!match) throw new Error(`unexpected request ${url}`)
  const id = decodeURIComponent(match[1])
  if (match[2] && opts?.method === 'PUT') {
    remoteAccessByController[id] = { ...remoteAccessByController[id], controllerId: id, vnc: opts.body }
  }
  const found = remoteAccessByController[id]
  if (!found) throw Object.assign(new Error('Not Found'), { statusCode: 404 })
  return found
})
vi.stubGlobal('$fetch', fetchMock)

const modalStubs = {
  Dialog: { template: '<div><slot /></div>' },
  DialogContent: { template: '<div><slot /></div>' },
  DialogTitle: { template: '<div><slot /></div>' },
  DialogDescription: { template: '<div><slot /></div>' },
  Badge: { template: '<span><slot /></span>' },
  Button: { template: '<button><slot /></button>' },
  RbacButton: { template: '<button><slot /></button>' },
  RbacTooltip: { template: '<div><slot /></div>' }
}

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
    rfbUrls.length = 0
    vi.stubGlobal('$fetch', fetchMock)
  })

  it('connects to the endpoint linked to the opened controller, not a shared default', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: { controller: mockController as any, open: true },
      global: { stubs: modalStubs }
    })
    await new Promise(r => setTimeout(r, 50))
    expect(rfbUrls).toEqual([`ws://${window.location.hostname}:8006/websockify`])

    // Same dialog, different controller: its own link is resolved and the first session is dropped.
    await wrapper.setProps({ controller: { ...mockController, id: 'ctrl-win-02', name: 'IPC-L02', hostname: 'IPC-L02', ipAddress: '10.42.2.11' } as any })
    await new Promise(r => setTimeout(r, 50))
    expect(rfbUrls).toEqual([`ws://${window.location.hostname}:8006/websockify`, 'ws://10.42.2.11:6080/vnc/ws'])
    expect(wrapper.text()).toContain('10.42.2.11:6080/vnc/ws')
  })

  it('does not open any session for a controller without a linked endpoint', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: { controller: { ...mockController, id: 'ctrl-unlinked', name: 'IPC-BARE', hostname: 'IPC-BARE', ipAddress: '10.42.9.9' } as any, open: true },
      global: { stubs: modalStubs }
    })
    await new Promise(r => setTimeout(r, 50))

    expect(rfbUrls).toEqual([])
    expect(wrapper.text()).toContain('No remote desktop linked to IPC-BARE')
    expect(wrapper.text()).not.toContain('RFB 3.8 Connected')
  })

  it('reports an unresolvable link instead of falling back to another machine', async () => {
    const wrapper = mount(RemoteQuickViewModal, {
      props: { controller: { ...mockController, id: 'ctrl-missing' } as any, open: true },
      global: { stubs: modalStubs }
    })
    await new Promise(r => setTimeout(r, 50))

    expect(rfbUrls).toEqual([])
    expect(wrapper.text()).toContain('Remote access link unavailable')
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
