import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import DelegationsManager from '../../../frontend/web/app/components/tickets/DelegationsManager.vue'
import MachineGroupManager from '../../../frontend/web/app/components/tickets/MachineGroupManager.vue'
import { navMenu } from '../../../frontend/web/app/constants/menus'

vi.stubGlobal('useAuthSession', () => ({
  user: { value: { id: 'usr-1', name: 'Plant Admin', email: 'admin@heimdall.dev' } },
  userRole: { value: 'system_admin' },
  dedicationTier: { value: 'manager' },
  simulatedPersona: { value: null },
  setSimulatedPersona: vi.fn(),
  clearSimulatedPersona: vi.fn()
}))

const mockGroups = [
  { id: 'grp-1', name: 'Battery Plant 01', description: 'Main plant floor', parentId: null, machineTypes: ['Milling', 'Fitting'] },
  { id: 'grp-2', name: 'Line 06 Module Assembly', description: 'Assembly line', parentId: 'grp-1', machineTypes: ['Screwing Station', 'AOI'] }
]

const mockRules = [
  { id: 'r-1', scopeType: 'Technology', target: 'Milling', technicianName: 'István Kovács', role: 'Manager' }
]

describe('Frontend Reorganization: Delegations and Machine Groups', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn((url: string) => {
      if (url === '/api/machine-groups') return Promise.resolve(mockGroups)
      if (url === '/api/technicians/rules') return Promise.resolve(mockRules)
      if (url === '/api/technicians/absences') return Promise.resolve([])
      if (url === '/api/integrations/teams/ooo') return Promise.resolve([])
      if (url === '/api/technicians/candidates') return Promise.resolve([
        { id: 'c-1', name: 'István Kovács', department: 'Maintenance', role: 'technician' }
      ])
      return Promise.resolve([])
    }))
  })

  it('organizes sidebar into the 4 requested sections: Management, Data, Maintenance, Settings', () => {
    const headings = navMenu.map(g => g.heading)
    expect(headings).toEqual(['Management', 'Data', 'Maintenance', 'Settings'])

    const mgmt = navMenu.find(g => g.heading === 'Management')!
    const data = navMenu.find(g => g.heading === 'Data')!
    const maint = navMenu.find(g => g.heading === 'Maintenance')!
    const settings = navMenu.find(g => g.heading === 'Settings')!

    // Management items
    expect(mgmt.items.map(i => i.link)).toContain('/dashboard')
    expect(mgmt.items.map(i => i.link)).toContain('/dashboard/inventory')
    expect(mgmt.items.map(i => i.link)).toContain('/dashboard/machines')
    expect(mgmt.items.map(i => i.link)).toContain('/dashboard/machine-groups')
    expect(mgmt.items.map(i => i.link)).toContain('/dashboard/clients')
    expect(mgmt.items.map(i => i.link)).toContain('/dashboard/map')

    // Data items
    expect(data.items.map(i => i.link)).toContain('/dashboard/telemetry')
    expect(data.items.map(i => i.link)).toContain('/dashboard/analytics')
    expect(data.items.map(i => i.link)).toContain('/dashboard/telemetry/templates')
    expect(data.items.map(i => i.link)).toContain('/dashboard/telemetry/configure')

    // Maintenance items
    expect(maint.items.map(i => i.link)).toContain('/dashboard/tickets')
    expect(maint.items.map(i => i.link)).toContain('/dashboard/delegations')

    // Settings items
    expect(settings.items.map(i => i.link)).toContain('/dashboard/users')
    expect(settings.items.map(i => i.link)).toContain('/dashboard/organizations')
    expect(settings.items.map(i => i.link)).toContain('/dashboard/security-groups')
    expect(settings.items.map(i => i.link)).toContain('/dashboard/admin/system-settings')
    expect(settings.items.map(i => i.link)).toContain('/admin/studio')
  })

  it('renders DelegationsManager with shift attendance, rules, and cluster tabs in full-page mode', async () => {
    const wrapper = mount(DelegationsManager, {
      props: { isModal: false },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          SearchableTargetCombobox: { template: '<div></div>' }
        }
      }
    })

    // Assert top stats render in page mode
    expect(wrapper.text()).toContain('Total Technicians')
    expect(wrapper.text()).toContain('Available On Duty')
    expect(wrapper.text()).toContain('Absences Marked')
    expect(wrapper.text()).toContain('Teams OOO Status')

    // Assert tab buttons
    expect(wrapper.text()).toContain('Shift Attendance')
    expect(wrapper.text()).toContain('Technician Dedication')
    expect(wrapper.text()).toContain('Machine Group Clusters')
  })

  it('renders MachineGroupManager with hierarchy and cluster tags', async () => {
    const wrapper = mount(MachineGroupManager, {
      props: { isModal: false },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' }
        }
      }
    })

    await new Promise(r => setTimeout(r, 10))

    // Assert page stats bar
    expect(wrapper.text()).toContain('Total Groups')
    expect(wrapper.text()).toContain('Root Plants')
    expect(wrapper.text()).toContain('Sub-Lines & Cells')

    // Assert hierarchy and form buttons
    expect(wrapper.text()).toContain('Group Structure Hierarchy')
    expect(wrapper.text()).toContain('Add Root Group')
  })
})
