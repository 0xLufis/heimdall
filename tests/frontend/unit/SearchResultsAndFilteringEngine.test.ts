import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import SearchPage from '../../../src/frontend/web/app/pages/dashboard/search.vue'

const mockPush = vi.fn()
const mockReplace = vi.fn()

vi.stubGlobal('definePageMeta', vi.fn())

vi.mock('vue-router', () => ({
  useRoute: () => ({ query: { q: 'status:active' } }),
  useRouter: () => ({ push: mockPush, replace: mockReplace })
}))

const mockMachines = [
  { id: 'm-1', name: 'Laser Welder 1', machineType: 'Welding', status: 'active', groupId: 'Line 1' },
  { id: 'm-2', name: 'Vision Station 2', machineType: 'Test', status: 'maintenance', groupId: 'Line 2' },
  { id: 'm-3', name: 'Dismantled Cell', machineType: 'Assembly', status: 'decommissioned', groupId: 'Line 1' }
]

const mockTickets = [
  { id: 't-1', ticketNumber: 'TICK-101', title: 'Laser calibration error', priority: 'critical', status: 'open', isEscalated: true }
]

describe('Standalone Search Results Page & Filtering Engine', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn((url: string) => {
      if (url.includes('/api/machines')) return Promise.resolve(mockMachines)
      if (url.includes('/api/tickets')) return Promise.resolve(mockTickets)
      if (url.includes('/api/clients')) return Promise.resolve([])
      if (url.includes('/api/inventory')) return Promise.resolve([])
      if (url.includes('/api/users')) return Promise.resolve([])
      return Promise.resolve([])
    }))
  })

  it('renders search input, provider filters, and sorting controls', async () => {
    const wrapper = mount(SearchPage, {
      global: {
        mocks: {
          $route: { query: { q: 'status:active' } },
          $router: { push: mockPush, replace: mockReplace }
        },
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          MachinePictogram: { template: '<div data-testid="pictogram"></div>' }
        }
      }
    })

    await new Promise(r => setTimeout(r, 20))

    expect(wrapper.text()).toContain('Omni Search Results')
    expect(wrapper.text()).toContain('All Providers')
    expect(wrapper.text()).toContain('Machines')
    expect(wrapper.text()).toContain('Tickets')
    expect(wrapper.find('select').exists()).toBe(true)
  })

  it('filters results according to positive and negative tags (AND logic gate)', async () => {
    const wrapper = mount(SearchPage, {
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          MachinePictogram: { template: '<div data-testid="pictogram"></div>' }
        }
      }
    })

    await new Promise(r => setTimeout(r, 30))

    const vm = wrapper.vm as any
    // Query with !status:decommissioned
    vm.searchQuery = 'status:active !status:decommissioned'
    await wrapper.vm.$nextTick()

    // Active machine should be present, decommissioned machine should be filtered out
    const names = vm.sortedResults.map((r: any) => r.name)
    expect(names).toContain('Laser Welder 1')
    expect(names).not.toContain('Dismantled Cell')
  })

  it('sorts results by matching rate and name', async () => {
    const wrapper = mount(SearchPage, {
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          MachinePictogram: { template: '<div data-testid="pictogram"></div>' }
        }
      }
    })

    await new Promise(r => setTimeout(r, 30))

    const vm = wrapper.vm as any
    vm.searchQuery = 'Welder'
    await wrapper.vm.$nextTick()

    expect(vm.sortedResults.length).toBeGreaterThan(0)
    expect(vm.sortedResults[0].name).toBe('Laser Welder 1')
    expect(vm.sortedResults[0].matchRate).toBeGreaterThan(0)
  })
})
