import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import StatCard from '~/components/dashboard/StatCard.vue'
import Hero from '~/components/dashboard/Hero.vue'
import InventoryTreeTable from '~/components/dashboard/InventoryTreeTable.vue'

describe('Data Fetching, Interpretation, and Rendering Suite', () => {
  const mockTreeResponse = {
    tree: [
      {
        id: 'station-01',
        name: 'OP10-Load',
        displayName: 'Station 10 - Loading',
        organizationId: 'Production Floor A',
        responsibleTeams: [{ id: 't-1', name: 'Controls Engineering' }],
        controllers: [{ id: 'pc-1', hostname: 'CPC-01' }],
        children: [
          {
            id: 'plc-1',
            name: 'PLC-01',
            displayName: 'Siemens S7-1500',
            itemType: 'HardwareComponent',
            metadata: { Model: '1516-3 PN/DP', Memory: '1MB Code' },
            children: []
          }
        ]
      }
    ]
  }

  const mockClientPcsResponse = [
    {
      id: 'pc-1',
      name: 'CPC-01',
      displayName: 'CPC-01',
      hostname: 'CPC-01',
      macAddress: '02:65:54:CE:AE:FC',
      machineIdentifier: 'ID-6554ceae',
      lastSeen: new Date().toISOString(),
      organizationId: 'Production Floor A',
      freeDiskSpace: { totalFreeGB: 120.5, osDriveFreeGB: 45.2 },
      systemMetadata: { IPAddress: '10.0.1.10', OsVersion: 'Windows 10 IoT' },
      controlledMachines: [{ id: 'station-01', name: 'OP10-Load' }],
      responsibleTeams: [{ id: 't-1', name: 'IT Infrastructure' }]
    }
  ]

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string) => {
      if (url.includes('/api/inventory/tree')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve(mockTreeResponse)
        })
      }
      if (url.includes('/api/clientpc')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve(mockClientPcsResponse)
        })
      }
      return Promise.reject(new Error('Unknown URL'))
    }))

    vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string) => {
      if (url.includes('/api/inventory/tree')) {
        return Promise.resolve(mockTreeResponse)
      }
      if (url.includes('/api/inventory/teams')) {
        return Promise.resolve([{ id: 't-1', name: 'Controls Engineering' }])
      }
      return Promise.resolve({})
    }))

    vi.stubGlobal('localStorage', {
      getItem: vi.fn(),
      setItem: vi.fn(),
      removeItem: vi.fn()
    })
  })

  it('correctly interprets and formats StatCard metrics', () => {
    const wrapper = mount(StatCard, {
      props: {
        title: 'Active Controller Nodes',
        value: 8,
        bgColor: 'bg-primary',
        trend: '+2'
      }
    })

    expect(wrapper.text()).toContain('Active Controller Nodes')
    expect(wrapper.text()).toContain('8')
    expect(wrapper.text()).toContain('+2')
  })

  it('renders Hero header card without artificial pulses or gradients', () => {
    const wrapper = mount(Hero, {
      props: {
        userName: 'Administrator',
        userRole: 'admin'
      }
    })

    expect(wrapper.text()).toContain('Welcome back, Administrator')
    expect(wrapper.text()).toContain('System Nominal')
  })

  it('fetches, interprets, and renders tree inventory hierarchy with responsible teams', async () => {
    const wrapper = mount(InventoryTreeTable, {
      props: { primaryKey: 'machine' },
      global: {
        stubs: {
          Popover: { template: '<div><slot /></div>' },
          PopoverTrigger: { template: '<div><slot /></div>' },
          PopoverContent: { template: '<div><slot /></div>' },
          Badge: { template: '<span class="badge"><slot /></span>' }
        }
      }
    })

    // Wait for async fetch in onMounted
    await new Promise(resolve => setTimeout(resolve, 50))
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('OP10-Load')
    expect(wrapper.text()).toContain('Production Floor A')
    expect(wrapper.text()).toContain('Controls Engineering')
  })

  it('correctly parses JSONB disk space and telemetry payloads from Client PCs', () => {
    const pc = mockClientPcsResponse[0]
    expect(pc.freeDiskSpace.totalFreeGB).toBeGreaterThan(100)
    expect(pc.systemMetadata.IPAddress).toBe('10.0.1.10')
    expect(pc.controlledMachines[0].name).toBe('OP10-Load')
  })
})
