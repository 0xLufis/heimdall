import { describe, it, expect } from 'vitest'
import {
  queryText,
  parseMachinesQuery,
  findMachineByRef,
  machineMatchesText,
  machineMatchesStatus,
  filterMachineDirectory,
  parseTicketsQuery,
  stationIdentifiers,
  stationAliases,
  ticketMatchesStation,
  ticketMatchesText,
  ticketMatchesTeam,
  teamStationAliases,
  filterTicketsByDeepLink,
  describeStationFilter,
  normalizeTicketsDeepLink,
  resolveMapFocus,
  resolveControllerSelection
} from '../../../src/frontend/web/app/utils/deepLinkQueries'
import { buildContextMenuActions } from '../../../src/frontend/web/app/utils/contextMenuActions'
import { buildEntityLink, resolveSubmitRoute } from '../../../src/frontend/web/app/utils/search/searchNavigation'

const queryOf = (route: string) => Object.fromEntries(new URL(route, 'http://heimdall.test').searchParams)

const OP100 = '008ed0dd-9223-54b8-a191-8d76b5af4f3f'
const OP010 = '11111111-2222-4333-8444-555555555555'
const OP050 = '52975097-411e-5e6a-b710-b6c64bb90800'

const machines = [
  {
    id: OP100, name: 'L01-OP100', displayName: 'L01-OP100 – Multi-Spindle Cover Fastener', customIdentifier: 'LINE-01-OP100',
    pinnedObjectHandle: 'L01-OP100', groupId: 'Line 01', lineGroupName: 'Body Line', machineType: 'FasteningStation', status: 'active',
    controllers: [{ id: 'c-1', hostname: 'IPC-L01-OP100' }], responsibleTeams: [{ id: 't-1', name: 'Plant Maintenance' }]
  },
  {
    id: OP010, name: 'L01-OP010', displayName: 'L01-OP010 – Loader', customIdentifier: 'LINE-01-OP010',
    pinnedObjectHandle: 'L01-OP010', groupId: 'Line 01', machineType: 'Loader', status: 'maintenance',
    controllers: [], responsibleTeams: [{ id: 't-2', name: 'Robotics Team' }]
  },
  {
    id: OP050, name: 'L03-OP050', displayName: 'L03-OP050 – Vision Cell', customIdentifier: 'LINE-03-OP050',
    pinnedObjectHandle: null, groupId: 'Line 03', machineType: 'Vision', status: 'decommissioned',
    controllers: [{ id: 'c-3', hostname: 'IPC-L03-VIS' }], responsibleTeams: [{ id: 't-1', name: 'Plant Maintenance' }]
  }
]

const tickets = [
  // Backend ticket: station id is the machine uuid
  { id: 't-uuid', ticketNumber: 'TKT-2298A810', stationId: OP050, stationName: 'L03-OP050', title: 'Vision System Misalignment', description: 'Intermittent misalignment during cycle.', assignedTechnicianName: 'Synthia Hal-Zero', tags: [] as string[], responsibleDepartment: 'Assy' },
  // Seed ticket: station id is the floor-plan handle
  { id: 't-handle', ticketNumber: 'TKT-20260928-0003', stationId: 'L01-OP100', stationName: 'Line 01 – Fastening', title: '[E-VIS-01] Camera lens cleaning required', description: 'Contrast dropped by 18%.', tags: ['#Vision', '#Quality'], responsibleDepartment: 'Vision', errorCode: 'E-VIS-01', controllerName: 'IPC-L01-OP100' },
  // Created through the form: station id is the custom identifier
  { id: 't-custom', ticketNumber: 'TKT-0007', stationId: 'LINE-01-OP100', stationName: 'L01-OP100 – Multi-Spindle Cover Fastener', title: 'Torque out of range', assignedTechnicianName: 'Tensor Flowski', tags: ['Plant Maintenance'] },
  // Tagged with the handle of another station
  { id: 't-tag', ticketNumber: 'TKT-0008', stationId: 'UNKNOWN-STATION', stationName: 'Plant Floor Cell', title: 'Guard door sensor', tags: ['l01-op010'] },
  // Station whose identifier merely starts like another one
  { id: 't-prefix', ticketNumber: 'TKT-0009', stationId: 'L01-OP1000', stationName: 'L01-OP1000', title: 'Unrelated station with a longer id', tags: [] as string[] }
]

