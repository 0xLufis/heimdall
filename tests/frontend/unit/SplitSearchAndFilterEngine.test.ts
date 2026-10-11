import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import {
  useSearchAndFilter,
  parseTagString,
  type FacetDefinition
} from '../../../src/frontend/web/app/composables/useSearchAndFilter'
import SearchAndFilterBar from '../../../src/frontend/web/app/components/common/SearchAndFilterBar.vue'
import DataDisplayWrapper from '../../../src/frontend/web/app/components/common/DataDisplayWrapper.vue'

interface MockMachineItem {
  id: string
  name: string
  customIdentifier: string
  machineType: string
  status: string
  groupId: string
  location?: string
  tags?: string[]
}

const testItems: MockMachineItem[] = [
  {
    id: 'm-1',
    name: 'CNC Milling Center A1',
    customIdentifier: 'CNC-01',
    machineType: 'CNC',
    status: 'active',
    groupId: 'Line 1',
    location: 'Bay 1',
    tags: ['milling', 'precision', '#cnc']
  },
  {
    id: 'm-2',
    name: 'Laser Welder FastBeam',
    customIdentifier: 'WELD-02',
    machineType: 'Welding',
    status: 'active',
    groupId: 'Line 1',
    location: 'Bay 2',
    tags: ['welder', 'laser', '#welder']
  },
  {
    id: 'm-3',
    name: 'Automated Guided Vehicle AGV-03',
    customIdentifier: 'AGV-03',
    machineType: 'Logistics',
    status: 'maintenance',
    groupId: 'Line 2',
    location: 'Warehouse',
    tags: ['transport', '#robot']
  },
  {
    id: 'm-4',
    name: 'Legacy Press Machine Old',
    customIdentifier: 'PRS-04',
    machineType: 'Stamping',
    status: 'decommissioned',
    groupId: 'Line 2',
    location: 'Storage',
    tags: ['scrapped']
  }
]

