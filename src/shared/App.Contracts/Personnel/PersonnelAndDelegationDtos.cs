namespace App.Contracts.Personnel;

using System;
using System.Collections.Generic;
using App.Contracts.Enums;

/// <summary>
/// Catalog skill, certificate, or authorization.
/// </summary>
public class SkillDefinitionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SkillCategory Category { get; set; } = SkillCategory.Certification;
    public string? Issuer { get; set; }
    public int? ValidityMonths { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// User qualification record.
/// </summary>
public class PersonnelSkillDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public Guid SkillId { get; set; }
    public string SkillCode { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
    public DateTimeOffset CertifiedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? CertificateNumber { get; set; }
    public string? VerifiedBy { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
}

/// <summary>
/// Team, shift crew, or discipline group.
/// </summary>
public class PersonnelGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DelegationDiscipline Discipline { get; set; } = DelegationDiscipline.General;
    public string? ShiftPattern { get; set; }
    public string? OrganizationId { get; set; }
    public List<string> MemberUserIds { get; set; } = new();
    public int MemberCount => MemberUserIds.Count;
}

/// <summary>
/// Recipe for responsibility defining who acts upon an event or target.
/// </summary>
public class DelegationRecipeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DelegationDiscipline Discipline { get; set; } = DelegationDiscipline.General;
    public string? TriggerEventCategory { get; set; }
    public WorkerScheduleType AllowedSchedules { get; set; } = WorkerScheduleType.Shift | WorkerScheduleType.OfficeTime | WorkerScheduleType.OnCall;
    public List<Guid> RequiredSkillIds { get; set; } = new();
    public List<string> RequiredSkillCodes { get; set; } = new();
    public Guid? PrimaryPersonnelGroupId { get; set; }
    public string? PrimaryPersonnelGroupName { get; set; }
    public string? PrimaryUserId { get; set; }
    public string? PrimaryUserName { get; set; }
    public string? OnCallFallbackUserId { get; set; }
    public string? OnCallFallbackUserName { get; set; }
    public Guid? EscalationGroupId { get; set; }
    public string? EscalationGroupName { get; set; }
    public int EscalationTimeoutMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>IDs of 1 or more Maintenance Groups referred to by this delegation.</summary>
    public List<Guid> MaintenanceGroupIds { get; set; } = new();

    /// <summary>Display names of Maintenance Groups referred to by this delegation.</summary>
    public List<string> MaintenanceGroupNames { get; set; } = new();

    /// <summary>IDs of 1 or more Machine Groups (IMachineGroup) referred to by this delegation.</summary>
    public List<Guid> MachineGroupIds { get; set; } = new();

    /// <summary>Display names of Machine Groups referred to by this delegation.</summary>
    public List<string> MachineGroupNames { get; set; } = new();

    /// <summary>Specific target technology for technology-primary technician dedication (e.g. Laser, Welding, Robotics, Controls).</summary>
    public string? Technology { get; set; }

    /// <summary>Whether this delegation prioritizes technology-dedicated technicians/engineers primarily.</summary>
    public bool IsTechnologyPrimary { get; set; } = true;

    /// <summary>Whether this recipe designates a complete machine dedication.</summary>
    public bool IsCompleteMachineDedication { get; set; } = false;
}

/// <summary>
/// Binding linking a machine to a delegation recipe.
/// </summary>
public class MachineDelegationBindingDto
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public Guid DelegationRecipeId { get; set; }
    public string DelegationRecipeName { get; set; } = string.Empty;
    public DelegationDiscipline Discipline { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Resolved personnel evaluation outcome.
/// </summary>
public class ResolvedPersonnelDto
{
    public Guid DelegationRecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public DelegationDiscipline Discipline { get; set; }
    public string? Technology { get; set; }
    public string? EffectiveAssigneeId { get; set; }
    public string? EffectiveAssigneeName { get; set; }
    public Guid? EffectiveGroupId { get; set; }
    public string? EffectiveGroupName { get; set; }
    public bool IsOnCallFallback { get; set; }
    public bool IsEscalated { get; set; }
    public string ResolutionReason { get; set; } = string.Empty;
    public string DedicationSource { get; set; } = "default";
}

/// <summary>
/// Multi-org hierarchical security group.
/// </summary>
public class SecurityGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OrganizationId { get; set; }
    public Guid? ParentGroupId { get; set; }
    public List<string> Permissions { get; set; } = new();
    public List<string> MemberUserIds { get; set; } = new();
}