describe('route query values', () => {
  it('reads strings, trims them and takes the first of a repeated parameter', () => {
    expect(queryText(' OP10 ')).toBe('OP10')
    expect(queryText(['', null, 'a', 'b'])).toBe('a')
    expect(queryText(null)).toBe('')
    expect(queryText(undefined)).toBe('')
    expect(queryText(42)).toBe('')
    expect(queryText([])).toBe('')
  })
})

describe('/dashboard/machines deep links', () => {
  it('parses the four parameters (and ?query= as a search alias)', () => {
    expect(parseMachinesQuery({ search: 'OP10', id: OP100, tree: OP010, decommission: 'LINE-01-OP010' })).toEqual({
      search: 'OP10', id: OP100, tree: OP010, decommission: 'LINE-01-OP010'
    })
    expect(parseMachinesQuery({ query: 'laser' }).search).toBe('laser')
    expect(parseMachinesQuery({})).toEqual({ search: '', id: '', tree: '', decommission: '' })
    expect(parseMachinesQuery(null)).toEqual({ search: '', id: '', tree: '', decommission: '' })
  })

  it('finds a station by uuid, identifier, name or handle, the uuid first', () => {
    expect(findMachineByRef(machines, OP100)!.name).toBe('L01-OP100')
    expect(findMachineByRef(machines, OP100.toUpperCase())!.name).toBe('L01-OP100')
    expect(findMachineByRef(machines, 'LINE-03-OP050')!.id).toBe(OP050)
    expect(findMachineByRef(machines, 'l01-op010')!.id).toBe(OP010)
    expect(findMachineByRef(machines, 'L01-OP1')).toBeNull()
    expect(findMachineByRef(machines, '')).toBeNull()
    expect(findMachineByRef([], OP100)).toBeNull()
    expect(findMachineByRef(null, OP100)).toBeNull()

    // An id always beats another station's name that happens to equal it
    const clash = [{ id: 'x', name: 'y' }, { id: 'y', name: 'z' }]
    expect(findMachineByRef(clash, 'y')!.name).toBe('z')
  })

  it('"Go to Machine" (?id=) narrows the directory to exactly that station', () => {
    const go = buildContextMenuActions({ entityType: 'machine', entityId: OP100, entityName: 'L01-OP100' }).find(a => a.key === 'machine')!
    const link = parseMachinesQuery(queryOf(go.route))
    const focus = findMachineByRef(machines, link.id)
    const shown = filterMachineDirectory(machines, { statusFilter: 'active_floor', search: '', focusId: focus?.id })
    expect(shown.map(m => m.id)).toEqual([OP100])
  })

  it('the focused station is shown whatever the status, type and text filters say', () => {
    const shown = filterMachineDirectory(machines, {
      statusFilter: 'active_floor',
      search: 'something else',
      focusId: OP050,
      isTypeVisible: () => false
    })
    expect(shown.map(m => m.id)).toEqual([OP050])
  })

  it('an id that names no station does not empty or narrow the directory by itself', () => {
    const shown = filterMachineDirectory(machines, { statusFilter: 'all', focusId: 'gone' })
    expect(shown).toHaveLength(3)
  })

  it('the text search matches a pasted uuid (the old filter never looked at the id)', () => {
    expect(filterMachineDirectory(machines, { statusFilter: 'all', search: OP010 }).map(m => m.id)).toEqual([OP010])
    // only a full id matches, a fragment of one does not
    expect(machineMatchesText(machines[0], OP100.slice(0, 8))).toBe(false)
  })

  it('searches names, identifiers, handle, line and controller hostnames', () => {
    const find = (search: string) => filterMachineDirectory(machines, { statusFilter: 'all', search }).map(m => m.name)
    expect(find('loader')).toEqual(['L01-OP010'])
    expect(find('LINE-03')).toEqual(['L03-OP050'])
    expect(find('body line')).toEqual(['L01-OP100'])
    expect(find('ipc-l03')).toEqual(['L03-OP050'])
    expect(find('l01-op')).toEqual(['L01-OP100', 'L01-OP010'])
    expect(find('  ')).toHaveLength(3)
    expect(find('nothing')).toEqual([])
  })

  it('applies the status tabs and the organization type filter', () => {
    const names = (filter: Parameters<typeof filterMachineDirectory>[1]) => filterMachineDirectory(machines, filter).map(m => m.name)
    expect(names({ statusFilter: 'active_floor' })).toEqual(['L01-OP100', 'L01-OP010'])
    expect(names({ statusFilter: 'maintenance' })).toEqual(['L01-OP010'])
    expect(names({ statusFilter: 'decommissioned' })).toEqual(['L03-OP050'])
    expect(names({ statusFilter: 'all' })).toHaveLength(3)
    expect(names({ statusFilter: 'all', isTypeVisible: t => t !== 'Vision' })).toEqual(['L01-OP100', 'L01-OP010'])
    expect(machineMatchesStatus({ status: 'dismantled' }, 'decommissioned')).toBe(true)
    expect(filterMachineDirectory(null)).toEqual([])
  })

  it('the search result link of a machine filters by name and names the station to open', () => {
    const link = parseMachinesQuery(queryOf(buildEntityLink('machine', OP100, 'L01-OP100')))
    expect(link).toEqual({ search: 'L01-OP100', id: '', tree: OP100, decommission: '' })
    expect(filterMachineDirectory(machines, { statusFilter: 'active_floor', search: link.search }).map(m => m.id)).toEqual([OP100])
    expect(findMachineByRef(machines, link.tree)!.id).toBe(OP100)
  })

  it('?decommission= resolves by uuid or custom identifier once the list is there', () => {
    expect(findMachineByRef([], OP010)).toBeNull()
    expect(findMachineByRef(machines, parseMachinesQuery({ decommission: OP010 }).decommission)!.name).toBe('L01-OP010')
    expect(findMachineByRef(machines, parseMachinesQuery({ decommission: 'LINE-01-OP010' }).decommission)!.id).toBe(OP010)
  })
})

