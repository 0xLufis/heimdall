namespace App.Shared.Plugins;

using System;
using System.Collections.Generic;
using App.Shared.Errors;

/// <summary>
/// Metadata manifest describing an agent plugin package, its runtime constraints,
/// and cryptographic signature provenance.
/// </summary>
public record PluginManifest
{
    /// <summary>Unique machine-readable slug identifier (e.g. "spindle-vibration-monitor").</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>Human-readable display name of the plugin.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Semantic version of the plugin package (e.g. "1.2.0").</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>Functional summary of the plugin's reporting responsibilities.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Relative entrypoint script or executable (e.g. "main.py", "run.sh", "monitor.exe").</summary>
    public string Entrypoint { get; init; } = string.Empty;

    /// <summary>Target execution runtime: Python, Shell, Executable, DotNet.</summary>
    public string RuntimeType { get; init; } = "Python";

    /// <summary>Target operating system: Windows, Linux, Any.</summary>
    public string TargetOs { get; init; } = "Any";

    /// <summary>Hex-encoded SHA-256 digest of the payload content bundle.</summary>
    public string PayloadHash { get; init; } = string.Empty;

    /// <summary>Base64-encoded cryptographic digital signature of the payload hash.</summary>
    public string Signature { get; init; } = string.Empty;

    /// <summary>Signature algorithm: RSA-SHA256, ECDSA-SHA256.</summary>
    public string SignatureAlgorithm { get; init; } = "RSA-SHA256";

    /// <summary>Flag indicating whether this plugin was signed by the master backend key.</summary>
    public bool IsSigned { get; init; } = false;

    /// <summary>UTC timestamp when the package was registered/signed.</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Transport bundle holding the manifest and embedded files/payload for agent deployment.
/// </summary>
public record PluginPackageBundle
{
    /// <summary>The package manifest.</summary>
    public PluginManifest Manifest { get; init; } = new();

    /// <summary>File dictionary mapping relative file paths to their UTF-8 / Base64 contents.</summary>
    public Dictionary<string, string> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional raw compressed archive payload (Base64).</summary>
    public string? PayloadBase64 { get; init; }
}

/// <summary>
/// Result status returned after an agent processes an INSTALL_PLUGIN command.
/// </summary>
public record PluginInstallResult(
    bool Success,
    string PluginId,
    bool IsSandboxed,
    string? Message = null,
    ErrorCode ErrorCode = ErrorCode.None);

/// <summary>
/// Execution output and status resulting from a plugin run.
/// </summary>
public record PluginExecutionResult(
    bool Success,
    string PluginId,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool IsSandboxed,
    ErrorCode ErrorCode = ErrorCode.None);
