import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { createRouter, createMemoryHistory } from 'vue-router'

// The page is tested for what it does with the URL; its children are replaced by probes.
vi.mock('../../../src/frontend/web/app/components/machines/MachineCardGrid.vue', () => ({
  default: defineComponent({
    name: 'MachineCardGrid',
    props: ['machines', 'loading'],
    setup: (props) => () => h('ul', { 'data-grid': '' }, (props.machines || []).map((m: any) => h('li', { 'data-machine': m.id }, m.name)))
  })
}))
vi.mock('../../../src/frontend/web/app/components/machines/StationDetailWorkbench.vue', () => ({
  default: defineComponent({
    name: 'StationDetailWorkbench',
    props: ['open', 'machine', 'initialTab'],
    emits: ['update:open', 'refresh'],
    setup: (props) => () => h('aside', { 'data-workbench': String(props.open), 'data-tab': props.initialTab, 'data-station': props.machine?.id || '', 'data-station-name': props.machine?.name || '' })
  })
}))
vi.mock('../../../src/frontend/web/app/components/machines/MachineLineHierarchy.vue', () => ({ default: defineComponent({ render: () => h('div') }) }))
vi.mock('../../../src/frontend/web/app/components/inventory/StationPhysicalView.vue', () => ({ default: defineComponent({ render: () => h('div') }) }))
vi.mock('../../../src/frontend/web/app/components/machines/MachineToolbar.vue', () => ({
  default: defineComponent({
    name: 'MachineToolbar',
    props: ['searchQuery', 'activeView', 'statusFilter', 'totalCount'],
    emits: ['update:searchQuery', 'update:activeView', 'update:statusFilter', 'reset'],
    setup: (props) => () => h('div', { 'data-toolbar': '', 'data-search': props.searchQuery, 'data-status': props.statusFilter, 'data-count': String(props.totalCount) })
  })
}))

const ACTIVE = '008ed0dd-9223-54b8-a191-8d76b5af4f3f'
const RETIRED = '52975097-411e-5e6a-b710-b6c64bb90800'
const machines = [
  { id: ACTIVE, name: 'L01-OP100', displayName: 'Cover Fastener', customIdentifier: 'LINE-01-OP100', pinnedObjectHandle: 'L01-OP100', status: 'active', machineType: 'FasteningStation', controllers: [] },
  { id: 'm-2', name: 'L01-OP010', displayName: 'Loader', customIdentifier: 'LINE-01-OP010', status: 'active', machineType: 'Loader', controllers: [] },
  { id: RETIRED, name: 'L03-OP050', displayName: 'Vision Cell', customIdentifier: 'LINE-03-OP050', status: 'decommissioned', machineType: 'Vision', controllers: [] }
]

let resolveMachines: ((value: any[]) => void) | null = null

const mountPage = async (start: string, { deferred = false } = {}) => {
  resolveMachines = null
  vi.stubGlobal('$fetch', vi.fn((url: string) => {
    if (url === '/api/machines') {
      return deferred ? new Promise<any[]>((resolve) => { resolveMachines = resolve }) : Promise.resolve(machines)
    }
    return Promise.resolve([])
  }))
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }] })
  await router.push(start)
  await router.isReady()
  const Page = (await import('../../../src/frontend/web/app/pages/dashboard/machines.vue')).default
  const wrapper = mount(Page, { global: { plugins: [router], stubs: { Button: { template: '<button><slot /></button>' } } } })
  await flushPromises()
  const shown = () => wrapper.findAll('[data-machine]').map(li => li.attributes('data-machine'))
  const workbench = () => wrapper.find('[data-workbench]').attributes()
  return { wrapper, router, shown, workbench }
}

