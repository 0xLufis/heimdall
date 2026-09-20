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
}