describe('/dashboard/tickets deep links', () => {
  const ids = (list: Array<{ id: string }>) => list.map(t => t.id)

  it('parses the contract parameters, with ?search= as an alias of ?query=', () => {
    expect(parseTicketsQuery({ query: 'lens', stationId: OP100, handle: 'L01-OP100', team: 'Robotics Team', technician: 'Ada', ticketId: 't-1', create: 'true' })).toEqual({
      text: 'lens', stationId: OP100, handle: 'L01-OP100', team: 'Robotics Team', technician: 'Ada', ticketId: 't-1', create: true
    })
    expect(parseTicketsQuery({ search: 'TKT-0007' }).text).toBe('TKT-0007')
    expect(parseTicketsQuery({ query: 'a', search: 'b' }).text).toBe('a')
    expect(parseTicketsQuery({ create: 'yes' }).create).toBe(false)
    expect(parseTicketsQuery(undefined)).toEqual({ text: '', stationId: '', handle: '', team: '', technician: '', ticketId: '', create: false })
  })

  it('?query= matches title, number, station name, assignee, tags and description', () => {
    const find = (text: string) => ids(filterTicketsByDeepLink(tickets, { ...parseTicketsQuery({ query: text }) }))
    expect(find('misalignment')).toEqual(['t-uuid'])
    expect(find('TKT-0007')).toEqual(['t-custom'])
    expect(find('tkt-2026')).toEqual(['t-handle'])
    expect(find('Line 01 – Fastening')).toEqual(['t-handle'])
    expect(find('flowski')).toEqual(['t-custom'])
    expect(find('#quality')).toEqual(['t-handle'])
    expect(find('contrast dropped')).toEqual(['t-handle'])
    expect(find('nothing-like-this')).toEqual([])
    expect(find('')).toHaveLength(tickets.length)
  })

  it('every term has to match, in any field', () => {
    expect(ticketMatchesText(tickets[0], 'vision synthia')).toBe(true)
    expect(ticketMatchesText(tickets[0], 'vision flowski')).toBe(false)
    expect(ticketMatchesText(tickets[1], 'E-VIS-01 ipc-l01')).toBe(true)
  })

  it('the alert-rule link (?search=<ticket number>) finds that ticket', () => {
    const link = parseTicketsQuery({ search: 'TKT-20260928-0003' })
    expect(ids(filterTicketsByDeepLink(tickets, link))).toEqual(['t-handle'])
  })

  it('?stationId=<uuid>&handle=<CAD handle> matches either, exactly', () => {
    const link = parseTicketsQuery({ stationId: OP100, handle: 'L01-OP100' })
    // Without the station directory: the uuid and the handle as sent
    expect(ids(filterTicketsByDeepLink(tickets, link))).toEqual(['t-handle'])
    // "L01-OP1000" is another station, not a match for "L01-OP100"
    expect(ids(filterTicketsByDeepLink(tickets, link))).not.toContain('t-prefix')
  })

  it('with the station directory, tickets filed under any identifier of the station match', () => {
    const link = parseTicketsQuery({ stationId: OP100 })
    expect(ids(filterTicketsByDeepLink(tickets, link, machines))).toEqual(['t-handle', 't-custom'])
    // A caller that only has the handle gets the same tickets
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ handle: 'L01-OP100' }), machines))).toEqual(['t-handle', 't-custom'])
    // ... and so does one that passes the handle as stationId (legacy links)
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ stationId: 'l01-op100' }), machines))).toEqual(['t-handle', 't-custom'])
  })

  it('matches the machine uuid of backend tickets and handle tags', () => {
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ stationId: OP050 })))).toEqual(['t-uuid'])
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ stationId: OP010, handle: 'L01-OP010' })))).toEqual(['t-tag'])
    expect(ticketMatchesStation({ tags: ['#L01-OP010'] }, stationAliases(['l01-op010']))).toBe(true)
    expect(ticketMatchesStation({ machineId: OP010 }, stationAliases([OP010]))).toBe(true)
    expect(ticketMatchesStation({ stationId: OP010 }, new Set())).toBe(false)
  })

  it('a station filter never matches by title text (free text has its own parameter)', () => {
    // "misalignment" and "lens" only occur in titles
    expect(filterTicketsByDeepLink(tickets, parseTicketsQuery({ stationId: 'misalignment' }))).toEqual([])
    expect(filterTicketsByDeepLink(tickets, parseTicketsQuery({ handle: 'lens' }))).toEqual([])
    // ... while a tag is an identity the contract allows ("#Vision")
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ stationId: 'vision' })))).toEqual(['t-handle'])
  })

  it('the menu link "Go to Maintenance Tickets" of a machine selects its tickets', () => {
    const action = buildContextMenuActions({ entityType: 'machine', entityId: OP100, entityName: 'L01-OP100', handle: 'L01-OP100' }).find(a => a.key === 'tickets')!
    const link = parseTicketsQuery(queryOf(action.route))
    expect(link).toMatchObject({ stationId: OP100, handle: 'L01-OP100' })
    expect(ids(filterTicketsByDeepLink(tickets, link, machines))).toEqual(['t-handle', 't-custom'])
  })

  it('?technician= filters by assignee', () => {
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ technician: 'synthia' })))).toEqual(['t-uuid'])
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ technician: 'Nobody' })))).toEqual([])
  })

  it('?team= selects tickets of the team: tag, department, or one of its stations', () => {
    // Tagged with the team name
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ team: 'Plant Maintenance' })))).toEqual(['t-custom'])
    // With the directory: all tickets on stations the team is responsible for (OP100 and OP050)
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ team: 'plant maintenance' }), machines))).toEqual(['t-uuid', 't-handle', 't-custom'])
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ team: 'Robotics Team' }), machines))).toEqual(['t-tag'])
    // Responsible department
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ team: 'vision' })))).toEqual(['t-handle'])
    expect(ticketMatchesTeam(tickets[0], '')).toBe(true)
    expect(teamStationAliases('Robotics Team', machines)).toEqual(new Set([OP010, 'line-01-op010', 'l01-op010']))
  })

  it('combines the filters', () => {
    const link = parseTicketsQuery({ stationId: OP100, query: 'torque', technician: 'tensor' })
    expect(ids(filterTicketsByDeepLink(tickets, link, machines))).toEqual(['t-custom'])
    expect(ids(filterTicketsByDeepLink(tickets, parseTicketsQuery({ stationId: OP100, query: 'lens', technician: 'tensor' }), machines))).toEqual([])
  })

  it('does not mutate the ticket list and tolerates missing fields', () => {
    const frozen = Object.freeze([...tickets])
    expect(filterTicketsByDeepLink(frozen, parseTicketsQuery({}))).toHaveLength(tickets.length)
    expect(filterTicketsByDeepLink(null, parseTicketsQuery({ query: 'x' }))).toEqual([])
    expect(filterTicketsByDeepLink([{ id: 'bare' }], parseTicketsQuery({ query: 'x', stationId: 'y', team: 'z', technician: 'w' }))).toEqual([])
  })

  it('a search term sent as ?stationId= (older links) becomes free text once the directory is known', () => {
    const legacy = parseTicketsQuery({ stationId: 'TKT-0007' })
    // Directory not loaded yet: nothing can be concluded
    expect(normalizeTicketsDeepLink(legacy, tickets, [])).toBe(legacy)
    const effective = normalizeTicketsDeepLink(legacy, tickets, machines)
    expect(effective).toMatchObject({ stationId: '', text: 'TKT-0007' })
    expect(ids(filterTicketsByDeepLink(tickets, effective, machines))).toEqual(['t-custom'])
    // Real station references are left alone: a known station, or the station id of a ticket
    const known = parseTicketsQuery({ stationId: OP010 })
    expect(normalizeTicketsDeepLink(known, tickets, machines)).toBe(known)
    const onTicket = parseTicketsQuery({ stationId: 'UNKNOWN-STATION' })
    expect(normalizeTicketsDeepLink(onTicket, tickets, machines)).toBe(onTicket)
    const withHandle = parseTicketsQuery({ stationId: 'x', handle: 'y' })
    expect(normalizeTicketsDeepLink(withHandle, tickets, machines)).toBe(withHandle)
  })

  it('names the station of a filter for the chip', () => {
    expect(describeStationFilter({ stationId: OP100, handle: '' }, machines)).toBe('L01-OP100 – Multi-Spindle Cover Fastener')
    expect(describeStationFilter({ stationId: '', handle: 'L01-OP010' }, machines)).toBe('L01-OP010 – Loader')
    expect(describeStationFilter({ stationId: OP100, handle: 'L01-OP100' }, [])).toBe('L01-OP100')
    expect(describeStationFilter({ stationId: OP100, handle: '' })).toBe(OP100)
  })

  it('lists every identifier of a station', () => {
    expect(stationIdentifiers(machines[0])).toEqual([OP100, 'line-01-op100', 'l01-op100', 'l01-op100'])
    expect(stationIdentifiers(machines[2])).toEqual([OP050, 'line-03-op050', 'l03-op050'])
    expect(stationIdentifiers(null)).toEqual([])
    expect(stationAliases(['', null, undefined], machines).size).toBe(0)
  })

  it('a submitted global search that targets tickets is honoured as a filter', () => {
    // Whatever parameter the search uses for tickets, the page filters by it
    const route = resolveSubmitRoute('#lens', '/dashboard/overview')!
    expect(route.startsWith('/dashboard/tickets')).toBe(true)
    const link = parseTicketsQuery(queryOf(route))
    expect(link.text || link.stationId).toBe('lens')
  })
})

