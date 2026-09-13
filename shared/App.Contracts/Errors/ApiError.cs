namespace App.Shared.Errors;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Structured, type-safe API error representation for consistent HTTP and RPC responses.
/// </summary>
public class ApiError
{
    public ErrorCode Code { get; set; } = ErrorCode.InternalError;

    public string CodeName => Code.ToString();

    public string Message { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public ApiError()
    {
    }

    public ApiError(ErrorCode code, string? details = null)
    {
        Code = code;
        Message = GetDefaultMessage(code);
        Details = details;
    }

    public ApiError(ErrorCode code, string message, string? details)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    public static string GetDefaultMessage(ErrorCode code) => code switch
    {
        ErrorCode.InvalidInput => "Invalid input parameter provided.",
        ErrorCode.MissingRequiredField => "A required field was missing from the request.",
        ErrorCode.InvalidPath => "The specified path is invalid or malformed.",
        ErrorCode.PathTraversalDetected => "Security violation: Path traversal sequence detected.",
        ErrorCode.UnsafeCommandDetected => "Security violation: Command contains unsafe or illegal characters.",
        ErrorCode.InvalidPayload => "The request payload could not be validated or deserialized.",
        ErrorCode.InvalidFormat => "Input data format is invalid.",
        ErrorCode.EntityNotFound => "The requested entity was not found.",
        ErrorCode.ClientPcNotFound => "Client PC / controller endpoint was not found.",
        ErrorCode.MachineNotFound => "Machine / station was not found.",
        ErrorCode.TicketNotFound => "Maintenance ticket was not found.",
        ErrorCode.TicketAlreadyClosed => "The maintenance ticket is already closed.",
        ErrorCode.DuplicateEntity => "An entity with the specified identifier already exists.",
        ErrorCode.AuthenticationRequired => "Authentication credentials are required.",
        ErrorCode.InvalidCredentials => "The provided credentials are invalid.",
        ErrorCode.InvalidAgentKey => "The agent authentication key is invalid or rejected.",
        ErrorCode.AccessDenied => "Access denied for the requested operation.",
        ErrorCode.OuNotApproved => "Active Directory Organizational Unit is not approved for Read/Write operations.",
        ErrorCode.RemoteExecutionDisabled => "Remote execution command rejected: Disabled by policy.",
        ErrorCode.SignatureVerificationFailed => "Command signature verification failed.",
        ErrorCode.CertificateInvalid => "The provided certificate is invalid or untrusted.",
        ErrorCode.CertificateExpired => "The provided certificate has expired.",
        ErrorCode.PluginSignatureInvalid => "Plugin signature verification failed or signature was missing.",
        ErrorCode.PluginExecutionDenied => "Plugin execution was denied by security policy.",
        ErrorCode.PluginNotFound => "The requested plugin was not found.",
        ErrorCode.SandboxViolation => "Plugin attempted an unauthorized action outside the development sandbox.",
        ErrorCode.ExtensionPayloadTooLarge => "The extension payload exceeded the maximum allowable size.",
        ErrorCode.ExtensionUnauthorized => "Authentication required to access the agent extension API.",
        ErrorCode.DriverNotFound => "The requested device driver was not found.",
        ErrorCode.DriverQueryFailed => "Querying system device drivers encountered an error.",
        ErrorCode.DriverNotBound => "The driver is installed but not bound to an active hardware device.",
        ErrorCode.HardwareQueryFailed => "Querying system hardware telemetry failed.",
        ErrorCode.SpoolError => "Local telemetry spooler operation failed.",
        ErrorCode.DatabaseError => "Database persistence operation failed.",
        ErrorCode.CommunicationError => "gRPC or network communication failure.",
        _ => "An internal system error occurred."
    };
}
