import { describe, it, expect } from 'vitest'
import {
  buildContextMenuActions,
  primaryLink,
  resolveContextIdentity,
  machineDirectoryLink,
  ticketsOfStationLink,
  createTicketLink,
  telemetryLink,
  mapLink,
  contextBadge,
  contextDisplayName,
  contextCopyValue,
  searchResultContext,
  rowContext,
  clampMenuPosition
} from '../../../src/frontend/web/app/utils/contextMenuActions'
import { buildEntityLink, resolveResultLink } from '../../../src/frontend/web/app/utils/search/searchNavigation'
import type { ContextMenuAction, ContextMenuContextData } from '../../../src/frontend/web/app/types/contextMenu'

const MACHINE_ID = '008ed0dd-9223-54b8-a191-8d76b5af4f3f'
const CONTROLLER_ID = 'c0ffee00-1111-4222-8333-444455556666'

const keys = (actions: ContextMenuAction[]) => actions.map(a => a.key)
const byKey = (actions: ContextMenuAction[], key: string) => actions.find(a => a.key === key)
const query = (route: string) => Object.fromEntries(new URL(route, 'http://heimdall.test').searchParams)
const path = (route: string) => new URL(route, 'http://heimdall.test').pathname

const machine: ContextMenuContextData = {
  entityType: 'machine',
  entityId: MACHINE_ID,
  entityName: 'L01-OP100 – Multi-Spindle Cover Fastener',
  handle: 'L01-OP100',
  machineId: MACHINE_ID,
  machineName: 'L01-OP100',
  controllerId: CONTROLLER_ID,
  controllerHostname: 'IPC-L01-OP100',
  ownerTeam: { name: 'Plant Maintenance' },
  ownerPerson: { name: 'Elena Engineer' },
  tickets: [{ id: 't-1', title: 'Lens dirty', status: 'Open' }, { id: 't-2', title: 'Jam', status: 'InProgress' }]
}

