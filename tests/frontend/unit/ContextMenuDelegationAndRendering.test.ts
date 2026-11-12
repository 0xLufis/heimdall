import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createApp, defineComponent, h, nextTick, ref, withDirectives } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createRouter, createMemoryHistory } from 'vue-router'
import GlobalContextMenu from '../../../src/frontend/web/app/components/common/GlobalContextMenu.vue'
import {
  useGlobalContextMenu,
  handleDelegatedContextMenu,
  installContextMenuDelegation,
  isTextEntryTarget,
  LONG_PRESS_MS
} from '../../../src/frontend/web/app/composables/useGlobalContextMenu'
import {
  vCtx,
  bindContextData,
  getContextData,
  findContextTarget,
  CTX_ATTRIBUTE
} from '../../../src/frontend/web/app/utils/contextMenuDirective'

const menu = useGlobalContextMenu()

const rightClick = (el: Element, init: MouseEventInit = {}) => {
  const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 120, clientY: 140, button: 2, ...init })
  el.dispatchEvent(event)
  return event
}

const touch = (el: Element, type: string, point?: { x: number; y: number }, count = 1) => {
  const event = new Event(type, { bubbles: true, cancelable: true })
  const touches = point ? Array.from({ length: count }, () => ({ clientX: point.x, clientY: point.y })) : []
  Object.defineProperty(event, 'touches', { value: touches })
  el.dispatchEvent(event)
  return event
}

const menuEl = () => document.querySelector<HTMLElement>('[data-context-menu]')
const menuText = () => menuEl()?.textContent || ''
const item = (key: string) => document.querySelector<HTMLElement>(`[data-context-action="${key}"]`)
const utility = (key: string) => document.querySelector<HTMLElement>(`[data-context-item="utility:${key}"]`)

const Host = defineComponent({
  props: { ticketTitle: { type: String, default: 'Lens dirty' }, bound: { type: Boolean, default: true } },
  emits: ['explicit'],
  setup(_props, { emit }) {
    const { openContextMenu } = useGlobalContextMenu()
    const onExplicit = (e: MouseEvent) => {
      emit('explicit')
      openContextMenu(e, { entityType: 'map-node', entityName: 'CAD block', handle: 'OP20_WELD' })
    }
    return { onExplicit }
  },
  template: `
    <div>
      <div id="unbound">plain area</div>
      <div id="scroller" style="overflow:auto">
        <div id="ticket" v-ctx="bound ? { entityType: 'ticket', entityId: 'tkt-1', entityName: ticketTitle, entityCode: 'TKT-0001' } : null">
          <span id="ticket-text">{{ ticketTitle }}</span>
          <input id="ticket-input" type="text" />
          <input id="ticket-check" type="checkbox" />
        </div>
      </div>
      <div id="other-scroller" style="overflow:auto"><p>log</p></div>
      <div id="machine" v-ctx="{ entityType: 'machine', entityId: 'm-1', entityName: 'OP10', handle: 'L01-OP10' }">
        <span id="machine-text">OP10</span>
        <button id="controller" v-ctx="{ entityType: 'controller', entityId: 'c-1', entityName: 'IPC-1' }">IPC-1</button>
      </div>
      <div id="explicit" @contextmenu="onExplicit">
        <span id="explicit-text">map block</span>
        <span id="explicit-bound" v-ctx="{ entityType: 'user', entityId: 'u-1', entityName: 'Ada' }">Ada</span>
      </div>
      <div id="search" v-ctx:search="{ id: 'c-9', name: 'IPC-9', itemType: 'endpoint' }">result</div>
    </div>
  `
})

const mountApp = (options: { router?: ReturnType<typeof createRouter> } = {}) => {
  const plugins = options.router ? [options.router] : []
  const host = mount(Host, { attachTo: document.body, global: { plugins } })
  const wrapper = mount(GlobalContextMenu, { attachTo: document.body, global: { plugins } })
  return { host, wrapper }
}

const el = (id: string) => document.getElementById(id)!

