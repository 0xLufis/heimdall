namespace App.Agent.Daemon.Infrastructure.Security;

using System.Text.RegularExpressions;

/// <summary>
/// Redacts sensitive operational secrets, credentials, tokens, and personal user names from process arguments and logs.
/// </summary>
public static partial class ProcessSecretScrubber
{
    [GeneratedRegex(@"(?i)(--?(?:pwd|password|secret|token|apikey|api[-_]?key|access[-_]?token|auth|bearer|connectionstring|conn[-_]?str(?:ing)?)\s*[:=\s]\s*)([^\s""']+|""[^""]*""|'[^']*')", RegexOptions.Compiled)]
    private static partial Regex SecretFlagRegex();

    // User profile paths can be preserved if explicitly configured by the administrator
    [GeneratedRegex(@"(?i)([a-zA-Z]:\\Users\\|/home/)([^/\\\s""']+)", RegexOptions.Compiled)]
    private static partial Regex UserHomePathRegex();

    [GeneratedRegex(@"\b(ey[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*)\b", RegexOptions.Compiled)]
    private static partial Regex JwtTokenRegex();

    /// <summary>
    /// Scrubs credentials, high-entropy tokens, and PII user paths from process command lines.
    /// User profile scrubbing can be bypassed by setting scrubUserHomePaths to false or configuring HEIMDALL_PRESERVE_USER_HOME_PATHS=true.
    /// </summary>
    public static string Scrub(string? input, bool? scrubUserHomePaths = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Scrub explicit secret parameters
        var sanitized = SecretFlagRegex().Replace(input, "$1[REDACTED]");

        // 2. Scrub high-entropy JWT tokens
        sanitized = JwtTokenRegex().Replace(sanitized, "[REDACTED_JWT]");

        // 3. Scrub personal user profile names in directory paths (GDPR/PII compliance unless explicitly preserved)
        bool shouldScrubHome = scrubUserHomePaths ?? (Environment.GetEnvironmentVariable("HEIMDALL_PRESERVE_USER_HOME_PATHS") != "true");
        if (shouldScrubHome)
        {
            sanitized = UserHomePathRegex().Replace(sanitized, "$1[USER_ACCOUNT]");
        }

        return sanitized;
    }
}
