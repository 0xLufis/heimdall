namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Backend.Api.Services;
using App.Contracts.Security;
using App.Shared.Entities;
using App.Shared.Errors;
using App.Shared.Lua.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public record ExecuteLuaRequestDto(
    string Code,
    Dictionary<string, object?>? Context = null
);

public record InvokeUiActionRequestDto(
    Dictionary<string, object?>? Context = null
);

public record EvaluateTelemetryLuaRequestDto(
    string MachineId,
    string Metric,
    double Value
);

[ApiController]
[Route("api/v1/lua")]
[Authorize]
public class LuaScriptController : ControllerBase
{
    private static readonly int MaxStoredScriptLength = new App.Shared.Lua.Core.LuaSandboxOptions().MaxScriptLength;
    private readonly ILuaAutomationService _luaService;

    public LuaScriptController(ILuaAutomationService luaService)
    {
        _luaService = luaService;
    }

    /// <summary>
    /// Lists all plant-specific automation scripts (including source), restricted to roles that may run them.
    /// </summary>
    [HttpGet("scripts")]
    [Authorize(Policy = AuthorizationPolicies.RemoteExecution)]
    public async Task<IActionResult> ListScripts(CancellationToken cancellationToken)
    {
        var scripts = await _luaService.ListScriptsAsync(cancellationToken);
        return Ok(scripts);
    }

    /// <summary>
    /// Gets a single script definition by ID.
    /// </summary>
    [HttpGet("scripts/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RemoteExecution)]
    public async Task<IActionResult> GetScript(Guid id, CancellationToken cancellationToken)
    {
        var script = await _luaService.GetScriptAsync(id, cancellationToken);
        if (script == null)
        {
            return NotFound(new ApiError(ErrorCode.None, $"Script with ID '{id}' was not found."));
        }
        return Ok(script);
    }

    /// <summary>
    /// Creates a new plant automation script.
    /// </summary>
    [HttpPost("scripts")]
    [Authorize(Policy = AuthorizationPolicies.EngineeringAdministration)]
    public async Task<IActionResult> CreateScript([FromBody] LuaScriptRecord script, CancellationToken cancellationToken)
    {
        if (script == null || string.IsNullOrWhiteSpace(script.Name) || string.IsNullOrWhiteSpace(script.Code))
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, "Script name and code are required."));
        }
        if (script.Code.Length > MaxStoredScriptLength)
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, $"Script code exceeds {MaxStoredScriptLength} characters."));
        }

        var created = await _luaService.CreateScriptAsync(script, User?.Identity?.Name, cancellationToken);
        return CreatedAtAction(nameof(GetScript), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing plant automation script.
    /// </summary>
    [HttpPut("scripts/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EngineeringAdministration)]
    public async Task<IActionResult> UpdateScript(Guid id, [FromBody] LuaScriptRecord script, CancellationToken cancellationToken)
    {
        if (script == null || string.IsNullOrWhiteSpace(script.Code))
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, "Script payload is required."));
        }
        if (script.Code.Length > MaxStoredScriptLength)
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, $"Script code exceeds {MaxStoredScriptLength} characters."));
        }

        var updated = await _luaService.UpdateScriptAsync(id, script, cancellationToken);
        if (updated == null)
        {
            return NotFound(new ApiError(ErrorCode.None, $"Script with ID '{id}' was not found."));
        }
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a script by ID.
    /// </summary>
    [HttpDelete("scripts/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.EngineeringAdministration)]
    public async Task<IActionResult> DeleteScript(Guid id, CancellationToken cancellationToken)
    {
        bool deleted = await _luaService.DeleteScriptAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new ApiError(ErrorCode.None, $"Script with ID '{id}' was not found."));
        }
        return NoContent();
    }

    /// <summary>
    /// Executes a registered script by ID.
    /// </summary>
    [HttpPost("scripts/{id:guid}/run")]
    [Authorize(Policy = AuthorizationPolicies.RemoteExecution)]
    public async Task<IActionResult> RunScript(
        Guid id,
        [FromBody] Dictionary<string, object?>? context,
        CancellationToken cancellationToken)
    {
        var result = await _luaService.RunScriptAsync(id, context, cancellationToken);
        if (result == null)
        {
            return NotFound(new ApiError(ErrorCode.None, $"Script with ID '{id}' was not found."));
        }
        return Ok(result);
    }

    /// <summary>
    /// Tests/evaluates an arbitrary Lua script snippet in the sandbox with optional context.
    /// Ad-hoc code has the same power as a stored script (it can mutate machines and tickets), so it
    /// requires the script-authoring policy rather than the broader RemoteExecution policy.
    /// </summary>
    [HttpPost("execute")]
    [Authorize(Policy = AuthorizationPolicies.EngineeringAdministration)]
    public async Task<IActionResult> TestExecute(
        [FromBody] ExecuteLuaRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, "Lua code is required."));
        }

        var result = await _luaService.TestExecuteAsync(request.Code, request.Context, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns dynamic UI extensions registered by plant Lua scripts.
    /// </summary>
    [HttpGet("ui-extensions")]
    public async Task<IActionResult> GetUiExtensions([FromQuery] LuaUiTargetZone? zone, CancellationToken cancellationToken)
    {
        var extensions = await _luaService.GetUiExtensionsAsync(zone, cancellationToken);
        return Ok(extensions);
    }

    /// <summary>
    /// Invokes the Lua handler for a registered dynamic UI action.
    /// </summary>
    [HttpPost("ui-extensions/{extensionId}/invoke")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> InvokeUiAction(
        string extensionId,
        [FromBody] InvokeUiActionRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _luaService.InvokeUiActionAsync(extensionId, request?.Context, cancellationToken);
        if (result == null)
        {
            return NotFound(new ApiError(ErrorCode.None, $"UI action '{extensionId}' is not registered."));
        }
        return Ok(result);
    }

    /// <summary>
    /// Evaluates active telemetry Lua rules against an incoming data point.
    /// </summary>
    [HttpPost("telemetry/evaluate")]
    [Authorize(Policy = AuthorizationPolicies.RemoteExecution)]
    public async Task<IActionResult> EvaluateTelemetry(
        [FromBody] EvaluateTelemetryLuaRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MachineId) || string.IsNullOrWhiteSpace(request.Metric))
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, "MachineId and Metric are required."));
        }

        var results = await _luaService.EvaluateTelemetryChangeAsync(request.MachineId, request.Metric, request.Value, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Evaluates SLA preventive maintenance rules across plant stations.
    /// </summary>
    [HttpPost("sla/evaluate")]
    [Authorize(Policy = AuthorizationPolicies.RemoteExecution)]
    public async Task<IActionResult> EvaluateSlaRules(CancellationToken cancellationToken)
    {
        var results = await _luaService.EvaluateSlaPreventiveRulesAsync(cancellationToken);
        return Ok(results);
    }
}