beforeEach(() => {
  menu.closeContextMenu()
  menu.isNativeMenuBypassed.value = false
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('v-ctx directive', () => {
  it('stores the data on the element and only emits the entity type as an attribute', () => {
    mountApp()
    const ticket = el('ticket')
    expect(ticket.getAttribute(CTX_ATTRIBUTE)).toBe('ticket')
    expect(getContextData(ticket)).toMatchObject({ entityType: 'ticket', entityId: 'tkt-1', entityCode: 'TKT-0001' })
    // Nothing but the type leaks into the DOM
    expect(ticket.outerHTML).not.toContain('TKT-0001')
  })

  it('follows binding updates and unbinds on a falsy value', async () => {
    const { host } = mountApp()
    await host.setProps({ ticketTitle: 'Conveyor jam' })
    expect(getContextData(el('ticket'))!.entityName).toBe('Conveyor jam')

    await host.setProps({ bound: false })
    expect(el('ticket').hasAttribute(CTX_ATTRIBUTE)).toBe(false)
    expect(getContextData(el('ticket'))).toBeNull()
    expect(findContextTarget(el('ticket-text'))).toBeNull()

    await host.setProps({ bound: true })
    expect(el('ticket').getAttribute(CTX_ATTRIBUTE)).toBe('ticket')
  })

  it('converts a search result with the :search argument', () => {
    mountApp()
    expect(el('search').getAttribute(CTX_ATTRIBUTE)).toBe('controller')
    expect(getContextData(el('search'))).toMatchObject({ entityType: 'controller', controllerId: 'c-9', controllerHostname: 'IPC-9' })
  })

  it('resolves the innermost bound element from any descendant, including text nodes', () => {
    mountApp()
    expect(findContextTarget(el('machine-text'))!.data.entityId).toBe('m-1')
    expect(findContextTarget(el('controller'))!.data.entityId).toBe('c-1')
    expect(findContextTarget(el('controller').firstChild)!.data.entityId).toBe('c-1')
    expect(findContextTarget(el('unbound'))).toBeNull()
    expect(findContextTarget(null)).toBeNull()
    expect(findContextTarget(document)).toBeNull()
  })

  it('skips an attribute that carries no data (e.g. markup that was never hydrated)', () => {
    mountApp()
    const stray = document.createElement('div')
    stray.setAttribute(CTX_ATTRIBUTE, 'ticket')
    el('machine').appendChild(stray)
    expect(findContextTarget(stray)!.data.entityId).toBe('m-1')
    stray.remove()
  })

  it('applies to the root element of a component', () => {
    const Row = defineComponent({ template: '<tr class="row"><slot /></tr>' })
    const wrapper = mount(defineComponent({
      components: { Row },
      template: `<table><tbody><Row v-ctx="{ entityType: 'user', entityId: 'u-2', entityName: 'Bo' }"><td>Bo</td></Row></tbody></table>`
    }), { attachTo: document.body })
    const tr = wrapper.find('tr').element
    expect(tr.getAttribute(CTX_ATTRIBUTE)).toBe('user')
    expect(findContextTarget(wrapper.find('td').element)!.data.entityId).toBe('u-2')
  })

  it('forgets the data when the element is unmounted', () => {
    const { host } = mountApp()
    const ticket = el('ticket')
    host.unmount()
    expect(getContextData(ticket)).toBeNull()
  })

  it('can be bound imperatively', () => {
    const div = document.createElement('div')
    bindContextData(div, { entityType: 'inventory', entityId: 'p-1' })
    expect(div.getAttribute(CTX_ATTRIBUTE)).toBe('inventory')
    bindContextData(div, { entityId: 'x' })
    expect(div.getAttribute(CTX_ATTRIBUTE)).toBe('general')
    bindContextData(div, null)
    expect(div.hasAttribute(CTX_ATTRIBUTE)).toBe(false)
  })

  it('renders the attribute on the server, on elements and on component roots', async () => {
    const Row = defineComponent({ render() { return h('tr', { class: 'row' }, this.$slots.default?.()) } })
    const app = createApp({
      render: () => h('table', [
        withDirectives(h('caption', 'unbound'), [[vCtx, null]]),
        withDirectives(h('tr', [h('td', 'Lens dirty')]), [[vCtx, { entityType: 'ticket', entityId: 'tkt-1', entityCode: 'TKT-0001' }]]),
        withDirectives(h(Row, null, { default: () => [h('td', 'IPC-9')] }), [[vCtx, { id: 'c-9', name: 'IPC-9', itemType: 'endpoint' }, 'search']])
      ])
    })
    const html = await renderToString(app)
    expect(html).toContain('<tr data-ctx="ticket">')
    expect(html).toContain('data-ctx="controller"')
    expect(html).toContain('<caption>unbound</caption>')
    expect(html).not.toContain('TKT-0001')
  })
})

describe('delegated right-click', () => {
  it('opens the menu for the bound row and suppresses the browser menu', async () => {
    mountApp()
    const event = rightClick(el('ticket-text'), { clientX: 300, clientY: 220 })
    await nextTick()

    expect(event.defaultPrevented).toBe(true)
    expect(menu.isOpen.value).toBe(true)
    expect(menu.contextData.value).toMatchObject({ entityType: 'ticket', entityId: 'tkt-1' })
    expect(menu.anchorEl.value).toBe(el('ticket'))
    expect(menu.x.value).toBe(300)
    expect(menu.y.value).toBe(220)
    expect(menuText()).toContain('Lens dirty')
    expect(menuText()).toContain('TICKET')
  })

  it('the innermost bound element wins over its bound parent', async () => {
    mountApp()
    rightClick(el('controller'))
    expect(menu.contextData.value!.entityType).toBe('controller')
    rightClick(el('machine-text'))
    expect(menu.contextData.value!.entityType).toBe('machine')
  })

  it('a right-click somewhere unbound closes an open menu and keeps the browser menu', async () => {
    mountApp()
    rightClick(el('ticket'))
    expect(menu.isOpen.value).toBe(true)

    const event = rightClick(el('unbound'))
    await nextTick()
    expect(menu.isOpen.value).toBe(false)
    expect(event.defaultPrevented).toBe(false)
    expect(menuEl()).toBeNull()
  })

  it('a right-click on the menu itself keeps it open', async () => {
    mountApp()
    rightClick(el('ticket'))
    await nextTick()
    const event = rightClick(utility('copy')!)
    expect(menu.isOpen.value).toBe(true)
    expect(event.defaultPrevented).toBe(true)
    expect(menu.contextData.value!.entityId).toBe('tkt-1')
  })

  it('Shift+right-click leaves the browser menu alone', () => {
    mountApp()
    rightClick(el('machine'))
    const event = rightClick(el('ticket'), { shiftKey: true })
    expect(event.defaultPrevented).toBe(false)
    expect(menu.isOpen.value).toBe(false)
  })

  it('text fields inside a bound row keep the browser menu, other controls do not', () => {
    mountApp()
    const onInput = rightClick(el('ticket-input'))
    expect(onInput.defaultPrevented).toBe(false)
    expect(menu.isOpen.value).toBe(false)

    const onCheckbox = rightClick(el('ticket-check'))
    expect(onCheckbox.defaultPrevented).toBe(true)
    expect(menu.isOpen.value).toBe(true)

    expect(isTextEntryTarget(el('ticket-input'))).toBe(true)
    expect(isTextEntryTarget(el('ticket-check'))).toBe(false)
    expect(isTextEntryTarget(el('ticket-text'))).toBe(false)
    const editable = document.createElement('div')
    editable.setAttribute('contenteditable', 'true')
    expect(isTextEntryTarget(editable)).toBe(true)
    expect(isTextEntryTarget(document.createElement('textarea'))).toBe(true)
  })

  it('explicit @contextmenu handlers keep working next to the delegation', async () => {
    const { host } = mountApp()
    rightClick(el('ticket'))
    const event = rightClick(el('explicit-text'))
    await nextTick()
    expect(host.emitted('explicit')).toHaveLength(1)
    expect(event.defaultPrevented).toBe(true)
    expect(menu.contextData.value).toMatchObject({ entityType: 'map-node', handle: 'OP20_WELD' })
    expect(menu.anchorEl.value).toBe(el('explicit-text'))
    expect(menuText()).toContain('CAD NODE')
  })

  it('a bound element inside an explicitly handled container opens its own menu', () => {
    const { host } = mountApp()
    rightClick(el('explicit-bound'))
    expect(host.emitted('explicit')).toBeUndefined()
    expect(menu.contextData.value).toMatchObject({ entityType: 'user', entityId: 'u-1' })
  })

  describe('one-shot native menu unlock', () => {
    it('hands exactly the next right-click on a bound row to the browser', () => {
      mountApp()
      menu.unlockNativeMenu()
      const first = rightClick(el('ticket'))
      expect(first.defaultPrevented).toBe(false)
      expect(menu.isOpen.value).toBe(false)
      expect(menu.isNativeMenuBypassed.value).toBe(false)

      const second = rightClick(el('ticket'))
      expect(second.defaultPrevented).toBe(true)
      expect(menu.isOpen.value).toBe(true)
    })

    it('is spent on an unbound right-click too', () => {
      mountApp()
      menu.unlockNativeMenu()
      rightClick(el('unbound'))
      expect(menu.isNativeMenuBypassed.value).toBe(false)
      expect(rightClick(el('ticket')).defaultPrevented).toBe(true)
    })

    it('an explicit handler of the same event does not open the custom menu', () => {
      const { host } = mountApp()
      menu.unlockNativeMenu()
      const event = rightClick(el('explicit-text'))
      expect(host.emitted('explicit')).toHaveLength(1)
      expect(event.defaultPrevented).toBe(false)
      expect(menu.isOpen.value).toBe(false)
    })
  })

  it('handleDelegatedContextMenu works without the component (plain DOM)', () => {
    const row = document.createElement('div')
    document.body.appendChild(row)
    bindContextData(row, { entityType: 'inventory', entityId: 'p-1', entityName: 'Bearing' })
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 10, clientY: 20 })
    Object.defineProperty(event, 'target', { value: row })
    handleDelegatedContextMenu(event)
    expect(menu.isOpen.value).toBe(true)
    expect(menu.contextData.value!.entityId).toBe('p-1')
    row.remove()
  })

  it('installs one set of listeners per document and removes it with the last user', () => {
    const add = vi.spyOn(document, 'addEventListener')
    const remove = vi.spyOn(document, 'removeEventListener')
    const first = installContextMenuDelegation()
    const second = installContextMenuDelegation()
    expect(add.mock.calls.filter(c => c[0] === 'contextmenu')).toHaveLength(1)
    first()
    expect(remove.mock.calls.filter(c => c[0] === 'contextmenu')).toHaveLength(0)
    second()
    expect(remove.mock.calls.filter(c => c[0] === 'contextmenu')).toHaveLength(1)
    // Releasing twice is harmless
    second()
  })
})

