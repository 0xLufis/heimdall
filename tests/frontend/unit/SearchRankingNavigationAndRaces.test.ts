import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { scoreRecord, rankRecords, highlightSegments } from '~/utils/search/searchRanking'
import { buildEntityLink, resolveEntityKind, resolveResultLink, resolveSubmitRoute } from '~/utils/search/searchNavigation'
import { loadCatalog, invalidateCatalog, resolveSearchScope, ticketToSearchItem, inventoryFilterExpression, searchServer, userToSearchItem } from '~/utils/search/searchSources'
import { addRecentSearch, MAX_RECENT_SEARCHES } from '~/utils/search/recentSearches'
import { useHeimdallSearch } from '~/composables/useHeimdallSearch'
import AutoTagSuggestionDropdown from '~/components/search/AutoTagSuggestionDropdown.vue'
import HeimdallSearchBar from '~/components/search/HeimdallSearchBar.vue'

const routerPush = vi.hoisted(() => vi.fn())
vi.mock('vue-router', () => ({ useRouter: () => ({ push: routerPush, currentRoute: { value: { path: '/' } } }) }))

describe('Search ranking', () => {
  it('requires every query token to match (AND), across different fields', () => {
    expect(scoreRecord('weld line2', ['OP20 Weld Cell', 'Line2'])).toBeGreaterThan(0)
    expect(scoreRecord('weld line9', ['OP20 Weld Cell', 'Line2'])).toBe(0)
  })

  it('takes the best field, not the first matching one', () => {
    // First field only contains the token; the second one equals it.
    const containsFirst = scoreRecord('op20', ['Line op20 buffer conveyor', 'unrelated'])
    const exactSecond = scoreRecord('op20', ['Line op20 buffer conveyor', 'OP20'])
    expect(exactSecond).toBeGreaterThan(containsFirst)
  })

  it('orders exact > prefix > substring > typo and keeps input order on ties', () => {
    const records = [
      { n: 'Main servo drive' },   // substring
      { n: 'Servo' },              // exact
      { n: 'Servo Motor 15kW' },   // prefix
      { n: 'Sevro amplifier' },    // typo
      { n: 'Conveyor' }            // no match
    ]
    const ranked = rankRecords('servo', records, r => [r.n]).map(r => r.item.n)
    expect(ranked).toEqual(['Servo', 'Servo Motor 15kW', 'Main servo drive', 'Sevro amplifier'])

    const ties = rankRecords('cell', [{ n: 'Cell B' }, { n: 'Cell A' }], r => [r.n]).map(r => r.item.n)
    expect(ties).toEqual(['Cell B', 'Cell A'])
  })

  it('does not typo-match short tokens and honours fuzzy: false', () => {
    expect(scoreRecord('ab', ['ac'])).toBe(0)
    expect(scoreRecord('sevro', ['Servo'], { fuzzy: false })).toBe(0)
    expect(scoreRecord('sevro', ['Servo'])).toBeGreaterThan(0)
  })

  it('never typo-matches identifiers that differ by a digit', () => {
    expect(scoreRecord('op30', ['OP20'])).toBe(0)
    expect(scoreRecord('line9', ['Line2'])).toBe(0)
  })

  it('returns everything with score 1 for an empty query', () => {
    expect(rankRecords('  ', [{ n: 'a' }, { n: 'b' }], r => [r.n]).map(r => r.score)).toEqual([1, 1])
  })
})

describe('Match highlighting', () => {
  it('marks every token occurrence case-insensitively and merges overlaps', () => {
    expect(highlightSegments('Laser Welder LASER', 'laser weld')).toEqual([
      { text: 'Laser', match: true },
      { text: ' ', match: false },
      { text: 'Weld', match: true },
      { text: 'er ', match: false },
      { text: 'LASER', match: true }
    ])
    expect(highlightSegments('abcd', 'abc bcd')).toEqual([{ text: 'abcd', match: true }])
  })

  it('treats regex metacharacters literally', () => {
    expect(highlightSegments('a.b (c)', '(c)')).toEqual([
      { text: 'a.b ', match: false },
      { text: '(c)', match: true }
    ])
  })

  it('renders entity names as text, never as HTML', () => {
    const wrapper = mount(AutoTagSuggestionDropdown, {
      props: {
        freeText: 'img',
        primaryResults: [{ id: 'x', name: '<img src=x onerror=alert(1)>', itemType: 'Machine' }]
      }
    })
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('[role="option"]').text()).toContain('<img src=x onerror=alert(1)>')
    expect(wrapper.find('[role="option"] mark').text()).toBe('img')
  })
})