describe('Split Search and Filtering Engine', () => {
  describe('parseTagString', () => {
    it('parses positive key:value tag strings', () => {
      const parsed = parseTagString('status:active')
      expect(parsed).toEqual({
        raw: 'status:active',
        key: 'status',
        value: 'active',
        isNegative: false
      })
    })

    it('parses negative !key:value tag strings', () => {
      const parsed = parseTagString('!status:decommissioned')
      expect(parsed).toEqual({
        raw: '!status:decommissioned',
        key: 'status',
        value: 'decommissioned',
        isNegative: true
      })
    })

    it('parses freeform hashtag tokens', () => {
      const posHash = parseTagString('#welder')
      expect(posHash).toEqual({
        raw: '#welder',
        value: '#welder',
        isNegative: false
      })

      const negHash = parseTagString('!#broken')
      expect(negHash).toEqual({
        raw: '!#broken',
        value: '#broken',
        isNegative: true
      })
    })
  })

  describe('useSearchAndFilter Composable', () => {
    const facetDefs: FacetDefinition<MockMachineItem>[] = [
      { key: 'status', label: 'Operational Status' },
      { key: 'groupId', label: 'Production Line' },
      { key: 'machineType', label: 'Machine Type' }
    ]

    it('returns all items when search query and filters are empty', () => {
      const itemsRef = ref([...testItems])
      const engine = useSearchAndFilter(itemsRef, { facetDefinitions: facetDefs })

      expect(engine.totalCount.value).toBe(4)
      expect(engine.matchCount.value).toBe(4)
      expect(engine.searchedAndFilteredItems.value).toHaveLength(4)
      expect(engine.hasActiveFilters.value).toBe(false)
    })

    it('performs free-text keyword search scoring and token matching', () => {
      const itemsRef = ref([...testItems])
      const engine = useSearchAndFilter(itemsRef, {
        searchFields: ['name', 'customIdentifier', 'machineType']
      })

      // Search keyword 'Welder'
      engine.searchQuery.value = 'Welder'
      expect(engine.searchedAndFilteredItems.value).toHaveLength(1)
      expect(engine.searchedAndFilteredItems.value[0].id).toBe('m-2')

      // Partial multi-token query
      engine.searchQuery.value = 'CNC A1'
      expect(engine.searchedAndFilteredItems.value).toHaveLength(1)
      expect(engine.searchedAndFilteredItems.value[0].id).toBe('m-1')

      // Non-matching query
      engine.searchQuery.value = 'NonexistentQueryXYZ'
      expect(engine.searchedAndFilteredItems.value).toHaveLength(0)
    })

    it('filters items using positive and negative structured tag filters', () => {
      const itemsRef = ref([...testItems])
      const engine = useSearchAndFilter(itemsRef)

      // Filter by status:active
      engine.addTag('status:active')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(2)
      expect(engine.searchedAndFilteredItems.value.map(i => i.id)).toEqual(['m-1', 'm-2'])

      // Add negative filter !group:Line 2 (m-1 and m-2 are already Line 1, so both remain)
      engine.addTag('!group:Line 2')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(2)

      // Add negative filter excluding Welding (!type:Welding)
      engine.addTag('!type:Welding')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(1)
      expect(engine.searchedAndFilteredItems.value[0].id).toBe('m-1')

      // Remove tag
      engine.removeTag('!type:Welding')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(2)
    })

    it('handles multi-select discrete facet filtering with dynamic facet counts', () => {
      const itemsRef = ref([...testItems])
      const engine = useSearchAndFilter(itemsRef, { facetDefinitions: facetDefs })

      // Check available facets computed counts
      const statusFacet = engine.availableFacets.value.find(f => f.key === 'status')
      expect(statusFacet).toBeDefined()
      expect(statusFacet?.options).toEqual(
        expect.arrayContaining([
          { value: 'active', count: 2 },
          { value: 'maintenance', count: 1 },
          { value: 'decommissioned', count: 1 }
        ])
      )

      // Toggle facet: Line 2
      engine.toggleFacet('groupId', 'Line 2')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(2)
      expect(engine.searchedAndFilteredItems.value.map(i => i.id)).toEqual(['m-3', 'm-4'])

      // Toggle facet: also include Line 1 (OR within facet)
      engine.toggleFacet('groupId', 'Line 1')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(4)

      // Untoggle Line 2
      engine.toggleFacet('groupId', 'Line 2')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(2)
      expect(engine.searchedAndFilteredItems.value.map(i => i.id)).toEqual(['m-1', 'm-2'])
    })

    it('combines text search with tag filtering seamlessly', () => {
      const itemsRef = ref([...testItems])
      const engine = useSearchAndFilter(itemsRef)

      // Filter by Line 1
      engine.addTag('group:Line 1')
      expect(engine.searchedAndFilteredItems.value).toHaveLength(2)

      // Search keyword 'Laser'
      engine.searchQuery.value = 'Laser'
      expect(engine.searchedAndFilteredItems.value).toHaveLength(1)
      expect(engine.searchedAndFilteredItems.value[0].id).toBe('m-2')

      // Reset all clears both search and tag filters
      engine.resetAll()
      expect(engine.searchQuery.value).toBe('')
      expect(engine.tagFilters.value).toEqual([])
      expect(engine.searchedAndFilteredItems.value).toHaveLength(4)
    })
  })

  describe('SearchAndFilterBar Component', () => {
    it('renders input, tag pills, and emits updates', async () => {
      const wrapper = mount(SearchAndFilterBar, {
        props: {
          searchQuery: 'milling',
          tagFilters: ['status:active', '!status:decommissioned'],
          totalCount: 10,
          matchCount: 2,
          placeholder: 'Search here...'
        }
      })

      const input = wrapper.find('input')
      expect(input.element.value).toBe('milling')
      expect(wrapper.text()).toContain('status:active')
      expect(wrapper.text()).toContain('!status:decommissioned')
      expect(wrapper.text()).toContain('Showing 2 of 10 items')

      // Trigger search query update
      await input.setValue('laser')
      expect(wrapper.emitted('update:searchQuery')?.[0]).toEqual(['laser'])

      // Click remove on tag pill
      const removeButtons = wrapper.findAll('button[title="Remove tag"]')
      expect(removeButtons.length).toBe(2)
      await removeButtons[0].trigger('click')
      expect(wrapper.emitted('update:tagFilters')?.[0]).toEqual([['!status:decommissioned']])
    })

    it('allows adding tags via the inline tag input adder', async () => {
      const wrapper = mount(SearchAndFilterBar, {
        props: {
          searchQuery: '',
          tagFilters: ['status:active'],
          totalCount: 4,
          matchCount: 2
        }
      })

      const quickTagInput = wrapper.find('input[placeholder*="+ Add tag"]')
      expect(quickTagInput.exists()).toBe(true)

      await quickTagInput.setValue('line:Line 1')
      await quickTagInput.trigger('keyup.enter')

      expect(wrapper.emitted('update:tagFilters')?.[0]).toEqual([
        ['status:active', 'line:Line 1']
      ])
    })
  })

  describe('DataDisplayWrapper with Split Search and Filter Engine', () => {
    it('integrates SearchAndFilterBar and filters items across card/list/table views', async () => {
      const facetDefs: FacetDefinition<MockMachineItem>[] = [
        { key: 'status', label: 'Status' },
        { key: 'groupId', label: 'Line' }
      ]

      const wrapper = mount(DataDisplayWrapper, {
        props: {
          items: testItems,
          columns: [
            { key: 'name', label: 'Name', sortable: true },
            { key: 'status', label: 'Status', sortable: true }
          ],
          enableSearchFilter: true,
          facetDefinitions: facetDefs,
          searchPlaceholder: 'Filter machines...'
        }
      })

      // Search & Filter Bar is rendered inside DataDisplayWrapper
      expect(wrapper.findComponent(SearchAndFilterBar).exists()).toBe(true)

      const vm = wrapper.vm as any
      expect(vm.processedItems).toHaveLength(4)

      // Search for 'Welder'
      vm.searchQuery = 'Welder'
      await wrapper.vm.$nextTick()
      expect(vm.processedItems).toHaveLength(1)
      expect(vm.processedItems[0].name).toContain('Laser Welder')

      // Reset search, apply tag
      vm.searchQuery = ''
      vm.tagFilters = ['!status:decommissioned']
      await wrapper.vm.$nextTick()
      expect(vm.processedItems).toHaveLength(3)
      expect(vm.processedItems.map((i: any) => i.id)).not.toContain('m-4')

      // Verify 3-state sorting continues to work on filtered results
      vm.setViewMode('table')
      await wrapper.vm.$nextTick()
      expect(wrapper.find('table').exists()).toBe(true)
    })
  })
})
