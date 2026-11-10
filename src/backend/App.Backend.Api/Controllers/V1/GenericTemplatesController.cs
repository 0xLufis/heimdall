namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using App.Contracts.Security;
using App.Contracts.Templates;
using App.Infrastructure.Services;
using App.Shared.Data;

/// <summary>
/// Generic template catalog. All endpoints require an authenticated caller; reads are scoped to the
/// caller's organization (plus global/builtin templates) through the <see cref="AppDbContext"/> tenant
/// query filter, and mutations require engineering configuration rights.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
[Authorize]
public class GenericTemplatesController : ControllerBase
{
    internal const string OrgClaimType = "OrgId";

    private readonly IGenericTemplateService _templateService;
    private readonly AppDbContext _context;
    private readonly IAuthorizationService _authorizationService;

    public GenericTemplatesController(
        IGenericTemplateService templateService,
        AppDbContext context,
        IAuthorizationService authorizationService)
    {
        _templateService = templateService;
        _context = context;
        _authorizationService = authorizationService;
    }

    private string? CallerOrganizationId => User.FindFirstValue(OrgClaimType);

    private async Task<bool> IsPlatformAdminAsync() =>
        (await _authorizationService.AuthorizeAsync(User, AuthorizationPolicies.SystemAdministration)).Succeeded;

    /// <summary>
    /// Restricts every template query issued during this request to the caller's organization and
    /// global templates. Platform administrators see all tenants.
    /// </summary>
    private async Task ApplyTenantScopeAsync()
    {
        _context.CurrentOrganizationId = await IsPlatformAdminAsync() ? null : CallerOrganizationId;
    }

    /// <summary>
    /// Global (organization-less) templates are shared by every tenant, so only platform
    /// administrators may change them once the caller is bound to an organization.
    /// </summary>
    private async Task<ActionResult?> EnsureCanModifyAsync(Guid id)
    {
        var existing = await _templateService.GetTemplateByIdAsync(id, HttpContext.RequestAborted);
        if (existing == null) return NotFound(new { error = $"Template '{id}' not found." });
        if (existing.OrganizationId == null && CallerOrganizationId != null && !await IsPlatformAdminAsync())
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Global templates can only be modified by platform administrators. Derive an organization template instead." });
        return null;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GenericTemplateDto>>> GetTemplates(
        [FromQuery] string? type = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] bool? isActive = null)
    {
        await ApplyTenantScopeAsync();

        // Idempotently upsert any missing builtin (global) templates so the catalog is editable in the DB.
        await _templateService.SeedDefaultTemplatesIfEmptyAsync(HttpContext.RequestAborted);

        var filter = new TemplateQueryFilterDto
        {
            TemplateType = type,
            Category = category,
            Search = search,
            Tag = tag,
            IsActive = isActive
        };

        var templates = await _templateService.GetTemplatesAsync(filter, HttpContext.RequestAborted);
        return Ok(templates);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<TemplateTypeDefinitionDto>>> GetTypes()
    {
        await ApplyTenantScopeAsync();
        var types = await _templateService.GetSupportedTemplateTypesAsync(HttpContext.RequestAborted);
        return Ok(types);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GenericTemplateDto>> GetTemplateById(Guid id)
    {
        await ApplyTenantScopeAsync();
        var template = await _templateService.GetTemplateByIdAsync(id, HttpContext.RequestAborted);
        if (template == null) return NotFound(new { error = $"Template '{id}' not found." });
        return Ok(template);
    }

    [HttpGet("code/{code}")]
    public async Task<ActionResult<GenericTemplateDto>> GetTemplateByCode(string code)
    {
        await ApplyTenantScopeAsync();
        var template = await _templateService.GetTemplateByCodeAsync(code, HttpContext.RequestAborted);
        if (template == null) return NotFound(new { error = $"Template with code '{code}' not found." });
        return Ok(template);
    }

    [HttpGet("{id:guid}/resolve")]
    public async Task<ActionResult<TemplateResolutionResultDto>> ResolveEffectiveSchema(Guid id)
    {
        await ApplyTenantScopeAsync();
        var resolved = await _templateService.ResolveEffectiveSchemaAsync(id, HttpContext.RequestAborted);
        if (resolved == null) return NotFound(new { error = $"Template '{id}' not found." });
        return Ok(resolved);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EndpointConfigManagement)]
    public async Task<ActionResult<GenericTemplateDto>> CreateTemplate([FromBody] CreateGenericTemplateDto dto)
    {
        await ApplyTenantScopeAsync();
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Template 'code' is required." });
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Template 'name' is required." });

        var orgId = CallerOrganizationId;

        try
        {
            var created = await _templateService.CreateTemplateAsync(dto, orgId, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetTemplateById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EndpointConfigManagement)]
    public async Task<ActionResult<GenericTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateGenericTemplateDto dto)
    {
        await ApplyTenantScopeAsync();
        var denied = await EnsureCanModifyAsync(id);
        if (denied != null) return denied;

        try
        {
            var updated = await _templateService.UpdateTemplateAsync(id, dto, HttpContext.RequestAborted);
            if (updated == null) return NotFound(new { error = $"Template '{id}' not found." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EndpointConfigManagement)]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        await ApplyTenantScopeAsync();
        var denied = await EnsureCanModifyAsync(id);
        if (denied != null) return denied;

        var deleted = await _templateService.DeleteTemplateAsync(id, HttpContext.RequestAborted);
        if (!deleted) return NotFound(new { error = $"Template '{id}' not found." });
        return NoContent();
    }

    [HttpPost("{id:guid}/derive")]
    [Authorize(Policy = AuthorizationPolicies.EndpointConfigManagement)]
    public async Task<ActionResult<GenericTemplateDto>> DeriveTemplate(Guid id, [FromBody] DeriveTemplateRequestDto dto)
    {
        await ApplyTenantScopeAsync();
        if (string.IsNullOrWhiteSpace(dto.NewCode))
            return BadRequest(new { error = "Derived template 'newCode' is required." });
        if (string.IsNullOrWhiteSpace(dto.NewName))
            return BadRequest(new { error = "Derived template 'newName' is required." });

        var orgId = CallerOrganizationId;

        try
        {
            var derived = await _templateService.DeriveTemplateAsync(id, dto, orgId, HttpContext.RequestAborted);
            if (derived == null) return NotFound(new { error = $"Source template '{id}' not found." });
            return CreatedAtAction(nameof(GetTemplateById), new { id = derived.Id }, derived);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<TemplateValidationResultDto>> ValidateInstance(Guid id, [FromBody] Dictionary<string, object?> data)
    {
        await ApplyTenantScopeAsync();
        var result = await _templateService.ValidateInstanceDataAsync(id, data, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("{id:guid}/instantiate")]
    public async Task<ActionResult<TemplateInstantiationResultDto>> Instantiate(Guid id, [FromBody] InstantiateTemplateRequestDto dto)
    {
        await ApplyTenantScopeAsync();
        var result = await _templateService.InstantiateTemplateAsync(id, dto, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("seed")]
    [Authorize(Policy = AuthorizationPolicies.SystemAdministration)]
    public async Task<ActionResult> TriggerSeed()
    {
        await ApplyTenantScopeAsync();
        await _templateService.SeedDefaultTemplatesIfEmptyAsync(HttpContext.RequestAborted);
        return Ok(new { success = true, message = "Seed check completed." });
    }
}