describe('long press', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  it('a 500 ms hold on a bound row opens the menu and swallows the following tap', () => {
    mountApp()
    touch(el('ticket-text'), 'touchstart', { x: 80, y: 90 })
    vi.advanceTimersByTime(LONG_PRESS_MS - 1)
    expect(menu.isOpen.value).toBe(false)
    vi.advanceTimersByTime(1)
    expect(menu.isOpen.value).toBe(true)
    expect(menu.contextData.value!.entityId).toBe('tkt-1')
    expect(menu.x.value).toBe(80)
    expect(menu.y.value).toBe(90)

    const end = touch(el('ticket-text'), 'touchend')
    expect(end.defaultPrevented).toBe(true)
    expect(menu.isOpen.value).toBe(true)
  })

  it('a hold reaches explicit handlers as a right-click', () => {
    const { host } = mountApp()
    touch(el('explicit-text'), 'touchstart', { x: 40, y: 50 })
    vi.advanceTimersByTime(LONG_PRESS_MS)
    expect(host.emitted('explicit')).toHaveLength(1)
    expect(menu.contextData.value!.entityType).toBe('map-node')
  })

  it('a short tap, a drag or a second finger never opens it', () => {
    mountApp()
    touch(el('ticket'), 'touchstart', { x: 80, y: 90 })
    vi.advanceTimersByTime(200)
    const tapEnd = touch(el('ticket'), 'touchend')
    vi.advanceTimersByTime(LONG_PRESS_MS)
    expect(tapEnd.defaultPrevented).toBe(false)
    expect(menu.isOpen.value).toBe(false)

    touch(el('ticket'), 'touchstart', { x: 80, y: 90 })
    touch(el('ticket'), 'touchmove', { x: 80, y: 105 })
    vi.advanceTimersByTime(LONG_PRESS_MS)
    expect(menu.isOpen.value).toBe(false)

    touch(el('ticket'), 'touchstart', { x: 80, y: 90 })
    touch(el('ticket'), 'touchmove', { x: 84, y: 93 })
    touch(el('ticket'), 'touchstart', { x: 80, y: 90 }, 2)
    vi.advanceTimersByTime(LONG_PRESS_MS)
    expect(menu.isOpen.value).toBe(false)

    touch(el('ticket'), 'touchstart', { x: 80, y: 90 })
    touch(el('ticket'), 'touchcancel')
    vi.advanceTimersByTime(LONG_PRESS_MS)
    expect(menu.isOpen.value).toBe(false)
  })

  it('a hold on an unbound area or in a text field does nothing', () => {
    mountApp()
    touch(el('unbound'), 'touchstart', { x: 10, y: 10 })
    vi.advanceTimersByTime(LONG_PRESS_MS)
    const end = touch(el('unbound'), 'touchend')
    expect(menu.isOpen.value).toBe(false)
    expect(end.defaultPrevented).toBe(false)

    touch(el('ticket-input'), 'touchstart', { x: 10, y: 10 })
    vi.advanceTimersByTime(LONG_PRESS_MS)
    expect(menu.isOpen.value).toBe(false)
  })
})

