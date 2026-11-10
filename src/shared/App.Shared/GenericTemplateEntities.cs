namespace App.Shared.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Universal generic template entity representing reusable blueprints across
/// machine components, delegations, telemetry probing, ticketing, and inventory assets.
/// </summary>
[Table("generic_templates", Schema = "backend")]
public class GenericTemplate
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Type domain: "component", "delegation", "telemetry", "ticket", "asset", "custom"
    /// </summary>
    [Required, MaxLength(64)]
    [Column("template_type")]
    public string TemplateType { get; set; } = "component";

    /// <summary>Unique semantic business code (e.g. CMP-SERVO-01, DLG-CTRL-01).</summary>
    [Required, MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Column("description")]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    [Column("category")]
    public string Category { get; set; } = "General";

    [MaxLength(64)]
    [Column("icon")]
    public string? Icon { get; set; } = "Layers";

    [MaxLength(32)]
    [Column("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>Optional parent template for single prototype inheritance/extension.</summary>
    [Column("extends_template_id")]
    public Guid? ExtendsTemplateId { get; set; }
    public GenericTemplate? ExtendsTemplate { get; set; }

    /// <summary>Semantic classification tags.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>JSON serialized list of TemplateFieldDefinitionDto.</summary>
    [Column("schema_json")]
    public string SchemaJson { get; set; } = "[]";

    /// <summary>JSON serialized dictionary of default values / configuration payload.</summary>
    [Column("default_values_json")]
    public string DefaultValuesJson { get; set; } = "{}";

    /// <summary>Optional UI layout and visual editor configuration JSON.</summary>
    [Column("ui_config_json")]
    public string? UiConfigJson { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_builtin")]
    public bool IsBuiltin { get; set; } = false;

    [MaxLength(128)]
    [Column("organization_id")]
    public string? OrganizationId { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
