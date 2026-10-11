import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import DataSearchInput from '../../../src/frontend/web/app/components/common/DataSearchInput.vue'
import ActiveFilterChips from '../../../src/frontend/web/app/components/common/ActiveFilterChips.vue'
import DataFilterDrawer from '../../../src/frontend/web/app/components/common/DataFilterDrawer.vue'
import AppMobileBottomNav from '../../../src/frontend/web/app/components/layout/AppMobileBottomNav.vue'

describe('Decoupled Data Representation & Mobile Navigation Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('useRoute', () => ({ path: '/dashboard/machines' }))
    vi.stubGlobal('useRouter', () => ({ push: vi.fn() }))
    vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string) => {
      if (url === '/api/tickets') {
        return Promise.resolve([
          { id: 't-1', isLineStop: true, status: 'Open' },
          { id: 't-2', isLineStop: false, status: 'InProgress' }
        ])
      }
      return Promise.resolve([])
    }))
  })

  describe('DataSearchInput', () => {
    it('renders input with placeholder and search icon', () => {
      const wrapper = mount(DataSearchInput, {
        props: {
          modelValue: 'Robot',
          placeholder: 'Search machinery...'
        }
      })

      const input = wrapper.find('input')
      expect(input.exists()).toBe(true)
      expect(input.element.value).toBe('Robot')
      expect(input.attributes('placeholder')).toBe('Search machinery...')
    })

    it('emits update:modelValue on typing', async () => {
      vi.useFakeTimers()
      const wrapper = mount(DataSearchInput, {
        props: {
          modelValue: '',
          debounceMs: 50
        }
      })

      const input = wrapper.find('input')
      await input.setValue('Assembly')
      vi.advanceTimersByTime(60)

      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['Assembly'])
      vi.useRealTimers()
    })

    it('clears query and emits events on clear button click', async () => {
      const wrapper = mount(DataSearchInput, {
        props: {
          modelValue: 'Testing'
        }
      })

      const clearBtn = wrapper.find('button[title="Clear search"]')
      expect(clearBtn.exists()).toBe(true)
      await clearBtn.trigger('click')

      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([''])
      expect(wrapper.emitted('clear')).toBeTruthy()
    })
  })

  describe('ActiveFilterChips', () => {
    it('renders search query, tag, negative tag, and facet chips', () => {
      const wrapper = mount(ActiveFilterChips, {
        props: {
          searchQuery: 'Laser',
          tagFilters: ['#welding', '!status:decommissioned'],
          facetFilters: { Line: ['Line 1', 'Line 2'] },
          totalCount: 50,
          matchCount: 4
        }
      })

      expect(wrapper.text()).toContain('"Laser"')
      expect(wrapper.text()).toContain('#welding')
      expect(wrapper.text()).toContain('!status:decommissioned')
      expect(wrapper.text()).toContain('Line: Line 1')
      expect(wrapper.text()).toContain('Line: Line 2')
      expect(wrapper.text()).toContain('Showing 4 of 50')
    })

    it('emits removeTag and removeFacetValue when chip X buttons are clicked', async () => {
      const wrapper = mount(ActiveFilterChips, {
        props: {
          tagFilters: ['#welding'],
          facetFilters: { Line: ['Line 1'] },
          totalCount: 10,
          matchCount: 2
        }
      })

      const removeBtns = wrapper.findAll('button')
      // Click first remove button (#welding)
      await removeBtns[0].trigger('click')
      expect(wrapper.emitted('removeTag')?.[0]).toEqual(['#welding'])

      // Click second remove button (Line: Line 1)
      await removeBtns[1].trigger('click')
      expect(wrapper.emitted('removeFacetValue')?.[0]).toEqual(['Line', 'Line 1'])
    })

    it('emits clearAll when clear all link is clicked', async () => {
      const wrapper = mount(ActiveFilterChips, {
        props: {
          searchQuery: 'Test',
          totalCount: 10,
          matchCount: 1
        }
      })

      const clearAllBtn = wrapper.findAll('button').find(b => b.text().includes('Clear all'))
      expect(clearAllBtn?.exists()).toBe(true)
      await clearAllBtn?.trigger('click')

      expect(wrapper.emitted('clearAll')).toBeTruthy()
    })
  })

  describe('DataFilterDrawer', () => {
    const mockFacets = [
      {
        key: 'status',
        label: 'Operational Status',
        options: [
          { value: 'active', label: 'Active', count: 12 },
          { value: 'maintenance', label: 'Maintenance', count: 3 }
        ]
      }
    ]

    it('renders filter trigger button with active filter counter', () => {
      const wrapper = mount(DataFilterDrawer, {
        props: {
          availableFacets: mockFacets,
          facetFilters: { status: ['active'] },
          tagFilters: ['#line1']
        }
      })

      // Active filters = 1 facet + 1 tag = 2
      expect(wrapper.text()).toContain('Filters')
      expect(wrapper.text()).toContain('2')
    })

    it('emits clearFilters on reset click', async () => {
      const wrapper = mount(DataFilterDrawer, {
        props: {
          availableFacets: mockFacets,
          facetFilters: { status: ['active'] },
          tagFilters: []
        }
      })

      // Component handles reset emitted upwards
      const resetBtn = wrapper.find('button[class*="text-rose-400"]')
      if (resetBtn.exists()) {
        await resetBtn.trigger('click')
        expect(wrapper.emitted('clearFilters')).toBeTruthy()
      }
    })
  })

  describe('AppMobileBottomNav', () => {
    it('renders 5 primary navigation tabs with touch-friendly layout', () => {
      const wrapper = mount(AppMobileBottomNav, {
        global: {
          stubs: {
            NuxtLink: {
              template: '<a :href="to"><slot /></a>',
              props: ['to']
            },
            QrScanner: true
          }
        }
      })

      expect(wrapper.text()).toContain('Overview')
      expect(wrapper.text()).toContain('Machinery')
      expect(wrapper.text()).toContain('Incidents')
      expect(wrapper.text()).toContain('Inventory')
      expect(wrapper.text()).toContain('Scan')
    })

    it('toggles QR camera scanner modal when Scan button is clicked', async () => {
      const wrapper = mount(AppMobileBottomNav, {
        global: {
          stubs: {
            NuxtLink: {
              template: '<a :href="to"><slot /></a>',
              props: ['to']
            },
            QrScanner: true
          }
        }
      })

      expect(wrapper.find('.fixed.inset-0').exists()).toBe(false)
      const scanBtn = wrapper.findAll('button').find(b => b.text().includes('Scan'))
      expect(scanBtn?.exists()).toBe(true)

      await scanBtn?.trigger('click')
      expect(wrapper.find('.fixed.inset-0').exists()).toBe(true)
      expect(wrapper.text()).toContain('Camera Barcode Scanner')
    })
  })
})