describe('Result navigation targets', () => {
  it('deep-links each entity kind to the page that opens that entity', () => {
    expect(buildEntityLink('machine', 'm-1', 'OP20 Weld')).toBe('/dashboard/machines?search=OP20+Weld&tree=m-1')
    expect(buildEntityLink('controller', 'c-1', 'IPC-L1-02')).toBe('/dashboard/clients?selected=c-1&hostname=IPC-L1-02')
    expect(buildEntityLink('ticket', 't-9')).toBe('/dashboard/tickets/t-9')
    expect(buildEntityLink('user', 'u 1')).toBe('/dashboard/users/u%201')
    expect(buildEntityLink('inventory', 'p-7')).toBe('/dashboard/inventory/p-7')
  })

  it('falls back to the list page for placeholder ids', () => {
    expect(buildEntityLink('inventory', 'asset-unknown')).toBe('/dashboard/inventory')
    expect(buildEntityLink('ticket', undefined)).toBe('/dashboard/tickets')
  })

  it('resolves the kind from item type first, then source table', () => {
    expect(resolveEntityKind({ itemType: 'ClientPc' })).toBe('controller')
    expect(resolveEntityKind({ itemType: 'Machine', sourceTable: 'inventory' })).toBe('machine')
    expect(resolveEntityKind({ itemType: 'hardware', sourceTable: 'inventory' })).toBe('inventory')
    expect(resolveEntityKind({ itemType: 'whatever', sourceTable: 'tickets' })).toBe('ticket')
  })

  it('honours an explicit link and otherwise derives one', () => {
    expect(resolveResultLink({ id: 'a', name: 'A', itemType: 'Machine', link: '/custom' })).toBe('/custom')
    expect(resolveResultLink({ id: 'c-1', name: 'IPC-1', itemType: 'ClientPc' })).toBe('/dashboard/clients?selected=c-1&hostname=IPC-1')
    expect(resolveResultLink({ id: 't-1', name: 'Leak', itemType: 'Ticket' })).toBe('/dashboard/tickets/t-1')
  })

  it('maps tickets to their detail route', () => {
    const item = ticketToSearchItem({ id: 't-3', ticketNumber: 'TCK-0003', title: 'Laser drift', status: 'Open', priority: 'Critical', stationName: 'OP060' })
    expect(item.link).toBe('/dashboard/tickets/t-3')
    expect(item.name).toBe('Laser drift')
    expect(item.subtitle).toBe('TCK-0003 • OP060 • Critical')
  })
})

describe('Submitted query routing', () => {
  it('returns null for an empty query', () => {
    expect(resolveSubmitRoute('   ')).toBeNull()
  })

  it('routes by explicit scope and strips the scope from the term', () => {
    expect(resolveSubmitRoute('station:"all" weld')).toBe('/dashboard/machines?search=weld')
    expect(resolveSubmitRoute('line:"Line 2"')).toBe('/dashboard/machines?search=Line%202')
    expect(resolveSubmitRoute('host:"all" ipc-l2')).toBe('/dashboard/clients?hostname=ipc-l2')
    // Free text goes to the ticket list's own `?query=` (it used to be squeezed into `?stationId=`).
    expect(resolveSubmitRoute('ticket:"all" drift')).toBe('/dashboard/tickets?query=drift')
    expect(resolveSubmitRoute('#ticket laser')).toBe('/dashboard/tickets?query=laser')
    expect(resolveSubmitRoute('TKT-F2AF7680')).toBe('/dashboard/tickets?query=TKT-F2AF7680')
    expect(resolveSubmitRoute('user:"all" tensor')).toBe('/dashboard/users')
    expect(resolveSubmitRoute('@machine')).toBe('/dashboard/machines')
  })

  it('recognises controller identifiers', () => {
    expect(resolveSubmitRoute('IPC-L02-OP060-PLC')).toBe('/dashboard/clients?hostname=IPC-L02-OP060-PLC')
    expect(resolveSubmitRoute('192.168.102.14')).toBe('/dashboard/clients?hostname=192.168.102.14')
    // "incident" and "tktx" are words, not ticket numbers.
    expect(resolveSubmitRoute('incident report')).toBe('/dashboard/inventory?query=incident%20report')
    expect(resolveSubmitRoute('02:65:54:CE:AE:FC')).toBe('/dashboard/clients?hostname=02%3A65%3A54%3ACE%3AAE%3AFC')
  })

  it('does not guess from ordinary words: plain text goes to the current list or inventory', () => {
    // Previously "op…", "robot", "assembly" etc. were hard-wired to the machines page.
    expect(resolveSubmitRoute('optical sensor')).toBe('/dashboard/inventory?query=optical%20sensor')
    expect(resolveSubmitRoute('robot gripper', '/dashboard/tickets')).toBe('/dashboard/tickets?query=robot%20gripper')
    expect(resolveSubmitRoute('servo', '/dashboard/machines')).toBe('/dashboard/machines?search=servo')
  })
})

