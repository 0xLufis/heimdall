/**
 * Types of the global right-click menu. They live here (not in `utils/` or `composables/`, which
 * Nuxt auto-imports) so every name has exactly one exporting module; import them explicitly.
 */

export type ContextEntityType = 'map-node' | 'machine' | 'controller' | 'ticket' | 'inventory' | 'user' | 'general'

/** What a bound element (`v-ctx`) or an explicit `openContextMenu` call says about its entity. */
export interface ContextMenuContextData {
  entityType?: ContextEntityType
  /** Id of the entity itself (machine uuid, controller id, ticket id, part id, user id). */
  entityId?: string
  entityName?: string
  /** Human-readable identifier (ticket number, part code, e-mail); preferred by "Copy". */
  entityCode?: string
  /** CAD / floor-plan handle of the station or controller. */
  handle?: string
  machineId?: string
  machineName?: string
  controllerId?: string
  controllerHostname?: string
  ownerTeam?: { id?: string; name: string }
  ownerPerson?: { id?: string; name: string; email?: string }
  tickets?: Array<{ id: string; title: string; status: string }>
  /** Explicit deep link of the entity; wins over the derived one (search results carry one). */
  link?: string
  raw?: any
}

export type ContextMenuActionKey =
  | 'open'
  | 'machine'
  | 'controller'
  | 'new-tab'
  | 'owner-team'
  | 'owner-person'
  | 'map'
  | 'tickets'
  | 'create-ticket'
  | 'telemetry'
  | 'component-tree'
  | 'decommission'

export type ContextMenuActionGroup = 'navigate' | 'work' | 'danger'

export interface ContextMenuAction {
  key: ContextMenuActionKey
  label: string
  group: ContextMenuActionGroup
  /** In-app path including the query string. */
  route: string
  newTab?: boolean
  /** Count shown next to the label (e.g. open tickets of the station). */
  badge?: number
}

export interface ContextMenuActionOptions {
  /** Path of the page the menu is opened on; destinations that are this page are left out. */
  currentPath?: string
}

/** The ids a context really carries, after applying "the entity's own id counts for its kind". */
export interface ContextIdentity {
  type: ContextEntityType
  entityId: string
  entityName: string
  machineId: string
  machineName: string
  controllerId: string
  controllerHostname: string
  handle: string
}

export interface MenuPoint { x: number; y: number }
export interface MenuSize { width: number; height: number }