describe('/dashboard/map deep link', () => {
  const stations = [
    { id: OP100, name: 'L01-OP100', customIdentifier: 'LINE-01-OP100', pinnedObjectHandle: 'L01-OP100' },
    { id: OP050, name: 'L03-OP050', customIdentifier: 'LINE-03-OP050', pinnedObjectHandle: null }
  ]
  const controllers = [
    { id: 'pc-1', name: 'IPC-L02-MAIN', hostname: 'IPC-L02-MAIN', pinnedObjectHandle: 'L02-OP020' },
    { id: 'pc-2', name: 'IPC-FREE', hostname: 'IPC-FREE', pinnedObjectHandle: '' }
  ]

  it('?stationId=<handle> focuses the pinned station', () => {
    expect(resolveMapFocus('L01-OP100', stations, controllers)).toEqual({ handle: 'L01-OP100', name: 'LINE-01-OP100', resolved: true })
    expect(resolveMapFocus('l01-op100', stations, controllers)!.handle).toBe('L01-OP100')
  })

  it('accepts the machine uuid, identifier or name of a pinned station', () => {
    expect(resolveMapFocus(OP100, stations, controllers)!.handle).toBe('L01-OP100')
    expect(resolveMapFocus('LINE-01-OP100', stations, controllers)!.handle).toBe('L01-OP100')
  })

  it('resolves pinned controllers by handle, id or hostname', () => {
    expect(resolveMapFocus('L02-OP020', stations, controllers)).toEqual({ handle: 'L02-OP020', name: 'IPC-L02-MAIN', resolved: true })
    expect(resolveMapFocus('pc-1', stations, controllers)!.handle).toBe('L02-OP020')
    expect(resolveMapFocus('ipc-l02-main', stations, controllers)!.handle).toBe('L02-OP020')
  })

  it('an unpinned or unknown station is still passed on as a raw handle, marked unresolved', () => {
    expect(resolveMapFocus(OP050, stations, controllers)).toEqual({ handle: OP050, name: '', resolved: false })
    expect(resolveMapFocus('pc-2', stations, controllers)!.resolved).toBe(false)
    expect(resolveMapFocus('DXF-ONLY-7', [], [])).toEqual({ handle: 'DXF-ONLY-7', name: '', resolved: false })
  })

  it('nothing requested, nothing focused', () => {
    expect(resolveMapFocus('', stations, controllers)).toBeNull()
    expect(resolveMapFocus(undefined, stations, controllers)).toBeNull()
    expect(resolveMapFocus('  ', stations, controllers)).toBeNull()
  })

  it('the telemetry and menu links use the parameter the page reads', () => {
    const action = buildContextMenuActions({ entityType: 'machine', entityId: OP100, entityName: 'L01-OP100', handle: 'L01-OP100' }).find(a => a.key === 'map')!
    expect(queryOf(action.route)).toEqual({ stationId: 'L01-OP100' })
    expect(resolveMapFocus(queryText(queryOf(action.route).stationId), stations, controllers)!.resolved).toBe(true)
  })
})

