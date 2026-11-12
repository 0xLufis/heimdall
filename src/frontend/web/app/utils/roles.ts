/**
 * Heimdall application roles and the role set behind each backend authorization policy.
 *
 * This is the single TypeScript definition used by the UI (role pickers, capability flags), the
 * Nitro BFF (route guards) and the Better-Auth configuration. It mirrors:
 *   - `HeimdallRoles`      in src/shared/App.Contracts/Security/SecurityConstants.cs
 *   - `options.AddPolicy`  in src/backend/App.Backend.Api/Program.cs
 * `tests/frontend/unit/RoleCatalogConsistency.test.ts` parses both C# files and fails when this
 * file drifts from them, so a role or policy must be changed in both places together.
 */

/** Every role a user can hold, in the order the backend declares them (most privileged first). */
export const HEIMDALL_ROLES = [
  'system_admin',
  'heimdall_admin',
  'admin',
  'plant_director',
  'it_admin',
  'it_site_admin',
  'engineering_admin',
  'plant_engineering_manager',
  'senior_engineering_manager',
  'group_leader',
  'shift_leader',
  'lead_engineer',
  'controls_engineer',
  'engineer',
  'technician',
  'operative_planner',
  'operator',
  'manager',
  'user'
] as const

export type HeimdallRole = typeof HEIMDALL_ROLES[number]

/** Role given to accounts that have none (matches the backend's and Better-Auth's default). */
export const DEFAULT_ROLE: HeimdallRole = 'user'

/** Roles accepted by each backend authorization policy (Program.cs). */
export const POLICY_ROLES = {
  SystemAdministration: ['system_admin', 'heimdall_admin', 'admin', 'plant_director'],
  ItAdministration: ['system_admin', 'it_admin', 'it_site_admin'],
  EngineeringAdministration: ['system_admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager'],
  EndpointConfigManagement: ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'group_leader', 'lead_engineer', 'engineer', 'controls_engineer'],
  RemoteExecution: ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'group_leader', 'lead_engineer', 'engineer'],
  MaintenanceOperations: ['system_admin', 'heimdall_admin', 'admin', 'plant_director', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'group_leader', 'shift_leader', 'lead_engineer', 'controls_engineer', 'engineer', 'technician', 'operative_planner', 'operator', 'manager', 'user'],
  LineStopRequest: ['system_admin', 'plant_director', 'plant_engineering_manager', 'operative_planner', 'group_leader'],
  FunctionalSettingsManagement: ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager'],
  DelegationManagement: ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager', 'group_leader', 'lead_engineer'],
  SkillsManagement: ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager', 'group_leader'],
  ComponentManagement: ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager', 'lead_engineer', 'controls_engineer', 'engineer', 'technician']
} as const satisfies Record<string, readonly HeimdallRole[]>

export type PolicyName = keyof typeof POLICY_ROLES

/**
 * Roles that may administer users through Better-Auth's admin plugin (set role, suspend, revoke
 * sessions). This is enforced by Better-Auth in the BFF, not by a backend policy.
 */
export const USER_ADMIN_ROLES = ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin'] as const satisfies readonly HeimdallRole[]

/**
 * Roles the backend promotes to IT administrators while the "Heimdall admin acts as pseudo IT
 * admin" delegation is switched on (DynamicSecurityGroupClaimsTransformer). Off by default.
 */
export const IT_ADMIN_DELEGABLE_ROLES = ['heimdall_admin', 'admin'] as const satisfies readonly HeimdallRole[]

export const ROLE_LABELS: Record<HeimdallRole, string> = {
  system_admin: 'System Administrator',
  heimdall_admin: 'Heimdall Platform Administrator',
  admin: 'Platform Administrator (legacy)',
  plant_director: 'Plant Director',
  it_admin: 'IT Infrastructure Administrator',
  it_site_admin: 'IT Site Administrator',
  engineering_admin: 'Engineering Administrator',
  plant_engineering_manager: 'Plant Engineering Manager',
  senior_engineering_manager: 'Senior Engineering Manager',
  group_leader: 'Engineering Group Leader',
  shift_leader: 'Shift Leader',
  lead_engineer: 'Lead Engineer',
  controls_engineer: 'Controls Engineer',
  engineer: 'Engineer',
  technician: 'Maintenance Technician',
  operative_planner: 'Operative Line Planner',
  operator: 'Operator',
  manager: 'Plant Operations Manager',
  user: 'User'
}

