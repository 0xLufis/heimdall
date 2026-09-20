namespace App.Contracts.Diagnostics;

using System.Collections.Generic;
using App.Contracts.Enums;

/// <summary>
/// Strongly typed request payload for whitelisted diagnostic routines on the agent.
/// Prohibits arbitrary shell string execution.
/// </summary>
public class DiagnosticRoutineRequest
{
    public DiagnosticRoutineType Routine { get; set; }
    public Dictionary<string, string> Arguments { get; set; } = new();
}
