namespace App.Contracts.Security;

/// <summary>
/// Canonical role definitions used across Heimdall authentication and authorization policies.
/// Eliminates hardcoded magic strings.
/// </summary>
public static class HeimdallRoles
{
    public const string SystemAdmin = "system_admin";
    public const string HeimdallAdmin = "heimdall_admin";
    public const string Admin = "admin";
    public const string PlantDirector = "plant_director";
    public const string ItAdmin = "it_admin";
    public const string ItSiteAdmin = "it_site_admin";
    public const string EngineeringAdmin = "engineering_admin";
    public const string PlantEngineeringManager = "plant_engineering_manager";
    public const string SeniorEngineeringManager = "senior_engineering_manager";
    public const string GroupLeader = "group_leader";
    public const string ShiftLeader = "shift_leader";
    public const string LeadEngineer = "lead_engineer";
    public const string ControlsEngineer = "controls_engineer";
    public const string Engineer = "engineer";
    public const string Technician = "technician";
    public const string OperativePlanner = "operative_planner";
    public const string Operator = "operator";
    public const string Manager = "manager";
    public const string User = "user";
}

/// <summary>
/// Canonical authorization policy names declared in ASP.NET Core Program.cs.
/// </summary>
public static class AuthorizationPolicies
{
    public const string SystemAdministration = "SystemAdministration";
    public const string ItAdministration = "ItAdministration";
    public const string EngineeringAdministration = "EngineeringAdministration";
    public const string EndpointConfigManagement = "EndpointConfigManagement";
    public const string RemoteExecution = "RemoteExecution";
    public const string MaintenanceOperations = "MaintenanceOperations";
    public const string LineStopRequest = "LineStopRequest";
    public const string FunctionalSettingsManagement = "FunctionalSettingsManagement";
    public const string DelegationManagement = "DelegationManagement";
    public const string SkillsManagement = "SkillsManagement";
    public const string ComponentManagement = "ComponentManagement";
    /// <summary>
    /// Create organizations and manage the ones the caller is a member of (Heimdall administrators:
    /// every organization). Met by the roles in <see cref="OrganizationManagementRules.Roles"/> or by
    /// the explicit per-user grant <see cref="HeimdallPermissions.OrganizationsManage"/>.
    /// </summary>
    public const string OrganizationManagement = "OrganizationManagement";
}

/// <summary>
/// Who holds <see cref="AuthorizationPolicies.OrganizationManagement"/>. Mirrored by
/// <c>POLICY_ROLES.OrganizationManagement</c> / <c>ORGANIZATION_ADMIN_ROLES</c> in
/// src/frontend/web/app/utils/roles.ts (RoleCatalogConsistency.test.ts checks both).
/// </summary>
public static class OrganizationManagementRules
{
    /// <summary>Heimdall administrators: manage every organization, member or not.</summary>
    public static readonly string[] HeimdallAdminRoles =
    {
        HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin
    };

    /// <summary>
    /// Roles holding the right by role: Heimdall administrators plus middle managers and up.
    /// First-line supervisors (shift_leader), senior individual contributors (lead_engineer), the IT
    /// roles and everyone else need the explicit grant.
    /// </summary>
    public static readonly string[] Roles =
    {
        HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin,
        HeimdallRoles.PlantDirector, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager,
        HeimdallRoles.SeniorEngineeringManager, HeimdallRoles.GroupLeader, HeimdallRoles.Manager
    };
}

/// <summary>Claim types Heimdall issues besides the standard ones.</summary>
public static class HeimdallClaimTypes
{
    /// <summary>One claim per explicit per-user permission grant (auth.user.permissions).</summary>
    public const string Permission = "heimdall:permission";
}

/// <summary>
/// Fine-grained permission keys used in Security Groups.
/// </summary>
public static class HeimdallPermissions
{
    public const string SystemSettingsManage = "system:settings:manage";
    public const string SystemPkiManage = "system:pki:manage";
    public const string SystemAgentsManage = "system:agents:manage";
    public const string SystemAuditRead = "system:audit:read";

    public const string FunctionalSettingsManage = "functional:settings:manage";
    public const string MachinesManage = "functional:machines:manage";
    public const string ComponentsManage = "functional:components:manage";
    public const string DelegationsManage = "functional:delegations:manage";
    public const string TicketsManage = "functional:tickets:manage";
    public const string SkillsManage = "functional:skills:manage";
    public const string LineStopApprove = "functional:linestop:approve";
    public const string InventoryManage = "functional:inventory:manage";

    /// <summary>Explicit per-user grant of <see cref="AuthorizationPolicies.OrganizationManagement"/>.</summary>
    public const string OrganizationsManage = "organizations.manage";
}

