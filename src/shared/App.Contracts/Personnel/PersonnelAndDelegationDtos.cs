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
    public string? EffectiveAssigneeId { get; set; }
    public string? EffectiveAssigneeName { get; set; }
    public Guid? EffectiveGroupId { get; set; }
    public string? EffectiveGroupName { get; set; }
    public bool IsOnCallFallback { get; set; }
    public bool IsEscalated { get; set; }
    public string ResolutionReason { get; set; } = string.Empty;
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