/// <summary>
/// Functional settings parameter.
/// </summary>
public class FunctionalSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public string Category { get; set; } = "General";
    public string UpdatedBy { get; set; } = "admin";
    public DateTimeOffset UpdatedAt { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Direct Dedications Architecture: Machines <-> Groups <-> Users
// ─────────────────────────────────────────────────────────────────────────────

public class DedicationAssignmentRequest
{
    public string TargetType { get; set; } = "person"; // "person" or "group"
    public string TargetId { get; set; } = string.Empty; // User ID or PersonnelGroup ID
    public string TargetName { get; set; } = string.Empty;
    public string? TargetEmail { get; set; }
    public string? Technology { get; set; }
}

public class MachineDirectDedicationDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TargetType { get; set; } = "person"; // "person" | "group"
    public string TargetId { get; set; } = string.Empty; // User ID or PersonnelGroup ID
    public string TargetName { get; set; } = string.Empty;
    public string? TargetEmail { get; set; }
    public string Technology { get; set; } = "General"; // "Controls", "Mechanical", "Vision", "General"
    public bool IsTechnologySpecific { get; set; }
    public bool IsGroupMemberOverride { get; set; }
    public string? OverriddenGroupName { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class MachineDedicationsDto
{
    public Guid MachineId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string CustomIdentifier { get; set; } = string.Empty;
    public List<string> Technologies { get; set; } = new();

    // Multiple direct machine dedications (complete machine or per-technology)
    public List<MachineDirectDedicationDto> DirectDedications { get; set; } = new();

    // Direct machine dedications (primary/fallback for backwards compatibility)
    public DedicatedPersonDto? DedicatedPerson { get; set; }
    public DedicatedGroupDto? DedicatedGroup { get; set; }

    // Inherited from machine group hierarchy (SubLine, Line, Project, BU)
    public List<InheritedDedicationDto> InheritedDedications { get; set; } = new();

    // Technological groups automatically dedicated, with superseding info
    public List<TechnologicalGroupDedicationDto> TechnologicalGroupDedications { get; set; } = new();
}

public class DedicatedPersonDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Source { get; set; } = "machine_preferred";
    public string Technology { get; set; } = "General";
    public bool IsGroupMemberOverride { get; set; }
    public string? OverriddenGroupName { get; set; }
}

public class DedicatedGroupDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public int MemberCount { get; set; }
    public List<string> MemberNames { get; set; } = new();
}

public class InheritedDedicationDto
{
    public Guid MachineGroupId { get; set; }
    public string MachineGroupName { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public DedicatedPersonDto? DedicatedPerson { get; set; }
    public DedicatedGroupDto? DedicatedGroup { get; set; }
}

public class TechnologicalGroupDedicationDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Technology { get; set; } = string.Empty;
    public bool IsAutomaticallyDedicated { get; set; } = true;
    public bool IsSuperseded { get; set; }
    public string? SupersededByUserId { get; set; }
    public string? SupersededByUserName { get; set; }
    public List<string> MemberUserIds { get; set; } = new();
    public List<string> MemberNames { get; set; } = new();
}

public class UserDedicationsDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<DedicatedMachineItemDto> DirectlyDedicatedMachines { get; set; } = new();
    public List<DedicatedMachineGroupItemDto> DedicatedMachineGroups { get; set; } = new();
    public List<UserTechnologicalCoverageDto> TechnologicalCoverage { get; set; } = new();
}

public class DedicatedMachineItemDto
{
    public Guid MachineId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string CustomIdentifier { get; set; } = string.Empty;
    public string? MachineType { get; set; }
    public string? GroupName { get; set; }
    public string Technology { get; set; } = "General";
    public bool SupersedesTechnologicalGroup { get; set; }
    public string? SupersededGroupName { get; set; }
}

public class DedicatedMachineGroupItemDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public int MachineCount { get; set; }
}

public class UserTechnologicalCoverageDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Technology { get; set; } = string.Empty;
    public List<DedicatedMachineItemDto> CoveredMachines { get; set; } = new();
}
