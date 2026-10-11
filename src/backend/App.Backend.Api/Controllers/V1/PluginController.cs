namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Backend.Api.Services.Plugins;
using App.Shared.Errors;
using App.Shared.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/[controller]")]
public class PluginController : ControllerBase
{
    private readonly IPluginService _pluginService;

    public PluginController(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    [HttpGet]
    public async Task<IActionResult> ListPlugins()
    {
        var plugins = await _pluginService.ListPluginsAsync();
        return Ok(plugins);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlugin(string id)
    {
        var bundle = await _pluginService.GetPluginBundleAsync(id);
        if (bundle == null)
        {
            return NotFound(new ApiError(ErrorCode.PluginNotFound, $"Plugin with ID '{id}' was not found."));
        }
        return Ok(bundle.Manifest);
    }

    [HttpGet("{id}/bundle")]
    public async Task<IActionResult> GetPluginBundle(string id)
    {
        var bundle = await _pluginService.GetPluginBundleAsync(id);
        if (bundle == null)
        {
            return NotFound(new ApiError(ErrorCode.PluginNotFound, $"Plugin with ID '{id}' was not found."));
        }
        return Ok(bundle);
    }

    [HttpGet("public-key")]
    public IActionResult GetMasterPublicKey()
    {
        string pem = _pluginService.GetMasterPublicKeyPem();
        return Ok(new { PublicKeyPem = pem });
    }

    public record RegisterPluginDto(PluginManifest Manifest, Dictionary<string, string> Files, bool Sign = true);

    [HttpPost]
    public async Task<IActionResult> RegisterPlugin([FromBody] RegisterPluginDto dto)
    {
        if (dto == null || dto.Manifest == null || dto.Files == null || dto.Files.Count == 0)
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, "Plugin manifest and file bundle are required."));
        }

        try
        {
            var registered = await _pluginService.RegisterAndSignPluginAsync(dto.Manifest, dto.Files, dto.Sign);
            return CreatedAtAction(nameof(GetPlugin), new { id = registered.PluginId }, registered);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, ex.Message));
        }
    }

    [HttpPost("{id}/push/agent/{clientPcId:guid}")]
    public async Task<IActionResult> PushToAgent(string id, Guid clientPcId)
    {
        bool success = await _pluginService.PushPluginToAgentAsync(id, clientPcId);
        if (!success)
        {
            return NotFound(new ApiError(ErrorCode.ClientPcNotFound, $"Failed to push plugin '{id}': Plugin or target Agent not found."));
        }
        return Ok(new { Message = $"Plugin '{id}' queued for deployment to agent {clientPcId}." });
    }

    [HttpPost("{id}/push/machine/{machineId:guid}")]
    public async Task<IActionResult> PushToMachine(string id, Guid machineId)
    {
        bool success = await _pluginService.PushPluginToMachineAsync(id, machineId);
        if (!success)
        {
            return NotFound(new ApiError(ErrorCode.MachineNotFound, $"Failed to push plugin '{id}': Plugin or target Machine not found."));
        }
        return Ok(new { Message = $"Plugin '{id}' queued for deployment to machine {machineId}." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlugin(string id)
    {
        bool removed = await _pluginService.DeletePluginAsync(id);
        if (!removed)
        {
            return NotFound(new ApiError(ErrorCode.PluginNotFound, $"Plugin with ID '{id}' was not found."));
        }
        return NoContent();
    }
}
