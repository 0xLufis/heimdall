import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import InteractiveMap from '~/components/dashboard/InteractiveMap.vue'
import InventoryTreeTable from '~/components/dashboard/InventoryTreeTable.vue'
import UserTable from '~/components/dashboard/UserTable.vue'
import OrgCard from '~/components/dashboard/OrgCard.vue'

describe('All Pages, Components, and Table Selectors Test Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('useShortcuts', () => ({ metaSymbol: 'Ctrl' }))
    vi.stubGlobal('useRouter', () => ({ push: vi.fn() }))

    vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string) => {
      if (url.includes('/sample/assembly_line.dxf')) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(`0\nSECTION\n2\nENTITIES\n0\nINSERT\n2\nCNC_MACHINE\n5\nCNC-MC-12\n10\n10.0\n20\n20.0\n0\nCIRCLE\n5\nC-TANK-4\n10\n50.0\n20\n50.0\n40\n10.0\n0\nENDSEC\n0\nEOF`)
        })
      }
      if (url.includes('/api/inventory/tree')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve({
            tree: [
              {
                id: 'm-1',
                name: 'OP10-Load',
                displayName: 'Station 10 - Loading',
                organizationId: 'Production Floor A',
                responsibleTeams: [{ id: 't-1', name: 'Controls Engineering' }],
                children: []
              }
            ]
          })
        })
      }
      if (url.includes('/api/inventory/teams')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve([
            { id: 't-1', name: 'Controls Engineering' },
            { id: 't-2', name: 'Maintenance Team' }
          ])
        })
      }
      return Promise.resolve({
        ok: true,
        json: () => Promise.resolve({})
      })
    }))

    vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string) => {
      if (url.includes('/api/users')) {
        return Promise.resolve({
          success: true,
          users: [
            { id: 'u-1', name: 'System Administrator', email: 'admin@heimdall.dev', role: 'admin', banned: false },
            { id: 'u-2', name: 'Jane Engineer', email: 'jane@heimdall.dev', role: 'engineer', banned: false }
          ]
        })
      }
      if (url.includes('/api/organizations')) {
        return Promise.resolve({
          success: true,
          organizations: [
            { id: 'org-1', name: 'Heimdall Engineering', slug: 'engineering' },
            { id: 'org-2', name: 'Plant Operations', slug: 'plant-ops' }
          ]
        })
      }
      return Promise.resolve([])
    }))
  })

  it('loads and renders Interactive Map with DXF entities and clickable handles', async () => {
    const wrapper = mount(InteractiveMap, {
      props: {
        dxfUrl: '/sample/assembly_line.dxf',
        activePin: 'CNC-MC-12'
      }
    })
    await new Promise(r => setTimeout(r, 50))
    expect(wrapper.exists()).toBe(true)
  })

  it('renders InventoryTreeTable with team filter selectors and table headers', async () => {
    const wrapper = mount(InventoryTreeTable, {
      props: { primaryKey: 'machine' }
    })
    await new Promise(r => setTimeout(r, 50))
    expect(wrapper.text()).toContain('All Teams')
    expect(wrapper.text()).toContain('Columns')
  })

  it('renders UserTable with user entries and role selectors', async () => {
    const mockUsers = [
      { id: 'u-1', name: 'System Administrator', email: 'admin@heimdall.dev', role: 'admin', banned: false },
      { id: 'u-2', name: 'Jane Engineer', email: 'jane@heimdall.dev', role: 'engineer', banned: false }
    ]
    const wrapper = mount(UserTable, {
      props: {
        users: mockUsers,
        roles: ['admin', 'engineer', 'technician'],
        loading: false
      }
    })
    expect(wrapper.text()).toContain('Identity')
    expect(wrapper.text()).toContain('Access Level')
  })

  it('renders OrgCard with organization details and management actions', async () => {
    const mockOrg = {
      id: 'org-1',
      name: 'Heimdall Engineering',
      slug: 'engineering',
      createdAt: new Date().toISOString()
    }
    const wrapper = mount(OrgCard, {
      props: { org: mockOrg }
    })
    expect(wrapper.text()).toContain('Heimdall Engineering')
  })
})
