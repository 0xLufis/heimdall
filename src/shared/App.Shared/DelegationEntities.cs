namespace App.Shared.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Contracts.Enums;

/// <summary>
/// Binding linking a machine to a delegation recipe.
/// Allows a machine to have multiple delegation groupings across disciplines (Mechanical, Controls, Laser, etc.).
/// </summary>
[Table("machine_delegation_bindings", Schema = "backend")]
public class MachineDelegationBinding
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MachineId { get; set; }
    public Machine? Machine { get; set; }

    [Required]
    public Guid DelegationRecipeId { get; set; }
    public DelegationRecipe? DelegationRecipe { get; set; }

    public DelegationDiscipline Discipline { get; set; } = DelegationDiscipline.General;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset BoundAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Recipe for responsibility defining who acts upon an event, machine, or component.
/// </summary>
[Table("delegation_recipes", Schema = "backend")]
public class DelegationRecipe
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DelegationDiscipline Discipline { get; set; } = DelegationDiscipline.General;

    [MaxLength(128)]
    public string? TriggerEventCategory { get; set; }

    public WorkerScheduleType AllowedSchedules { get; set; } = WorkerScheduleType.Shift | WorkerScheduleType.OfficeTime | WorkerScheduleType.OnCall;

    /// <summary>JSON array of required SkillDefinition IDs (GUIDs).</summary>
    public string RequiredSkillIdsJson { get; set; } = "[]";

    /// <summary>JSON array of required skill code strings (e.g. ["CERT-LASER-4", "SKILL-TWINCAT3"]).</summary>
    public string RequiredSkillCodesJson { get; set; } = "[]";

    public Guid? PrimaryPersonnelGroupId { get; set; }
    public PersonnelGroup? PrimaryPersonnelGroup { get; set; }

    [MaxLength(128)]
    public string? PrimaryUserId { get; set; }

    [MaxLength(255)]
    public string? PrimaryUserName { get; set; }

    [MaxLength(128)]
    public string? OnCallFallbackUserId { get; set; }

    [MaxLength(255)]
    public string? OnCallFallbackUserName { get; set; }

    public Guid? EscalationGroupId { get; set; }
    public PersonnelGroup? EscalationGroup { get; set; }

    public int EscalationTimeoutMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    /// <summary>Specific target technology for technology-primary technician dedication (e.g. Laser, Welding, Robotics, Controls).</summary>
    [MaxLength(128)]
    [Column("technology")]
    public string? Technology { get; set; }

    /// <summary>Whether this delegation prioritizes technology-dedicated technicians/engineers primarily.</summary>
    [Column("is_technology_primary")]
    public bool IsTechnologyPrimary { get; set; } = true;

    /// <summary>Whether this recipe designates a complete machine dedication (dedicating technician to the entire machine).</summary>
    [Column("is_complete_machine_dedication")]
    public bool IsCompleteMachineDedication { get; set; } = false;

    /// <summary>JSON array of Maintenance / Personnel Group IDs (referring to 1 or more maintenance groups).</summary>
    [Column("maintenance_group_ids_json")]
    public string MaintenanceGroupIdsJson { get; set; } = "[]";

    /// <summary>JSON array of IMachineGroup IDs (referring to 1 or more machine groups across the hierarchy).</summary>
    [Column("machine_group_ids_json")]
    public string MachineGroupIdsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<MachineDelegationBinding> Bindings { get; set; } = new();

    /// <summary>Multi-maintenance group references decoupled from single group.</summary>
    public List<DelegationMaintenanceGroup> MaintenanceGroups { get; set; } = new();

    /// <summary>Multi-machine group references (IMachineGroup hierarchy: Business Unit, Project, Line, Sub-Line, Machine).</summary>
    public List<DelegationMachineGroup> MachineGroups { get; set; } = new();

    [NotMapped]
    public List<Guid> MaintenanceGroupIds
    {
        get
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(MaintenanceGroupIdsJson) ?? new();
                if (PrimaryPersonnelGroupId.HasValue && !list.Contains(PrimaryPersonnelGroupId.Value))
                    list.Insert(0, PrimaryPersonnelGroupId.Value);
                return list;
            }
            catch { return PrimaryPersonnelGroupId.HasValue ? new List<Guid> { PrimaryPersonnelGroupId.Value } : new(); }
        }
        set => MaintenanceGroupIdsJson = System.Text.Json.JsonSerializer.Serialize(value ?? new());
    }

    [NotMapped]
    public List<Guid> MachineGroupIds
    {
        get
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(MachineGroupIdsJson) ?? new(); }
            catch { return new(); }
        }
        set => MachineGroupIdsJson = System.Text.Json.JsonSerializer.Serialize(value ?? new());
    }
}

