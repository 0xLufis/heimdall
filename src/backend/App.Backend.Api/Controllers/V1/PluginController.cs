namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using App.Backend.Api.Services.Plugins;
using App.Contracts.Security;
using App.Shared.Errors;
using App.Shared.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Agent plugins. Every endpoint requires the EndpointConfigManagement policy. Installing goes
/// through <c>POST signed</c>: the user signs the package with their user certificate, the server
/// verifies and countersigns with its computer certificate, agents verify both before installing.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = AuthorizationPolicies.EndpointConfigManagement)]
public class PluginController : ControllerBase
{
    private readonly IPluginService _pluginService;
    private readonly IConfiguration? _configuration;
    private readonly IWebHostEnvironment? _environment;

    public PluginController(IPluginService pluginService, IConfiguration? configuration = null, IWebHostEnvironment? environment = null)
    {
        _pluginService = pluginService;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>Unsigned (server-signed only) registration is a development convenience.</summary>
    private bool DevPluginUploadAllowed =>
        string.Equals(_configuration?["HEIMDALL_ENABLE_DEV"] ?? Environment.GetEnvironmentVariable("HEIMDALL_ENABLE_DEV"), "true", StringComparison.OrdinalIgnoreCase) ||
        _environment?.IsDevelopment() == true;

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

    /// <summary>Signed upload: the package file (Base64) and the installing user's detached signature envelope.</summary>
    public record RegisterSignedPluginDto(string PackageBase64, PluginSignatureEnvelope Signature);

    /// <summary>
    /// Installs a plugin signed by the calling user (see scripts/sign-plugin.sh, scripts/Sign-HeimdallPlugin.ps1).
    /// 403 when the signature, its certificate chain or the certificate-to-user mapping does not hold.
    /// </summary>
    [HttpPost("signed")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> RegisterSignedPlugin([FromBody] RegisterSignedPluginDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.PackageBase64) || dto.Signature == null)
            return BadRequest(new ApiError(ErrorCode.InvalidInput, "PackageBase64 and Signature are required."));

        byte[] package;
        try { package = Convert.FromBase64String(dto.PackageBase64); }
        catch (FormatException) { return BadRequest(new ApiError(ErrorCode.InvalidInput, "PackageBase64 is not valid Base64.")); }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        try
        {
            var bundle = await _pluginService.RegisterSignedPluginAsync(package, dto.Signature, userId);
            return CreatedAtAction(nameof(GetPlugin), new { id = bundle.Manifest.PluginId }, new
            {
                bundle.Manifest,
                UserSigner = bundle.UserSignature?.CertificateChainPem.Length > 0 ? "verified" : null,
                bundle.ServerSignature
            });
        }
        catch (PluginSignatureException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError(ErrorCode.PluginSignatureInvalid, ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ErrorCode.InvalidInput, ex.Message));
        }
    }

    /// <summary>
    /// Development only (HEIMDALL_ENABLE_DEV=true or Development): registers a plugin without a user
    /// signature. Agents outside dev mode reject such plugins.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterPlugin([FromBody] RegisterPluginDto dto)
    {
        if (!DevPluginUploadAllowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError(ErrorCode.PluginSignatureInvalid,
                "Unsigned plugin registration is disabled outside dev mode. Upload a user-signed package to POST /api/v1/plugin/signed."));
        }
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
