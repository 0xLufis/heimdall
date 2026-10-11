namespace App.Contracts.Enums;

/// <summary>
/// Strictly defined command types dispatched from the Heimdall server to the edge agent.
/// Replaces raw string switching.
/// </summary>
public enum AgentCommandType
{
    UpdateConfig = 1,
    SetMasterPolicy = 2,
    FileCheck = 3,
    ExecuteDiagnostic = 4,
    InstallPlugin = 5,
    UninstallPlugin = 6,
    ExecutePlugin = 7,
    TriggerDiagnosticSnapshot = 8
}
