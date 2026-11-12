import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { ref } from 'vue'
import MachineCardGrid from '../../../src/frontend/web/app/components/machines/MachineCardGrid.vue'
import MachineLineHierarchy from '../../../src/frontend/web/app/components/machines/MachineLineHierarchy.vue'
import StationPhysicalView from '../../../src/frontend/web/app/components/inventory/StationPhysicalView.vue'
import ControllerTelemetryCard from '../../../src/frontend/web/app/components/controllers/ControllerTelemetryCard.vue'

const stubs = {
  NuxtLink: { props: ['to'], template: '<a :data-to="JSON.stringify(to)" @click="$emit(\'click\', $event)"><slot /></a>' },
  Badge: { template: '<span><slot /></span>' }
}

const machines = [
  {
    id: 'st-laser', name: 'NV-L2-OP060', displayName: 'Rotor Laser Welding Cell', customIdentifier: 'NV-L2-OP060',
    machineType: 'LaserWelding', lineGroupName: 'Line 2', lineGroupId: 'grp-l2', status: 'active',
    controllers: [{ id: 'ipc-a', hostname: 'nv-ipc-l2-laser' }, { id: 'ipc-b', hostname: 'nv-ipc-l2-main' }]
  },
  { id: 'st-cnc', name: 'NV-L3-OP020', displayName: 'Housing CNC', customIdentifier: 'NV-L3-OP020', machineType: 'CncMachining', lineGroupName: 'Line 3', status: 'maintenance', controllers: [] }
]

describe('Machine cards', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn(async () => []))
    vi.stubGlobal('useState', (_key: string, init?: () => any) => ref(init ? init() : undefined))
    vi.stubGlobal('useRuntimeConfig', () => ({ public: {} }))
  })

  it('opens the station when the card is clicked or activated by keyboard', async () => {
    const wrapper = mount(MachineCardGrid, { props: { machines }, global: { stubs } })
    const cards = wrapper.findAll('[role="button"]')
    expect(cards).toHaveLength(2)

    await cards[0].trigger('click')
    await cards[1].trigger('keydown', { key: 'Enter' })
    await cards[1].trigger('keydown', { key: ' ' })

    const opened = wrapper.emitted('open')!.map(e => (e[0] as any).id)
    expect(opened).toEqual(['st-laser', 'st-cnc', 'st-cnc'])
  })

  it('inner actions run their own handler without also opening the card', async () => {
    const wrapper = mount(MachineCardGrid, { props: { machines }, global: { stubs } })
    const card = wrapper.findAll('[role="button"]')[0]
    const button = (label: string) => card.findAll('button').find(b => b.text().includes(label))!

    await button('BOM').trigger('click')
    await button('3D').trigger('click')
    await button('nv-ipc-l2-main').trigger('click')
    await card.find('button[aria-label="Decommission machine"]').trigger('click')
    await card.findAll('a').find(a => a.text().includes('Live'))!.trigger('click')

    expect(wrapper.emitted('open')).toBeUndefined()
    expect(wrapper.emitted('openTree')).toEqual([['st-laser']])
    expect((wrapper.emitted('open3d')![0][0] as any).id).toBe('st-laser')
    // The clicked controller (the second one) is passed on, together with its station
    const quick = wrapper.emitted('openQuickView')![0] as any[]
    expect(quick[0].id).toBe('ipc-b')
    expect(quick[1].id).toBe('st-laser')
    expect((wrapper.emitted('openDecommission')![0][0] as any).id).toBe('st-laser')
  })

  it('lists every bound controller and says so when there is none', () => {
    const wrapper = mount(MachineCardGrid, { props: { machines }, global: { stubs } })
    const text = wrapper.text()
    expect(text).toContain('nv-ipc-l2-laser')
    expect(text).toContain('nv-ipc-l2-main')
    expect(text).toContain('No controller bound to this station')
    expect(text).toContain('Line 2')
    expect(text).toContain('No telemetry')
  })

  it('forwards card events (including the context menu target) through the line hierarchy', async () => {
    const wrapper = mount(MachineLineHierarchy, { props: { machines, mode: 'lines' }, global: { stubs } })
    expect(wrapper.text()).toContain('Line 2')
    expect(wrapper.text()).toContain('Line 3')

    const card = wrapper.findAll('[role="button"]')[0]
    await card.trigger('click')
    await card.trigger('contextmenu')
    await card.findAll('button').find(b => b.text().includes('nv-ipc-l2-laser'))!.trigger('click')

    expect((wrapper.emitted('open')![0][0] as any).id).toBe('st-laser')
    const menu = wrapper.emitted('contextMenu')![0] as any[]
    expect(menu[0].id).toBe('st-laser')
    expect(menu[1]).toBeInstanceOf(MouseEvent)
    const quick = wrapper.emitted('openQuickView')![0] as any[]
    expect(quick[0].id).toBe('ipc-a')
    expect(quick[1].id).toBe('st-laser')
  })
})

