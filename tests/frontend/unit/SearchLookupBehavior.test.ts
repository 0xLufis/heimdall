import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import SearchableTargetCombobox from '../../../frontend/web/app/components/common/SearchableTargetCombobox.vue'
import MachineSearchCombobox from '../../../frontend/web/app/components/tickets/MachineSearchCombobox.vue'
import OmniSearchBar from '../../../frontend/web/app/components/search/OmniSearchBar.vue'

describe('Search Functions - Lookup Behavior & Premature Save Prevention', () => {
  describe('SearchableTargetCombobox', () => {
    it('does NOT emit update:modelValue on the first character typed', async () => {
      const queryFn = vi.fn().mockResolvedValue([
        { id: '1', label: 'Siemens PLC' },
        { id: '2', label: 'Beckhoff IPC' }
      ])

      const wrapper = mount(SearchableTargetCombobox, {
        props: {
          modelValue: '',
          queryFn
        }
      })

      const input = wrapper.find('input')
      await input.setValue('S')

      // queryFn should be called to look up results
      expect(queryFn).toHaveBeenCalledWith('S')

      // Must NOT have emitted update:modelValue on the 1st character!
      expect(wrapper.emitted('update:modelValue')).toBeUndefined()

      // The input should display the typed search query
      expect((input.element as HTMLInputElement).value).toBe('S')

      // Clicking an item from the looked-up results should then emit update:modelValue
      const item = wrapper.find('li')
      expect(item.exists()).toBe(true)
      await item.trigger('click')

      expect(wrapper.emitted('update:modelValue')).toBeDefined()
      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['Siemens PLC'])
    })

    it('does not mutate modelValue if user clicks outside without selecting', async () => {
      const wrapper = mount(SearchableTargetCombobox, {
        props: {
          modelValue: 'Original Machine',
          options: [
            { id: '1', label: 'Original Machine' },
            { id: '2', label: 'Other Machine' }
          ]
        }
      })

      const input = wrapper.find('input')
      await input.setValue('Oth')

      // modelValue should NOT be emitted
      expect(wrapper.emitted('update:modelValue')).toBeUndefined()

      // Simulate click outside
      document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }))
      await wrapper.vm.$nextTick()

      // Still no update:modelValue emitted
      expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    })
  })

  describe('MachineSearchCombobox', () => {
    it('does NOT emit update:modelValue or update:stationName when user types first character', async () => {
      const wrapper = mount(MachineSearchCombobox, {
        props: {
          modelValue: '',
          stationName: ''
        }
      })

      const input = wrapper.find('input')
      await input.setValue('L')

      // Must NOT emit update:modelValue or update:stationName on typing 1 character
      expect(wrapper.emitted('update:modelValue')).toBeUndefined()
      expect(wrapper.emitted('update:stationName')).toBeUndefined()

      // Suggestions should appear and be filtered
      const list = wrapper.findAll('li')
      expect(list.length).toBeGreaterThan(0)

      // Select first item
      await list[0].trigger('click')
      expect(wrapper.emitted('update:modelValue')).toBeDefined()
      expect(wrapper.emitted('update:stationName')).toBeDefined()
    })
  })

  describe('OmniSearchBar', () => {
    it('does not emit search event on a single character query without tags', async () => {
      const wrapper = mount(OmniSearchBar, {
        props: {
          config: {
            minCharsForSuggestions: 2,
            debounceMs: 50
          }
        }
      })

      const input = wrapper.find('input')
      await input.setValue('a')

      // Wait for debounce period
      await new Promise(resolve => setTimeout(resolve, 80))

      // Should NOT emit search on 1st character!
      expect(wrapper.emitted('search')).toBeUndefined()

      // Now type a 2nd character
      await input.setValue('ab')
      await new Promise(resolve => setTimeout(resolve, 80))

      // Should now emit search
      expect(wrapper.emitted('search')).toBeDefined()
      expect(wrapper.emitted('search')?.[0]).toEqual(['ab'])
    })

    it('executes search and closes the search menu on Enter when there is no pill to complete', async () => {
      const wrapper = mount(OmniSearchBar, {
        props: {
          config: {
            minCharsForSuggestions: 2,
            debounceMs: 50
          }
        }
      })

      const input = wrapper.find('input')
      await input.trigger('focus')
      await input.setValue('nonexistentquery123')

      // Since 'nonexistentquery123' has no matching pill/auto-suggestion,
      // hitting Enter should execute search, emit search, and close the dropdown menu
      await input.trigger('keydown', { key: 'Enter' })

      expect(wrapper.emitted('search')).toBeDefined()
      const searchEmits = wrapper.emitted('search')
      expect(searchEmits?.[searchEmits.length - 1]).toEqual(['nonexistentquery123'])

      // Dropdown menu must be closed!
      const dropdown = wrapper.findComponent({ name: 'AutoTagSuggestionDropdown' })
      expect(dropdown.exists()).toBe(false)
    })

    it('completes the pill on Enter when auto-suggestions exist', async () => {
      const wrapper = mount(OmniSearchBar, {
        props: {
          config: {
            minCharsForSuggestions: 2,
            debounceMs: 50
          }
        }
      })

      const input = wrapper.find('input')
      await input.trigger('focus')
      // "siemns" triggers fuzzy match for Siemens
      await input.setValue('siemns')

      // Wait a tick for autoTagEngine to populate autoSuggestions
      await wrapper.vm.$nextTick()

      // Press Enter to complete the pill
      await input.trigger('keydown', { key: 'Enter' })

      // Pill should be completed and rawInput cleared
      expect((input.element as HTMLInputElement).value).toBe('')
    })

    it('closes the search menu on Escape', async () => {
      const wrapper = mount(OmniSearchBar, {
        props: {
          config: {
            minCharsForSuggestions: 2,
            debounceMs: 50
          }
        }
      })

      const input = wrapper.find('input')
      await input.trigger('focus')
      await input.setValue('siemns')
      await wrapper.vm.$nextTick()

      // Hit Escape
      await input.trigger('keydown', { key: 'Escape' })
      await wrapper.vm.$nextTick()

      // Dropdown must be closed
      const dropdown = wrapper.findComponent({ name: 'AutoTagSuggestionDropdown' })
      expect(dropdown.exists()).toBe(false)
    })
  })
})
