namespace App.Agent.Daemon.Reporting;

/// <summary>
/// Cryptographic transport authentication mode for agent-to-backend communication.
/// </summary>
public enum AuthMode
{
    /// <summary>No mutual authentication (development/testing).</summary>
    NoAuth,

    /// <summary>Mutual TLS using a provisioned Heimdall client certificate.</summary>
    HeimdallCert,

    /// <summary>Mutual TLS using a machine store certificate.</summary>
    UserCert,

    /// <summary>Shared pre-shared key header authentication.</summary>
    ApiKey
}
