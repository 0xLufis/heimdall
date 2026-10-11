namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Contracts.Enums;
using App.Contracts.Personnel;
using App.Contracts.Security;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

public record BindRecipeRequest(Guid MachineId, Guid RecipeId, DelegationDiscipline Discipline = DelegationDiscipline.General);

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DelegationController : ControllerBase
{
    private readonly IDelegationRecipeService _delegationService;
    private readonly AppDbContext _context;

    public DelegationController(IDelegationRecipeService delegationService, AppDbContext context)
    {
        _delegationService = delegationService;
        _context = context;
    }

    [HttpGet("machine/{machineId}")]
    public async Task<ActionResult<List<DelegationRecipeDto>>> GetRecipesForMachine(Guid machineId)
    {
        var recipes = await _delegationService.GetRecipesForMachineAsync(machineId);
        return Ok(recipes);
    }

    [HttpPost("bind")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult<MachineDelegationBindingDto>> BindRecipe([FromBody] BindRecipeRequest request)
    {
        var result = await _delegationService.BindRecipeToMachineAsync(request.MachineId, request.RecipeId, request.Discipline);
        return Ok(result);
    }

    [HttpDelete("bindings/{bindingId}")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> UnbindRecipe(Guid bindingId)
    {
        var success = await _delegationService.UnbindRecipeFromMachineAsync(bindingId);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpGet("recipes")]
    public async Task<ActionResult<List<DelegationRecipeDto>>> GetAllRecipes()
    {
        var recipes = await _delegationService.GetAllRecipesAsync();
        return Ok(recipes);
    }

    [HttpGet("recipes/{id}")]
    public async Task<ActionResult<DelegationRecipeDto>> GetRecipeById(Guid id)
    {
        var recipe = await _delegationService.GetRecipeByIdAsync(id);
        if (recipe == null) return NotFound();
        return Ok(recipe);
    }

    [HttpPost("recipes")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult<DelegationRecipeDto>> CreateOrUpdateRecipe([FromBody] DelegationRecipeDto dto)
    {
        var saved = await _delegationService.CreateOrUpdateRecipeAsync(dto);
        return Ok(saved);
    }

    [HttpGet("resolve/{machineId}")]
    public async Task<ActionResult<ResolvedPersonnelDto>> ResolvePersonnel(
        Guid machineId,
        [FromQuery] DelegationDiscipline discipline = DelegationDiscipline.General,
        [FromQuery] WorkerScheduleType scheduleType = WorkerScheduleType.Shift)
    {
        var resolved = await _delegationService.ResolvePersonnelForMachineAsync(machineId, discipline, scheduleType);
        return Ok(resolved);
    }

    [HttpGet("groups")]
    public async Task<ActionResult<List<PersonnelGroupDto>>> GetPersonnelGroups()
    {
        var groups = await _context.PersonnelGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .OrderBy(g => g.Name)
            .ToListAsync();

        var dtos = groups.Select(g => new PersonnelGroupDto
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            Discipline = g.Discipline,
            ShiftPattern = g.ShiftPattern,
            OrganizationId = g.OrganizationId,
            MemberUserIds = g.Members.Select(m => m.UserId).ToList()
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("groups")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult<PersonnelGroupDto>> CreatePersonnelGroup([FromBody] PersonnelGroupDto dto)
    {
        var group = new PersonnelGroup
        {
            Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Discipline = dto.Discipline,
            ShiftPattern = dto.ShiftPattern,
            OrganizationId = dto.OrganizationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (dto.MemberUserIds != null)
        {
            foreach (var userId in dto.MemberUserIds)
            {
                group.Members.Add(new PersonnelGroupMember
                {
                    Id = Guid.NewGuid(),
                    PersonnelGroupId = group.Id,
                    UserId = userId,
                    JoinedAt = DateTimeOffset.UtcNow
                });
            }
        }

        _context.PersonnelGroups.Add(group);
        await _context.SaveChangesAsync();

        dto.Id = group.Id;
        return CreatedAtAction(nameof(GetPersonnelGroups), new { id = group.Id }, dto);
    }
}
