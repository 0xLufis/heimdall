namespace App.Shared.Errors;

/// <summary>
/// Domain and infrastructure error codes for standardized error handling.
/// </summary>
public enum ErrorCode
{
    None = 0,

    // Input & Validation (1000 - 1999)
    InvalidInput = 1000,
    MissingRequiredField = 1001,
    InvalidPath = 1002,
    PathTraversalDetected = 1003,
    UnsafeCommandDetected = 1004,
    InvalidPayload = 1005,
    InvalidFormat = 1006,

    // Entity & Domain (2000 - 2999)
    EntityNotFound = 2000,
    ClientPcNotFound = 2001,
    MachineNotFound = 2002,
    TicketNotFound = 2003,
    TicketAlreadyClosed = 2004,
    DuplicateEntity = 2005,

    // Security, Authentication & Governance (3000 - 3999)
    AuthenticationRequired = 3000,
    InvalidCredentials = 3001,
    InvalidAgentKey = 3002,
    AccessDenied = 3003,
    OuNotApproved = 3004,
    RemoteExecutionDisabled = 3005,
    SignatureVerificationFailed = 3006,
    CertificateInvalid = 3007,
    CertificateExpired = 3008,
    PluginSignatureInvalid = 3010,
    PluginExecutionDenied = 3011,
    PluginNotFound = 3012,
    SandboxViolation = 3013,
    ExtensionPayloadTooLarge = 3014,
    ExtensionUnauthorized = 3015,

    // Hardware & Device Drivers (4000 - 4999)
    DriverNotFound = 4000,
    DriverQueryFailed = 4001,
    DriverNotBound = 4002,
    HardwareQueryFailed = 4003,

    // System & Infrastructure (5000 - 5999)
    InternalError = 5000,
    DatabaseError = 5001,
    SpoolError = 5002,
    CommunicationError = 5003
}
