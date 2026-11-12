namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using App.Backend.Api.Security;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Machine groups (business units, projects, lines, sub-lines, technology fleets, manual groups).
///
/// Reads are global: every signed-in user sees every group. Writes (create, update incl. machine
/// membership, delete) additionally to <see cref="AuthorizationPolicies.EndpointConfigManagement"/>
/// are scoped by the group's <see cref="MachineGroup.OrganizationId"/>:
/// <list type="bullet">
///   <item>platform administrators (<see cref="AuthorizationPolicies.SystemAdministration"/>): any group;</item>
///   <item>an organization's groups: members of that organization;</item>
///   <item>groups without an organization (platform-managed fleets that span organizations):
///   platform administrators and <see cref="AuthorizationPolicies.EngineeringAdministration"/>, whose
///   remit is exactly the plant-wide machine-group structure.</item>
/// </list>
/// New groups belong to the caller's active organization; only platform administrators may pick
/// another one (or none, by posting an empty <c>organizationId</c>) or move a group between organizations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MachineGroupController : ControllerBase
{
    private readonly IMachineGroupRepository _repository;
    private readonly IAuthorizationService? _authorization;

    public MachineGroupController(IMachineGroupRepository repository, IAuthorizationService? authorization = null)
    {
        _repository = repository;
        _authorization = authorization;
    }

    [HttpGet]
    public async Task<ActionResult<List<MachineGroup>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetAllAsync(cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MachineGroup>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var group = await _repository.GetByIdAsync(id, cancellationToken);
        if (group == null) return NotFound();
        return Ok(group);
    }

    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<MachineGroup>> Create([FromBody] MachineGroup group, CancellationToken cancellationToken)
    {
        // Only the group's own columns are taken from the client: a posted parent / children /
        // membership graph would otherwise be inserted along with it.
        group.Parent = null;
        group.Children = new();
        group.Memberships = new();
        group.Machines = new();

        var isPlatformAdmin = await SatisfiesAsync(AuthorizationPolicies.SystemAdministration);
        string? organizationId;
        if (isPlatformAdmin && group.OrganizationId != null)
        {
            // A platform administrator picks the organization; an empty value means platform-managed.
            organizationId = string.IsNullOrWhiteSpace(group.OrganizationId) ? null : group.OrganizationId.Trim();
            if (organizationId != null && !await _repository.OrganizationExistsAsync(organizationId, cancellationToken))
            {
                return BadRequest(new { message = $"Organization '{organizationId}' does not exist." });
            }
        }
        else
        {
            organizationId = await ActiveOrganizationIdAsync(cancellationToken);
        }

        if (!await CanWriteAsync(organizationId, cancellationToken))
        {
            return Forbidden(organizationId == null
                ? "Machine groups without an organization are platform-managed: only platform and engineering administrators may create them. Switch to an organization you are a member of first."
                : $"Only members of organization '{organizationId}' may create machine groups for it.");
        }

        group.OrganizationId = organizationId;
        var created = await _repository.CreateAsync(group, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<MachineGroup>> Update(Guid id, [FromBody] MachineGroup updates, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing == null) return NotFound();
        if (!await CanWriteAsync(existing.OrganizationId, cancellationToken)) return ForbiddenFor(existing);

        if (await SatisfiesAsync(AuthorizationPolicies.SystemAdministration))
        {
            // PUT replaces every field: the posted organization (null / empty = platform-managed) applies.
            var target = string.IsNullOrWhiteSpace(updates.OrganizationId) ? null : updates.OrganizationId.Trim();
            if (target != null && target != existing.OrganizationId && !await _repository.OrganizationExistsAsync(target, cancellationToken))
            {
                return BadRequest(new { message = $"Organization '{target}' does not exist." });
            }
            updates.OrganizationId = target;
        }
        else
        {
            // Everyone else edits the group where it is; moving it to another organization is refused silently.
            updates.OrganizationId = existing.OrganizationId;
        }

        MachineGroup? updated;
        try
        {
            updated = await _repository.UpdateAsync(id, updates, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing == null) return NotFound();
        if (!await CanWriteAsync(existing.OrganizationId, cancellationToken)) return ForbiddenFor(existing);

        var success = await _repository.DeleteAsync(id, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>The write rule described on the class. Fails closed without an authorization service.</summary>
    private async Task<bool> CanWriteAsync(string? organizationId, CancellationToken cancellationToken)
    {
        if (await SatisfiesAsync(AuthorizationPolicies.SystemAdministration)) return true;
        if (organizationId == null) return await SatisfiesAsync(AuthorizationPolicies.EngineeringAdministration);

        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId)
               && await _repository.IsOrganizationMemberAsync(userId, organizationId, cancellationToken);
    }

    /// <summary>
    /// The session's active organization (the <c>OrgId</c> claim, which BetterAuthHandler only issues
    /// while the user is a member). Synthetic organizations of the development fallback principals
    /// ("Heimdall Root", "Global") are not real organizations and count as none.
    /// </summary>
    private async Task<string?> ActiveOrganizationIdAsync(CancellationToken cancellationToken)
    {
        var orgId = User?.FindFirstValue(TenantScopeMiddleware.OrgClaimType);
        if (string.IsNullOrWhiteSpace(orgId)) return null;
        return await _repository.OrganizationExistsAsync(orgId, cancellationToken) ? orgId : null;
    }

    private async Task<bool> SatisfiesAsync(string policy) =>
        _authorization != null && User != null && (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

    private ObjectResult ForbiddenFor(MachineGroup group) => Forbidden(group.OrganizationId == null
        ? $"Machine group '{group.Name}' is platform-managed: only platform and engineering administrators may change it."
        : $"Machine group '{group.Name}' belongs to organization '{group.OrganizationId}': only its members may change it.");

    private ObjectResult Forbidden(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message });
}
