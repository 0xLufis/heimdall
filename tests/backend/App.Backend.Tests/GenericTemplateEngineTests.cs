namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using App.Backend.Api.Controllers.V1;
using App.Contracts.Templates;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

public class GenericTemplateEngineTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TemplateEngineTestDb_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateTemplate_ValidRequest_PersistsAndReturnsDto()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var dto = new CreateGenericTemplateDto
        {
            TemplateType = GenericTemplateTypes.Component,
            Code = "CMP-VALVE-01",
            Name = "Pneumatic Proportional Valve",
            Category = "Pneumatics",
            Description = "High precision pressure regulating valve",
            Icon = "Sliders",
            Tags = new List<string> { "Pneumatics", "Valve", "Pressure" },
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "pressure_bar", Label = "Operating Pressure", FieldType = "number", Required = true, DefaultValue = 6.0, Unit = "bar", Min = 0.5, Max = 16.0 },
                new() { Key = "valve_type", Label = "Port Configuration", FieldType = "select", Required = true, DefaultValue = "5/2-way", Options = new List<TemplateFieldOptionDto>
                {
                    new() { Label = "5/2-way monostable", Value = "5/2-way" },
                    new() { Label = "3/2-way normally closed", Value = "3/2-way" }
                }}
            },
            DefaultValues = new Dictionary<string, object?>
            {
                { "pressure_bar", 6.0 },
                { "valve_type", "5/2-way" }
            }
        };

        var created = await service.CreateTemplateAsync(dto);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("CMP-VALVE-01", created.Code);
        Assert.Equal("Pneumatics", created.Category);
        Assert.Equal(2, created.Schema.Count);
        Assert.True(created.IsActive);

        var fetched = await service.GetTemplateByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal("CMP-VALVE-01", fetched.Code);
    }

    [Fact]
    public async Task CreateTemplate_DuplicateCode_ThrowsInvalidOperationException()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var dto = new CreateGenericTemplateDto
        {
            TemplateType = GenericTemplateTypes.Component,
            Code = "CMP-DUPLICATE-01",
            Name = "First",
            Category = "Motion"
        };
        await service.CreateTemplateAsync(dto);

        var dupDto = new CreateGenericTemplateDto
        {
            TemplateType = GenericTemplateTypes.Component,
            Code = "cmp-duplicate-01", // case-insensitive duplicate
            Name = "Second",
            Category = "Motion"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTemplateAsync(dupDto));
    }

    [Fact]
    public async Task GetTemplates_FilterByTypeAndCategory_ReturnsFilteredResults()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        await service.CreateTemplateAsync(new CreateGenericTemplateDto { TemplateType = "component", Code = "CMP-01", Name = "Part A", Category = "Motion" });
        await service.CreateTemplateAsync(new CreateGenericTemplateDto { TemplateType = "component", Code = "CMP-02", Name = "Part B", Category = "Optics" });
        await service.CreateTemplateAsync(new CreateGenericTemplateDto { TemplateType = "delegation", Code = "DLG-01", Name = "Team A", Category = "Controls" });

        var motionComponents = await service.GetTemplatesAsync(new TemplateQueryFilterDto { TemplateType = "component", Category = "Motion" });
        Assert.Single(motionComponents);
        Assert.Equal("CMP-01", motionComponents[0].Code);

        var allDelegations = await service.GetTemplatesAsync(new TemplateQueryFilterDto { TemplateType = "delegation" });
        Assert.Single(allDelegations);
        Assert.Equal("DLG-01", allDelegations[0].Code);
    }

    [Fact]
    public async Task GetTemplates_FilterBySearchAndTag_ReturnsMatchingTemplates()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "ticket",
            Code = "TCK-SAFETY-01",
            Name = "Emergency Stop Tripped",
            Category = "Safety",
            Tags = new List<string> { "E-Stop", "Safety", "SIL3" }
        });

        await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "ticket",
            Code = "TCK-LUBE-01",
            Name = "Bearing Grease Replenishment",
            Category = "Maintenance",
            Tags = new List<string> { "Lubrication", "Mechanical" }
        });

        var searchResults = await service.GetTemplatesAsync(new TemplateQueryFilterDto { Search = "Emergency" });
        Assert.Single(searchResults);
        Assert.Equal("TCK-SAFETY-01", searchResults[0].Code);

        var tagResults = await service.GetTemplatesAsync(new TemplateQueryFilterDto { Tag = "SIL3" });
        Assert.Single(tagResults);
        Assert.Equal("TCK-SAFETY-01", tagResults[0].Code);
    }

    [Fact]
    public async Task ResolveEffectiveSchema_SingleLineageInheritance_AccumulatesFieldsAndMergesDefaults()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        // Root Template: Base Rotating Equipment
        var root = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = GenericTemplateTypes.Component,
            Code = "BASE-ROTATING",
            Name = "Base Rotating Machine",
            Category = "Mechanical",
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "rated_rpm", Label = "Rated RPM", FieldType = "number", Required = true, DefaultValue = 1500, Unit = "RPM" },
                new() { Key = "bearing_type", Label = "Bearing Type", FieldType = "string", DefaultValue = "Deep Groove Ball" }
            },
            DefaultValues = new Dictionary<string, object?>
            {
                { "rated_rpm", 1500 },
                { "bearing_type", "Deep Groove Ball" }
            }
        });

        // Child Template: Centrifugal Pump (extends Base Rotating)
        var pump = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = GenericTemplateTypes.Component,
            Code = "PUMP-CENTRIFUGAL",
            Name = "Centrifugal Pump",
            Category = "Pumping",
            ExtendsTemplateId = root.Id,
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "impeller_diameter_mm", Label = "Impeller Diameter", FieldType = "number", Required = true, DefaultValue = 180, Unit = "mm" },
                new() { Key = "flow_rate_lpm", Label = "Nominal Flow Rate", FieldType = "number", DefaultValue = 250, Unit = "L/min" }
            },
            DefaultValues = new Dictionary<string, object?>
            {
                { "impeller_diameter_mm", 180 },
                { "flow_rate_lpm", 250 }
            }
        });

        var resolved = await service.ResolveEffectiveSchemaAsync(pump.Id);

        Assert.NotNull(resolved);
        Assert.Equal("PUMP-CENTRIFUGAL", resolved.Code);
        Assert.Equal(2, resolved.Lineage.Count);
        Assert.Equal("BASE-ROTATING", resolved.Lineage[0].Code);
        Assert.Equal("PUMP-CENTRIFUGAL", resolved.Lineage[1].Code);

        // Should have 4 fields: 2 inherited + 2 local
        Assert.Equal(4, resolved.EffectiveSchema.Count);
        var keys = resolved.EffectiveSchema.Select(f => f.Key).ToList();
        Assert.Contains("rated_rpm", keys);
        Assert.Contains("bearing_type", keys);
        Assert.Contains("impeller_diameter_mm", keys);
        Assert.Contains("flow_rate_lpm", keys);

        // Inherited flags checked
        var rpmField = resolved.EffectiveSchema.First(f => f.Key == "rated_rpm");
        Assert.True(rpmField.IsInherited);
        Assert.Equal("BASE-ROTATING", rpmField.InheritedFromCode);

        var impellerField = resolved.EffectiveSchema.First(f => f.Key == "impeller_diameter_mm");
        Assert.False(impellerField.IsInherited);

        // Default values merged
        Assert.Equal(4, resolved.EffectiveDefaultValues.Count);
        Assert.Equal(1500, Convert.ToInt32(resolved.EffectiveDefaultValues["rated_rpm"]));
        Assert.Equal(180, Convert.ToInt32(resolved.EffectiveDefaultValues["impeller_diameter_mm"]));
    }

    [Fact]
    public async Task ResolveEffectiveSchema_ChildOverridesParentField_OverridesDefinitionAndTracksInheritance()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var parent = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "telemetry",
            Code = "TEL-BASE",
            Name = "Base Telemetry",
            Category = "Telemetry",
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "polling_interval_ms", Label = "Polling Rate", FieldType = "number", Required = false, DefaultValue = 2000, Min = 100 }
            },
            DefaultValues = new Dictionary<string, object?> { { "polling_interval_ms", 2000 } }
        });

        var child = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "telemetry",
            Code = "TEL-HIGH-SPEED",
            Name = "High Speed Telemetry",
            Category = "Telemetry",
            ExtendsTemplateId = parent.Id,
            Schema = new List<TemplateFieldDefinitionDto>
            {
                // Override polling_interval_ms to required and tighter default 100ms
                new() { Key = "polling_interval_ms", Label = "Ultra High Speed Polling Rate", FieldType = "number", Required = true, DefaultValue = 100, Min = 10, Max = 500 }
            },
            DefaultValues = new Dictionary<string, object?> { { "polling_interval_ms", 100 } }
        });

        var resolved = await service.ResolveEffectiveSchemaAsync(child.Id);
        Assert.NotNull(resolved);
        Assert.Single(resolved.EffectiveSchema);

        var overriddenField = resolved.EffectiveSchema[0];
        Assert.Equal("polling_interval_ms", overriddenField.Key);
        Assert.Equal("Ultra High Speed Polling Rate", overriddenField.Label);
        Assert.True(overriddenField.Required);
        Assert.Equal(100, Convert.ToInt32(overriddenField.DefaultValue));
        Assert.Equal(100, Convert.ToInt32(resolved.EffectiveDefaultValues["polling_interval_ms"]));
    }

    [Fact]
    public async Task ValidateInstanceData_ValidatesRequiredAndConstraints()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var template = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "component",
            Code = "TEST-CONSTRAINTS",
            Name = "Constraint Test",
            Category = "Test",
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "required_text", Label = "Required Text", FieldType = "string", Required = true },
                new() { Key = "bounded_num", Label = "Bounded Number", FieldType = "number", Required = true, Min = 10, Max = 100 },
                new() { Key = "select_mode", Label = "Mode", FieldType = "select", Required = true, Options = new List<TemplateFieldOptionDto>
                {
                    new() { Label = "Auto", Value = "AUTO" },
                    new() { Label = "Manual", Value = "MANUAL" }
                }}
            }
        });

        // 1. Missing required field
        var invalidMissing = new Dictionary<string, object?>
        {
            { "bounded_num", 50 },
            { "select_mode", "AUTO" }
        };
        var res1 = await service.ValidateInstanceDataAsync(template.Id, invalidMissing);
        Assert.False(res1.IsValid);
        Assert.Contains(res1.Errors, e => e.Contains("Required Text"));

        // 2. Out of bounds number
        var invalidBounds = new Dictionary<string, object?>
        {
            { "required_text", "ok" },
            { "bounded_num", 150 },
            { "select_mode", "AUTO" }
        };
        var res2 = await service.ValidateInstanceDataAsync(template.Id, invalidBounds);
        Assert.False(res2.IsValid);
        Assert.Contains(res2.Errors, e => e.Contains("cannot exceed 100"));

        // 3. Invalid select option
        var invalidOption = new Dictionary<string, object?>
        {
            { "required_text", "ok" },
            { "bounded_num", 50 },
            { "select_mode", "INVALID_MODE" }
        };
        var res3 = await service.ValidateInstanceDataAsync(template.Id, invalidOption);
        Assert.False(res3.IsValid);
        Assert.Contains(res3.Errors, e => e.Contains("not a valid option"));

        // 4. Fully valid payload
        var fullyValid = new Dictionary<string, object?>
        {
            { "required_text", "Operational" },
            { "bounded_num", 75 },
            { "select_mode", "AUTO" }
        };
        var res4 = await service.ValidateInstanceDataAsync(template.Id, fullyValid);
        Assert.True(res4.IsValid);
        Assert.Empty(res4.Errors);
    }

    [Fact]
    public async Task InstantiateTemplate_SubstitutesDynamicTokens_AndProducesValidInstance()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var template = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "asset",
            Code = "AST-TOKEN-TEST",
            Name = "Dynamic Token Asset",
            Category = "Sensors",
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "asset_tag", Label = "Asset Tag", FieldType = "string", Required = true },
                new() { Key = "sample_interval", Label = "Interval", FieldType = "number", DefaultValue = 500 }
            },
            DefaultValues = new Dictionary<string, object?>
            {
                { "asset_tag", "TAG-{{$uuid}}" },
                { "sample_interval", 500 }
            }
        });

        var request = new InstantiateTemplateRequestDto
        {
            Values = new Dictionary<string, object?>
            {
                { "asset_tag", "DEVICE-{{$date}}-{{$uuid}}" }
            }
        };

        var result = await service.InstantiateTemplateAsync(template.Id, request);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.NotNull(result.InstanceData);

        var assetTag = result.InstanceData["asset_tag"]?.ToString();
        Assert.NotNull(assetTag);
        Assert.StartsWith("DEVICE-", assetTag);
        Assert.DoesNotContain("{{$uuid}}", assetTag);
        Assert.DoesNotContain("{{$date}}", assetTag);

        Assert.Equal(500, Convert.ToInt32(result.InstanceData["sample_interval"]));
    }

    [Fact]
    public async Task DeriveTemplate_CreatesChildWithParentReference_InheritsAndExtendsSchema()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var parent = await service.CreateTemplateAsync(new CreateGenericTemplateDto
        {
            TemplateType = "component",
            Code = "CMP-VALVE-BASE",
            Name = "Base Pneumatic Valve",
            Category = "Pneumatics",
            Schema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "pressure_bar", Label = "Pressure", FieldType = "number", DefaultValue = 6.0 }
            }
        });

        var derived = await service.DeriveTemplateAsync(parent.Id, new DeriveTemplateRequestDto
        {
            NewCode = "CMP-VALVE-SMART",
            NewName = "Smart Proportional Valve with IO-Link",
            AdditionalSchema = new List<TemplateFieldDefinitionDto>
            {
                new() { Key = "io_link_vendor_id", Label = "Vendor ID", FieldType = "string", DefaultValue = "0x002B" }
            },
            DefaultOverrides = new Dictionary<string, object?>
            {
                { "pressure_bar", 8.0 }
            }
        });

        Assert.NotNull(derived);
        Assert.Equal("CMP-VALVE-SMART", derived.Code);
        Assert.Equal(parent.Id, derived.ExtendsTemplateId);

        var resolved = await service.ResolveEffectiveSchemaAsync(derived.Id);
        Assert.NotNull(resolved);
        Assert.Equal(2, resolved.EffectiveSchema.Count);
        Assert.Equal(8.0, Convert.ToDouble(resolved.EffectiveDefaultValues["pressure_bar"]));
    }

    [Fact]
    public async Task SeedDefaultTemplatesIfEmpty_PopulatesAllFiveCoreDomains()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        await service.SeedDefaultTemplatesIfEmptyAsync();

        var templates = await service.GetTemplatesAsync(new TemplateQueryFilterDto());
        Assert.NotEmpty(templates);

        var types = templates.Select(t => t.TemplateType).Distinct().ToList();
        Assert.Contains(GenericTemplateTypes.Machine, types);
        Assert.Contains(GenericTemplateTypes.Component, types);
        Assert.Contains(GenericTemplateTypes.Delegation, types);
        Assert.Contains(GenericTemplateTypes.Telemetry, types);
        Assert.Contains(GenericTemplateTypes.Ticket, types);
    }

    [Fact]
    public async Task SeedDefaultTemplates_PopulatesExtensiveMachineAndComponentCatalog()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        await service.SeedDefaultTemplatesIfEmptyAsync();

        var allTemplates = await service.GetTemplatesAsync(new TemplateQueryFilterDto());
        Assert.True(allTemplates.Count >= 48, $"Expected at least 48 seeded templates, but found {allTemplates.Count}");

        var machineTemplates = allTemplates.Where(t => t.TemplateType == GenericTemplateTypes.Machine).ToList();
        Assert.True(machineTemplates.Count >= 24, $"Expected at least 24 machine templates, found {machineTemplates.Count}");

        var componentTemplates = allTemplates.Where(t => t.TemplateType == GenericTemplateTypes.Component).ToList();
        Assert.True(componentTemplates.Count >= 24, $"Expected at least 24 component templates, found {componentTemplates.Count}");

        // Verify key automotive Tier 1 PCBA machines
        Assert.Contains(machineTemplates, m => m.Code == "MCH-SMT-PRINT-01" && m.Category == "SMT");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-AOI-3D-01" && m.Category == "Inspection");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-AXI-3D-01" && m.Category == "Inspection");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-SLD-SELECT-01" && m.Category == "Soldering");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-SCREW-AUTO-01" && m.Category == "Fastening");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-PRESS-FIT-01" && m.Category == "Assembly");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-TEST-EOL-01" && m.Category == "Testing");
        Assert.Contains(machineTemplates, m => m.Code == "MCH-TEST-HIPOT-01" && m.Category == "Testing");

        // Verify key components
        Assert.Contains(componentTemplates, c => c.Code == "CMP-SERVO-01" && c.Category == "Motion");
        Assert.Contains(componentTemplates, c => c.Code == "CMP-DISP-JETVALVE-01" && c.Category == "Tooling");
        Assert.Contains(componentTemplates, c => c.Code == "CMP-CAM-GIGE-01" && c.Category == "Optics");
        Assert.Contains(componentTemplates, c => c.Code == "CMP-TEST-PIN-01" && c.Category == "Tooling");
    }

    [Fact]
    public async Task SeedDefaultTemplates_IsIdempotent_DoesNotDuplicate()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        await service.SeedDefaultTemplatesIfEmptyAsync();
        var firstRunCount = await context.GenericTemplates.CountAsync();

        // Run second time
        await service.SeedDefaultTemplatesIfEmptyAsync();
        var secondRunCount = await context.GenericTemplates.CountAsync();

        Assert.Equal(firstRunCount, secondRunCount);
    }

    [Fact]
    public async Task MachineTemplate_Instantiation_ValidatesAndMergesProcessLimits()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        await service.SeedDefaultTemplatesIfEmptyAsync();

        var printerTemplate = await service.GetTemplateByCodeAsync("MCH-SMT-PRINT-01");
        Assert.NotNull(printerTemplate);

        var instantiationResult = await service.InstantiateTemplateAsync(printerTemplate.Id, new InstantiateTemplateRequestDto
        {
            Values = new Dictionary<string, object?>
            {
                { "squeegee_pressure_kg", 8.2 },
                { "print_speed_mm_s", 50.0 }
            }
        });

        Assert.True(instantiationResult.Success, string.Join("; ", instantiationResult.Errors));
        Assert.NotNull(instantiationResult.InstanceData);
        Assert.Equal(8.2, Convert.ToDouble(instantiationResult.InstanceData["squeegee_pressure_kg"]));
        Assert.Equal(50.0, Convert.ToDouble(instantiationResult.InstanceData["print_speed_mm_s"]));
        Assert.Equal("SAC305 Type 4", instantiationResult.InstanceData["solder_paste_alloy"]?.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Security & correctness regression tests
    // ─────────────────────────────────────────────────────────────────────

    private static GenericTemplate NewTemplate(string code, string? orgId, List<TemplateFieldDefinitionDto>? schema = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        TemplateType = GenericTemplateTypes.Custom,
        OrganizationId = orgId,
        SchemaJson = JsonSerializer.Serialize(schema ?? new List<TemplateFieldDefinitionDto>())
    };

    [Fact]
    public async Task ValidateInstance_CatastrophicRegex_TimesOutInsteadOfHanging()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);
        var template = NewTemplate("CUS-REDOS", null, new List<TemplateFieldDefinitionDto>
        {
            new() { Key = "serial", Label = "Serial", FieldType = "string", ValidationRegex = "^(a+)+$" }
        });
        context.GenericTemplates.Add(template);
        await context.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        var result = await service.ValidateInstanceDataAsync(template.Id, new Dictionary<string, object?>
        {
            ["serial"] = new string('a', 40) + "!"
        });
        sw.Stop();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("timed out"));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Validation took {sw.Elapsed}");
    }

    [Fact]
    public async Task ValidateInstance_NumberParsing_IsCultureInvariant()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);
        var template = NewTemplate("CUS-NUM", null, new List<TemplateFieldDefinitionDto>
        {
            new() { Key = "pressure", Label = "Pressure", FieldType = "number", Min = 1, Max = 10 }
        });
        context.GenericTemplates.Add(template);
        await context.SaveChangesAsync();

        var previous = CultureInfo.CurrentCulture;
        try
        {
            // de-DE treats '.' as a group separator: "7.5" would otherwise parse as 75 and fail Max.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var result = await service.ValidateInstanceDataAsync(template.Id, new Dictionary<string, object?> { ["pressure"] = "7.5" });
            Assert.True(result.IsValid, string.Join("; ", result.Errors));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Instantiate_JsonElementValues_AreNormalizedAndTokensSubstituted()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);
        var template = NewTemplate("CUS-TOKENS", null);
        context.GenericTemplates.Add(template);
        await context.SaveChangesAsync();

        // Simulate what MVC model binding produces for Dictionary<string, object?> bodies.
        var values = JsonSerializer.Deserialize<Dictionary<string, object?>>("{\"label\":\"ST-{{$year}}\"}")!;
        var result = await service.InstantiateTemplateAsync(template.Id, new InstantiateTemplateRequestDto { Values = values });

        Assert.Equal($"ST-{DateTimeOffset.UtcNow.Year}", result.InstanceData["label"]);
    }

    [Fact]
    public async Task TenantFilter_HidesOtherOrganizationsTemplates_ButKeepsGlobalOnes()
    {
        var dbName = $"TemplateTenantDb_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var foreign = NewTemplate("ORG-B-ONLY", "Plant-B");
        using (var seed = new AppDbContext(options))
        {
            seed.GenericTemplates.AddRange(NewTemplate("GLOBAL-1", null), NewTemplate("ORG-A-ONLY", "Plant-A"), foreign);
            await seed.SaveChangesAsync();
        }

        using var context = new AppDbContext(options) { CurrentOrganizationId = "Plant-A" };
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);

        var visible = await service.GetTemplatesAsync(new TemplateQueryFilterDto());
        Assert.Contains(visible, t => t.Code == "GLOBAL-1");
        Assert.Contains(visible, t => t.Code == "ORG-A-ONLY");
        Assert.DoesNotContain(visible, t => t.Code == "ORG-B-ONLY");
        Assert.Null(await service.GetTemplateByIdAsync(foreign.Id));
        Assert.False(await service.DeleteTemplateAsync(foreign.Id));

        // Codes are globally unique: a clash with an invisible template must still be a clean conflict.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTemplateAsync(new CreateGenericTemplateDto { Code = "org-b-only", Name = "Clash" }, "Plant-A"));
    }

    [Fact]
    public async Task Seed_IsIdempotent_WhenBuiltinCodeWasRenamed()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);
        await service.SeedDefaultTemplatesIfEmptyAsync();
        var count = await context.GenericTemplates.CountAsync();

        var builtin = await context.GenericTemplates.FirstAsync(t => t.IsBuiltin);
        builtin.Code = "RENAMED-BY-ENGINEER";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await service.SeedDefaultTemplatesIfEmptyAsync();
        Assert.Equal(count, await context.GenericTemplates.CountAsync());
    }

    [Fact]
    public async Task Update_WithUnknownParent_IsRejected()
    {
        using var context = CreateContext();
        var service = new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance);
        var template = NewTemplate("CUS-ORPHAN", null);
        context.GenericTemplates.Add(template);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateTemplateAsync(template.Id, new UpdateGenericTemplateDto { ExtendsTemplateId = Guid.NewGuid() }));
    }

    private sealed class FixedAuthorizationService(bool allow) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(allow ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(allow ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private static GenericTemplatesController CreateController(AppDbContext context, string orgId, bool platformAdmin)
    {
        var controller = new GenericTemplatesController(
            new GenericTemplateService(context, NullLogger<GenericTemplateService>.Instance),
            context,
            new FixedAuthorizationService(platformAdmin));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("OrgId", orgId) }, "Test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task Controller_ScopesReadsToCallerOrganization_AndProtectsGlobalTemplates()
    {
        using var context = CreateContext();
        var foreign = NewTemplate("ORG-B-PRIVATE", "Plant-B");
        var global = NewTemplate("GLOBAL-SHARED", null);
        context.GenericTemplates.AddRange(foreign, global);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context, "Plant-A", platformAdmin: false);

        Assert.IsType<NotFoundObjectResult>((await controller.GetTemplateById(foreign.Id)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.UpdateTemplate(foreign.Id, new UpdateGenericTemplateDto { Name = "x" })).Result);

        var update = await controller.UpdateTemplate(global.Id, new UpdateGenericTemplateDto { Name = "Hijacked" });
        var forbidden = Assert.IsType<ObjectResult>(update.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.IsType<ObjectResult>(await controller.DeleteTemplate(global.Id));

        var created = await controller.CreateTemplate(new CreateGenericTemplateDto { Code = "ORG-A-NEW", Name = "New" });
        var dto = Assert.IsType<GenericTemplateDto>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);
        Assert.Equal("Plant-A", dto.OrganizationId);

        // Platform administrators may maintain the shared catalog.
        var admin = CreateController(context, "Plant-A", platformAdmin: true);
        Assert.IsType<OkObjectResult>((await admin.UpdateTemplate(global.Id, new UpdateGenericTemplateDto { Name = "Curated" })).Result);
    }

    [Fact]
    public void Controller_AllEndpointsRequireAuthentication_AndMutationsRequirePolicies()
    {
        var type = typeof(GenericTemplatesController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(actions, m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);

        foreach (var name in new[] { "CreateTemplate", "UpdateTemplate", "DeleteTemplate", "DeriveTemplate", "TriggerSeed" })
        {
            var attr = type.GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.False(string.IsNullOrEmpty(attr?.Policy), $"{name} must declare an authorization policy.");
        }
    }
}
