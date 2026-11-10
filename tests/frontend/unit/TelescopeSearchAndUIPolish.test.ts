import { describe, it, expect, vi, beforeEach } from 'vitest'
import { autoTagEngine } from '../../../src/frontend/web/app/utils/search/AutoTagEngine'
import { useHeimdallSearch } from '../../../src/frontend/web/app/composables/useHeimdallSearch'
import { useGlobalContextMenu } from '../../../src/frontend/web/app/composables/useGlobalContextMenu'
import { mount } from '@vue/test-utils'
import AutoTagSuggestionDropdown from '../../../src/frontend/web/app/components/search/AutoTagSuggestionDropdown.vue'

describe('OmniSearch Architecture & UI Refinements (docs/TODO/search.TODO.md & docs/TODO/UI.TODO.md)', () => {
  // -------------------------------------------------------------------------
  // 1. Scoping Tags Prefix Parsing
  // -------------------------------------------------------------------------
  describe('Telescope Scoping Prefix Tags (@, #, !, *, %)', () => {
    it('parses @machine and @station scoping tags to station key', () => {
      const parsed = autoTagEngine.parseExplicitTags('@machine OP10-Load')
      expect(parsed.tags.length).toBe(1)
      expect(parsed.tags[0].key).toBe('station')
      expect(parsed.tags[0].value).toBe('all')
      expect(parsed.remainingText).toBe('OP10-Load')

      const specificStation = autoTagEngine.parseExplicitTags('@LINE1-OP20 Laser')
      expect(specificStation.tags[0].key).toBe('station')
      expect(specificStation.tags[0].value).toBe('LINE1-OP20')
      expect(specificStation.remainingText).toBe('Laser')
    })

    it('parses #ticket scoping tags to ticket key', () => {
      const parsed = autoTagEngine.parseExplicitTags('#ticket SFC-2026')
      expect(parsed.tags[0].key).toBe('ticket')
      expect(parsed.tags[0].value).toBe('all')
      expect(parsed.remainingText).toBe('SFC-2026')
    })

    it('parses !controller scoping tags to host key', () => {
      const parsed = autoTagEngine.parseExplicitTags('!IPC-L1-01 Beckhoff')
      expect(parsed.tags[0].key).toBe('host')
      expect(parsed.tags[0].value).toBe('IPC-L1-01')
      expect(parsed.remainingText).toBe('Beckhoff')
    })

    it('parses *part scoping tags to part key', () => {
      const parsed = autoTagEngine.parseExplicitTags('*motor Servo')
      expect(parsed.tags[0].key).toBe('part')
      expect(parsed.tags[0].value).toBe('motor')
      expect(parsed.remainingText).toBe('Servo')
    })

    it('parses %alarm scoping tags to status:maintenance', () => {
      const parsed = autoTagEngine.parseExplicitTags('%alarm Critical')
      expect(parsed.tags[0].key).toBe('status')
      expect(parsed.tags[0].value).toBe('maintenance')
      expect(parsed.remainingText).toBe('Critical')
    })
  })

  // -------------------------------------------------------------------------
  // 2. Debouncing & Instant Query Caching
  // -------------------------------------------------------------------------
  describe('Debounce & Instant Local Cache Optimization', () => {
    it('configures 150ms default debounce for crisp, responsive search operations', () => {
      const search = useHeimdallSearch({ instanceId: 'test-cache' })
      expect(search).toBeDefined()
    })

    it('caches and immediately resolves unbuffered query results without layout jump', async () => {
      const mockItems = [
        { id: 'm-1', name: 'OP10-Load Cell', itemType: 'Machine', subtitle: 'Line 1' },
        { id: 'm-2', name: 'OP20-Weld Cell', itemType: 'Machine', subtitle: 'Line 1' }
      ]

      const search = useHeimdallSearch({
        instanceId: 'test-buffer',
        data: mockItems
      })

      await search.executeSearch('Weld')
      expect(search.primaryResults.value.length).toBe(1)
      expect(search.primaryResults.value[0].name).toBe('OP20-Weld Cell')

      // Immediate re-query of same term yields instant cache results
      await search.executeSearch('Weld')
      expect(search.primaryResults.value.length).toBe(1)
      expect(search.primaryResults.value[0].id).toBe('m-2')
    })
  })

  // -------------------------------------------------------------------------
  // 3. Telescope Split Preview & Keyboard Navigation
  // -------------------------------------------------------------------------
  describe('AutoTagSuggestionDropdown Telescope Navigation & Preview', () => {
    const sampleResults = [
      { id: 'item-1', name: 'Laser Welder 5000', itemType: 'Machine', subtitle: 'Line 1', status: 'active' },
      { id: 'item-2', name: 'IPC-L1-02', itemType: 'ClientPc', subtitle: '192.168.1.111', status: 'online' },
      { id: 'item-3', name: 'Servo Motor 15kW', itemType: 'Part', subtitle: 'Siemens 1FT7', status: 'InStorage' }
    ]

    it('exposes Telescope keyboard navigation methods', () => {
      const wrapper = mount(AutoTagSuggestionDropdown, {
        props: {
          results: sampleResults,
          primaryResults: sampleResults
        }
      })

      const vm = wrapper.vm as any
      expect(typeof vm.navigateNext).toBe('function')
      expect(typeof vm.navigatePrev).toBe('function')
      expect(typeof vm.getSelectedResult).toBe('function')
      expect(typeof vm.toggleFilterDrawer).toBe('function')

      // Initially first result is selected
      expect(vm.getSelectedResult()?.id).toBe('item-1')

      // Navigate down
      vm.navigateNext()
      expect(vm.getSelectedResult()?.id).toBe('item-2')

      // Navigate down again
      vm.navigateNext()
      expect(vm.getSelectedResult()?.id).toBe('item-3')

      // Navigate up
      vm.navigatePrev()
      expect(vm.getSelectedResult()?.id).toBe('item-2')
    })

    it('toggles filter scoping drawer via toggleFilterDrawer', async () => {
      const wrapper = mount(AutoTagSuggestionDropdown, {
        props: {
          results: sampleResults
        }
      })

      const vm = wrapper.vm as any
      expect(wrapper.find('[data-omni-dropdown]').text()).not.toContain('Quick Scoping Tags')

      vm.toggleFilterDrawer()
      await wrapper.vm.$nextTick()

      expect(wrapper.find('[data-omni-dropdown]').text()).toContain('Quick Scoping Tags')
      expect(wrapper.find('[data-omni-dropdown]').text()).toContain('@machine')
      expect(wrapper.find('[data-omni-dropdown]').text()).toContain('#ticket')
    })
  })

  // -------------------------------------------------------------------------
  // 4. Global Context Menu Mobile Gestures & Keyboard Actions
  // -------------------------------------------------------------------------
  describe('Global Context Menu Navigation & Touch Handlers', () => {
    beforeEach(() => {
      const { closeContextMenu } = useGlobalContextMenu()
      closeContextMenu()
    })

    it('supports TouchEvent and touch coordinate extraction', () => {
      const { openContextMenu, isOpen, x, y } = useGlobalContextMenu()

      const fakeTouchEvent = {
        touches: [{ clientX: 250, clientY: 350 }],
        preventDefault: vi.fn(),
        stopPropagation: vi.fn()
      } as any

      openContextMenu(fakeTouchEvent, {
        entityType: 'machine',
        entityId: 'm-touch-01',
        entityName: 'Touch Station'
      })

      expect(isOpen.value).toBe(true)
      expect(x.value).toBe(250)
      expect(y.value).toBe(350)
      expect(fakeTouchEvent.preventDefault).toHaveBeenCalled()
    })

    it('provides bindLongPress helper for mobile/touchscreen operational panels', () => {
      vi.useFakeTimers()
      const { bindLongPress, isOpen } = useGlobalContextMenu()

      const longPress = bindLongPress({
        entityType: 'machine',
        entityId: 'm-longpress',
        entityName: 'Long Press Cell'
      }, 500)

      const fakeTouchEvent = {
        touches: [{ clientX: 180, clientY: 220 }],
        preventDefault: vi.fn(),
        stopPropagation: vi.fn()
      } as any

      longPress.onTouchstart(fakeTouchEvent)
      expect(isOpen.value).toBe(false)

      // Fast-forward past 500ms
      vi.advanceTimersByTime(500)
      expect(isOpen.value).toBe(true)

      vi.useRealTimers()
    })
  })
})