export const ROLE_DESCRIPTIONS: Record<HeimdallRole, string> = {
  system_admin: 'System Administrator: Full platform superuser authority across all modules, IT directory approvals, engineering telemetry, and user management.',
  heimdall_admin: 'Platform Administrator: Master system governance, plant organizations, audit logs, and Identity Studio console access.',
  admin: 'Platform Administrator: Master system governance, plant organizations, audit logs, and Identity Studio console access.',
  plant_director: 'Plant Director: Plant-wide governance and visibility across all organizations, final approvals, and line stop authorization.',
  it_admin: 'IT Infrastructure Administrator: Active Directory & Entra ID integration, OU Read/Write approvals, security groups, and PKI.',
  it_site_admin: 'IT Site Administrator: Site-level Active Directory & Entra ID OU approvals, security groups, PKI, and plant networks.',
  engineering_admin: 'Engineering Administrator: Technical functional settings, OT telemetry templates, machine groups, and app user administration.',
  plant_engineering_manager: 'Plant Engineering Manager: Head of site engineering. Functional settings, delegations, skills, endpoint configuration, and line stop approvals.',
  senior_engineering_manager: 'Senior Engineering Manager: Engineering administration, endpoint configuration, and remote commands for the operations they lead.',
  manager: 'Plant Line Management: Manages operational teams, technician shift rotas, and absence authorizations.',
  group_leader: 'Engineering Group Leader: Station, line, and technology dedications across production cells.',
  shift_leader: 'Technician Shift Leader: Shift maintenance scheduling and dedicated shift technicians.',
  lead_engineer: 'Lead Engineer: Endpoint configuration, remote commands, delegations, and component lifecycle for their machine groups.',
  controls_engineer: 'Controls Engineer: PLC automation, TwinCAT Copia code review, telemetry models, and machine diagnosis.',
  engineer: 'Controls Engineer: PLC automation, TwinCAT Copia code review, telemetry models, and machine diagnosis.',
  technician: 'Maintenance Technician: Incident triage, field repair dispatch, telemetry inspection, and parts management.',
  operative_planner: 'Operative Line Planner: Line stop requests, production windows, and shift quotas.',
  operator: 'Operator: Machine line operator. Incident reporting and machine status overview.',
  user: 'User: Signed-in account without a plant role. Incident reporting and machine status overview.'
}

/** Lower-cases and trims a role string; `null`/empty become an empty string. */
export function normalizeRole(role: unknown): string {
  return typeof role === 'string' ? role.trim().toLowerCase() : ''
}

/**
 * Better-Auth can store several roles as a comma-separated string. Heimdall assigns exactly one
 * role per user (the backend issues the stored string as a single role claim), but values are
 * split defensively so a stray "a,b" never matches as a whole.
 */
export function splitRoles(role: unknown): string[] {
  if (Array.isArray(role)) return role.flatMap(splitRoles)
  return normalizeRole(role).split(',').map(r => r.trim()).filter(Boolean)
}

export function isKnownRole(role: unknown): role is HeimdallRole {
  return (HEIMDALL_ROLES as readonly string[]).includes(normalizeRole(role))
}

/** True when any of the given roles is accepted by the backend policy of that name. */
export function roleSatisfies(role: unknown, policy: PolicyName): boolean {
  const allowed = POLICY_ROLES[policy] as readonly string[]
  return splitRoles(role).some(r => allowed.includes(r))
}

/**
 * Platform administrators govern every organization and are not tenant-scoped. Same definition as
 * the backend's SystemAdministration policy (TenantScopeMiddleware, MaintenanceHub).
 */
export function isPlatformAdminRole(role: unknown): boolean {
  return roleSatisfies(role, 'SystemAdministration')
}

export function isUserAdminRole(role: unknown): boolean {
  return splitRoles(role).some(r => (USER_ADMIN_ROLES as readonly string[]).includes(r))
}

/**
 * Privilege tier used to stop user administrators from escalating:
 *   3  system_admin (holds every policy)
 *   2  other platform administrators (SystemAdministration)
 *   1  IT / engineering administration roles
 *   0  everyone else
 */
export function roleRank(role: unknown): number {
  let rank = 0
  for (const r of splitRoles(role)) {
    if (r === 'system_admin') rank = Math.max(rank, 3)
    else if ((POLICY_ROLES.SystemAdministration as readonly string[]).includes(r)) rank = Math.max(rank, 2)
    else if (
      (POLICY_ROLES.ItAdministration as readonly string[]).includes(r) ||
      (POLICY_ROLES.EngineeringAdministration as readonly string[]).includes(r)
    ) rank = Math.max(rank, 1)
  }
  return rank
}

export function roleLabel(role: unknown): string {
  const r = normalizeRole(role)
  return isKnownRole(r) ? ROLE_LABELS[r] : (r || ROLE_LABELS[DEFAULT_ROLE])
}

export function roleDescription(role: unknown): string {
  const r = normalizeRole(role)
  return isKnownRole(r) ? ROLE_DESCRIPTIONS[r] : ROLE_DESCRIPTIONS[DEFAULT_ROLE]
}