describe('GlobalContextMenu rendering', () => {
  it('shows only the entries the entity has data for', async () => {
    mountApp()
    rightClick(el('ticket'))
    await nextTick()
    expect(menuText()).toContain('Open Ticket')
    expect(menuText()).toContain('Open in New Tab')
    for (const absent of ['Go to Machine', 'Go to Node / Controller', 'Decommission Machine', 'Inspect Component Tree', 'Go to Maintenance Tickets', 'Live Telemetry']) {
      expect(menuText()).not.toContain(absent)
    }
    for (const always of ['Copy Handle / ID', 'Export Entity Data (JSON)', 'Open Browser Context Menu']) {
      expect(menuText()).toContain(always)
    }

    rightClick(el('machine-text'))
    await nextTick()
    for (const present of ['Go to Machine', 'Show on Plant Map', 'Go to Maintenance Tickets', 'Create Incident Ticket', 'Live Telemetry & Diagnostics', 'Inspect Component Tree', 'Decommission Machine']) {
      expect(menuText()).toContain(present)
    }
    expect(menuText()).not.toContain('Go to Node / Controller')
    expect(menuText()).not.toContain('Open Ticket')
    expect(menuEl()!.querySelectorAll('[role="separator"]')).toHaveLength(2)
  })

  it('a general context shows the utilities only', async () => {
    mountApp()
    menu.openContextMenu({ clientX: 10, clientY: 10 }, { entityType: 'general', entityName: 'Master Production Hall', handle: 'production_hall' })
    await nextTick()
    expect(menuEl()!.querySelectorAll('[data-context-action]')).toHaveLength(0)
    expect(menuEl()!.querySelectorAll('[role="menuitem"]')).toHaveLength(3)
    expect(menuEl()!.querySelectorAll('[role="separator"]')).toHaveLength(0)
    expect(menuText()).toContain('ASSET')
  })

  it('shows the ticket count of the station', async () => {
    mountApp()
    menu.openContextMenu({ clientX: 10, clientY: 10 }, {
      entityType: 'machine', entityId: 'm-1', entityName: 'OP10',
      tickets: [{ id: 't1', title: 'a', status: 'Open' }, { id: 't2', title: 'b', status: 'Open' }, { id: 't3', title: 'c', status: 'Open' }]
    })
    await nextTick()
    expect(item('tickets')!.textContent).toContain('3')
  })

  describe('navigation', () => {
    const makeRouter = async (start = '/dashboard/tickets') => {
      const router = createRouter({
        history: createMemoryHistory(),
        routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }]
      })
      await router.push(start)
      await router.isReady()
      return router
    }

    it('pushes the route of the clicked entry and closes', async () => {
      const router = await makeRouter()
      mountApp({ router })
      rightClick(el('ticket'))
      await nextTick()
      item('open')!.click()
      await flushPromises()
      expect(router.currentRoute.value.fullPath).toBe('/dashboard/tickets/tkt-1')
      expect(menu.isOpen.value).toBe(false)
    })

    it('station entries carry the machine id, never another entity id', async () => {
      const router = await makeRouter('/dashboard/machines')
      mountApp({ router })
      rightClick(el('machine-text'))
      await nextTick()
      item('tickets')!.click()
      await flushPromises()
      expect(router.currentRoute.value.path).toBe('/dashboard/tickets')
      expect(router.currentRoute.value.query).toEqual({ stationId: 'm-1', handle: 'L01-OP10' })

      rightClick(el('machine-text'))
      await nextTick()
      item('decommission')!.click()
      await flushPromises()
      expect(router.currentRoute.value.path).toBe('/dashboard/machines')
      expect(router.currentRoute.value.query).toEqual({ decommission: 'm-1' })
    })

    it('opens the primary link in a new tab without navigating', async () => {
      const router = await makeRouter()
      const open = vi.fn()
      vi.stubGlobal('open', open)
      mountApp({ router })
      rightClick(el('ticket'))
      await nextTick()
      item('new-tab')!.click()
      await flushPromises()
      expect(open).toHaveBeenCalledWith('/dashboard/tickets/tkt-1', '_blank', 'noopener')
      expect(router.currentRoute.value.fullPath).toBe('/dashboard/tickets')
      expect(menu.isOpen.value).toBe(false)
    })

    it('does not offer the plant map while on the plant map', async () => {
      const router = await makeRouter('/dashboard/map')
      mountApp({ router })
      rightClick(el('machine-text'))
      await nextTick()
      expect(item('map')).toBeNull()
      expect(item('tickets')).not.toBeNull()
    })

    it('closes when the route changes underneath it', async () => {
      const router = await makeRouter()
      mountApp({ router })
      rightClick(el('ticket'))
      await nextTick()
      await router.push('/dashboard/overview')
      await flushPromises()
      expect(menu.isOpen.value).toBe(false)
    })
  })

  describe('utilities', () => {
    it('copies the human-readable identifier and confirms before closing', async () => {
      vi.useFakeTimers()
      const writeText = vi.fn(async () => {})
      vi.stubGlobal('navigator', { clipboard: { writeText } })
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      utility('copy')!.click()
      await flushPromises()
      expect(writeText).toHaveBeenCalledWith('TKT-0001')
      expect(utility('copy')!.textContent).toContain('Identifier Copied!')
      vi.advanceTimersByTime(1200)
      expect(menu.isOpen.value).toBe(false)
    })

    it('exports the context as JSON, even when raw data is circular', async () => {
      const writeText = vi.fn(async () => {})
      vi.stubGlobal('navigator', { clipboard: { writeText } })
      mountApp()
      const raw: any = { name: 'loop' }
      raw.self = raw
      menu.openContextMenu({ clientX: 10, clientY: 10 }, { entityType: 'machine', entityId: 'm-1', entityName: 'OP10', raw })
      await nextTick()
      utility('export')!.click()
      await flushPromises()
      const exported = JSON.parse(writeText.mock.calls[0][0] as string)
      expect(exported).toMatchObject({ entityType: 'machine', entityId: 'm-1', entityName: 'OP10' })
      expect(typeof exported.exportedAt).toBe('string')
      expect(exported.raw).toBeUndefined()
      expect(utility('export')!.textContent).toContain('JSON Copied!')
    })

    it('says so when the clipboard is unavailable, and stays open', async () => {
      vi.useFakeTimers()
      vi.stubGlobal('navigator', {})
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      utility('copy')!.click()
      await flushPromises()
      expect(utility('copy')!.textContent).toContain('Clipboard unavailable')
      vi.advanceTimersByTime(2000)
      await nextTick()
      expect(menu.isOpen.value).toBe(true)
      expect(utility('copy')!.textContent).toContain('Copy Handle / ID')
    })

    it('a pending confirmation never closes a menu opened afterwards', async () => {
      vi.useFakeTimers()
      vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn(async () => {}) } })
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      utility('copy')!.click()
      await flushPromises()
      vi.advanceTimersByTime(600)

      menu.closeContextMenu()
      await nextTick()
      rightClick(el('machine-text'))
      await nextTick()
      vi.advanceTimersByTime(1200)
      expect(menu.isOpen.value).toBe(true)
      expect(menu.contextData.value!.entityType).toBe('machine')
    })

    it('unlocks the browser menu for the next right-click and announces it', async () => {
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      utility('native')!.click()
      await nextTick()
      expect(menu.isOpen.value).toBe(false)
      expect(document.body.textContent).toContain('Native Browser Menu Unlocked')
      expect(rightClick(el('ticket')).defaultPrevented).toBe(false)
    })
  })

  describe('keyboard', () => {
    const press = (key: string) => {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true })
      ;(document.activeElement || document.body).dispatchEvent(event)
      return event
    }

    it('arrow keys walk the entries and wrap, Enter runs the highlighted one', async () => {
      const open = vi.fn()
      vi.stubGlobal('open', open)
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      const active = () => menuEl()!.querySelector('[data-active="true"]')?.getAttribute('data-context-item')

      expect(active()).toBeUndefined()
      press('ArrowDown')
      await nextTick()
      expect(active()).toBe('action:open')
      expect(document.activeElement).toBe(item('open'))
      press('ArrowDown')
      await nextTick()
      expect(active()).toBe('action:new-tab')
      press('ArrowUp')
      press('ArrowUp')
      await nextTick()
      expect(active()).toBe('utility:native')
      press('Home')
      await nextTick()
      expect(active()).toBe('action:open')
      press('End')
      await nextTick()
      expect(active()).toBe('utility:native')
      press('ArrowDown')
      press('ArrowDown')
      await nextTick()
      expect(active()).toBe('action:new-tab')

      const enter = press('Enter')
      expect(enter.defaultPrevented).toBe(true)
      expect(open).toHaveBeenCalledTimes(1)
      expect(menu.isOpen.value).toBe(false)
    })

    it('Enter without a highlighted entry does nothing, Escape closes', async () => {
      const open = vi.fn()
      vi.stubGlobal('open', open)
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      press('Enter')
      expect(menu.isOpen.value).toBe(true)
      expect(open).not.toHaveBeenCalled()
      press('Escape')
      expect(menu.isOpen.value).toBe(false)
    })

    it('handled keys do not reach the page, and nothing is intercepted while closed', async () => {
      mountApp()
      const seen: string[] = []
      const listener = (e: KeyboardEvent) => seen.push(e.key)
      document.addEventListener('keydown', listener)

      press('ArrowDown')
      press('Enter')
      expect(seen).toEqual(['ArrowDown', 'Enter'])

      rightClick(el('ticket'))
      await nextTick()
      press('ArrowDown')
      press('a')
      press('Escape')
      expect(seen).toEqual(['ArrowDown', 'Enter', 'a'])
      document.removeEventListener('keydown', listener)
    })

    it('gives focus back to the element that had it', async () => {
      mountApp()
      const input = el('ticket-input') as HTMLInputElement
      input.focus()
      rightClick(el('machine-text'))
      await nextTick()
      press('ArrowDown')
      await nextTick()
      expect(document.activeElement).toBe(item('machine'))
      press('Escape')
      await nextTick()
      expect(document.activeElement).toBe(input)
    })
  })

  describe('dismissal', () => {
    it('closes on a pointer press outside, also where click propagation is stopped', async () => {
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      const stopper = (e: Event) => e.stopPropagation()
      el('unbound').addEventListener('pointerdown', stopper)
      el('unbound').addEventListener('click', stopper)

      utility('copy')!.dispatchEvent(new Event('pointerdown', { bubbles: true }))
      expect(menu.isOpen.value).toBe(true)

      el('unbound').dispatchEvent(new Event('pointerdown', { bubbles: true }))
      expect(menu.isOpen.value).toBe(false)
    })

    it('does not let presses on the menu reach document-level outside-click handlers', async () => {
      mountApp()
      rightClick(el('ticket'))
      await nextTick()
      const outside = vi.fn()
      document.addEventListener('pointerdown', outside)
      utility('copy')!.dispatchEvent(new Event('pointerdown', { bubbles: true }))
      expect(outside).not.toHaveBeenCalled()
      document.removeEventListener('pointerdown', outside)
    })

    it('closes when the page or a container around the row scrolls, not for unrelated scrolling', async () => {
      mountApp()
      const open = async () => {
        rightClick(el('ticket-text'))
        await nextTick()
        expect(menu.isOpen.value).toBe(true)
      }

      await open()
      el('other-scroller').dispatchEvent(new Event('scroll'))
      menuEl()!.dispatchEvent(new Event('scroll'))
      expect(menu.isOpen.value).toBe(true)

      el('scroller').dispatchEvent(new Event('scroll'))
      expect(menu.isOpen.value).toBe(false)

      await open()
      document.dispatchEvent(new Event('scroll'))
      expect(menu.isOpen.value).toBe(false)

      await open()
      window.dispatchEvent(new Event('resize'))
      expect(menu.isOpen.value).toBe(false)

      await open()
      window.dispatchEvent(new Event('blur'))
      expect(menu.isOpen.value).toBe(false)
    })
  })

  describe('placement', () => {
    it('clamps with the rendered size so a tall menu is never cut off at the bottom', async () => {
      const rect = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
        const menuBox = this.hasAttribute('data-context-menu')
        return { x: 0, y: 0, left: 0, top: 0, right: 0, bottom: 0, width: menuBox ? 256 : 0, height: menuBox ? 470 : 0, toJSON: () => ({}) } as DOMRect
      })
      mountApp()
      rightClick(el('machine-text'), { clientX: 1000, clientY: 740 })
      await nextTick()

      expect(menu.y.value + 470).toBe(window.innerHeight - 12)
      expect(menu.x.value + 256).toBe(window.innerWidth - 12)
      expect(menuEl()!.style.top).toBe(`${menu.y.value}px`)
      expect(menuEl()!.style.left).toBe(`${menu.x.value}px`)

      // Re-opening elsewhere measures again from the new pointer position
      rightClick(el('ticket'), { clientX: 40, clientY: 60 })
      await nextTick()
      expect(menu.x.value).toBe(40)
      expect(menu.y.value).toBe(60)
      rect.mockRestore()
    })

    it('positions a keyboard-invoked menu at the element', () => {
      mountApp()
      const rect = vi.spyOn(el('machine'), 'getBoundingClientRect').mockReturnValue({ left: 200, top: 300, width: 400, height: 100 } as DOMRect)
      rightClick(el('machine'), { clientX: 0, clientY: 0, button: 0 })
      expect(menu.x.value).toBe(224)
      expect(menu.y.value).toBe(324)
      rect.mockRestore()
    })
  })
})

describe('explicit openContextMenu API', () => {
  it('reports whether it opened', () => {
    const opened = menu.openContextMenu({ clientX: 5, clientY: 5 }, { entityType: 'general' })
    expect(opened).toBe(true)
    const bypassed = menu.openContextMenu({ clientX: 5, clientY: 5, shiftKey: true }, { entityType: 'general' })
    expect(bypassed).toBe(false)
    expect(menu.isOpen.value).toBe(false)
  })

  it('keeps the state shallow: row objects are not made reactive', () => {
    const rowObject = { nested: { value: 1 } }
    menu.openContextMenu({ clientX: 5, clientY: 5 }, { entityType: 'machine', entityId: 'm', raw: rowObject })
    expect(menu.contextData.value!.raw).toBe(rowObject)
    const probe = ref(rowObject)
    expect(menu.contextData.value!.raw).not.toBe(probe.value)
  })
})