describe('Search scope tags', () => {
  const tag = (key: string, value: string) => ({ id: `${key}-${value}`, key, value })

  it('treats station/host/ticket/part tags as scopes and the rest as filters', () => {
    const scope = resolveSearchScope([tag('station', 'all'), tag('mfr', 'Siemens')])
    expect([...scope.kinds!]).toEqual(['machines'])
    expect(scope.terms).toEqual([])
    expect(scope.filterTags.map(t => t.key)).toEqual(['mfr'])
  })

  it('uses a concrete scope value as a search term', () => {
    const scope = resolveSearchScope([tag('host', 'IPC-L1')])
    expect([...scope.kinds!]).toEqual(['controllers'])
    expect(scope.terms).toEqual(['IPC-L1'])
  })

  it('has no scope without scope tags', () => {
    expect(resolveSearchScope([tag('status', 'online')]).kinds).toBeNull()
  })

  it('scopes ip / mac tags to controllers and user tags to users, with the value as the term', () => {
    const ip = resolveSearchScope([tag('ip', '192.168.101.15')])
    expect([...ip.kinds!]).toEqual(['controllers'])
    expect(ip.terms).toEqual(['192.168.101.15'])
    expect(ip.filterTags).toEqual([])
    expect([...resolveSearchScope([tag('mac', '02:65:54:01:65:05')]).kinds!]).toEqual(['controllers'])
    expect([...resolveSearchScope([tag('user', 'all')]).kinds!]).toEqual(['users'])
  })

  it('turns a tag no source can filter on into a search term instead of dropping it', () => {
    const scope = resolveSearchScope([tag('spec', '15KW'), tag('status', 'InStorage')])
    expect(scope.kinds).toBeNull()
    expect(scope.terms).toEqual(['15KW'])
    expect(scope.filterTags.map(t => t.key)).toEqual(['status'])
  })

  it('sends inventory filters under the key the backend understands', () => {
    expect(inventoryFilterExpression([tag('mfr', 'Siemens'), tag('status', 'InStorage')])).toBe('manufacturer:"Siemens" status:"InStorage"')
  })
})

describe('Server search source', () => {
  const response = {
    query: 'laser',
    kinds: ['stations', 'controllers', 'tickets', 'users', 'inventory'],
    stations: [{ id: 'm-1', name: 'L01-OP060', displayName: 'Laser Welder', groupId: 'Line 01', status: 'active', score: 0.9 }],
    controllers: [{ id: 'c-1', hostname: 'IPC-L01-LASER', ipAddress: '192.168.101.15', macAddress: '02:00:00:00:00:01', score: 0.9 }],
    tickets: [{ id: 't-1', ticketNumber: 'TKT-F2AF7680', title: 'Laser drift', status: 'Open', priority: 'High', stationName: 'L01-OP060', score: 0.9 }],
    users: [{ id: 'usr 1', name: 'Laser Larry', email: 'larry@plant.test', role: 'technician', score: 0.9 }],
    inventory: [{ id: 'p-1', name: 'Laser glass', itemType: 'HardwareComponent', serialNumber: 'SN-1', manufacturerName: 'Trumpf', score: 0.9 }]
  }

  it('asks for the term, the kinds and the limit in one request and maps every kind to its deep link', async () => {
    const fetcher = vi.fn().mockResolvedValue(response)
    const result = await searchServer('laser weld', ['stations', 'controllers', 'tickets', 'users', 'inventory'], 8, fetcher)

    expect(fetcher).toHaveBeenCalledTimes(1)
    expect(fetcher.mock.calls[0][0]).toBe('/api/search?q=laser+weld&kinds=stations%2Ccontrollers%2Ctickets%2Cusers%2Cinventory&limit=8')
    expect(result.stations[0].link).toBe('/dashboard/machines?search=L01-OP060&tree=m-1')
    expect(result.controllers[0].link).toBe('/dashboard/clients?selected=c-1&hostname=IPC-L01-LASER')
    expect(result.controllers[0].subtitle).toBe('IP: 192.168.101.15 • MAC: 02:00:00:00:00:01')
    expect(result.tickets[0].link).toBe('/dashboard/tickets/t-1')
    expect(result.tickets[0].subtitle).toBe('TKT-F2AF7680 • L01-OP060 • High')
    expect(result.users[0]).toMatchObject({ itemType: 'User', name: 'Laser Larry', subtitle: 'larry@plant.test', typeLabel: 'technician', link: '/dashboard/users/usr%201' })
    expect(result.inventory[0]).toMatchObject({ link: '/dashboard/inventory/p-1', manufacturerName: 'Trumpf', subtitle: 'SN: SN-1' })
  })

  it('returns only the kinds that were asked for', async () => {
    const result = await searchServer('laser', ['tickets'], 20, async () => response)
    expect(result.tickets).toHaveLength(1)
    expect(result.stations).toEqual([])
    expect(result.users).toEqual([])
  })

  it('rejects anything that is not a search result so the caller can fall back', async () => {
    await expect(searchServer('laser', ['tickets'], 8, async () => [])).rejects.toThrow()
    await expect(searchServer('laser', ['tickets'], 8, async () => ({ statusCode: 502, error: 'Bad Gateway' }))).rejects.toThrow()
    await expect(searchServer('laser', ['tickets'], 8, async () => { throw new Error('offline') })).rejects.toThrow('offline')
  })

  it('never invents a status or department for a user', () => {
    const item = userToSearchItem({ id: 'u-1', name: 'Tensor Flowski', email: 't@x.test', role: 'lead_engineer' })
    expect(item.status).toBeUndefined()
    expect(item.subtitle).toBe('t@x.test')
  })
})