/// <summary>
/// Junction linking a delegation recipe to one or more maintenance / personnel groups.
/// Decouples delegations to support multiple maintenance teams.
/// </summary>
[Table("delegation_maintenance_groups", Schema = "backend")]
public class DelegationMaintenanceGroup
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DelegationRecipeId { get; set; }
    public DelegationRecipe? DelegationRecipe { get; set; }

    [Required]
    public Guid MaintenanceGroupId { get; set; }
    public PersonnelGroup? MaintenanceGroup { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Junction linking a delegation recipe to one or more machine groups (IMachineGroup).
/// Decouples delegations to support hierarchy levels: Business Unit > Project > Line > Sub-Line > Machine.
/// </summary>
[Table("delegation_machine_groups", Schema = "backend")]
public class DelegationMachineGroup
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DelegationRecipeId { get; set; }
    public DelegationRecipe? DelegationRecipe { get; set; }

    [Required]
    public Guid MachineGroupId { get; set; }
    public MachineGroup? MachineGroup { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Catalog skill, certificate, or authorization.
/// </summary>
[Table("skill_definitions", Schema = "backend")]
public class SkillDefinition
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public SkillCategory Category { get; set; } = SkillCategory.Certification;

    [MaxLength(255)]
    public string? Issuer { get; set; }

    public int? ValidityMonths { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// User qualification record associating a person with a certified skill.
/// </summary>
[Table("personnel_skills", Schema = "backend")]
public class PersonnelSkill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public Guid SkillId { get; set; }
    public SkillDefinition? Skill { get; set; }

    public DateTimeOffset CertifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }

    [MaxLength(128)]
    public string? CertificateNumber { get; set; }

    [MaxLength(128)]
    public string? VerifiedBy { get; set; }
}

/// <summary>
/// Team, shift crew, or discipline group.
/// </summary>
[Table("personnel_groups", Schema = "backend")]
public class PersonnelGroup
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DelegationDiscipline Discipline { get; set; } = DelegationDiscipline.General;

    [MaxLength(64)]
    public string? ShiftPattern { get; set; } // "Shift 1", "Shift 2", "Shift 3", "OfficeHours", "24/7 Continental"

    [MaxLength(128)]
    public string? OrganizationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<PersonnelGroupMember> Members { get; set; } = new();
}

/// <summary>
/// Membership junction linking users to a PersonnelGroup.
/// </summary>
[Table("personnel_group_members", Schema = "backend")]
public class PersonnelGroupMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PersonnelGroupId { get; set; }
    public PersonnelGroup? PersonnelGroup { get; set; }

    [Required, MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? UserName { get; set; }

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Multi-org hierarchical security group managing fine-grained privileges.
/// </summary>
[Table("security_groups", Schema = "backend")]
public class SecurityGroup
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(128)]
    public string? OrganizationId { get; set; }

    public Guid? ParentGroupId { get; set; }
    public SecurityGroup? ParentGroup { get; set; }

    /// <summary>JSON array of permission string keys.</summary>
    public string PermissionsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SecurityGroupMember> Members { get; set; } = new();
}

/// <summary>
/// Membership junction linking users to a SecurityGroup.
/// </summary>
[Table("security_group_members", Schema = "backend")]
public class SecurityGroupMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SecurityGroupId { get; set; }
    public SecurityGroup? SecurityGroup { get; set; }

    [Required, MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Plant functional setting managed by functional administrators (Admins).
/// Separated from low-level SystemSettings managed by sysadmins.
/// </summary>
[Table("functional_settings", Schema = "backend")]
public class FunctionalSetting
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string ValueJson { get; set; } = "{}";

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // Escalation, Shifts, Tolerances, Workflows

    [MaxLength(100)]
    public string UpdatedBy { get; set; } = "admin";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Junction table linking machines to manual MachineGroups.
/// </summary>
[Table("machine_group_memberships", Schema = "backend")]
public class MachineGroupMembership
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid GroupId { get; set; }
    public MachineGroup? Group { get; set; }

    [Required]
    public Guid MachineId { get; set; }
    public Machine? Machine { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(128)]
    public string? AssignedBy { get; set; }
}