describe('/dashboard/clients deep link', () => {
  const fleet = [
    { id: 'c-1', hostname: 'IPC-L01-01' },
    { id: 'c-2', hostname: 'IPC-L01-02' },
    { id: 'IPC-L01-01', hostname: 'IPC-CLASH' }
  ]

  it('selects by id, the id winning over a hostname match', () => {
    expect(resolveControllerSelection(fleet, 'c-2', 'IPC-L01-01')).toEqual({ controller: fleet[1], filterText: '' })
    expect(resolveControllerSelection(fleet, 'IPC-L01-01', '').controller).toBe(fleet[2])
  })

  it('falls back to the hostname, case-insensitively', () => {
    expect(resolveControllerSelection(fleet, 'gone', 'ipc-l01-02').controller).toBe(fleet[1])
    expect(resolveControllerSelection(fleet, '', 'IPC-L01-01').controller).toBe(fleet[0])
  })

  it('accepts a hostname sent as the id (tickets store the hostname)', () => {
    expect(resolveControllerSelection(fleet, 'ipc-l01-02', undefined).controller).toBe(fleet[1])
  })

  it('a hostname fragment that names no controller becomes the fleet filter', () => {
    expect(resolveControllerSelection(fleet, '', 'IPC-L0')).toEqual({ controller: null, filterText: 'IPC-L0' })
    expect(resolveControllerSelection(fleet, 'gone', '')).toEqual({ controller: null, filterText: '' })
    expect(resolveControllerSelection([], 'c-1', 'IPC-L01-01')).toEqual({ controller: null, filterText: 'IPC-L01-01' })
    expect(resolveControllerSelection(null, null, null)).toEqual({ controller: null, filterText: '' })
  })

  it('the search and menu links resolve to the controller', () => {
    const fromSearch = queryOf(buildEntityLink('controller', 'c-2', 'IPC-L01-02'))
    expect(resolveControllerSelection(fleet, fromSearch.selected, fromSearch.hostname).controller).toBe(fleet[1])
    const action = buildContextMenuActions({ entityType: 'controller', entityId: 'c-1', entityName: 'IPC-L01-01' }).find(a => a.key === 'controller')!
    const fromMenu = queryOf(action.route)
    expect(resolveControllerSelection(fleet, fromMenu.selected, fromMenu.hostname).controller).toBe(fleet[0])
  })
})
