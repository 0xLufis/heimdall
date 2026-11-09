namespace App.Contracts.Templates;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class GenericTemplateTypes
{
    public const string Machine = "machine";
    public const string Component = "component";
    public const string Delegation = "delegation";
    public const string Telemetry = "telemetry";
    public const string Ticket = "ticket";
    public const string Asset = "asset";
    public const string Custom = "custom";

    public static readonly string[] All = { Machine, Component, Delegation, Telemetry, Ticket, Asset, Custom };
}

public class OrganizationMachineTypeFilterDto
{
    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    [JsonPropertyName("enabledCategories")]
    public List<string> EnabledCategories { get; set; } = new();

    [JsonPropertyName("enabledMachineCodes")]
    public List<string> EnabledMachineCodes { get; set; } = new();
}

public class TemplateFieldOptionDto
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class TemplateFieldDefinitionDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("fieldType")]
    public string FieldType { get; set; } = "string"; // "string" | "number" | "boolean" | "select" | "multiselect" | "textarea" | "code" | "json" | "tags"

    [JsonPropertyName("required")]
    public bool Required { get; set; } = false;

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("options")]
    public List<TemplateFieldOptionDto>? Options { get; set; }

    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }

    [JsonPropertyName("step")]
    public double? Step { get; set; }

    [JsonPropertyName("validationRegex")]
    public string? ValidationRegex { get; set; }

    [JsonPropertyName("section")]
    public string Section { get; set; } = "General";

    [JsonPropertyName("order")]
    public int Order { get; set; } = 0;

    [JsonPropertyName("isInherited")]
    public bool IsInherited { get; set; } = false;

    [JsonPropertyName("inheritedFromCode")]
    public string? InheritedFromCode { get; set; }
}

public class GenericTemplateDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("templateType")]
    public string TemplateType { get; set; } = GenericTemplateTypes.Component;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; } = "Layers";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("extendsTemplateId")]
    public Guid? ExtendsTemplateId { get; set; }

    [JsonPropertyName("extendsTemplateCode")]
    public string? ExtendsTemplateCode { get; set; }

    [JsonPropertyName("extendsTemplateName")]
    public string? ExtendsTemplateName { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("schema")]
    public List<TemplateFieldDefinitionDto> Schema { get; set; } = new();

    [JsonPropertyName("defaultValues")]
    public Dictionary<string, object?> DefaultValues { get; set; } = new();

    [JsonPropertyName("uiConfig")]
    public Dictionary<string, object?>? UiConfig { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("isBuiltin")]
    public bool IsBuiltin { get; set; } = false;

    [JsonPropertyName("organizationId")]
    public string? OrganizationId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateGenericTemplateDto
{
    [JsonPropertyName("templateType")]
    public string TemplateType { get; set; } = GenericTemplateTypes.Component;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("extendsTemplateId")]
    public Guid? ExtendsTemplateId { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("schema")]
    public List<TemplateFieldDefinitionDto> Schema { get; set; } = new();

    [JsonPropertyName("defaultValues")]
    public Dictionary<string, object?> DefaultValues { get; set; } = new();

    [JsonPropertyName("uiConfig")]
    public Dictionary<string, object?>? UiConfig { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class UpdateGenericTemplateDto
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("extendsTemplateId")]
    public Guid? ExtendsTemplateId { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("schema")]
    public List<TemplateFieldDefinitionDto>? Schema { get; set; }

    [JsonPropertyName("defaultValues")]
    public Dictionary<string, object?>? DefaultValues { get; set; }

    [JsonPropertyName("uiConfig")]
    public Dictionary<string, object?>? UiConfig { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

public class DeriveTemplateRequestDto
{
    [JsonPropertyName("newCode")]
    public string NewCode { get; set; } = string.Empty;

    [JsonPropertyName("newName")]
    public string NewName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("additionalSchema")]
    public List<TemplateFieldDefinitionDto>? AdditionalSchema { get; set; }

    [JsonPropertyName("defaultOverrides")]
    public Dictionary<string, object?>? DefaultOverrides { get; set; }
}

public class TemplateQueryFilterDto
{
    public string? TemplateType { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public string? Tag { get; set; }
    public bool? IsActive { get; set; }
}

public class TemplateResolutionResultDto
{
    [JsonPropertyName("templateId")]
    public Guid TemplateId { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lineage")]
    public List<TemplateLineageItemDto> Lineage { get; set; } = new();

    [JsonPropertyName("effectiveSchema")]
    public List<TemplateFieldDefinitionDto> EffectiveSchema { get; set; } = new();

    [JsonPropertyName("effectiveDefaultValues")]
    public Dictionary<string, object?> EffectiveDefaultValues { get; set; } = new();
}

public class TemplateLineageItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class InstantiateTemplateRequestDto
{
    [JsonPropertyName("values")]
    public Dictionary<string, object?> Values { get; set; } = new();

    [JsonPropertyName("contextParameters")]
    public Dictionary<string, object?>? ContextParameters { get; set; }
}

public class TemplateInstantiationResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("instanceData")]
    public Dictionary<string, object?> InstanceData { get; set; } = new();

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("appliedSchema")]
    public List<TemplateFieldDefinitionDto> AppliedSchema { get; set; } = new();
}

public class TemplateValidationResultDto
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

public class TemplateTypeDefinitionDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "Layers";

    [JsonPropertyName("defaultCategories")]
    public List<string> DefaultCategories { get; set; } = new();
}
