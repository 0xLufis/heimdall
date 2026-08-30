namespace App.Agent.Daemon.Infrastructure.Security;

using System.Text.RegularExpressions;

/// <summary>
/// Redacts sensitive operational secrets, credentials, tokens, and personal user names from process arguments and logs.
/// </summary>
public static partial class ProcessSecretScrubber
{
    [GeneratedRegex(@"(?i)(--?(?:pwd|password|secret|token|apikey|api[-_]?key|access[-_]?token|auth|bearer|connectionstring|conn[-_]?str(?:ing)?)\s*[:=\s]\s*)([^\s""']+|""[^""]*""|'[^']*')", RegexOptions.Compiled)]
    private static partial Regex SecretFlagRegex();

    [GeneratedRegex(@"(?i)([a-zA-Z]:\\Users\\|/home/)([^/\\\s""']+)", RegexOptions.Compiled)]
    private static partial Regex UserHomePathRegex();

    [GeneratedRegex(@"\b(ey[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*)\b", RegexOptions.Compiled)]
    private static partial Regex JwtTokenRegex();

    public static string Scrub(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Scrub explicit secret parameters
        var sanitized = SecretFlagRegex().Replace(input, "$1[REDACTED]");

        // 2. Scrub high-entropy JWT tokens
        sanitized = JwtTokenRegex().Replace(sanitized, "[REDACTED_JWT]");

        // 3. Scrub personal user profile names in directory paths (GDPR/PII compliance)
        sanitized = UserHomePathRegex().Replace(sanitized, "$1[USER_ACCOUNT]");

        return sanitized;
    }
}
