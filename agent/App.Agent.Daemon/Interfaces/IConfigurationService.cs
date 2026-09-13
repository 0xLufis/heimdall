namespace App.Agent.Daemon.Interfaces;

using App.Shared.Protos;

/// <summary>
/// Service contract for managing agent configuration, master policy, and cryptographic verification.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// The active agent configuration.
    /// </summary>
    AgentConfig Config { get; }

    /// <summary>
    /// Verifies the cryptographic signature of an incoming server command.
    /// </summary>
    bool VerifyCommandSignature(ServerCommand command);

    /// <summary>
    /// Updates configuration payload after validating signature against master public key.
    /// </summary>
    bool UpdateConfigSigned(string payload, string? signature);

    /// <summary>
    /// Persists configuration to disk.
    /// </summary>
    void SaveConfig(AgentConfig config);
}