describe('/dashboard/machines deep links (page)', () => {
  beforeEach(() => {
    vi.stubGlobal('definePageMeta', vi.fn())
    vi.stubGlobal('useState', (_key: string, init?: () => any) => ref(init ? init() : undefined))
    vi.stubGlobal('useRuntimeConfig', () => ({ public: {} }))
  })

  it('?id=<uuid> narrows the directory to that station and can be released', { timeout: 30000 }, async () => {
    const { wrapper, router, shown } = await mountPage(`/dashboard/machines?id=${ACTIVE}`)
    expect(shown()).toEqual([ACTIVE])
    // The uuid is not dumped into the search box
    expect(wrapper.find('[data-toolbar]').attributes('data-search')).toBe('')
    expect(wrapper.find('[data-testid="machine-focus-chip"]').text()).toContain('Cover Fastener')

    await wrapper.find('[data-testid="machine-focus-chip"] button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.id).toBeUndefined()
    expect(shown()).toEqual([ACTIVE, 'm-2'])
    expect(wrapper.find('[data-testid="machine-focus-chip"]').exists()).toBe(false)
  })

  it('?id= shows a decommissioned station although the status tab hides those', async () => {
    const { shown, wrapper } = await mountPage(`/dashboard/machines?id=${RETIRED}`)
    expect(wrapper.find('[data-toolbar]').attributes('data-status')).toBe('active_floor')
    expect(shown()).toEqual([RETIRED])
  })

  it('typing in the search box takes over from the id focus', async () => {
    const { wrapper, router, shown } = await mountPage(`/dashboard/machines?id=${RETIRED}`)
    wrapper.findComponent({ name: 'MachineToolbar' }).vm.$emit('update:searchQuery', 'loader')
    await flushPromises()
    expect(router.currentRoute.value.query.id).toBeUndefined()
    expect(shown()).toEqual(['m-2'])
  })

  it('an id that names no station becomes a text search instead of listing everything', async () => {
    const { wrapper, shown } = await mountPage('/dashboard/machines?id=does-not-exist')
    expect(shown()).toEqual([])
    expect(wrapper.find('[data-toolbar]').attributes('data-search')).toBe('does-not-exist')
  })

  it('?search= filters the directory', async () => {
    const { wrapper, shown } = await mountPage('/dashboard/machines?search=loader')
    expect(wrapper.find('[data-toolbar]').attributes('data-search')).toBe('loader')
    expect(shown()).toEqual(['m-2'])
  })

  it('?decommission=<identifier> opens the lifecycle dialog once the machines are loaded', async () => {
    const { workbench } = await mountPage('/dashboard/machines?decommission=LINE-01-OP100', { deferred: true })
    // The list is still loading: nothing to decommission yet
    expect(workbench()['data-workbench']).toBe('false')

    resolveMachines!(machines)
    await flushPromises()
    expect(workbench()).toMatchObject({ 'data-workbench': 'true', 'data-tab': 'lifecycle', 'data-station': ACTIVE })
  })

  it('?decommission=<uuid> works when the link arrives on the open page', async () => {
    const { router, workbench } = await mountPage('/dashboard/machines')
    expect(workbench()['data-workbench']).toBe('false')
    await router.push(`/dashboard/machines?decommission=${RETIRED}`)
    await flushPromises()
    expect(workbench()).toMatchObject({ 'data-workbench': 'true', 'data-tab': 'lifecycle', 'data-station': RETIRED })
  })

  it('?search=&tree= opens the station workbench with the loaded record; closing it frees the link', async () => {
    const link = `/dashboard/machines?search=L01-OP100&tree=${ACTIVE}`
    const { wrapper, router, shown, workbench } = await mountPage(link, { deferred: true })
    // Opens straight away by id, before the list is there
    expect(workbench()).toMatchObject({ 'data-workbench': 'true', 'data-tab': 'tree', 'data-station': ACTIVE })

    resolveMachines!(machines)
    await flushPromises()
    expect(workbench()['data-station-name']).toBe('L01-OP100')
    expect(shown()).toEqual([ACTIVE])

    wrapper.findComponent({ name: 'StationDetailWorkbench' }).vm.$emit('update:open', false)
    await flushPromises()
    expect(workbench()['data-workbench']).toBe('false')
    expect(router.currentRoute.value.query).toEqual({ search: 'L01-OP100' })

    // Following the same link again is a real navigation now, so the workbench reopens
    await router.push(link)
    await flushPromises()
    expect(workbench()).toMatchObject({ 'data-workbench': 'true', 'data-station': ACTIVE })
  })
})
