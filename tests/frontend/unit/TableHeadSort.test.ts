import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TableHead from '~/components/ui/table/TableHead.vue'

describe('TableHead.vue - 3-State Sortable Header', () => {
  it('renders default non-sortable header without sort icons or sorting emissions', async () => {
    const wrapper = mount(TableHead, {
      slots: {
        default: 'Controller Hostname'
      }
    })

    expect(wrapper.text()).toContain('Controller Hostname')
    expect(wrapper.classes()).not.toContain('cursor-pointer')
    expect(wrapper.attributes('aria-sort')).toBeUndefined()

    // Clicking non-sortable header emits native click, but not sort events
    await wrapper.trigger('click')
    expect(wrapper.emitted('click')).toHaveLength(1)
    expect(wrapper.emitted('sort')).toBeUndefined()
    expect(wrapper.emitted('update:sortDirection')).toBeUndefined()
  })

  it('cycles through 3 states on click: asc -> desc -> null (undo sorting on 3rd click)', async () => {
    const wrapper = mount(TableHead, {
      props: {
        sortable: true
      },
      slots: {
        default: 'Host Node'
      }
    })

    expect(wrapper.classes()).toContain('cursor-pointer')
    expect(wrapper.attributes('aria-sort')).toBe('none')

    // Click 1: Null -> Ascending
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[0]).toEqual(['asc'])
    expect(wrapper.emitted('update:sortDirection')?.[0]).toEqual(['asc'])
    expect(wrapper.attributes('aria-sort')).toBe('ascending')

    // Click 2: Ascending -> Descending
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[1]).toEqual(['desc'])
    expect(wrapper.emitted('update:sortDirection')?.[1]).toEqual(['desc'])
    expect(wrapper.attributes('aria-sort')).toBe('descending')

    // Click 3: Descending -> Null (Undo Sorting!)
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[2]).toEqual([null])
    expect(wrapper.emitted('update:sortDirection')?.[2]).toEqual([null])
    expect(wrapper.attributes('aria-sort')).toBe('none')

    // Click 4: Restarts cycle -> Ascending
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[3]).toEqual(['asc'])
    expect(wrapper.emitted('update:sortDirection')?.[3]).toEqual(['asc'])
    expect(wrapper.attributes('aria-sort')).toBe('ascending')
  })

  it('respects controlled sortDirection prop updates', async () => {
    const wrapper = mount(TableHead, {
      props: {
        sortable: true,
        sortDirection: 'asc'
      },
      slots: {
        default: 'Network Identity'
      }
    })

    expect(wrapper.attributes('aria-sort')).toBe('ascending')

    // When clicked in 'asc' state, next is 'desc'
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[0]).toEqual(['desc'])

    // Update prop to 'desc'
    await wrapper.setProps({ sortDirection: 'desc' })
    expect(wrapper.attributes('aria-sort')).toBe('descending')

    // When clicked in 'desc' state, next is null (undo)
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[1]).toEqual([null])

    // Update prop to null
    await wrapper.setProps({ sortDirection: null })
    expect(wrapper.attributes('aria-sort')).toBe('none')

    // When clicked in null state, next is 'asc'
    await wrapper.trigger('click')
    expect(wrapper.emitted('sort')?.[2]).toEqual(['asc'])
  })
})