describe('Catalogue cache', () => {
  beforeEach(() => invalidateCatalog())

  it('shares one request between concurrent and repeated callers within the TTL', async () => {
    const fetcher = vi.fn().mockResolvedValue([{ id: 1 }])
    const [a, b] = await Promise.all([loadCatalog('machines', fetcher, 1000), loadCatalog('machines', fetcher, 1000)])
    await loadCatalog('machines', fetcher, 20_000)
    expect(fetcher).toHaveBeenCalledTimes(1)
    expect(a).toBe(b)

    await loadCatalog('machines', fetcher, 40_000)
    expect(fetcher).toHaveBeenCalledTimes(2)
  })

  it('does not cache a failed request', async () => {
    const fetcher = vi.fn().mockRejectedValueOnce(new Error('down')).mockResolvedValue([{ id: 1 }])
    await expect(loadCatalog('controllers', fetcher, 0)).rejects.toThrow('down')
    await expect(loadCatalog('controllers', fetcher, 1)).resolves.toEqual([{ id: 1 }])
  })

  it('unwraps the tickets envelope', async () => {
    await expect(loadCatalog('tickets', async () => ({ tickets: [{ id: 't' }], metrics: {} }), 0)).resolves.toEqual([{ id: 't' }])
  })
})

describe('Recent searches', () => {
  it('moves a repeated query to the front without duplicating it', () => {
    expect(addRecentSearch(['servo', 'weld'], 'Weld')).toEqual(['Weld', 'servo'])
  })

  it('ignores queries shorter than two characters and caps the list', () => {
    const list = ['a1', 'a2', 'a3', 'a4', 'a5', 'a6']
    expect(addRecentSearch(list, 'x')).toBe(list)
    const next = addRecentSearch(list, 'new')
    expect(next).toHaveLength(MAX_RECENT_SEARCHES)
    expect(next[0]).toBe('new')
    expect(next).not.toContain('a6')
  })
})

