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
    public async Task<ActionResult<List<DelegationRecipeDto>>> GetRecipesForMachine(string machineId)
    {
        var recipes = await _delegationService.GetRecipesForMachineAsync(machineId);
        return Ok(recipes);
    }

    [NonAction]
    public Task<ActionResult<List<DelegationRecipeDto>>> GetRecipesForMachine(Guid machineId)
        => GetRecipesForMachine(machineId.ToString());

    [HttpPost("bind")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult<MachineDelegationBindingDto>> BindRecipe([FromBody] BindRecipeRequest request)
    {
        try
        {
            var result = await _delegationService.BindRecipeToMachineAsync(request.MachineId, request.RecipeId, request.Discipline);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("bindings/{bindingId}")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> UnbindRecipe(Guid bindingId)
    {
        var success = await _delegationService.UnbindRecipeFromMachineAsync(bindingId);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("recipes/{id}")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> DeleteRecipe(Guid id)
    {
        return await _delegationService.DeleteRecipeAsync(id) ? NoContent() : NotFound();
    }

    [HttpDelete("groups/{id}")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> DeletePersonnelGroup(Guid id)
    {
        return await _delegationService.DeletePersonnelGroupAsync(id) switch
        {
            PersonnelGroupDeleteResult.Deleted => NoContent(),
            PersonnelGroupDeleteResult.InUse => Conflict(new { error = "Personnel group is still referenced by one or more delegation recipes. Reassign those recipes first." }),
            _ => NotFound()
        };
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
        [FromQuery] WorkerScheduleType scheduleType = WorkerScheduleType.Shift,
        [FromQuery] string? technology = null)
    {
        var resolved = await _delegationService.ResolvePersonnelForMachineAsync(machineId, discipline, scheduleType, technology);
        return Ok(resolved);
    }

    [HttpGet("applicable/{machineId}")]
    public async Task<ActionResult<List<DelegationRecipeDto>>> GetApplicableRecipes(Guid machineId)
    {
        var recipes = await _delegationService.GetRecipesApplicableToMachineAsync(machineId);
        return Ok(recipes);
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
            // Server-assigned id and tenant: never trust the client to choose either.
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Discipline = dto.Discipline,
            ShiftPattern = dto.ShiftPattern,
            OrganizationId = User.FindFirst("OrgId")?.Value ?? dto.OrganizationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (dto.MemberUserIds != null)
        {
            foreach (var userId in dto.MemberUserIds.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
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
        dto.OrganizationId = group.OrganizationId;
        return CreatedAtAction(nameof(GetPersonnelGroups), new { id = group.Id }, dto);
    }

    [HttpGet("machine/{machineId}/dedications")]
    public async Task<ActionResult<MachineDedicationsDto>> GetMachineDedications(string machineId)
    {
        try
        {
            var dedications = await _delegationService.GetMachineDedicationsAsync(machineId);
            return Ok(dedications);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [NonAction]
    public Task<ActionResult<MachineDedicationsDto>> GetMachineDedications(Guid machineId)
        => GetMachineDedications(machineId.ToString());

    [HttpPost("machine/{machineId}/dedicate")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult<MachineDedicationsDto>> DedicateToMachine(string machineId, [FromBody] DedicationAssignmentRequest request)
    {
        try
        {
            var result = await _delegationService.DedicateToMachineAsync(machineId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [NonAction]
    public Task<ActionResult<MachineDedicationsDto>> DedicateToMachine(Guid machineId, DedicationAssignmentRequest request)
        => DedicateToMachine(machineId.ToString(), request);

    [HttpDelete("machine/{machineId}/dedicate")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> RemoveMachineDedication(
        string machineId,
        [FromQuery] string? targetId = null,
        [FromQuery] string? technology = null)
    {
        var success = await _delegationService.RemoveMachineDedicationAsync(machineId, targetId, technology);
        if (!success) return NotFound();
        return NoContent();
    }

    [NonAction]
    public Task<ActionResult> RemoveMachineDedication(Guid machineId, string? targetId = null, string? technology = null)
        => RemoveMachineDedication(machineId.ToString(), targetId, technology);

    [HttpGet("user/{userId}/dedications")]
    public async Task<ActionResult<UserDedicationsDto>> GetUserDedications(string userId)
    {
        var dedications = await _delegationService.GetUserDedicationsAsync(userId);
        return Ok(dedications);
    }

    [HttpPost("user/{userId}/dedicate-machine/{machineId}")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> DedicateUserToMachine(
        string userId,
        string machineId,
        [FromQuery] string? technology = null)
    {
        var success = await _delegationService.DedicateUserToMachineAsync(userId, machineId, technology);
        if (!success) return NotFound();
        return Ok(new { success = true });
    }

    [NonAction]
    public Task<ActionResult> DedicateUserToMachine(string userId, Guid machineId, string? technology = null)
        => DedicateUserToMachine(userId, machineId.ToString(), technology);

    [HttpDelete("user/{userId}/dedicate-machine/{machineId}")]
    [Authorize(Policy = AuthorizationPolicies.DelegationManagement)]
    public async Task<ActionResult> RemoveUserDedicationFromMachine(
        string userId,
        string machineId,
        [FromQuery] string? technology = null)
    {
        var success = await _delegationService.RemoveUserDedicationFromMachineAsync(userId, machineId, technology);
        if (!success) return NotFound();
        return NoContent();
    }

    [NonAction]
    public Task<ActionResult> RemoveUserDedicationFromMachine(string userId, Guid machineId, string? technology = null)
        => RemoveUserDedicationFromMachine(userId, machineId.ToString(), technology);
}
