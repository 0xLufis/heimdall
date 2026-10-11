namespace App.Agent.Daemon.Interfaces;

using System.Threading.Tasks;
using App.Shared.Errors;
using App.Shared.Protos;

/// <summary>
/// Result of executing an authorized server command.
/// </summary>
public record CommandExecutionResult(bool Success, string Message, ErrorCode ErrorCode = ErrorCode.None);

/// <summary>
/// Service contract for handling, verifying, and dispatching incoming server commands.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Authenticates, sanitizes, and executes an incoming server command.
    /// </summary>
    Task<CommandExecutionResult> HandleCommandAsync(ServerCommand command);
}
