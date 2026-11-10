namespace App.Agent.Daemon.Infrastructure.FileSystem;

using System;
using System.Collections.Generic;

/// <summary>
/// Options configuring directory traversal and asset filtering for <see cref="FileSystemScanner"/>.
/// Allows defining custom banned extensions, custom banned file names, and allowed file naming templates.
/// </summary>
public sealed class FileSystemScannerOptions
{
    /// <summary>
    /// Additional file extensions to scan (e.g., ".bak", ".dat").
    /// </summary>
    public HashSet<string> AdditionalAllowedExtensions { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Custom file extensions that should be prohibited from being scanned (e.g., ".exe", ".dll", ".sh").
    /// Custom banned extensions take strict precedence over allowed extensions.
    /// </summary>
    public HashSet<string> CustomBannedExtensions { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Custom file names to ignore during scanning.
    /// </summary>
    public HashSet<string> CustomBannedFileNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Custom regular expression templates or patterns for allowed filenames or versioned extensions
    /// (e.g. Siemens TIA archive patterns like @"^.*\.zal\d+$", @"^.*\.zap\d+$", @"^.*\.ap\d+$").
    /// </summary>
    public List<string> AllowedFileNamePatterns { get; init; } = new();

    /// <summary>
    /// Maximum file size in bytes to inspect/hash (default: 2 GB). Files exceeding this are safely skipped.
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = 2L * 1024 * 1024 * 1024;
}
