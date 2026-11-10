namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Contracts.Machine;
using App.Contracts.Security;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ComponentController : ControllerBase
{
    private readonly IComponentInventoryBridge _bridge;
    private readonly AppDbContext _context;

    public ComponentController(IComponentInventoryBridge bridge, AppDbContext context)
    {
        _bridge = bridge;
        _context = context;
    }

    [HttpGet("machine/{machineId}")]
    public async Task<ActionResult<List<MachineComponentDto>>> GetMachineComponents(Guid machineId)
    {
        var components = await _bridge.GetMachineComponentsAsync(machineId);
        return Ok(components);
    }

    [HttpGet("{componentId}")]
    public async Task<ActionResult<MachineComponentDto>> GetComponent(Guid componentId)
    {
        var component = await _bridge.GetComponentByIdAsync(componentId);
        if (component == null) return NotFound(new { error = $"Component '{componentId}' not found." });
        return Ok(component);
    }

    [HttpPost("assign-part")]
    [Authorize(Policy = AuthorizationPolicies.ComponentManagement)]
    public async Task<ActionResult<ComponentPartAssignmentResult>> AssignPart([FromBody] AssignPartToComponentRequestDto request)
    {
        var result = await _bridge.AssignPartAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("{componentId}/history")]
    public async Task<ActionResult<List<ComponentPartHistoryDto>>> GetComponentPartHistory(Guid componentId)
    {
        var history = await _bridge.GetComponentPartHistoryAsync(componentId);
        return Ok(history);
    }

    [HttpGet("recipes")]
    public async Task<ActionResult<List<ComponentRecipeDto>>> GetRecipes()
    {
        var recipes = await _context.ComponentRecipes
            .AsNoTracking()
            .OrderBy(r => r.RecipeCode)
            .ToListAsync();

        var dtos = recipes.Select(r =>
        {
            JsonDocument? schema = null;
            if (!string.IsNullOrWhiteSpace(r.SpecificationSchemaJson))
            {
                try { schema = JsonDocument.Parse(r.SpecificationSchemaJson); } catch { }
            }

            EplanCadReference? eplanDefaults = null;
            if (!string.IsNullOrWhiteSpace(r.EplanCadDefaultsJson))
            {
                try { eplanDefaults = JsonSerializer.Deserialize<EplanCadReference>(r.EplanCadDefaultsJson); } catch { }
            }

            return new ComponentRecipeDto
            {
                Id = r.Id,
                RecipeCode = r.RecipeCode,
                Name = r.Name,
                Description = r.Description,
                Category = r.Category,
                ExtendsRecipeId = r.ExtendsRecipeId,
                Manufacturer = r.Manufacturer,
                ModelNumber = r.ModelNumber,
                BaseCostEur = r.BaseCostEur,
                SpecificationSchema = schema,
                AutoTags = r.AutoTags,
                EplanCadDefaults = eplanDefaults,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("recipes")]
    [Authorize(Policy = AuthorizationPolicies.ComponentManagement)]
    public async Task<ActionResult<ComponentRecipeDto>> CreateRecipe([FromBody] ComponentRecipeDto dto)
    {
        var exists = await _context.ComponentRecipes.AnyAsync(r => r.RecipeCode == dto.RecipeCode);
        if (exists)
        {
            return Conflict(new { error = $"Recipe with code '{dto.RecipeCode}' already exists." });
        }

        var recipe = new ComponentRecipe
        {
            Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
            RecipeCode = dto.RecipeCode,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            ExtendsRecipeId = dto.ExtendsRecipeId,
            Manufacturer = dto.Manufacturer,
            ModelNumber = dto.ModelNumber,
            BaseCostEur = dto.BaseCostEur,
            SpecificationSchemaJson = dto.SpecificationSchema?.RootElement.GetRawText(),
            AutoTags = dto.AutoTags ?? new List<string>(),
            EplanCadDefaultsJson = dto.EplanCadDefaults != null ? JsonSerializer.Serialize(dto.EplanCadDefaults) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.ComponentRecipes.Add(recipe);
        await _context.SaveChangesAsync();

        dto.Id = recipe.Id;
        dto.CreatedAt = recipe.CreatedAt;
        dto.UpdatedAt = recipe.UpdatedAt;

        return CreatedAtAction(nameof(GetRecipeById), new { id = recipe.Id }, dto);
    }

    [HttpGet("recipes/{id}")]
    public async Task<ActionResult<ComponentRecipeDto>> GetRecipeById(Guid id)
    {
        var r = await _context.ComponentRecipes.FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();

        JsonDocument? schema = null;
        if (!string.IsNullOrWhiteSpace(r.SpecificationSchemaJson))
        {
            try { schema = JsonDocument.Parse(r.SpecificationSchemaJson); } catch { }
        }

        EplanCadReference? eplanDefaults = null;
        if (!string.IsNullOrWhiteSpace(r.EplanCadDefaultsJson))
        {
            try { eplanDefaults = JsonSerializer.Deserialize<EplanCadReference>(r.EplanCadDefaultsJson); } catch { }
        }

        return Ok(new ComponentRecipeDto
        {
            Id = r.Id,
            RecipeCode = r.RecipeCode,
            Name = r.Name,
            Description = r.Description,
            Category = r.Category,
            ExtendsRecipeId = r.ExtendsRecipeId,
            Manufacturer = r.Manufacturer,
            ModelNumber = r.ModelNumber,
            BaseCostEur = r.BaseCostEur,
            SpecificationSchema = schema,
            AutoTags = r.AutoTags,
            EplanCadDefaults = eplanDefaults,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        });
    }
}
