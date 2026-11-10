namespace App.Shared.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Trigger category for a plant-level Lua script. Serialized by name so the frontend contract
/// (<c>app/types/lua.ts</c>) stays string-based.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LuaScriptTriggerType>))]
public enum LuaScriptTriggerType
{
    Manual = 0,
    TelemetryChange = 1,
    MachineStateChange = 2,
    SlaPreventiveCheck = 3,
    UiExtension = 4,
    Scheduled = 5
}

/// <summary>
/// Persisted plant-specific Lua automation script executed by the sandboxed Lua engine (App.Shared.Lua).
/// </summary>
[Table("lua_scripts", Schema = "backend")]
[Index(nameof(TriggerType), nameof(IsActive))]
public class LuaScriptRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name of the script / automation rule.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the business purpose or plant workflow automated by this script.</summary>
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Trigger type for automated execution.</summary>
    public LuaScriptTriggerType TriggerType { get; set; } = LuaScriptTriggerType.Manual;

    /// <summary>Raw Lua source code.</summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    /// <summary>Whether this script is active and eligible for event hooks.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Author or user who created this script.</summary>
    [MaxLength(255)]
    public string? CreatedBy { get; set; }

    /// <summary>Timestamp when the script was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp when the script was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp of the last execution.</summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>Flag indicating if last execution succeeded.</summary>
    public bool? LastExecutionSuccess { get; set; }

    /// <summary>Last error message, if failed.</summary>
    [MaxLength(4000)]
    public string? LastExecutionError { get; set; }

    /// <summary>Custom metadata (e.g., target machines, metric patterns). Persisted as JSON in <see cref="MetadataJson"/>.</summary>
    [NotMapped]
    public Dictionary<string, string> Metadata
    {
        get => string.IsNullOrEmpty(MetadataJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson) ?? new Dictionary<string, string>();
        set => MetadataJson = value == null || value.Count == 0 ? null : JsonSerializer.Serialize(value);
    }

    /// <summary>Storage column backing <see cref="Metadata"/>.</summary>
    [JsonIgnore]
    public string? MetadataJson { get; set; }
}