describe('context menu action rules', () => {
  describe('machine', () => {
    const actions = buildContextMenuActions(machine)

    it('offers every station action, in a stable order', () => {
      expect(keys(actions)).toEqual([
        'machine', 'controller', 'new-tab', 'owner-team', 'owner-person',
        'map', 'tickets', 'create-ticket', 'telemetry', 'component-tree', 'decommission'
      ])
      expect(actions.map(a => a.group)).toEqual([
        'navigate', 'navigate', 'navigate', 'navigate', 'navigate',
        'work', 'work', 'work', 'work', 'work', 'danger'
      ])
    })

    it('"Go to Machine" focuses the station by id in the directory', () => {
      const go = byKey(actions, 'machine')!
      expect(go.label).toBe('Go to Machine')
      expect(path(go.route)).toBe('/dashboard/machines')
      expect(query(go.route)).toEqual({ id: MACHINE_ID })
    })

    it('"Inspect Component Tree" and "Open in New Tab" use the link a search result opens', () => {
      const searchLink = buildEntityLink('machine', MACHINE_ID, 'L01-OP100')
      expect(byKey(actions, 'component-tree')!.route).toBe(searchLink)
      expect(byKey(actions, 'new-tab')!.route).toBe(searchLink)
      expect(byKey(actions, 'new-tab')!.newTab).toBe(true)
      expect(query(searchLink)).toEqual({ search: 'L01-OP100', tree: MACHINE_ID })
    })

    it('sends the controller to the clients page by id and hostname', () => {
      const node = byKey(actions, 'controller')!
      expect(node.label).toBe('Go to Node / Controller')
      expect(path(node.route)).toBe('/dashboard/clients')
      expect(query(node.route)).toEqual({ selected: CONTROLLER_ID, hostname: 'IPC-L01-OP100' })
    })

    it('filters tickets by machine id and floor-plan handle and shows the ticket count', () => {
      const tickets = byKey(actions, 'tickets')!
      expect(path(tickets.route)).toBe('/dashboard/tickets')
      expect(query(tickets.route)).toEqual({ stationId: MACHINE_ID, handle: 'L01-OP100' })
      expect(tickets.badge).toBe(2)
    })

    it('pre-fills the new incident form with the station and its controller hostname', () => {
      const create = byKey(actions, 'create-ticket')!
      expect(path(create.route)).toBe('/dashboard/tickets/new')
      expect(query(create.route)).toEqual({ stationId: MACHINE_ID, stationName: 'L01-OP100', controllerId: 'IPC-L01-OP100' })
    })

    it('opens telemetry on the station, not on its first controller', () => {
      expect(query(byKey(actions, 'telemetry')!.route)).toEqual({ station: MACHINE_ID })
    })

    it('shows the station on the plant map by handle, except on the map page itself', () => {
      expect(byKey(actions, 'map')!.route).toBe('/dashboard/map?stationId=L01-OP100')
      const onMap = buildContextMenuActions(machine, { currentPath: '/dashboard/map' })
      expect(keys(onMap)).not.toContain('map')
    })

    it('decommissions by machine id', () => {
      const d = byKey(actions, 'decommission')!
      expect(d.group).toBe('danger')
      expect(query(d.route)).toEqual({ decommission: MACHINE_ID })
    })

    it('team and person lead to their tickets', () => {
      expect(byKey(actions, 'owner-team')!.label).toBe('Owner Team: Plant Maintenance')
      expect(query(byKey(actions, 'owner-team')!.route)).toEqual({ team: 'Plant Maintenance' })
      expect(byKey(actions, 'owner-person')!.label).toBe('Owner Person: Elena Engineer')
      expect(query(byKey(actions, 'owner-person')!.route)).toEqual({ technician: 'Elena Engineer' })
    })

    it('a machine known only by entityId / entityName still resolves', () => {
      const bare = buildContextMenuActions({ entityType: 'machine', entityId: MACHINE_ID, entityName: 'OP10' })
      expect(keys(bare)).toEqual(['machine', 'new-tab', 'tickets', 'create-ticket', 'telemetry', 'component-tree', 'decommission'])
      expect(query(byKey(bare, 'tickets')!.route)).toEqual({ stationId: MACHINE_ID })
      expect(byKey(bare, 'tickets')!.badge).toBeUndefined()
      expect(query(byKey(bare, 'create-ticket')!.route)).toEqual({ stationId: MACHINE_ID, stationName: 'OP10' })
    })

    it('without an id only the name-based entries remain', () => {
      const named = buildContextMenuActions({ entityType: 'machine', entityName: 'OP10' })
      expect(keys(named)).toEqual(['machine', 'new-tab'])
      expect(byKey(named, 'machine')!.route).toBe('/dashboard/machines?search=OP10')
    })
  })

  describe('controller', () => {
    const controller: ContextMenuContextData = {
      entityType: 'controller',
      entityId: CONTROLLER_ID,
      entityName: 'IPC-L01-01',
      controllerId: CONTROLLER_ID,
      controllerHostname: 'IPC-L01-01'
    }

    it('never treats the controller id as a station id', () => {
      const actions = buildContextMenuActions(controller)
      expect(keys(actions)).toEqual(['controller', 'new-tab', 'create-ticket', 'telemetry'])
      for (const a of actions) {
        expect(a.route).not.toContain('/dashboard/machines')
        expect(query(a.route).stationId).toBeUndefined()
        expect(query(a.route).tree).toBeUndefined()
        expect(query(a.route).decommission).toBeUndefined()
      }
      expect(query(byKey(actions, 'telemetry')!.route)).toEqual({ controllerId: CONTROLLER_ID })
      expect(query(byKey(actions, 'create-ticket')!.route)).toEqual({ controllerId: 'IPC-L01-01' })
      expect(byKey(actions, 'controller')!.route).toBe(buildEntityLink('controller', CONTROLLER_ID, 'IPC-L01-01'))
      expect(byKey(actions, 'new-tab')!.route).toBe(byKey(actions, 'controller')!.route)
    })

    it('gains the station entries when it is bound to a station, but no decommission', () => {
      const actions = buildContextMenuActions({ ...controller, machineId: MACHINE_ID, machineName: 'L01-OP100', handle: 'IPC-L01' })
      expect(keys(actions)).toEqual(['machine', 'controller', 'new-tab', 'map', 'tickets', 'create-ticket', 'telemetry', 'component-tree'])
      expect(query(byKey(actions, 'machine')!.route)).toEqual({ id: MACHINE_ID })
      expect(query(byKey(actions, 'tickets')!.route)).toEqual({ stationId: MACHINE_ID, handle: 'IPC-L01' })
      // The controller's own telemetry link stays controller-scoped
      expect(query(byKey(actions, 'telemetry')!.route)).toEqual({ controllerId: CONTROLLER_ID })
    })

    it('uses entityId when controllerId is not passed separately', () => {
      const actions = buildContextMenuActions({ entityType: 'controller', entityId: 'ctrl-9', entityName: 'IPC-9' })
      expect(query(byKey(actions, 'controller')!.route)).toEqual({ selected: 'ctrl-9', hostname: 'IPC-9' })
    })
  })

  describe('ticket', () => {
    const ticket: ContextMenuContextData = {
      entityType: 'ticket',
      entityId: 'tkt-42',
      entityName: 'Vision system misalignment',
      entityCode: 'TKT-20261101-0042'
    }

    it('opens the ticket detail route, not a query on the list', () => {
      const actions = buildContextMenuActions(ticket)
      expect(keys(actions)).toEqual(['open', 'new-tab'])
      expect(byKey(actions, 'open')!.label).toBe('Open Ticket')
      expect(byKey(actions, 'open')!.route).toBe('/dashboard/tickets/tkt-42')
      expect(byKey(actions, 'new-tab')!.route).toBe('/dashboard/tickets/tkt-42')
      expect(byKey(actions, 'new-tab')!.newTab).toBe(true)
    })

    it('never uses the ticket id as a station or machine id', () => {
      for (const a of buildContextMenuActions(ticket)) {
        expect(a.route).not.toContain('/dashboard/machines')
        expect(a.route).not.toContain('stationId')
        expect(a.route).not.toContain('ticketId=')
      }
    })

    it('adds the station, controller and assignee entries a ticket row really has', () => {
      const actions = buildContextMenuActions({
        ...ticket,
        machineId: MACHINE_ID,
        machineName: 'L03-OP050',
        controllerId: 'ctrl-301',
        controllerHostname: 'IPC-L03-05',
        ownerPerson: { name: 'Synthia Hal-Zero' }
      })
      expect(keys(actions)).toEqual([
        'open', 'machine', 'controller', 'new-tab', 'owner-person',
        'tickets', 'create-ticket', 'telemetry', 'component-tree'
      ])
      expect(byKey(actions, 'owner-person')!.label).toBe('Assignee: Synthia Hal-Zero')
      expect(query(byKey(actions, 'machine')!.route)).toEqual({ id: MACHINE_ID })
      expect(query(byKey(actions, 'tickets')!.route)).toEqual({ stationId: MACHINE_ID })
      expect(query(byKey(actions, 'telemetry')!.route)).toEqual({ station: MACHINE_ID })
      expect(query(byKey(actions, 'component-tree')!.route)).toEqual({ search: 'L03-OP050', tree: MACHINE_ID })
      expect(keys(actions)).not.toContain('decommission')
    })

    it('encodes ids with reserved characters', () => {
      expect(primaryLink({ entityType: 'ticket', entityId: 'tkt/7 a' })).toBe('/dashboard/tickets/tkt%2F7%20a')
    })
  })

  describe('inventory and user', () => {
    it('an inventory item opens its detail page and nothing station-related', () => {
      const actions = buildContextMenuActions({ entityType: 'inventory', entityId: 'part-1', entityName: 'Optic Head', entityCode: 'PRT-OP10-01' })
      expect(keys(actions)).toEqual(['open', 'new-tab'])
      expect(byKey(actions, 'open')!.label).toBe('Open Inventory Item')
      expect(byKey(actions, 'open')!.route).toBe('/dashboard/inventory/part-1')
    })

    it('an installed part links to its machine and component tree only', () => {
      const actions = buildContextMenuActions({ entityType: 'inventory', entityId: 'part-1', entityName: 'Optic Head', machineId: MACHINE_ID, machineName: 'L01-OP100' })
      expect(keys(actions)).toEqual(['open', 'machine', 'new-tab', 'component-tree'])
      expect(query(byKey(actions, 'component-tree')!.route)).toEqual({ search: 'L01-OP100', tree: MACHINE_ID })
    })

    it('a user opens the profile and their assigned tickets', () => {
      const actions = buildContextMenuActions({ entityType: 'user', entityId: 'usr-7', entityName: 'Ada Admin', entityCode: 'ada@heimdall.test' })
      expect(keys(actions)).toEqual(['open', 'new-tab', 'owner-person'])
      expect(byKey(actions, 'open')!.label).toBe('Open User Profile')
      expect(byKey(actions, 'open')!.route).toBe('/dashboard/users/usr-7')
      expect(byKey(actions, 'owner-person')!.label).toBe('Tickets Assigned to Ada Admin')
      expect(query(byKey(actions, 'owner-person')!.route)).toEqual({ technician: 'Ada Admin' })
    })

    it('a user without an id has no profile entry', () => {
      expect(keys(buildContextMenuActions({ entityType: 'user', entityName: 'Ada Admin' }))).toEqual(['owner-person'])
    })
  })

  describe('map node and general', () => {
    it('an unbound CAD node is addressed by its handle only', () => {
      const actions = buildContextMenuActions({ entityType: 'map-node', entityId: 'OP20_WELD', entityName: 'WELD_CELL', handle: 'OP20_WELD' })
      expect(keys(actions)).toEqual(['new-tab', 'map', 'tickets', 'create-ticket', 'telemetry'])
      expect(byKey(actions, 'new-tab')!.route).toBe('/dashboard/map?stationId=OP20_WELD')
      expect(query(byKey(actions, 'tickets')!.route)).toEqual({ handle: 'OP20_WELD' })
      expect(query(byKey(actions, 'create-ticket')!.route)).toEqual({ stationId: 'OP20_WELD' })
      expect(query(byKey(actions, 'telemetry')!.route)).toEqual({ station: 'OP20_WELD' })
      // The handle is not a machine id
      expect(actions.some(a => a.route.includes('/dashboard/machines'))).toBe(false)
    })

    it('a CAD node resolved to a station behaves like that station without decommission', () => {
      const actions = buildContextMenuActions({
        entityType: 'map-node', handle: 'OP20_WELD', machineId: 'm-op20', machineName: 'OP20 Laser Cell',
        controllerId: 'c-2', controllerHostname: 'IPC-L1-02'
      }, { currentPath: '/dashboard/map' })
      expect(keys(actions)).toEqual(['machine', 'controller', 'new-tab', 'tickets', 'create-ticket', 'telemetry', 'component-tree'])
      expect(byKey(actions, 'new-tab')!.route).toBe(buildEntityLink('machine', 'm-op20', 'OP20 Laser Cell'))
    })

    it('a general context has no navigation entries', () => {
      expect(buildContextMenuActions({ entityType: 'general', entityName: 'Master Production Hall', handle: 'production_hall' })).toEqual([])
      expect(buildContextMenuActions({})).toEqual([])
      expect(buildContextMenuActions(null)).toEqual([])
      expect(primaryLink({ entityType: 'general', entityName: 'x' })).toBeNull()
      expect(primaryLink(null)).toBeNull()
    })

    it('placeholder ids never deep-link', () => {
      expect(keys(buildContextMenuActions({ entityType: 'ticket', entityId: 'ticket-unknown', entityName: 'x' }))).toEqual([])
      expect(resolveContextIdentity({ entityType: 'machine', entityId: 'undefined' }).machineId).toBe('')
      expect(machineDirectoryLink('machine-unknown', 'OP10')).toBe('/dashboard/machines?search=OP10')
      expect(machineDirectoryLink('', '')).toBe('/dashboard/machines')
    })
  })

  describe('primary link agrees with search navigation', () => {
    const cases: Array<[string, any]> = [
      ['machine', { id: MACHINE_ID, name: 'L01-OP100', itemType: 'machine', sourceTable: 'machines' }],
      ['controller', { id: CONTROLLER_ID, name: 'IPC-L01-01', itemType: 'clientpc', sourceTable: 'nodes' }],
      ['ticket', { id: 'tkt-42', name: 'Vision misalignment', itemType: 'ticket', sourceTable: 'tickets' }],
      ['user', { id: 'usr-7', name: 'Ada Admin', itemType: 'user', sourceTable: 'users' }],
      ['inventory', { id: 'part-1', name: 'Optic Head', itemType: 'Hardware', sourceTable: 'inventory' }],
      ['telemetry', { id: 'cfg-1', name: 'Spindle Load', itemType: 'telemetry', sourceTable: 'telemetry' }]
    ]

    it.each(cases)('%s result: menu primary link equals the result link', (_kind, item) => {
      const ctx = searchResultContext(item)!
      expect(primaryLink(ctx)).toBe(resolveResultLink(item))
    })

    it('an explicit result link wins', () => {
      const ctx = searchResultContext({ id: 'x', name: 'X', itemType: 'machine', link: '/dashboard/custom?x=1' })!
      expect(primaryLink(ctx)).toBe('/dashboard/custom?x=1')
      expect(byKey(buildContextMenuActions(ctx), 'new-tab')!.route).toBe('/dashboard/custom?x=1')
    })

    it('maps result kinds onto entity types and keeps the filter label', () => {
      expect(searchResultContext({ id: MACHINE_ID, name: 'L01-OP100', displayName: 'Cover Fastener', itemType: 'station' })).toMatchObject({
        entityType: 'machine', entityId: MACHINE_ID, entityName: 'Cover Fastener', machineId: MACHINE_ID, machineName: 'L01-OP100'
      })
      expect(searchResultContext({ id: 'c1', name: 'IPC-1', itemType: 'endpoint' })).toMatchObject({ entityType: 'controller', controllerId: 'c1', controllerHostname: 'IPC-1' })
      expect(searchResultContext({ id: 'p1', name: 'Bearing', itemType: 'Hardware' })!.entityType).toBe('inventory')
      expect(searchResultContext({ id: 'machine-unknown', name: 'Ghost', itemType: 'machine' })!.entityId).toBeUndefined()
      expect(searchResultContext(null)).toBeNull()
    })

    it('external or malformed links are ignored', () => {
      expect(primaryLink({ entityType: 'ticket', entityId: 't1', link: 'https://evil.example/x' })).toBe('/dashboard/tickets/t1')
      expect(primaryLink({ entityType: 'general', link: 'javascript:alert(1)' })).toBeNull()
    })
  })

  describe('link helpers', () => {
    it('station tickets link carries whichever identifiers exist', () => {
      expect(ticketsOfStationLink(MACHINE_ID, 'L01-OP100')).toBe(`/dashboard/tickets?stationId=${MACHINE_ID}&handle=L01-OP100`)
      expect(ticketsOfStationLink('', 'L01-OP100')).toBe('/dashboard/tickets?handle=L01-OP100')
      expect(ticketsOfStationLink(MACHINE_ID)).toBe(`/dashboard/tickets?stationId=${MACHINE_ID}`)
      expect(ticketsOfStationLink()).toBe('/dashboard/tickets')
    })

    it('create link never invents a station name for a bare controller', () => {
      expect(createTicketLink({ machineId: '', machineName: 'Stale Name', handle: '', controllerHostname: 'IPC-1' })).toBe('/dashboard/tickets/new?controllerId=IPC-1')
    })

    it('telemetry link falls back from station to controller', () => {
      expect(telemetryLink({ type: 'ticket', machineId: '', handle: '', controllerId: 'c1' })).toBe('/dashboard/telemetry?controllerId=c1')
      expect(telemetryLink({ type: 'ticket', machineId: '', handle: 'H1', controllerId: '' })).toBeNull()
      expect(telemetryLink({ type: 'user', machineId: '', handle: '', controllerId: '' })).toBeNull()
    })

    it('query values are URL-encoded', () => {
      expect(mapLink('L01 OP/10&x')).toBe('/dashboard/map?stationId=L01+OP%2F10%26x')
      expect(query(mapLink('L01 OP/10&x'))).toEqual({ stationId: 'L01 OP/10&x' })
      const team = buildContextMenuActions({ entityType: 'machine', entityId: 'm1', ownerTeam: { name: 'R&D / Vision' } })
      expect(query(byKey(team, 'owner-team')!.route)).toEqual({ team: 'R&D / Vision' })
    })
  })

  describe('header and copy value', () => {
    it('labels each entity type', () => {
      expect(contextBadge({ entityType: 'map-node' })).toBe('CAD NODE')
      expect(contextBadge({ entityType: 'machine' })).toBe('MACHINE')
      expect(contextBadge({ entityType: 'controller' })).toBe('CONTROLLER')
      expect(contextBadge({ entityType: 'ticket' })).toBe('TICKET')
      expect(contextBadge({ entityType: 'inventory' })).toBe('INVENTORY')
      expect(contextBadge({ entityType: 'user' })).toBe('USER')
      expect(contextBadge({ entityType: 'general' })).toBe('ASSET')
      expect(contextBadge({})).toBe('ASSET')
      expect(contextBadge(null)).toBe('ASSET')
    })

    it('picks the most specific display name', () => {
      expect(contextDisplayName(null)).toBe('Entity')
      expect(contextDisplayName({})).toBe('Selected Asset')
      expect(contextDisplayName({ handle: 'H1' })).toBe('H1')
      expect(contextDisplayName({ handle: 'H1', controllerHostname: 'IPC-1' })).toBe('IPC-1')
      expect(contextDisplayName({ handle: 'H1', machineName: 'OP10', controllerHostname: 'IPC-1' })).toBe('OP10')
      expect(contextDisplayName({ entityName: 'Named', machineName: 'OP10' })).toBe('Named')
    })

    it('copies the handle, else the human code, else the id', () => {
      expect(contextCopyValue({ handle: 'L01-OP100', entityId: MACHINE_ID })).toBe('L01-OP100')
      expect(contextCopyValue({ entityType: 'ticket', entityId: 'tkt-42', entityCode: 'TKT-0042' })).toBe('TKT-0042')
      expect(contextCopyValue({ entityType: 'user', entityId: 'usr-7', entityName: 'Ada' })).toBe('usr-7')
      expect(contextCopyValue({ entityName: 'Master Production Hall' })).toBe('Master Production Hall')
      expect(contextCopyValue(null)).toBe('')
    })
  })

  describe('row records (v-ctx:<kind>)', () => {
    it('a ticket row carries its number, station, controller and assignee', () => {
      const ctx = rowContext('ticket', {
        id: 'tkt-42', ticketNumber: 'TKT-0042', title: 'Lens dirty', stationId: MACHINE_ID, stationName: 'L03-OP050',
        controllerId: 'ctrl-301', controllerName: 'IPC-L03-05', assignedTechnicianName: 'Synthia Hal-Zero'
      })!
      expect(ctx).toEqual({
        entityType: 'ticket', entityId: 'tkt-42', entityName: 'Lens dirty', entityCode: 'TKT-0042',
        machineId: MACHINE_ID, machineName: 'L03-OP050', controllerId: 'ctrl-301', controllerHostname: 'IPC-L03-05',
        ownerPerson: { name: 'Synthia Hal-Zero' }
      })
      expect(keys(buildContextMenuActions(ctx))).toEqual([
        'open', 'machine', 'controller', 'new-tab', 'owner-person', 'tickets', 'create-ticket', 'telemetry', 'component-tree'
      ])
      // A ticket without station data gets none of the station entries
      expect(keys(buildContextMenuActions(rowContext('ticket', { id: 'tkt-1', ticketNumber: 'TKT-1', title: 'x' })))).toEqual(['open', 'new-tab'])
    })

    it('an inventory row links to its detail page and to the machine it is installed in', () => {
      const ctx = rowContext('inventory', { id: 'part-1', name: 'Optic Head', customIdentifier: 'PRT-OP10-01', linkedMachineId: MACHINE_ID, linkedMachineName: 'L01-OP100' })!
      expect(ctx).toMatchObject({ entityType: 'inventory', entityId: 'part-1', entityName: 'Optic Head', entityCode: 'PRT-OP10-01', machineId: MACHINE_ID })
      expect(keys(buildContextMenuActions(ctx))).toEqual(['open', 'machine', 'new-tab', 'component-tree'])
      expect(keys(buildContextMenuActions(rowContext('inventory', { id: 'part-2', name: 'Bearing' })))).toEqual(['open', 'new-tab'])
    })

    it('a controller row uses its first controlled station and owning team', () => {
      const ctx = rowContext('controller', {
        id: CONTROLLER_ID, name: 'ipc', hostname: 'IPC-L01-01', pinnedObjectHandle: 'IPC-L01',
        controlledMachines: [{ id: MACHINE_ID, name: 'L01-OP100' }], responsibleTeams: [{ name: 'Controls' }]
      })!
      expect(ctx).toEqual({
        entityType: 'controller', entityId: CONTROLLER_ID, entityName: 'IPC-L01-01', handle: 'IPC-L01',
        controllerId: CONTROLLER_ID, controllerHostname: 'IPC-L01-01', machineId: MACHINE_ID, machineName: 'L01-OP100',
        ownerTeam: { name: 'Controls' }
      })
      expect(rowContext('controller', { id: 'c', hostname: 'h' })!.machineId).toBeUndefined()
    })

    it('user, machine and station KPI rows', () => {
      expect(rowContext('user', { id: 'u-1', name: 'Ada', email: 'ada@heimdall.test' })).toEqual({
        entityType: 'user', entityId: 'u-1', entityName: 'Ada', entityCode: 'ada@heimdall.test'
      })
      expect(rowContext('machine', {
        id: MACHINE_ID, name: 'L01-OP100', displayName: 'Cover Fastener', pinnedObjectHandle: 'L01-OP100',
        controllers: [{ id: 'c-1', hostname: 'IPC-1' }], preferredTechnicianName: 'Elena'
      })).toMatchObject({
        entityType: 'machine', entityId: MACHINE_ID, entityName: 'Cover Fastener', machineName: 'L01-OP100',
        handle: 'L01-OP100', controllerId: 'c-1', controllerHostname: 'IPC-1', ownerPerson: { name: 'Elena' }
      })
      const station = rowContext('station', { stationId: MACHINE_ID, handle: 'L01-OP100', name: 'L01-OP100' })!
      expect(station).toEqual({ entityType: 'machine', entityId: MACHINE_ID, entityName: 'L01-OP100', handle: 'L01-OP100', machineId: MACHINE_ID, machineName: 'L01-OP100' })
      expect(query(byKey(buildContextMenuActions(station), 'telemetry')!.route)).toEqual({ station: MACHINE_ID })
    })

    it('search rows, pass-through and empty values', () => {
      expect(rowContext('search', { id: 'c-9', name: 'IPC-9', itemType: 'endpoint' })!.entityType).toBe('controller')
      const literal = { entityType: 'machine' as const, entityId: 'm' }
      expect(rowContext(undefined, literal)).toBe(literal)
      expect(rowContext('ticket', null)).toBeNull()
      expect(rowContext('ticket', 'x')).toBeNull()
    })
  })

  describe('viewport clamping', () => {
    const viewport = { width: 1024, height: 768 }

    it('leaves a menu that fits where the pointer is', () => {
      expect(clampMenuPosition({ x: 150, y: 200 }, { width: 256, height: 420 }, viewport)).toEqual({ x: 150, y: 200 })
    })

    it('shifts a menu that would overflow the right or bottom edge', () => {
      expect(clampMenuPosition({ x: 1000, y: 700 }, { width: 256, height: 420 }, viewport)).toEqual({ x: 1024 - 256 - 12, y: 768 - 420 - 12 })
    })

    it('uses the real height: a tall menu near the bottom ends 12px above the edge', () => {
      const pos = clampMenuPosition({ x: 300, y: 500 }, { width: 256, height: 470 }, viewport)
      expect(pos.y + 470).toBe(768 - 12)
      expect(pos.x).toBe(300)
    })

    it('keeps the padding on the leading edges', () => {
      expect(clampMenuPosition({ x: 2, y: -40 }, { width: 256, height: 300 }, viewport)).toEqual({ x: 12, y: 12 })
    })

    it('pins a menu larger than the viewport to the top-left padding', () => {
      expect(clampMenuPosition({ x: 200, y: 300 }, { width: 256, height: 900 }, { width: 240, height: 400 })).toEqual({ x: 12, y: 12 })
    })

    it('honours a custom padding and rounds to whole pixels', () => {
      expect(clampMenuPosition({ x: 100.6, y: 50.2 }, { width: 100, height: 100 }, viewport, 0)).toEqual({ x: 101, y: 50 })
    })
  })
})