describe('useHeimdallSearch execution', () => {
  const deferred = <T,>() => {
    let resolve!: (v: T) => void
    const promise = new Promise<T>(r => { resolve = r })
    return { promise, resolve }
  }

  it('discards a slow, older response that arrives after a newer one', async () => {
    const slow = deferred<any[]>()
    const fast = deferred<any[]>()
    const handler = vi.fn((q: string) => (q === 'old' ? slow.promise : fast.promise))
    const search = useHeimdallSearch({ instanceId: 'race', dataSource: { type: 'custom', handler }, template: 'machine' })

    const first = search.executeSearch('old')
    const second = search.executeSearch('new')

    fast.resolve([{ id: 'n', name: 'new result' }])
    await second
    expect(search.primaryResults.value.map(r => r.id)).toEqual(['n'])
    expect(search.isLoading.value).toBe(false)

    slow.resolve([{ id: 'o', name: 'old result' }])
    await first
    expect(search.primaryResults.value.map(r => r.id)).toEqual(['n'])
  })

  it('keeps the spinner on while the latest search is still running', async () => {
    const slow = deferred<any[]>()
    const handler = vi.fn((q: string) => (q === 'first' ? Promise.resolve([]) : slow.promise))
    const search = useHeimdallSearch({ instanceId: 'race-loading', dataSource: { type: 'custom', handler }, template: 'machine' })

    const first = search.executeSearch('first')
    const second = search.executeSearch('second')
    await first
    expect(search.isLoading.value).toBe(true)
    slow.resolve([])
    await second
    expect(search.isLoading.value).toBe(false)
  })

  it('a cleared search is not repopulated by an in-flight response', async () => {
    const pending = deferred<any[]>()
    const search = useHeimdallSearch({ instanceId: 'race-clear', dataSource: { type: 'custom', handler: () => pending.promise }, template: 'machine' })
    const run = search.executeSearch('weld')
    search.clearAllTags()
    pending.resolve([{ id: 'late', name: 'weld cell' }])
    await run
    expect(search.results.value).toEqual([])
    expect(search.isLoading.value).toBe(false)
  })

  it('ranks in-memory data by relevance and filters with AND semantics', async () => {
    const data = [
      { id: 'a', name: 'Buffer conveyor for weld line', groupId: 'Line 2' },
      { id: 'b', name: 'Weld', groupId: 'Line 1' },
      { id: 'c', name: 'Weld Cell OP20', groupId: 'Line 2' }
    ]
    const search = useHeimdallSearch({ instanceId: 'rank', data, template: 'machine' })
    await search.executeSearch('weld')
    expect(search.primaryResults.value.map(r => r.id)).toEqual(['b', 'c', 'a'])

    await search.executeSearch('weld line 2')
    expect(search.primaryResults.value.map(r => r.id).sort()).toEqual(['a', 'c'])
  })

  it('uses the explicitly passed query even when the input holds other text', async () => {
    const search = useHeimdallSearch({ instanceId: 'explicit', data: [{ id: 'a', name: 'Alpha' }, { id: 'b', name: 'Beta' }], template: 'machine' })
    search.rawInput.value = 'alpha'
    await search.executeSearch('beta')
    expect(search.primaryResults.value.map(r => r.id)).toEqual(['b'])
  })

  it('keeps the instance id separate from the placeholder', () => {
    const search = useHeimdallSearch({ config: { placeholder: 'Find things' } })
    expect(search.config.instanceId).toBe('dashboard')
    expect(search.config.placeholder).toBe('Find things')
  })

  describe('server-side search', () => {
    const serverResponse = (overrides: Record<string, any> = {}) => ({
      query: 'laser',
      kinds: ['stations', 'controllers', 'tickets', 'users', 'inventory'],
      stations: [{ id: 'm-1', name: 'OP060 Laser Welding Cell', groupId: 'Line 2' }],
      controllers: [{ id: 'c-1', hostname: 'IPC-L02-LASER', macAddress: '02:00:00:00:00:01' }],
      tickets: [{ id: 't-1', ticketNumber: 'TKT-1', title: 'Laser power drift', status: 'Open' }],
      users: [{ id: 'u-1', name: 'Laser Larry', email: 'larry@plant.test', role: 'technician' }],
      inventory: [{ id: 'p-1', name: 'Laser protective glass', itemType: 'HardwareComponent' }],
      ...overrides
    })
    let fetchMock: ReturnType<typeof vi.fn>
    const urls = () => fetchMock.mock.calls.map(([url]) => String(url))

    beforeEach(() => {
      invalidateCatalog()
      fetchMock = vi.fn(async (url: string) => {
        if (url.startsWith('/api/search')) return serverResponse()
        if (url.startsWith('/api/proxy/inventory/search')) return [{ id: 'p-9', name: 'Filtered part', itemType: 'hardware' }]
        throw new Error(`unexpected ${url}`)
      })
      vi.stubGlobal('$fetch', fetchMock)
    })
    afterEach(() => vi.unstubAllGlobals())

    it('gets every kind, users included, from one request and downloads no catalogue', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.rawInput.value = 'Laser'
      await search.executeSearch()

      expect(urls()).toEqual(['/api/search?q=laser&kinds=inventory%2Cstations%2Ccontrollers%2Ctickets%2Cusers&limit=8'])
      expect(search.error.value).toBeNull()
      expect(search.failedSources.value).toEqual([])
      expect(search.primaryResults.value.map(r => r.link)).toEqual(['/dashboard/inventory/p-1'])
      expect(search.crossTableResults.value.map(r => r.link)).toEqual([
        '/dashboard/machines?search=OP060+Laser+Welding+Cell&tree=m-1',
        '/dashboard/clients?selected=c-1&hostname=IPC-L02-LASER',
        '/dashboard/tickets/t-1',
        '/dashboard/users/u-1'
      ])
    })

    it('restricts the request to the scoped kind and asks for more of it', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.addTag({ key: 'host', value: 'all' })
      search.rawInput.value = 'laser'
      await search.executeSearch()
      expect(urls().filter(u => u.startsWith('/api/search'))).toEqual(['/api/search?q=laser&kinds=controllers&limit=20'])
      expect(search.results.value.map(r => r.itemType)).toEqual(['ClientPc'])
    })

    it('treats an ip tag as a controller search for that address', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.addTag({ key: 'ip', value: '192.168.101.15' })
      await search.executeSearch()
      expect(urls().at(-1)).toBe('/api/search?q=192.168.101.15&kinds=controllers&limit=20')
    })

    it('keeps a tag that cannot be filtered on as a search term', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.addTag({ key: 'spec', value: '15KW' })
      search.rawInput.value = 'servo'
      await search.executeSearch()
      expect(urls().at(-1)).toContain('/api/search?q=servo+15kw&')
    })

    it('sends inventory filter tags to the inventory search under the backend key', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.addTag({ key: 'mfr', value: 'Siemens' })
      search.rawInput.value = 'cpu'
      await search.executeSearch()
      const inventoryCalls = urls().filter(u => u.startsWith('/api/proxy/inventory/search'))
      expect(decodeURIComponent(inventoryCalls.at(-1)!)).toBe('/api/proxy/inventory/search?query=manufacturer:"Siemens" cpu')
      expect(urls().some(u => u.startsWith('/api/search'))).toBe(false)
      expect(search.primaryResults.value.map(r => r.id)).toEqual(['p-9'])
    })

    it('uses the station list for the machines instance and the controller list for the clients instance', async () => {
      const machines = useHeimdallSearch({ instanceId: 'machines' })
      machines.rawInput.value = 'laser'
      await machines.executeSearch()
      expect(urls().at(-1)).toBe('/api/search?q=laser&kinds=stations%2Cinventory&limit=20')
      expect(machines.primaryResults.value.map(r => r.itemType)).toEqual(['Machine'])
      expect(machines.crossTableResults.value.map(r => r.id)).toEqual(['p-1'])

      const clients = useHeimdallSearch({ instanceId: 'clients' })
      clients.rawInput.value = 'laser'
      await clients.executeSearch()
      expect(urls().at(-1)).toBe('/api/search?q=laser&kinds=controllers%2Cstations&limit=20')
      expect(clients.primaryResults.value.map(r => r.itemType)).toEqual(['ClientPc'])
    })

    it('reports users as unavailable when they were asked for and the endpoint is down', async () => {
      fetchMock.mockRejectedValue(new Error('offline'))
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.addTag({ key: 'user', value: 'all' })
      search.rawInput.value = 'tensor'
      await search.executeSearch()
      expect(search.results.value).toEqual([])
      expect(search.failedSources.value).toEqual(['users'])
      expect(search.error.value).toMatch(/unavailable/i)
    })
  })

  describe('catalogue fallback (search endpoint unavailable)', () => {
    const machines = [
      { id: 'm-1', name: 'OP060 Laser Welding Cell', groupId: 'Line 2' },
      { id: 'm-2', name: 'OP010 Loader', groupId: 'Line 1' }
    ]
    const controllers = [{ id: 'c-1', hostname: 'IPC-L02-LASER', macAddress: '02:00:00:00:00:01' }]
    const tickets = { tickets: [{ id: 't-1', ticketNumber: 'TCK-1', title: 'Laser power drift', status: 'Open' }] }
    const inventory = [{ id: 'p-1', name: 'Laser protective glass', itemType: 'hardware' }]
    let fetchMock: ReturnType<typeof vi.fn>

    beforeEach(() => {
      invalidateCatalog()
      fetchMock = vi.fn(async (url: string) => {
        if (url.startsWith('/api/proxy/v1/machine')) return machines
        if (url.startsWith('/api/proxy/v1/ClientPc')) return controllers
        if (url.startsWith('/api/tickets')) return tickets
        if (url.startsWith('/api/proxy/inventory/search')) return inventory
        throw new Error(`unexpected ${url}`)
      })
      vi.stubGlobal('$fetch', fetchMock)
    })
    afterEach(() => vi.unstubAllGlobals())

    it('searches inventory, stations, controllers and tickets and links each to its entity', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.rawInput.value = 'laser'
      await search.executeSearch()

      expect(search.error.value).toBeNull()
      expect(search.primaryResults.value.map(r => r.link)).toEqual(['/dashboard/inventory/p-1'])
      expect(search.crossTableResults.value.map(r => r.link)).toEqual([
        '/dashboard/machines?search=OP060+Laser+Welding+Cell&tree=m-1',
        '/dashboard/clients?selected=c-1&hostname=IPC-L02-LASER',
        '/dashboard/tickets/t-1'
      ])
    })

    it('does not refetch the catalogues on the next keystroke', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.rawInput.value = 'las'
      await search.executeSearch()
      search.rawInput.value = 'lase'
      await search.executeSearch()
      const catalogCalls = fetchMock.mock.calls.filter(([url]) => !String(url).includes('/inventory/') && !String(url).startsWith('/api/search'))
      expect(catalogCalls).toHaveLength(3)
    })

    it('does not list a station a second time as an inventory row', async () => {
      // The inventory search returns station rows too (they share the table); stations come from their own source.
      fetchMock.mockImplementation(async (url: string) => {
        if (url.startsWith('/api/proxy/v1/machine')) return machines
        if (url.startsWith('/api/proxy/v1/ClientPc')) return []
        if (url.startsWith('/api/tickets')) return { tickets: [] }
        if (url.startsWith('/api/proxy/inventory/search')) return [{ id: 'm-1', name: 'OP060 Laser Welding Cell', itemType: 'Machine' }, ...inventory]
        throw new Error(`unexpected ${url}`)
      })
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.rawInput.value = 'laser'
      await search.executeSearch()
      expect(search.results.value.map(r => r.id)).toEqual(['p-1', 'm-1'])
      expect(search.results.value.find(r => r.id === 'm-1')!.link).toBe('/dashboard/machines?search=OP060+Laser+Welding+Cell&tree=m-1')
    })

    it('restricts the search to the scoped entity kind', async () => {
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.addTag({ key: 'host', value: 'all' })
      search.rawInput.value = 'laser'
      await search.executeSearch()
      expect(search.primaryResults.value).toEqual([])
      expect(search.crossTableResults.value.map(r => r.itemType)).toEqual(['ClientPc'])
      expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/inventory/'))).toBe(false)
    })

    it('reports a partial outage but still returns the sources that answered', async () => {
      fetchMock.mockImplementation(async (url: string) => {
        if (url.startsWith('/api/proxy/v1/machine')) return machines
        throw new Error('503')
      })
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.rawInput.value = 'laser'
      await search.executeSearch()
      expect(search.error.value).toBeNull()
      expect(search.failedSources.value.sort()).toEqual(['controllers', 'inventory', 'tickets'])
      expect(search.results.value.map(r => r.id)).toEqual(['m-1'])
    })

    it('raises an error state when every source fails', async () => {
      fetchMock.mockRejectedValue(new Error('offline'))
      const search = useHeimdallSearch({ instanceId: 'global' })
      search.rawInput.value = 'laser'
      await search.executeSearch()
      expect(search.results.value).toEqual([])
      expect(search.error.value).toMatch(/unavailable/i)
    })
  })
})

