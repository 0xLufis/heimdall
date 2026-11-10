namespace App.Shared.Sanitization;

using System;
using System.IO;
using System.Linq;
using App.Shared.Errors;

/// <summary>
/// Hardened path sanitizer that guards against directory traversal, null-byte injection,
/// device name attacks, and unauthorized root escapes.
/// </summary>
public static class PathSanitizer
{
    private static readonly string[] DangerousDeviceNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly string[] DangerousExtensions =
    {
        ".exe", ".com", ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse",
        ".wsf", ".wsh", ".msc", ".ps1", ".ps1xml", ".ps2", ".ps2xml",
        ".psc1", ".psc2", ".msh", ".msh1", ".msh2", ".mshxml", ".msh1xml", ".msh2xml"
    };

    /// <summary>
    /// Validates and sanitizes a file or directory path.
    /// </summary>
    /// <param name="rawPath">The incoming path string.</param>
    /// <param name="sanitizedPath">The normalized, absolute safe path if valid; otherwise null.</param>
    /// <param name="errorCode">The failure code if invalid; otherwise ErrorCode.None.</param>
    /// <param name="disallowExecutables">If true, disallows executable file extensions.</param>
    /// <returns>True if the path is safe and well-formed; false otherwise.</returns>
    public static bool TrySanitizePath(
        string? rawPath,
        out string sanitizedPath,
        out ErrorCode errorCode,
        bool disallowExecutables = false)
    {
        sanitizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            errorCode = ErrorCode.MissingRequiredField;
            return false;
        }

        // 1. Guard against null byte poisoning (e.g. "safe.json\0.exe")
        if (rawPath.Contains('\0'))
        {
            errorCode = ErrorCode.PathTraversalDetected;
            return false;
        }

        // 2. Guard against control characters
        if (rawPath.Any(char.IsControl))
        {
            errorCode = ErrorCode.InvalidPath;
            return false;
        }

        // 3. Reject URI scheme prefixes (file://, http://, ftp://, etc.)
        if (rawPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = ErrorCode.InvalidPath;
            return false;
        }

        // 4. Guard against explicit traversal sequences
        string normalizedSlashes = rawPath.Replace('\\', '/');
        if (normalizedSlashes.Contains("/../") ||
            normalizedSlashes.StartsWith("../") ||
            normalizedSlashes.EndsWith("/..") ||
            normalizedSlashes == ".." ||
            normalizedSlashes.Contains("/.../") ||
            normalizedSlashes.Contains("/..../"))
        {
            errorCode = ErrorCode.PathTraversalDetected;
            return false;
        }

        // 5. Guard against reserved DOS device names in any path segment (Windows legacy vector)
        var segments = rawPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(seg =>
        {
            string segWithoutExt = Path.GetFileNameWithoutExtension(seg).Trim();
            return DangerousDeviceNames.Any(dev => segWithoutExt.Equals(dev, StringComparison.OrdinalIgnoreCase));
        }))
        {
            errorCode = ErrorCode.InvalidPath;
            return false;
        }

        // 6. Enforce safe extension if executable restriction is active
        if (disallowExecutables)
        {
            string ext = Path.GetExtension(rawPath);
            if (!string.IsNullOrEmpty(ext) && DangerousExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                errorCode = ErrorCode.UnsafeCommandDetected;
                return false;
            }
        }

        // 7. Resolve full canonical path without throwing on unexpected input
        try
        {
            sanitizedPath = Path.GetFullPath(rawPath);
            errorCode = ErrorCode.None;
            return true;
        }
        catch
        {
            sanitizedPath = string.Empty;
            errorCode = ErrorCode.InvalidPath;
            return false;
        }
    }

    /// <summary>
    /// Verifies that a target path resides strictly within an authorized root directory.
    /// </summary>
    public static bool IsWithinRoot(string targetPath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(rootPath))
            return false;

        try
        {
            string fullTarget = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullTarget.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                   fullTarget.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
