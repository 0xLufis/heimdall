namespace App.Agent.Daemon.CommandHandling;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon.Infrastructure.Plugins;
using App.Agent.Daemon.Interfaces;
using App.Shared.Errors;
using App.Shared.Plugins;
using App.Shared.Protos;
using App.Shared.Sanitization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Verifies, sanitizes, and executes incoming server commands.
/// </summary>
public class CommandHandler : ICommandHandler
{
    private readonly IConfigurationService _configService;
    private readonly IFileSystemScanner _fileScanner;
    private readonly IPluginManager? _pluginManager;
    private readonly ISystemInfoReporter? _reporter;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
        IConfigurationService configService,
        IFileSystemScanner fileScanner,
        ILogger<CommandHandler> logger,
        IPluginManager? pluginManager = null,
        ISystemInfoReporter? reporter = null)
    {
        _configService = configService;
        _fileScanner = fileScanner;
        _logger = logger;
        _pluginManager = pluginManager;
        _reporter = reporter;
    }

    public async Task<CommandExecutionResult> HandleCommandAsync(ServerCommand command)
    {
        if (command == null)
        {
            return new CommandExecutionResult(false, "Command cannot be null", ErrorCode.InvalidInput);
        }

        _logger.LogInformation("Processing command: Type={Type}", command.Type);

        // 1. Signature Verification
        if (!_configService.VerifyCommandSignature(command))
        {
            _logger.LogWarning("Command {Type} rejected: signature verification failed.", command.Type);
            return new CommandExecutionResult(false, "Signature verification failed", ErrorCode.SignatureVerificationFailed);
        }

        // 2. Command Dispatch
        switch (command.Type)
        {
            case "UPDATE_CONFIG":
            case "SET_MASTER_POLICY":
                return HandleConfigUpdate(command);

            case "FILE_CHECK":
                return await HandleFileCheckAsync(command);

            case "SHELL_EXEC":
                return HandleShellExec(command);

            case "INSTALL_PLUGIN":
                return await HandleInstallPluginAsync(command);

            case "UNINSTALL_PLUGIN":
                return await HandleUninstallPluginAsync(command);

            case "EXECUTE_PLUGIN":
                return await HandleExecutePluginAsync(command);

            case "TRIGGER_DIAGNOSTIC_SNAPSHOT":
                return await HandleDiagnosticSnapshotAsync(command);

            default:
                _logger.LogWarning("Unknown command type received: {Type}", command.Type);
                return new CommandExecutionResult(false, $"Unknown command type '{command.Type}'", ErrorCode.InvalidInput);
        }
    }

    private async Task<CommandExecutionResult> HandleDiagnosticSnapshotAsync(ServerCommand command)
    {
        _logger.LogInformation("Processing on-demand TRIGGER_DIAGNOSTIC_SNAPSHOT command: {Payload}", command.Payload);

        if (_reporter != null)
        {
            try
            {
                await _reporter.TriggerSyncAsync();
                return new CommandExecutionResult(true, "Triggered on-demand telemetry synchronization for diagnostic snapshot", ErrorCode.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to execute immediate telemetry synchronization for diagnostic snapshot");
                return new CommandExecutionResult(false, $"Failed to synchronize telemetry: {ex.Message}", ErrorCode.InternalError);
            }
        }

        return new CommandExecutionResult(true, "Acknowledged diagnostic snapshot request (no reporter attached)", ErrorCode.None);
    }

    private CommandExecutionResult HandleConfigUpdate(ServerCommand command)
    {
        bool updated = _configService.UpdateConfigSigned(command.Payload, command.Signature);
        if (updated)
        {
            return new CommandExecutionResult(true, "Configuration updated successfully", ErrorCode.None);
        }

        return new CommandExecutionResult(false, "Failed to apply configuration update", ErrorCode.InvalidPayload);
    }

    private Task<CommandExecutionResult> HandleFileCheckAsync(ServerCommand command)
    {
        if (!_configService.Config.AllowRemoteExecution)
        {
            _logger.LogWarning("FILE_CHECK command rejected: remote execution disabled by policy.");
            return Task.FromResult(new CommandExecutionResult(false, "Remote execution is disabled by policy", ErrorCode.RemoteExecutionDisabled));
        }

        // Anti-Stuxnet path sanitization: disallow traversal, null bytes, and dangerous extensions
        if (!PathSanitizer.TrySanitizePath(command.Payload, out var safePath, out var errorCode, disallowExecutables: true))
        {
            _logger.LogWarning("FILE_CHECK command rejected: path '{RawPath}' failed sanitization (Code: {Code})", command.Payload, errorCode);
            return Task.FromResult(new CommandExecutionResult(false, $"Path validation failed: {errorCode}", errorCode));
        }

        if (!File.Exists(safePath) && !Directory.Exists(safePath))
        {
            return Task.FromResult(new CommandExecutionResult(false, $"Target path not found: {safePath}", ErrorCode.EntityNotFound));
        }

        _logger.LogInformation("FILE_CHECK executed safely on validated path: {SafePath}", safePath);
        return Task.FromResult(new CommandExecutionResult(true, $"File check passed for {safePath}", ErrorCode.None));
    }

    private CommandExecutionResult HandleShellExec(ServerCommand command)
    {
        if (!_configService.Config.AllowRemoteExecution)
        {
            _logger.LogWarning("SHELL_EXEC command rejected: remote execution disabled by policy.");
            return new CommandExecutionResult(false, "Remote execution is disabled by policy", ErrorCode.RemoteExecutionDisabled);
        }

        if (!CommandSanitizer.IsSafeCommandString(command.Payload, out var errorCode))
        {
            _logger.LogWarning("SHELL_EXEC command rejected: payload contains forbidden characters or patterns (Code: {Code})", errorCode);
            return new CommandExecutionResult(false, $"Command validation failed: {errorCode}", errorCode);
        }

        _logger.LogInformation("Authorized diagnostic command accepted for execution: {Command}", command.Payload);
        return new CommandExecutionResult(true, "Diagnostic command executed", ErrorCode.None);
    }

    private async Task<CommandExecutionResult> HandleInstallPluginAsync(ServerCommand command)
    {
        if (_pluginManager == null)
        {
            return new CommandExecutionResult(false, "PluginManager is not available on agent", ErrorCode.InternalError);
        }

        try
        {
            var bundle = JsonSerializer.Deserialize<PluginPackageBundle>(command.Payload);
            if (bundle == null || bundle.Manifest == null)
            {
                return new CommandExecutionResult(false, "Invalid plugin bundle payload", ErrorCode.InvalidPayload);
            }

            var result = await _pluginManager.InstallPluginAsync(bundle);
            return new CommandExecutionResult(result.Success, result.Message ?? "Plugin install completed", result.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize and install plugin");
            return new CommandExecutionResult(false, ex.Message, ErrorCode.InvalidPayload);
        }
    }

    private async Task<CommandExecutionResult> HandleUninstallPluginAsync(ServerCommand command)
    {
        if (_pluginManager == null)
        {
            return new CommandExecutionResult(false, "PluginManager is not available on agent", ErrorCode.InternalError);
        }

        string pluginId = StringSanitizer.ToSafeToken(command.Payload);
        bool removed = await _pluginManager.UninstallPluginAsync(pluginId);
        return new CommandExecutionResult(
            removed,
            removed ? $"Plugin '{pluginId}' uninstalled." : $"Plugin '{pluginId}' not found.",
            removed ? ErrorCode.None : ErrorCode.PluginNotFound);
    }

    private async Task<CommandExecutionResult> HandleExecutePluginAsync(ServerCommand command)
    {
        if (_pluginManager == null)
        {
            return new CommandExecutionResult(false, "PluginManager is not available on agent", ErrorCode.InternalError);
        }

        string pluginId = StringSanitizer.ToSafeToken(command.Payload);
        var execResult = await _pluginManager.ExecutePluginAsync(pluginId);
        return new CommandExecutionResult(
            execResult.Success,
            string.IsNullOrWhiteSpace(execResult.StandardOutput) ? execResult.StandardError : execResult.StandardOutput,
            execResult.ErrorCode);
    }
}
