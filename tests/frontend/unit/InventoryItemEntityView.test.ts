import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import InventoryItemPage from '../../../src/frontend/web/app/pages/dashboard/inventory/[id].vue'
import type { InventoryPart } from '../../../src/frontend/web/app/types/inventory'

const mockPush = vi.fn()

vi.stubGlobal('definePageMeta', vi.fn())

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { id: 'prt-test-101' } }),
  useRouter: () => ({ push: mockPush })
}))

// Mock useInventoryLive
vi.mock('../../../src/frontend/web/app/composables/useInventoryLive', () => ({
  useInventoryLive: () => ({
    onInventoryUpdate: vi.fn()
  })
}))

const mockSamplePart: InventoryPart = {
  id: 'prt-test-101',
  customIdentifier: 'IPC-BECK-101',
  name: 'Beckhoff CX5140 Embedded PC',
  displayName: 'Beckhoff Industrial Controller CX5140',
  category: 'Hardware',
  trackingType: 'serialized',
  condition: 'new',
  operationalState: 'working',
  stockAlertStatus: 'optimal',
  manufacturer: { name: 'Beckhoff Automation' },
  supplier: { name: 'Kvalitex Industrial Supply' },
  owner: { type: 'organization', id: 'org-1', name: 'Line 1 Operations' },
  location: 'Cabinet 4A / Shelf 2',
  priceEur: 3200,
  estimatedResellPriceEur: 3200,
  quantity: 1,
  minQuantity: 1,
  serialNumber: 'SN-CX5140-9941',
  isMachineLinked: true,
  linkedMachineId: 'mach-cnc-1',
  linkedMachineName: 'CNC Mill #1',
  alternateParts: [
    { id: 'prt-alt-1', customIdentifier: 'IPC-SIEM-202', name: 'Siemens IPC227E', quantity: 2, priceEur: 3100 }
  ],
  tags: ['#Beckhoff', '#IPC', '#Controls'],
  createdAt: '2026-03-01T00:00:00Z',
  updatedAt: '2026-03-01T00:00:00Z'
}

describe('Inventory Item Entity View ([id].vue)', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn((url: string, opts?: any) => {
      if (url.includes('/api/inventory/parts/prt-test-101/audit')) {
        return Promise.resolve({
          auditLogs: [
            { id: 'aud-1', action: 'log_part', timestamp: '2026-03-01T10:00:00Z', userName: 'Alex', quantityChanged: 1, remainingQuantity: 1 }
          ]
        })
      }
      if (url.includes('/api/inventory/parts/prt-test-101')) {
        return Promise.resolve({
          success: true,
          part: mockSamplePart
        })
      }
      if (url.includes('/api/inventory/filter')) {
        return Promise.resolve({ items: [] })
      }
      return Promise.resolve(null)
    }))
  })

  it('fetches and renders full inventory part entity attributes cleanly', async () => {
    const wrapper = mount(InventoryItemPage, {
      props: {
        itemId: 'prt-test-101'
      },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          RemoteQuickViewModal: { template: '<div data-testid="remote-modal" />' },
          DashboardInventoryEditModal: { template: '<div data-testid="edit-modal" />' },
          PartAuditLogDrawer: { template: '<div data-testid="audit-drawer" />' },
          UsePartModal: { template: '<div data-testid="use-modal" />' }
        }
      }
    })

    await new Promise(r => setTimeout(r, 40))

    expect(wrapper.text()).toContain('Beckhoff Industrial Controller CX5140')
    expect(wrapper.text()).toContain('IPC-BECK-101')
    expect(wrapper.text()).toContain('serialized')
    expect(wrapper.text()).toContain('new')
    expect(wrapper.text()).toContain('working')
    expect(wrapper.text()).toContain('Beckhoff Automation')
    expect(wrapper.text()).toContain('Cabinet 4A / Shelf 2')
    expect(wrapper.text()).toContain('3200 €')
    expect(wrapper.text()).toContain('CNC Mill #1')
  })

  it('renders scannable QR code asset identifier', async () => {
    const wrapper = mount(InventoryItemPage, {
      props: {
        itemId: 'prt-test-101'
      },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          RemoteQuickViewModal: true,
          DashboardInventoryEditModal: true,
          PartAuditLogDrawer: true,
          UsePartModal: true
        }
      }
    })

    await new Promise(r => setTimeout(r, 40))

    const img = wrapper.find('img[alt="Asset QR Code"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toContain('data:image/svg+xml')
  })

  it('renders approved drop-in alternate parts', async () => {
    const wrapper = mount(InventoryItemPage, {
      props: {
        itemId: 'prt-test-101'
      },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          RemoteQuickViewModal: true,
          DashboardInventoryEditModal: true,
          PartAuditLogDrawer: true,
          UsePartModal: true
        }
      }
    })

    await new Promise(r => setTimeout(r, 40))

    expect(wrapper.text()).toContain('Approved Drop-in Alternates')
    expect(wrapper.text()).toContain('IPC-SIEM-202')
    expect(wrapper.text()).toContain('Siemens IPC227E')
  })

  it('triggers action modals on user interaction', async () => {
    const wrapper = mount(InventoryItemPage, {
      props: {
        itemId: 'prt-test-101'
      },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          RemoteQuickViewModal: true,
          DashboardInventoryEditModal: true,
          PartAuditLogDrawer: true,
          UsePartModal: true
        }
      }
    })

    await new Promise(r => setTimeout(r, 40))

    const vm = wrapper.vm as any

    // Trigger Audit Trail
    const auditBtn = wrapper.findAll('button').find(b => b.text().includes('Audit Trail'))
    expect(auditBtn).toBeDefined()
    await auditBtn!.trigger('click')
    expect(vm.showAuditDrawer).toBe(true)

    // Trigger Use Part
    const useBtn = wrapper.findAll('button').find(b => b.text().includes('Use / Issue Part'))
    expect(useBtn).toBeDefined()
    await useBtn!.trigger('click')
    expect(vm.isUsePartModalOpen).toBe(true)

    // Trigger Edit Entity
    const editBtn = wrapper.findAll('button').find(b => b.text().includes('Edit Entity'))
    expect(editBtn).toBeDefined()
    await editBtn!.trigger('click')
    expect(vm.showEditModal).toBe(true)
  })

  it('handles not found gracefully when entity does not exist', async () => {
    vi.stubGlobal('$fetch', vi.fn(() => Promise.reject(new Error('Not found'))))

    const wrapper = mount(InventoryItemPage, {
      props: {
        itemId: 'prt-test-101'
      },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          RemoteQuickViewModal: true,
          DashboardInventoryEditModal: true,
          PartAuditLogDrawer: true,
          UsePartModal: true
        }
      }
    })

    await new Promise(r => setTimeout(r, 40))

    expect(wrapper.text()).toContain('Asset Entity Not Found')
    expect(wrapper.text()).toContain('Return to Inventory')
  })
})
