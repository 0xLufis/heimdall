namespace App.Contracts.Enums;

/// <summary>
/// Predefined, whitelisted diagnostic routines executable by the edge agent.
/// Prohibits arbitrary shell command strings from user input.
/// </summary>
public enum DiagnosticRoutineType
{
    PingGateway = 1,
    VerifyDiskSpace = 2,
    CheckTwincatRouter = 3,
    CollectOpcNodes = 4,
    VerifyNetworkAdapters = 5
}
