namespace App.Agent.Daemon.Infrastructure.Plugins;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using App.Shared.Errors;
using App.Shared.Plugins;
using App.Shared.Sanitization;
using Microsoft.Extensions.Logging;

public class PluginSandboxService : IPluginSandboxService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<PluginSandboxService> _logger;

    public PluginSandboxService(IConfigurationService configService, ILogger<PluginSandboxService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public string GetSandboxDirectory(string pluginId)
    {
        string safeId = StringSanitizer.ToSafeToken(pluginId);
        string rootSandboxDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _configService.Config.SandboxesDirectory));
        return Path.Combine(rootSandboxDir, safeId);
    }

    public void PurgeSandbox(string pluginId)
    {
        string sandboxDir = GetSandboxDirectory(pluginId);
        try
        {
            if (Directory.Exists(sandboxDir))
            {
                Directory.Delete(sandboxDir, recursive: true);
                _logger.LogInformation("Purged sandbox directory for plugin '{PluginId}'", pluginId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to completely purge sandbox directory '{Path}'", sandboxDir);
        }
    }

    public async Task<PluginExecutionResult> ExecuteSandboxedAsync(PluginPackageBundle bundle, string[]? arguments = null)
    {
        var manifest = bundle.Manifest;
        string safePluginId = StringSanitizer.ToSafeToken(manifest.PluginId);

        // Security check: strictly disallow unsigned plugins in Production
        string env = _configService.Config.Environment;
        bool isProd = string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);
        if (!manifest.IsSigned && isProd && !_configService.Config.AllowUnsignedPlugins)
        {
            _logger.LogWarning("Security violation: Rejected unsigned plugin '{PluginId}' in Production mode.", safePluginId);
            return new PluginExecutionResult(
                Success: false,
                PluginId: safePluginId,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: "Security violation: Unsigned plugins are strictly prohibited in Production mode.",
                IsSandboxed: false,
                ErrorCode: ErrorCode.PluginSignatureInvalid);
        }

        string sandboxDir = GetSandboxDirectory(safePluginId);
        Directory.CreateDirectory(sandboxDir);

        // Extract and verify files strictly inside sandbox root
        try
        {
            foreach (var file in bundle.Files)
            {
                string relativePath = file.Key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string targetPath = Path.GetFullPath(Path.Combine(sandboxDir, relativePath));

                // Anti-Stuxnet path containment verification
                if (!PathSanitizer.IsWithinRoot(targetPath, sandboxDir))
                {
                    _logger.LogWarning("Sandbox security violation: Plugin '{PluginId}' attempted path traversal to '{Target}'",
                        safePluginId, targetPath);
                    return new PluginExecutionResult(
                        Success: false,
                        PluginId: safePluginId,
                        ExitCode: -1,
                        StandardOutput: string.Empty,
                        StandardError: $"Sandbox violation: File path '{file.Key}' escapes sandbox root.",
                        IsSandboxed: true,
                        ErrorCode: ErrorCode.SandboxViolation);
                }

                string? parentDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                await File.WriteAllTextAsync(targetPath, file.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to materialize sandbox files for plugin '{PluginId}'", safePluginId);
            return new PluginExecutionResult(
                Success: false,
                PluginId: safePluginId,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: $"Failed to stage sandbox files: {ex.Message}",
                IsSandboxed: true,
                ErrorCode: ErrorCode.InternalError);
        }

        string entrypointPath = Path.GetFullPath(Path.Combine(sandboxDir, manifest.Entrypoint));
        if (!File.Exists(entrypointPath))
        {
            return new PluginExecutionResult(
                Success: false,
                PluginId: safePluginId,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: $"Plugin entrypoint '{manifest.Entrypoint}' not found inside sandbox.",
                IsSandboxed: true,
                ErrorCode: ErrorCode.PluginNotFound);
        }

        // Determine executable and argument string based on runtime
        string fileName;
        var argsBuilder = new StringBuilder();

        if (manifest.Entrypoint.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python3";
            argsBuilder.Append($"\"{entrypointPath}\"");
        }
        else if (manifest.Entrypoint.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "/bin/bash";
            argsBuilder.Append($"\"{entrypointPath}\"");
        }
        else
        {
            fileName = entrypointPath;
        }

        if (arguments != null)
        {
            foreach (var arg in arguments)
            {
                if (CommandSanitizer.IsSafeCommandString(arg, out _))
                {
                    argsBuilder.Append(' ');
                    argsBuilder.Append(arg);
                }
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = argsBuilder.ToString(),
            WorkingDirectory = sandboxDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Secret Scrubbing: Strip agent keys and master secrets from child environment
        startInfo.Environment.Remove("HEIMDALL_AGENT_KEY");
        startInfo.Environment.Remove("HEIMDALL_MASTER_PRIVATE_KEY");
        startInfo.Environment.Remove("HEIMDALL_ENCRYPTION_KEY");
        startInfo.Environment["HEIMDALL_SANDBOXED"] = "true";
        startInfo.Environment["HEIMDALL_PLUGIN_ID"] = safePluginId;

        int timeoutMs = _configService.Config.PluginExecutionTimeoutSeconds * 1000;
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            _logger.LogInformation("Sandboxed plugin '{PluginId}' completed with exit code {ExitCode}",
                safePluginId, process.ExitCode);

            return new PluginExecutionResult(
                Success: process.ExitCode == 0,
                PluginId: safePluginId,
                ExitCode: process.ExitCode,
                StandardOutput: stdout,
                StandardError: stderr,
                IsSandboxed: true,
                ErrorCode: process.ExitCode == 0 ? ErrorCode.None : ErrorCode.InternalError);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sandboxed plugin '{PluginId}' timed out after {Timeout}s",
                safePluginId, _configService.Config.PluginExecutionTimeoutSeconds);
            return new PluginExecutionResult(
                Success: false,
                PluginId: safePluginId,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: $"Plugin execution exceeded timeout of {_configService.Config.PluginExecutionTimeoutSeconds} seconds.",
                IsSandboxed: true,
                ErrorCode: ErrorCode.InternalError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing sandboxed plugin '{PluginId}'", safePluginId);
            return new PluginExecutionResult(
                Success: false,
                PluginId: safePluginId,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: ex.Message,
                IsSandboxed: true,
                ErrorCode: ErrorCode.InternalError);
        }
    }
}