describe('Results dropdown', () => {
  const results = [
    { id: 'p-1', name: 'Laser glass', itemType: 'hardware', sourceTable: 'inventory' },
    { id: 'm-1', name: 'Laser Welder', itemType: 'Machine', isCrossTable: true },
    { id: 'p-2', name: 'Laser diode', itemType: 'hardware', sourceTable: 'inventory' },
    { id: 't-1', name: 'Laser drift', itemType: 'Ticket', isCrossTable: true }
  ]

  it('groups results by entity type with counts and navigates in render order', async () => {
    const wrapper = mount(AutoTagSuggestionDropdown, { props: { results, freeText: 'laser', listboxId: 'lb' } })
    const groups = wrapper.findAll('[role="group"]')
    expect(groups.map(g => g.attributes('aria-label'))).toEqual(['Inventory, 2 results', 'Stations, 1 results', 'Tickets, 1 results'])

    const vm = wrapper.vm as any
    const order = [vm.getSelectedResult().id]
    for (let i = 0; i < 3; i++) { vm.navigateNext(); order.push(vm.getSelectedResult().id) }
    expect(order).toEqual(['p-1', 'p-2', 'm-1', 't-1'])
    vm.navigateNext()
    expect(vm.getSelectedResult().id).toBe('p-1')
  })

  it('exposes listbox semantics and reports the active option', async () => {
    const wrapper = mount(AutoTagSuggestionDropdown, { props: { results, freeText: 'laser', listboxId: 'lb' } })
    expect(wrapper.find('#lb').attributes('role')).toBe('listbox')
    const options = wrapper.findAll('[role="option"]')
    expect(options).toHaveLength(4)
    expect(options[0].attributes('aria-selected')).toBe('true')

    ;(wrapper.vm as any).navigateNext()
    await nextTick()
    expect(wrapper.findAll('[role="option"]')[1].attributes('aria-selected')).toBe('true')
    const emitted = wrapper.emitted('active-change')!.map(e => e[0])
    expect(emitted).toEqual(['lb-option-0', 'lb-option-1'])
  })

  it('resets the selection when a different result set arrives', async () => {
    const wrapper = mount(AutoTagSuggestionDropdown, { props: { results, freeText: 'laser' } })
    const vm = wrapper.vm as any
    vm.navigateNext(); vm.navigateNext()
    await wrapper.setProps({ results: [{ id: 'z-1', name: 'Zeta', itemType: 'Machine' }, { id: 'z-2', name: 'Zulu', itemType: 'Machine' }] })
    expect(vm.getSelectedResult().id).toBe('z-1')
  })

  it('lists users under People and previews e-mail and role, not part fields', () => {
    const user = userToSearchItem({ id: 'u-1', name: 'Tensor Flowski', email: 'tensor@plant.test', role: 'lead_engineer' })
    const wrapper = mount(AutoTagSuggestionDropdown, { props: { results: [user], freeText: 'tensor' } })
    expect(wrapper.find('[role="group"]').attributes('aria-label')).toBe('People, 1 results')
    expect(wrapper.text()).toContain('E-mail:')
    expect(wrapper.text()).toContain('lead_engineer')
    expect(wrapper.text()).not.toContain('Specification:')
    expect(wrapper.text()).toContain('Opens /dashboard/users/u-1')
  })

  it('selecting an option emits the item', async () => {
    const wrapper = mount(AutoTagSuggestionDropdown, { props: { results, freeText: 'laser' } })
    await wrapper.findAll('[role="option"]')[2].trigger('click')
    expect((wrapper.emitted('select-result')![0][0] as any).id).toBe('m-1')
  })

  it('distinguishes loading, no-match, error and recent-search states', async () => {
    const wrapper = mount(AutoTagSuggestionDropdown, { props: { freeText: 'zzzz', isLoading: true } })
    expect(wrapper.text()).toContain('Searching')
    expect(wrapper.text()).not.toContain('No matches')

    await wrapper.setProps({ isLoading: false })
    expect(wrapper.text()).toContain('No matches for “zzzz”')

    await wrapper.setProps({ error: 'Search is unavailable right now.' })
    expect(wrapper.find('[role="alert"]').text()).toContain('unavailable')
    expect(wrapper.text()).not.toContain('No matches')
    await wrapper.find('[role="alert"] button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)

    await wrapper.setProps({ error: null, freeText: '', recentSearches: ['servo drive'] })
    expect(wrapper.text()).toContain('Recent Searches')
    await wrapper.findAll('button').find(b => b.text() === 'servo drive')!.trigger('click')
    expect(wrapper.emitted('select-recent')![0]).toEqual(['servo drive'])
  })
})

describe('Search bar keyboard behaviour', () => {
  const inventory = [
    { id: 'p-1', name: 'Laser protective glass', itemType: 'hardware' },
    { id: 'p-2', name: 'Laser diode module', itemType: 'hardware' }
  ]

  beforeEach(() => {
    invalidateCatalog()
    routerPush.mockClear()
    vi.stubGlobal('$fetch', vi.fn(async (url: string) => {
      if (url.startsWith('/api/proxy/inventory/search')) return inventory
      if (url.startsWith('/api/proxy/v1/machine')) return [{ id: 'm-1', name: 'Laser Welding Cell' }]
      if (url.startsWith('/api/tickets')) return { tickets: [] }
      return []
    }))
  })
  afterEach(() => vi.unstubAllGlobals())

  const mountWithResults = async () => {
    const wrapper = mount(HeimdallSearchBar, { props: { config: { instanceId: 'global', debounceMs: 10 } }, attachTo: document.body })
    const input = wrapper.find('input')
    await input.trigger('focus')
    await input.setValue('laser')
    await vi.waitFor(() => expect(wrapper.findAll('[role="option"]').length).toBe(3))
    return { wrapper, input }
  }

  it('wires the input as a combobox pointing at the active option', async () => {
    const { wrapper, input } = await mountWithResults()
    expect(input.attributes('role')).toBe('combobox')
    expect(input.attributes('aria-expanded')).toBe('true')
    const listbox = wrapper.find('[role="listbox"]')
    expect(input.attributes('aria-controls')).toBe(listbox.attributes('id'))
    expect(input.attributes('aria-activedescendant')).toBe(wrapper.findAll('[role="option"]')[0].attributes('id'))

    await input.trigger('keydown', { key: 'ArrowDown' })
    expect(input.attributes('aria-activedescendant')).toBe(wrapper.findAll('[role="option"]')[1].attributes('id'))
  })

  it('Enter opens the highlighted entity, not a list page', async () => {
    const { wrapper, input } = await mountWithResults()
    await input.trigger('keydown', { key: 'ArrowDown' })
    await input.trigger('keydown', { key: 'ArrowDown' })
    await input.trigger('keydown', { key: 'Enter' })
    expect(routerPush).toHaveBeenCalledWith('/dashboard/machines?search=Laser+Welding+Cell&tree=m-1')
    expect((wrapper.emitted('select-result')![0][0] as any).id).toBe('m-1')
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('clicking a result navigates to it', async () => {
    const { wrapper } = await mountWithResults()
    await wrapper.findAll('[role="option"]')[1].trigger('click')
    expect(routerPush).toHaveBeenCalledWith('/dashboard/inventory/p-2')
  })

  it('leaves paste, cut and Ctrl+K alone while results are listed', async () => {
    const { input } = await mountWithResults()
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    for (const key of ['v', 'x', 'k']) {
      const event = new KeyboardEvent('keydown', { key, ctrlKey: true, cancelable: true, bubbles: true })
      input.element.dispatchEvent(event)
      expect(event.defaultPrevented, `Ctrl+${key}`).toBe(false)
    }
    expect(open).not.toHaveBeenCalled()
    expect(routerPush).not.toHaveBeenCalled()
  })

  it('Ctrl+Enter opens the highlighted result in a new tab', async () => {
    const { input } = await mountWithResults()
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    await input.trigger('keydown', { key: 'Enter', ctrlKey: true })
    expect(open).toHaveBeenCalledWith('/dashboard/inventory/p-1', '_blank', 'noopener')
    expect(routerPush).not.toHaveBeenCalled()
  })

  it('Tab is not trapped when there is nothing to complete', async () => {
    const wrapper = mount(HeimdallSearchBar, { props: { config: { instanceId: 'global', debounceMs: 10 } }, attachTo: document.body })
    const input = wrapper.find('input')
    await input.trigger('focus')
    const event = new KeyboardEvent('keydown', { key: 'Tab', cancelable: true, bubbles: true })
    input.element.dispatchEvent(event)
    expect(event.defaultPrevented).toBe(false)
  })

  it('Enter completes a pending key:value before opening stale results', async () => {
    const { wrapper, input } = await mountWithResults()
    await input.setValue('status:onl')
    await input.trigger('keydown', { key: 'Enter' })
    expect(routerPush).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('online')
    expect((input.element as HTMLInputElement).value).toBe('')
  })

  it('Enter without results submits the query and remembers it', async () => {
    vi.stubGlobal('$fetch', vi.fn(async (url: string) => (String(url).startsWith('/api/tickets') ? { tickets: [] } : [])))
    const wrapper = mount(HeimdallSearchBar, { props: { config: { instanceId: 'global', debounceMs: 10, enableAutoTagging: false } }, attachTo: document.body })
    const input = wrapper.find('input')
    await input.trigger('focus')
    await input.setValue('qqqq')
    await input.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('submit')![0]).toEqual(['qqqq'])
  })
})