describe('Station hardware schematic', () => {
  const trees: Record<string, any> = {
    'st-laser': {
      stationId: 'st-laser', stationName: 'NV-L2-OP060', displayName: 'Rotor Laser Welding Cell', customIdentifier: 'NV-L2-OP060',
      controllers: [{ id: 'ipc-a', name: 'NV-IPC-L2-LASER', hostname: 'nv-ipc-l2-laser', ipAddress: '10.42.2.16', macAddress: '02:4E:56:02:00:02', lastOnline: new Date().toISOString() }],
      installedParts: [
        { id: 'part-optic', name: 'Optic Head', serialNumber: 'NV-DEMO-OH-2060', equipmentStatus: 'InMachine', functionalSlotIdentifier: 'TOOL-HEAD-01', manufacturerName: 'Lumen (Demo)' },
        { id: 'part-servo', name: 'Servo Drive X', equipmentStatus: 'UnderRepair', functionalSlotIdentifier: 'GANTRY-X-01' }
      ]
    },
    'st-empty': { stationId: 'st-empty', stationName: 'NV-L1-OP010', controllers: [], installedParts: [] }
  }
  const fetchMock = vi.fn(async (url: string) => {
    const id = decodeURIComponent(url.split('/').pop() || '')
    if (url.includes('/station-tree/')) {
      if (!trees[id]) throw Object.assign(new Error('Not Found'), { statusCode: 404 })
      return trees[id]
    }
    if (url === '/api/machines') return [{ id: 'st-laser', name: 'NV-L2-OP060', displayName: 'Rotor Laser Welding Cell' }]
    return []
  })

  beforeEach(() => {
    fetchMock.mockClear()
    vi.stubGlobal('$fetch', fetchMock)
  })

  it('draws the hardware of the given station and nothing else', async () => {
    const wrapper = mount(StationPhysicalView, { props: { stationId: 'st-laser' }, global: { stubs } })
    await flushPromises()

    const text = wrapper.text()
    expect(fetchMock).toHaveBeenCalledWith('/api/proxy/v1/inventory/station-tree/st-laser')
    expect(text).toContain('Rotor Laser Welding Cell')
    expect(text).toContain('nv-ipc-l2-laser')
    expect(text).toContain('Optic Head')
    expect(text).toContain('Servo Drive X')
    // The former built-in sample cell is gone
    expect(text).not.toContain('Beckhoff CX5140')
    expect(text).not.toContain('Keyence')

    await wrapper.findAll('button').find(b => b.text().includes('Optic Head'))!.trigger('click')
    const node = wrapper.emitted('selectComponent')![0][0] as any
    expect(node.installedPartId).toBe('part-optic')
    expect(wrapper.text()).toContain('S/N NV-DEMO-OH-2060')
  })

  it('reloads for another station and shows an explicit empty state', async () => {
    const wrapper = mount(StationPhysicalView, { props: { stationId: 'st-laser' }, global: { stubs } })
    await flushPromises()
    await wrapper.setProps({ stationId: 'st-empty' })
    await flushPromises()

    expect(wrapper.text()).not.toContain('Optic Head')
    expect(wrapper.text()).toContain('No hardware registered for this station')

    await wrapper.setProps({ stationId: 'st-unknown' })
    await flushPromises()
    expect(wrapper.text()).toContain('not registered in the machine directory')
  })

  it('offers its own station picker when no station is passed', async () => {
    const wrapper = mount(StationPhysicalView, { global: { stubs } })
    await flushPromises()
    await flushPromises()
    expect(fetchMock).toHaveBeenCalledWith('/api/machines')
    expect(wrapper.find('select').exists()).toBe(true)
    expect(wrapper.text()).toContain('Optic Head')
  })
})

describe('Controller detail card', () => {
  const base = { id: 'ipc-a', name: 'NV-IPC-A', hostname: 'nv-ipc-a', macAddress: '02:00:00:00:00:0A' }

  it('shows only properties the backend holds for that controller', () => {
    const wrapper = mount(ControllerTelemetryCard, {
      props: {
        controller: {
          ...base,
          ipAddress: '10.42.2.16',
          lastOnline: new Date().toISOString(),
          telemetry: { isOnline: true, cpuUsagePercent: 78.4, ramUsagePercent: 71.3 },
          freeDiskSpace: { totalFreeGB: 21.5, osDriveFreeGB: 6.8, drives: { 'C:': 6.8, 'D:': 14.7 } },
          systemMetadata: { OperatingSystem: 'Windows 10 IoT (demo)', Role: 'Dedicated laser cell controller', RemoteAccess: { Vnc: { Port: 8006, Path: 'websockify' } } },
          controlledMachines: [{ id: 'st-laser', name: 'Rotor Laser Welding Cell', customIdentifier: 'NV-L2-OP060' }]
        } as any
      },
      global: { stubs }
    })
    const text = wrapper.text()
    expect(text).toContain('78.4 %')
    expect(text).toContain('21.5 GB free')
    expect(text).toContain('Dedicated laser cell controller')
    expect(text).toContain('NV-L2-OP060')
    expect(text).toContain(':8006/websockify')
    for (const invented of ['Intel Core i7', 'KEPServerEX', 'BitLocker', 'SAP MII', '192.168.1.']) {
      expect(text).not.toContain(invented)
    }
  })

  it('does not invent values for a controller that reported nothing', async () => {
    const wrapper = mount(ControllerTelemetryCard, { props: { controller: { ...base, telemetry: { isOnline: false } } as any }, global: { stubs } })
    const text = wrapper.text()
    expect(text).toContain('IP: not reported')
    expect(text).not.toContain('CPU Load')
    expect(text).not.toMatch(/\d+ days, \d+ hours/)
    // Only categories with data are offered as filters
    expect(text).not.toContain('PLC & Runtime')
  })

  it('emits unpin for a pinned controller', async () => {
    const wrapper = mount(ControllerTelemetryCard, { props: { controller: { ...base, pinnedObjectHandle: 'IPC-L02', telemetry: { isOnline: true } } as any }, global: { stubs } })
    await wrapper.findAll('button').find(b => b.text().includes('Unpin'))!.trigger('click')
    expect((wrapper.emitted('unpin-dxf')![0][0] as any).id).toBe('ipc-a')
  })
})
