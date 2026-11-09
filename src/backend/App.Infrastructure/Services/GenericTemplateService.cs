namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using App.Contracts.Templates;
using App.Shared.Data;
using App.Shared.Entities;

public class GenericTemplateService : IGenericTemplateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GenericTemplateService> _logger;

    public GenericTemplateService(AppDbContext context, ILogger<GenericTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GenericTemplateDto>> GetTemplatesAsync(TemplateQueryFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.GenericTemplates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.TemplateType))
        {
            var typeLower = filter.TemplateType.Trim().ToLowerInvariant();
            query = query.Where(t => t.TemplateType.ToLower() == typeLower);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category) && !string.Equals(filter.Category, "all", StringComparison.OrdinalIgnoreCase))
        {
            var catLower = filter.Category.Trim().ToLowerInvariant();
            query = query.Where(t => t.Category.ToLower() == catLower);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchLower = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Code.ToLower().Contains(searchLower) ||
                t.Name.ToLower().Contains(searchLower) ||
                (t.Description != null && t.Description.ToLower().Contains(searchLower)) ||
                t.Category.ToLower().Contains(searchLower));
        }

        var entities = await query
            .OrderBy(t => t.TemplateType)
            .ThenBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tagLower = filter.Tag.Trim().ToLowerInvariant();
            entities = entities.Where(t => t.Tags != null && t.Tags.Any(tg => tg.ToLower() == tagLower)).ToList();
        }

        // Map parent codes for convenience
        var parentIds = entities.Where(e => e.ExtendsTemplateId.HasValue).Select(e => e.ExtendsTemplateId!.Value).Distinct().ToList();
        var parents = parentIds.Count > 0
            ? await _context.GenericTemplates.AsNoTracking().Where(p => parentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct)
            : new Dictionary<Guid, GenericTemplate>();

        return entities.Select(e =>
        {
            var parent = e.ExtendsTemplateId.HasValue && parents.TryGetValue(e.ExtendsTemplateId.Value, out var p) ? p : null;
            return MapToDto(e, parent);
        }).ToList();
    }

    public async Task<GenericTemplateDto?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return null;

        GenericTemplate? parent = null;
        if (entity.ExtendsTemplateId.HasValue)
        {
            parent = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == entity.ExtendsTemplateId.Value, ct);
        }

        return MapToDto(entity, parent);
    }

    public async Task<GenericTemplateDto?> GetTemplateByCodeAsync(string code, CancellationToken ct = default)
    {
        var codeLower = code.Trim().ToLowerInvariant();
        var entity = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Code.ToLower() == codeLower, ct);
        if (entity == null) return null;

        GenericTemplate? parent = null;
        if (entity.ExtendsTemplateId.HasValue)
        {
            parent = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == entity.ExtendsTemplateId.Value, ct);
        }

        return MapToDto(entity, parent);
    }

    public async Task<GenericTemplateDto> CreateTemplateAsync(CreateGenericTemplateDto dto, string? organizationId = null, CancellationToken ct = default)
    {
        var cleanCode = dto.Code.Trim().ToUpperInvariant();
        var exists = await _context.GenericTemplates.AnyAsync(t => t.Code.ToUpper() == cleanCode, ct);
        if (exists)
        {
            throw new InvalidOperationException($"A template with code '{cleanCode}' already exists.");
        }

        if (dto.ExtendsTemplateId.HasValue)
        {
            var parentExists = await _context.GenericTemplates.AnyAsync(t => t.Id == dto.ExtendsTemplateId.Value, ct);
            if (!parentExists)
            {
                throw new InvalidOperationException($"Parent template '{dto.ExtendsTemplateId.Value}' does not exist.");
            }
        }

        var entity = new GenericTemplate
        {
            Id = Guid.NewGuid(),
            TemplateType = string.IsNullOrWhiteSpace(dto.TemplateType) ? GenericTemplateTypes.Component : dto.TemplateType.Trim().ToLowerInvariant(),
            Code = cleanCode,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "Layers" : dto.Icon.Trim(),
            Version = string.IsNullOrWhiteSpace(dto.Version) ? "1.0.0" : dto.Version.Trim(),
            ExtendsTemplateId = dto.ExtendsTemplateId,
            Tags = dto.Tags ?? new List<string>(),
            SchemaJson = JsonSerializer.Serialize(dto.Schema ?? new List<TemplateFieldDefinitionDto>()),
            DefaultValuesJson = JsonSerializer.Serialize(dto.DefaultValues ?? new Dictionary<string, object?>()),
            UiConfigJson = dto.UiConfig != null ? JsonSerializer.Serialize(dto.UiConfig) : null,
            IsActive = dto.IsActive,
            IsBuiltin = false,
            OrganizationId = organizationId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.GenericTemplates.Add(entity);
        await _context.SaveChangesAsync(ct);

        GenericTemplate? parent = null;
        if (entity.ExtendsTemplateId.HasValue)
        {
            parent = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == entity.ExtendsTemplateId.Value, ct);
        }

        return MapToDto(entity, parent);
    }

    public async Task<GenericTemplateDto?> UpdateTemplateAsync(Guid id, UpdateGenericTemplateDto dto, CancellationToken ct = default)
    {
        var entity = await _context.GenericTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var cleanCode = dto.Code.Trim().ToUpperInvariant();
            if (!string.Equals(entity.Code, cleanCode, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _context.GenericTemplates.AnyAsync(t => t.Id != id && t.Code.ToUpper() == cleanCode, ct);
                if (exists)
                {
                    throw new InvalidOperationException($"A template with code '{cleanCode}' already exists.");
                }
                entity.Code = cleanCode;
            }
        }

        if (dto.Name != null) entity.Name = dto.Name.Trim();
        if (dto.Description != null) entity.Description = dto.Description.Trim();
        if (dto.Category != null) entity.Category = dto.Category.Trim();
        if (dto.Icon != null) entity.Icon = dto.Icon.Trim();
        if (dto.Version != null) entity.Version = dto.Version.Trim();
        if (dto.Tags != null) entity.Tags = dto.Tags;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

        if (dto.ExtendsTemplateId.HasValue)
        {
            if (dto.ExtendsTemplateId.Value == id)
            {
                throw new InvalidOperationException("A template cannot inherit from itself.");
            }
            // Check for circular inheritance
            var visited = new HashSet<Guid> { id };
            var currentParentId = dto.ExtendsTemplateId;
            while (currentParentId.HasValue)
            {
                if (visited.Contains(currentParentId.Value))
                {
                    throw new InvalidOperationException("Circular template inheritance detected.");
                }
                visited.Add(currentParentId.Value);
                var p = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentParentId.Value, ct);
                currentParentId = p?.ExtendsTemplateId;
            }
            entity.ExtendsTemplateId = dto.ExtendsTemplateId.Value;
        }

        if (dto.Schema != null)
        {
            entity.SchemaJson = JsonSerializer.Serialize(dto.Schema);
        }

        if (dto.DefaultValues != null)
        {
            entity.DefaultValuesJson = JsonSerializer.Serialize(dto.DefaultValues);
        }

        if (dto.UiConfig != null)
        {
            entity.UiConfigJson = JsonSerializer.Serialize(dto.UiConfig);
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        GenericTemplate? parent = null;
        if (entity.ExtendsTemplateId.HasValue)
        {
            parent = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == entity.ExtendsTemplateId.Value, ct);
        }

        return MapToDto(entity, parent);
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.GenericTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;

        // If other templates extend this one, detach them
        var children = await _context.GenericTemplates.Where(t => t.ExtendsTemplateId == id).ToListAsync(ct);
        foreach (var c in children)
        {
            c.ExtendsTemplateId = null;
        }

        _context.GenericTemplates.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<GenericTemplateDto?> DeriveTemplateAsync(Guid sourceId, DeriveTemplateRequestDto dto, string? organizationId = null, CancellationToken ct = default)
    {
        var source = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == sourceId, ct);
        if (source == null) return null;

        var sourceSchema = ParseSchema(source.SchemaJson);
        var sourceDefaults = ParseDictionary(source.DefaultValuesJson);

        var cleanCode = dto.NewCode.Trim().ToUpperInvariant();
        var exists = await _context.GenericTemplates.AnyAsync(t => t.Code.ToUpper() == cleanCode, ct);
        if (exists)
        {
            throw new InvalidOperationException($"A template with code '{cleanCode}' already exists.");
        }

        var newSchema = dto.AdditionalSchema ?? new List<TemplateFieldDefinitionDto>();
        var newDefaults = dto.DefaultOverrides ?? new Dictionary<string, object?>();

        var child = new GenericTemplate
        {
            Id = Guid.NewGuid(),
            TemplateType = source.TemplateType,
            Code = cleanCode,
            Name = dto.NewName.Trim(),
            Description = dto.Description?.Trim() ?? $"Derived from {source.Name} ({source.Code})",
            Category = string.IsNullOrWhiteSpace(dto.Category) ? source.Category : dto.Category.Trim(),
            Icon = source.Icon,
            Version = "1.0.0",
            ExtendsTemplateId = source.Id,
            Tags = new List<string>(source.Tags) { "derived" },
            SchemaJson = JsonSerializer.Serialize(newSchema),
            DefaultValuesJson = JsonSerializer.Serialize(newDefaults),
            UiConfigJson = source.UiConfigJson,
            IsActive = true,
            IsBuiltin = false,
            OrganizationId = organizationId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.GenericTemplates.Add(child);
        await _context.SaveChangesAsync(ct);

        return MapToDto(child, source);
    }

    public async Task<TemplateResolutionResultDto?> ResolveEffectiveSchemaAsync(Guid templateId, CancellationToken ct = default)
    {
        var target = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (target == null) return null;

        // Traverse lineage from target up to root
        var lineageList = new List<GenericTemplate> { target };
        var visited = new HashSet<Guid> { target.Id };
        var currentParentId = target.ExtendsTemplateId;

        while (currentParentId.HasValue)
        {
            if (visited.Contains(currentParentId.Value)) break;
            visited.Add(currentParentId.Value);

            var parent = await _context.GenericTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == currentParentId.Value, ct);
            if (parent == null) break;

            lineageList.Add(parent);
            currentParentId = parent.ExtendsTemplateId;
        }

        // Reverse so order is Root -> Child -> ... -> Target
        lineageList.Reverse();

        var effectiveFieldMap = new Dictionary<string, TemplateFieldDefinitionDto>(StringComparer.OrdinalIgnoreCase);
        var effectiveDefaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in lineageList)
        {
            var isCurrent = t.Id == target.Id;
            var fields = ParseSchema(t.SchemaJson);

            foreach (var f in fields)
            {
                if (effectiveFieldMap.TryGetValue(f.Key, out var existing))
                {
                    // Descendant override
                    f.IsInherited = !isCurrent;
                    f.InheritedFromCode = existing.InheritedFromCode ?? t.Code;
                    effectiveFieldMap[f.Key] = f;
                }
                else
                {
                    f.IsInherited = !isCurrent;
                    if (!isCurrent) f.InheritedFromCode = t.Code;
                    effectiveFieldMap[f.Key] = f;
                }
            }

            var defaults = ParseDictionary(t.DefaultValuesJson);
            foreach (var kvp in defaults)
            {
                effectiveDefaults[kvp.Key] = kvp.Value;
            }
        }

        var orderedFields = effectiveFieldMap.Values
            .OrderBy(f => f.Section)
            .ThenBy(f => f.Order)
            .ThenBy(f => f.Label)
            .ToList();

        return new TemplateResolutionResultDto
        {
            TemplateId = target.Id,
            Code = target.Code,
            Name = target.Name,
            Lineage = lineageList.Select(l => new TemplateLineageItemDto
            {
                Id = l.Id,
                Code = l.Code,
                Name = l.Name
            }).ToList(),
            EffectiveSchema = orderedFields,
            EffectiveDefaultValues = effectiveDefaults
        };
    }

    public async Task<TemplateValidationResultDto> ValidateInstanceDataAsync(Guid templateId, Dictionary<string, object?> data, CancellationToken ct = default)
    {
        var resolved = await ResolveEffectiveSchemaAsync(templateId, ct);
        if (resolved == null)
        {
            return new TemplateValidationResultDto
            {
                IsValid = false,
                Errors = new List<string> { $"Template '{templateId}' not found." }
            };
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var field in resolved.EffectiveSchema)
        {
            object? rawVal = null;
            var hasValue = data != null && data.TryGetValue(field.Key, out rawVal) && rawVal != null && !string.IsNullOrWhiteSpace(rawVal.ToString());

            if (field.Required && !hasValue)
            {
                errors.Add($"Field '{field.Label}' ({field.Key}) is required.");
                continue;
            }

            if (!hasValue) continue;

            // Type and constraint validation
            switch (field.FieldType.ToLowerInvariant())
            {
                case "number":
                    if (!double.TryParse(rawVal!.ToString(), out var numVal))
                    {
                        errors.Add($"Field '{field.Label}' must be a valid number.");
                    }
                    else
                    {
                        if (field.Min.HasValue && numVal < field.Min.Value)
                            errors.Add($"Field '{field.Label}' must be at least {field.Min.Value}.");
                        if (field.Max.HasValue && numVal > field.Max.Value)
                            errors.Add($"Field '{field.Label}' cannot exceed {field.Max.Value}.");
                    }
                    break;

                case "boolean":
                    if (!bool.TryParse(rawVal!.ToString(), out _))
                    {
                        errors.Add($"Field '{field.Label}' must be a valid boolean (true/false).");
                    }
                    break;

                case "select":
                    if (field.Options != null && field.Options.Count > 0)
                    {
                        var valStr = rawVal!.ToString();
                        var validOpt = field.Options.Any(o => string.Equals(o.Value, valStr, StringComparison.OrdinalIgnoreCase));
                        if (!validOpt)
                        {
                            errors.Add($"Value '{valStr}' for '{field.Label}' is not a valid option.");
                        }
                    }
                    break;

                case "json":
                    try
                    {
                        using var doc = JsonDocument.Parse(rawVal!.ToString()!);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Field '{field.Label}' contains invalid JSON: {ex.Message}");
                    }
                    break;

                case "string":
                case "textarea":
                    if (!string.IsNullOrWhiteSpace(field.ValidationRegex))
                    {
                        try
                        {
                            var regex = new Regex(field.ValidationRegex);
                            if (!regex.IsMatch(rawVal!.ToString()!))
                            {
                                errors.Add($"Field '{field.Label}' does not match required format ({field.ValidationRegex}).");
                            }
                        }
                        catch (Exception ex)
                        {
                            warnings.Add($"Invalid regex on field '{field.Label}': {ex.Message}");
                        }
                    }
                    break;
            }
        }

        return new TemplateValidationResultDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<TemplateInstantiationResultDto> InstantiateTemplateAsync(Guid templateId, InstantiateTemplateRequestDto dto, CancellationToken ct = default)
    {
        var resolved = await ResolveEffectiveSchemaAsync(templateId, ct);
        if (resolved == null)
        {
            return new TemplateInstantiationResultDto
            {
                Success = false,
                Errors = new List<string> { $"Template '{templateId}' not found." }
            };
        }

        // Start with effective defaults
        var merged = new Dictionary<string, object?>(resolved.EffectiveDefaultValues, StringComparer.OrdinalIgnoreCase);

        // Apply provided instance values
        if (dto.Values != null)
        {
            foreach (var kvp in dto.Values)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        // Substitute dynamic tokens if present in string values
        var now = DateTimeOffset.UtcNow;
        var sysTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "$uuid", Guid.NewGuid().ToString() },
            { "$timestamp", now.ToString("o") },
            { "$date", now.ToString("yyyy-MM-dd") },
            { "$year", now.Year.ToString() }
        };

        if (dto.ContextParameters != null)
        {
            foreach (var cp in dto.ContextParameters)
            {
                if (cp.Value != null)
                {
                    sysTokens[cp.Key] = cp.Value.ToString() ?? "";
                }
            }
        }

        var keysToProcess = merged.Keys.ToList();
        foreach (var k in keysToProcess)
        {
            var val = merged[k];
            if (val is string strVal && strVal.Contains("{{"))
            {
                foreach (var token in sysTokens)
                {
                    strVal = strVal.Replace($"{{{{{token.Key}}}}}", token.Value, StringComparison.OrdinalIgnoreCase);
                    strVal = strVal.Replace($"{{{{ {token.Key} }}}}", token.Value, StringComparison.OrdinalIgnoreCase);
                }
                merged[k] = strVal;
            }
        }

        var validation = await ValidateInstanceDataAsync(templateId, merged, ct);

        return new TemplateInstantiationResultDto
        {
            Success = validation.IsValid,
            InstanceData = merged,
            Errors = validation.Errors,
            AppliedSchema = resolved.EffectiveSchema
        };
    }

    public Task<IReadOnlyList<TemplateTypeDefinitionDto>> GetSupportedTemplateTypesAsync(CancellationToken ct = default)
    {
        var list = new List<TemplateTypeDefinitionDto>
        {
            new()
            {
                Type = GenericTemplateTypes.Machine,
                Label = "Machine & Production Station",
                Description = "Standard industrial machine blueprint with functional specifications, cycle time targets, safety ratings, and required tooling.",
                Icon = "Factory",
                DefaultCategories = new List<string> { "SMT", "Inspection", "Soldering", "Depaneling", "Dispensing", "Fastening", "Testing", "Assembly", "Robotics" }
            },
            new()
            {
                Type = GenericTemplateTypes.Component,
                Label = "Machine Component",
                Description = "Reusable blueprint for instantiating mechanical, electrical, and sensor component slots on industrial machines.",
                Icon = "Cpu",
                DefaultCategories = new List<string> { "Motion", "Pneumatics", "Vision", "Sensors", "Safety", "Tooling" }
            },
            new()
            {
                Type = GenericTemplateTypes.Delegation,
                Label = "Technician Delegation",
                Description = "Operational responsibility recipe defining dispatch rules, required technician skills, and escalation procedures.",
                Icon = "Users",
                DefaultCategories = new List<string> { "Mechanical", "Electrical", "Controls", "Robotics", "General" }
            },
            new()
            {
                Type = GenericTemplateTypes.Telemetry,
                Label = "Telemetry & IoT Probe",
                Description = "Industrial data ingestion profile specifying polling intervals, field protocols, deadbands, and alarm thresholds.",
                Icon = "Activity",
                DefaultCategories = new List<string> { "Drives", "Vibration", "Thermal", "Pressure", "Energy" }
            },
            new()
            {
                Type = GenericTemplateTypes.Ticket,
                Label = "Error & Maintenance Ticket",
                Description = "Standardized plant event recipe matching TwinCAT/PLC function blocks, fault codes, and remediation workflows.",
                Icon = "ShieldAlert",
                DefaultCategories = new List<string> { "Error", "Prevention", "Improvement", "Safety" }
            },
            new()
            {
                Type = GenericTemplateTypes.Asset,
                Label = "Inventory Asset & Spare Part",
                Description = "Catalog blueprint defining inventory specifications, manufacturer attributes, and procurement metadata.",
                Icon = "Boxes",
                DefaultCategories = new List<string> { "Controller", "Sensor", "Vision", "Motion", "Hardware", "Software" }
            },
            new()
            {
                Type = GenericTemplateTypes.Custom,
                Label = "Custom Universal Blueprint",
                Description = "Free-form schema with custom visual fields for specialized plant processes, inspections, or configurations.",
                Icon = "Layers",
                DefaultCategories = new List<string> { "Standard", "Experimental", "Quality" }
            }
        };

        return Task.FromResult<IReadOnlyList<TemplateTypeDefinitionDto>>(list);
    }

    public async Task SeedDefaultTemplatesIfEmptyAsync(CancellationToken ct = default)
    {
        var existingCodes = await _context.GenericTemplates
            .Select(t => t.Code.ToUpper())
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var missingSeeds = SystemTemplateCatalog.All
            .Where(seed => !existingSet.Contains(seed.Code))
            .ToList();

        if (missingSeeds.Count == 0) return;

        _logger.LogInformation("Seeding {Count} system default universal templates and blueprints...", missingSeeds.Count);

        _context.GenericTemplates.AddRange(missingSeeds);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} system default generic templates successfully.", missingSeeds.Count);
    }

    private static GenericTemplateDto MapToDto(GenericTemplate e, GenericTemplate? parent = null)
    {
        return new GenericTemplateDto
        {
            Id = e.Id,
            TemplateType = e.TemplateType,
            Code = e.Code,
            Name = e.Name,
            Description = e.Description,
            Category = e.Category,
            Icon = e.Icon,
            Version = e.Version,
            ExtendsTemplateId = e.ExtendsTemplateId,
            ExtendsTemplateCode = parent?.Code,
            ExtendsTemplateName = parent?.Name,
            Tags = e.Tags ?? new List<string>(),
            Schema = ParseSchema(e.SchemaJson),
            DefaultValues = ParseDictionary(e.DefaultValuesJson),
            UiConfig = !string.IsNullOrWhiteSpace(e.UiConfigJson) ? ParseDictionary(e.UiConfigJson) : null,
            IsActive = e.IsActive,
            IsBuiltin = e.IsBuiltin,
            OrganizationId = e.OrganizationId,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }

    private static List<TemplateFieldDefinitionDto> ParseSchema(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<TemplateFieldDefinitionDto>();
        try
        {
            var list = JsonSerializer.Deserialize<List<TemplateFieldDefinitionDto>>(json) ?? new List<TemplateFieldDefinitionDto>();
            foreach (var item in list)
            {
                if (item.DefaultValue is JsonElement je)
                {
                    item.DefaultValue = ConvertJsonElement(je);
                }
            }
            return list;
        }
        catch
        {
            return new List<TemplateFieldDefinitionDto>();
        }
    }

    private static Dictionary<string, object?> ParseDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = ConvertJsonElement(prop.Value);
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static object? ConvertJsonElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var l)) return l;
                if (el.TryGetDouble(out var d)) return d;
                return el.GetRawText();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in el.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var p in el.EnumerateObject())
                {
                    dict[p.Name] = ConvertJsonElement(p.Value);
                }
                return dict;
            default:
                return el.GetRawText();
        }
    }
}
