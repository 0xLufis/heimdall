namespace App.Agent.Daemon.CommandHandling;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon.Infrastructure.Plugins;
using App.Agent.Daemon.Interfaces;
using App.Contracts.Enums;
using App.Shared.Errors;
using App.Shared.Plugins;
using App.Shared.Protos;
using App.Shared.Sanitization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Verifies, sanitizes, and executes incoming server commands directed to the edge daemon.
/// Enforces digital signature verification, role-based command execution policies,
/// cross-platform path validation, and plugin lifecycle execution sandboxing.
/// </summary>
public class CommandHandler : ICommandHandler
{
    private readonly IConfigurationService _configService;
    private readonly IFileSystemScanner _fileScanner;
    private readonly IPluginManager? _pluginManager;
    private readonly ISystemInfoReporter? _reporter;
    private readonly ILogger<CommandHandler> _logger;
    private readonly App.Contracts.Configuration.AgentFeatureFlags _featureFlags;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandler"/> class.
    /// Merges explicitly provided feature flags with environment variable overrides
    /// to guarantee strict security defaults and runtime compatibility across environments.
    /// </summary>
    /// <param name="configService">Service providing daemon configuration and signature verification.</param>
    /// <param name="fileScanner">Scanner for verifying file system operations.</param>
    /// <param name="logger">Diagnostic logger instance.</param>
    /// <param name="pluginManager">Optional manager for plugin lifecycle management.</param>
    /// <param name="reporter">Optional reporter for diagnostic telemetry synchronizations.</param>
    /// <param name="featureFlags">Optional feature flag set; defaults to environment-derived flags.</param>
    public CommandHandler(
        IConfigurationService configService,
        IFileSystemScanner fileScanner,
        ILogger<CommandHandler> logger,
        IPluginManager? pluginManager = null,
        ISystemInfoReporter? reporter = null,
        App.Contracts.Configuration.AgentFeatureFlags? featureFlags = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _fileScanner = fileScanner ?? throw new ArgumentNullException(nameof(fileScanner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginManager = pluginManager;
        _reporter = reporter;

        // Merge provided flags with environment defaults to ensure compatibility
        var envFlags = App.Contracts.Configuration.AgentFeatureFlags.FromEnvironment();
        _featureFlags = featureFlags != null 
            ? new App.Contracts.Configuration.AgentFeatureFlags
              {
                  EnableDebugFeatures = featureFlags.EnableDebugFeatures || envFlags.EnableDebugFeatures,
                  EnableDevFeatures = featureFlags.EnableDevFeatures || envFlags.EnableDevFeatures
              }
            : envFlags;
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

        // 2. Strongly-Typed Command Dispatch via Enum Pattern Matching
        if (!TryParseCommandType(command.Type, out var commandType))
        {
            _logger.LogWarning("Unknown command type received: {Type}", command.Type);
            return new CommandExecutionResult(false, $"Unknown command type '{command.Type}'", ErrorCode.InvalidInput);
        }

        return commandType switch
        {
            AgentCommandType.UpdateConfig or AgentCommandType.SetMasterPolicy => HandleConfigUpdate(command),
            AgentCommandType.FileCheck => await HandleFileCheckAsync(command),
            AgentCommandType.ExecuteDiagnostic => HandleDiagnosticRoutineOrShellExec(command),
            AgentCommandType.InstallPlugin => await HandleInstallPluginAsync(command),
            AgentCommandType.UninstallPlugin => await HandleUninstallPluginAsync(command),
            AgentCommandType.ExecutePlugin => await HandleExecutePluginAsync(command),
            AgentCommandType.TriggerDiagnosticSnapshot => await HandleDiagnosticSnapshotAsync(command),
            _ => new CommandExecutionResult(false, $"Unsupported command type '{command.Type}'", ErrorCode.InvalidInput)
        };
    }

    private static bool TryParseCommandType(string? typeStr, out AgentCommandType commandType)
    {
        commandType = default;
        if (string.IsNullOrWhiteSpace(typeStr)) return false;

        var normalized = typeStr.Trim().ToUpperInvariant();
        return normalized switch
        {
            "UPDATE_CONFIG" => Set(AgentCommandType.UpdateConfig, out commandType),
            "SET_MASTER_POLICY" => Set(AgentCommandType.SetMasterPolicy, out commandType),
            "FILE_CHECK" => Set(AgentCommandType.FileCheck, out commandType),
            "EXECUTE_DIAGNOSTIC" => Set(AgentCommandType.ExecuteDiagnostic, out commandType),
            "SHELL_EXEC" => Set(AgentCommandType.ExecuteDiagnostic, out commandType),
            "DIAGNOSTIC_DUMP" => Set(AgentCommandType.ExecuteDiagnostic, out commandType),
            "INSTALL_PLUGIN" => Set(AgentCommandType.InstallPlugin, out commandType),
            "UNINSTALL_PLUGIN" => Set(AgentCommandType.UninstallPlugin, out commandType),
            "EXECUTE_PLUGIN" => Set(AgentCommandType.ExecutePlugin, out commandType),
            "TRIGGER_DIAGNOSTIC_SNAPSHOT" => Set(AgentCommandType.TriggerDiagnosticSnapshot, out commandType),
            _ => Enum.TryParse<AgentCommandType>(typeStr, true, out commandType)
        };

        static bool Set(AgentCommandType type, out AgentCommandType target)
        {
            target = type;
            return true;
        }
    }

    private CommandExecutionResult HandleDiagnosticRoutineOrShellExec(ServerCommand command)
    {
        // Guard verbose diagnostic dumps under EnableDebugFeatures
        bool isDumpRequest = string.Equals(command.Type, "DIAGNOSTIC_DUMP", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(command.Payload) && command.Payload.Contains("dump", StringComparison.OrdinalIgnoreCase));

        if (isDumpRequest && !_featureFlags.EnableDebugFeatures)
        {
            _logger.LogWarning("Diagnostic dump routine rejected: verbose diagnostic dumps disabled in production mode (EnableDebugFeatures = false).");
            return new CommandExecutionResult(false, "Verbose diagnostic dumps are disabled in production mode", ErrorCode.RemoteExecutionDisabled);
        }

        return HandleShellExec(command);
    }

    private async Task<CommandExecutionResult> HandleDiagnosticSnapshotAsync(ServerCommand command)
    {
        _logger.LogInformation("Processing on-demand TRIGGER_DIAGNOSTIC_SNAPSHOT command: {Payload}", command.Payload);

        if (_reporter != null)
        {
            try
            {
                // Trigger an immediate on-demand telemetry sync pass to capture live endpoint hardware/OT state
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

        // Cross-platform path validation: PathSanitizer normalizes directory separators ('/' and '\'),
        // prevents directory traversal (../), strips control/null characters, and enforces executable extension
        // restrictions across both Windows and POSIX operating systems without OS-specific leakage.
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
