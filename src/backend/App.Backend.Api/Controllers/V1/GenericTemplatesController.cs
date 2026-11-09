namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using App.Contracts.Security;
using App.Contracts.Templates;
using App.Infrastructure.Services;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public class GenericTemplatesController : ControllerBase
{
    private readonly IGenericTemplateService _templateService;

    public GenericTemplatesController(IGenericTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GenericTemplateDto>>> GetTemplates(
        [FromQuery] string? type = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] bool? isActive = null)
    {
        // Auto-seed initial templates on first load if empty
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
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TemplateTypeDefinitionDto>>> GetTypes()
    {
        var types = await _templateService.GetSupportedTemplateTypesAsync(HttpContext.RequestAborted);
        return Ok(types);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<GenericTemplateDto>> GetTemplateById(Guid id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id, HttpContext.RequestAborted);
        if (template == null) return NotFound(new { error = $"Template '{id}' not found." });
        return Ok(template);
    }

    [HttpGet("code/{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<GenericTemplateDto>> GetTemplateByCode(string code)
    {
        var template = await _templateService.GetTemplateByCodeAsync(code, HttpContext.RequestAborted);
        if (template == null) return NotFound(new { error = $"Template with code '{code}' not found." });
        return Ok(template);
    }

    [HttpGet("{id:guid}/resolve")]
    [AllowAnonymous]
    public async Task<ActionResult<TemplateResolutionResultDto>> ResolveEffectiveSchema(Guid id)
    {
        var resolved = await _templateService.ResolveEffectiveSchemaAsync(id, HttpContext.RequestAborted);
        if (resolved == null) return NotFound(new { error = $"Template '{id}' not found." });
        return Ok(resolved);
    }

    [HttpPost]
    public async Task<ActionResult<GenericTemplateDto>> CreateTemplate([FromBody] CreateGenericTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Template 'code' is required." });
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Template 'name' is required." });

        var orgId = User.FindFirstValue("organization_id") ?? User.FindFirstValue("tenant_id");

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
    public async Task<ActionResult<GenericTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateGenericTemplateDto dto)
    {
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
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        var deleted = await _templateService.DeleteTemplateAsync(id, HttpContext.RequestAborted);
        if (!deleted) return NotFound(new { error = $"Template '{id}' not found." });
        return NoContent();
    }

    [HttpPost("{id:guid}/derive")]
    public async Task<ActionResult<GenericTemplateDto>> DeriveTemplate(Guid id, [FromBody] DeriveTemplateRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewCode))
            return BadRequest(new { error = "Derived template 'newCode' is required." });
        if (string.IsNullOrWhiteSpace(dto.NewName))
            return BadRequest(new { error = "Derived template 'newName' is required." });

        var orgId = User.FindFirstValue("organization_id") ?? User.FindFirstValue("tenant_id");

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
    [AllowAnonymous]
    public async Task<ActionResult<TemplateValidationResultDto>> ValidateInstance(Guid id, [FromBody] Dictionary<string, object?> data)
    {
        var result = await _templateService.ValidateInstanceDataAsync(id, data, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("{id:guid}/instantiate")]
    [AllowAnonymous]
    public async Task<ActionResult<TemplateInstantiationResultDto>> Instantiate(Guid id, [FromBody] InstantiateTemplateRequestDto dto)
    {
        var result = await _templateService.InstantiateTemplateAsync(id, dto, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("seed")]
    public async Task<ActionResult> TriggerSeed()
    {
        await _templateService.SeedDefaultTemplatesIfEmptyAsync(HttpContext.RequestAborted);
        return Ok(new { success = true, message = "Seed check completed." });
    }
}
