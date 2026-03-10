//using System;
//using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace App.Shared.Entities;

// --- DOMAIN ENTITIES ---

public class ClientPc
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Hostname { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string MachineIdentifier { get; set; } = string.Empty;

    [Required]
    [MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    public DateTimeOffset? LastOnline { get; set; }

    // JSONB Columns
    public HardwareConfig HardwareConfig { get; set; } = new();
    public SoftwareConfig SoftwareConfig { get; set; } = new();

    // Dynamic user-defined data points (Flags, WMIC data, FS data)
    public JsonDocument? CustomDataPoints { get; set; }

    // Predecessors stored as a JSONB array of objects
    public List<PcPredecessor> Predecessors { get; set; } = new();
}

public class Component
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ComponentType Type { get; set; }

    public string? Description { get; set; }

    // Consistent fields managed by admins stored as JSONB to allow flexible schema evolution
    public JsonDocument? AdminManagedFields { get; set; }
}

public enum ComponentType
{
    Hardware = 0,
    Software = 1
}

// --- JSONB POCOs ---

public class HardwareConfig
{
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    // Add other predictable HW properties here
}

public class SoftwareConfig
{
    public string OsVersion { get; set; } = string.Empty;
    public List<string> InstalledPackages { get; set; } = new();
    // Add other predictable SW properties here
}

public class PcPredecessor
{
    public string Hostname { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
}

// --- BETTER-AUTH ENTITIES ---
// Note: Better-Auth expects specific table names (usually lowercase).
// We map these explicitly so Nuxt can read/write them seamlessly.

[Table("user")]
public class AppUser
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("emailVerified")]
    public bool EmailVerified { get; set; }

    [Column("image")]
    public string? Image { get; set; }

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [Column("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("banned")]
    public bool? Banned { get; set; }

    [Column("banReason")]
    public string? BanReason { get; set; }

    [Column("banExpires")]
    public DateTime? BanExpires { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    [Column("displayUsername")]
    public string? DisplayUsername { get; set; }
}

[Table("session")]
public class AppSession
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("userId")]
    public string UserId { get; set; } = string.Empty;

    [Column("token")]
    public string Token { get; set; } = string.Empty;

    [Column("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [Column("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [Column("ipAddress")]
    public string? IpAddress { get; set; }

    [Column("userAgent")]
    public string? UserAgent { get; set; }

    [Column("impersonatedBy")]
    public string? ImpersonatedBy { get; set; }

    [Column("activeOrganizationId")]
    public string? ActiveOrganizationId { get; set; }

    [ForeignKey("UserId")]
    public AppUser User { get; set; } = null!;
}

[Table("organization")]
public class AppOrganization
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string? Slug { get; set; }

    [Column("logo")]
    public string? Logo { get; set; }

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [Column("metadata")]
    public string? Metadata { get; set; }
}

[Table("member")]
public class AppMember
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    [Column("userId")]
    public string UserId { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("OrganizationId")]
    public AppOrganization Organization { get; set; } = null!;

    [ForeignKey("UserId")]
    public AppUser User { get; set; } = null!;
}

[Table("invitation")]
public class AppInvitation
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("role")]
    public string? Role { get; set; }

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [Column("inviterId")]
    public string InviterId { get; set; } = string.Empty;

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("OrganizationId")]
    public AppOrganization Organization { get; set; } = null!;

    [ForeignKey("InviterId")]
    public AppUser Inviter { get; set; } = null!;
}

[Table("account")]
public class AppAccount
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("userId")]
    public string UserId { get; set; } = string.Empty;

    [Column("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [Column("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [Column("accessToken")]
    public string? AccessToken { get; set; }

    [Column("refreshToken")]
    public string? RefreshToken { get; set; }

    [Column("idToken")]
    public string? IdToken { get; set; }

    [Column("accessTokenExpiresAt")]
    public DateTime? AccessTokenExpiresAt { get; set; }

    [Column("refreshTokenExpiresAt")]
    public DateTime? RefreshTokenExpiresAt { get; set; }

    [Column("scope")]
    public string? Scope { get; set; }

    [Column("password")]
    public string? Password { get; set; }

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [Column("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("UserId")]
    public AppUser User { get; set; } = null!;
}

[Table("verification")]
public class AppVerification
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [Column("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [Column("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
