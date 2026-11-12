using App.Backend.Api.Logging;
using App.Contracts.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Log verbosity at runtime: read the effective minimum level per category for the console and the file
/// output, and override it without a restart. Overrides live in memory only (gone after a restart).
/// Persistent levels belong in <c>Logging:LogLevel</c> / <c>Logging:File:LogLevel</c> (appsettings or
/// environment variables such as <c>Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information</c>).
/// </summary>
[ApiController]
[Route("api/v1/admin/logging")]
[Authorize(Policy = AuthorizationPolicies.SystemAdministration)]
public class LoggingAdminController : ControllerBase
{
    private static readonly string[] DefaultCategories =
    {
        "Default",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore.Database.Command",
        "App.Backend.Api",
    };

    private readonly RuntimeLogLevelSwitch _levels;
    private readonly IOptionsMonitor<LoggerFilterOptions> _filters;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoggingAdminController> _logger;
    private readonly FileLoggerProvider? _fileLogger;

    public LoggingAdminController(
        RuntimeLogLevelSwitch levels,
        IOptionsMonitor<LoggerFilterOptions> filters,
        IConfiguration configuration,
        ILogger<LoggingAdminController> logger,
        FileLoggerProvider? fileLogger = null)
    {
        _levels = levels;
        _filters = filters;
        _configuration = configuration;
        _logger = logger;
        _fileLogger = fileLogger;
    }

    public sealed record SetLogLevelRequest(string? Category, string? Level, string? Provider);

    /// <summary>
    /// Effective levels of the given categories (default: a few well-known ones), the configured level
    /// sections, the runtime overrides and the state of the file logger.
    /// </summary>
    [HttpGet]
    public ActionResult Get([FromQuery(Name = "category")] string[]? categories = null)
    {
        var requested = categories is { Length: > 0 } ? categories : DefaultCategories;
        if (requested.Any(c => !RuntimeLogLevelSwitch.IsValidCategory(c)))
            return BadRequest(new { success = false, message = "Invalid category name." });
        return Ok(View(requested));
    }

    /// <summary>
    /// Sets the minimum level of a category (or <c>Default</c>) for <c>All</c> outputs, <c>Console</c> or
    /// <c>File</c>. <c>level</c> null or <c>Reset</c> removes the override.
    /// </summary>
    [HttpPut("levels")]
    public ActionResult SetLevel([FromBody] SetLogLevelRequest request)
    {
        if (!RuntimeLogLevelSwitch.IsValidCategory(request.Category))
            return BadRequest(new { success = false, message = "category is required (a logger category prefix such as Microsoft.EntityFrameworkCore.Database.Command, or Default)." });
        var provider = RuntimeLogLevelSwitch.NormalizeProvider(request.Provider);
        if (provider == null)
            return BadRequest(new { success = false, message = "provider must be All, Console or File." });

        LogLevel? level = null;
        if (!string.IsNullOrWhiteSpace(request.Level) && !request.Level.Equals("Reset", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<LogLevel>(request.Level, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(request.Level, out _))
                return BadRequest(new { success = false, message = "level must be Trace, Debug, Information, Warning, Error, Critical, None or Reset." });
            level = parsed;
        }

        _levels.Set(request.Category!, level, provider);
        _logger.LogWarning("Runtime log level override: {Category} = {Level} for {Provider} output(s), by {User}",
            request.Category, level?.ToString() ?? "(configured)", provider, User.Identity?.Name ?? "unknown");
        return Ok(View(new[] { request.Category! }));
    }

    /// <summary>Removes the override of one category (all outputs unless <c>provider</c> is given), or every override when no category is given.</summary>
    [HttpDelete("levels")]
    public ActionResult Reset([FromQuery] string? category = null, [FromQuery] string? provider = null)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            _levels.ResetAll();
            _logger.LogWarning("Runtime log level overrides cleared by {User}", User.Identity?.Name ?? "unknown");
            return Ok(View(DefaultCategories));
        }
        if (!RuntimeLogLevelSwitch.IsValidCategory(category))
            return BadRequest(new { success = false, message = "Invalid category name." });
        var normalized = RuntimeLogLevelSwitch.NormalizeProvider(provider);
        if (normalized == null)
            return BadRequest(new { success = false, message = "provider must be All, Console or File." });

        _levels.Set(category, null, normalized);
        return Ok(View(new[] { category }));
    }

    private object View(IEnumerable<string> categories)
    {
        var filters = _filters.CurrentValue;
        var consoleType = typeof(ConsoleLoggerProvider).FullName!;
        var fileType = typeof(FileLoggerProvider).FullName!;

        return new
        {
            success = true,
            levels = categories.Select(category =>
            {
                var lookup = category.Equals("Default", StringComparison.OrdinalIgnoreCase) ? string.Empty : category;
                return new
                {
                    category,
                    console = EffectiveLogLevel.For(filters, consoleType, "Console", lookup).ToString(),
                    file = _fileLogger == null ? null : EffectiveLogLevel.For(filters, fileType, "File", lookup).ToString(),
                };
            }).ToList(),
            overrides = _levels.Overrides,
            configured = new
            {
                all = Section("Logging:LogLevel"),
                console = Section("Logging:Console:LogLevel"),
                file = Section("Logging:File:LogLevel"),
            },
            file = FileState(),
        };
    }

    private Dictionary<string, string?> Section(string path) =>
        _configuration.GetSection(path).GetChildren().ToDictionary(c => c.Key, c => c.Value);

    private object FileState()
    {
        if (_fileLogger == null) return new { enabled = false };
        var rotated = _fileLogger.GetRotatedFiles();
        var activeBytes = FileLoggerProvider.LengthOf(new FileInfo(_fileLogger.ActiveFilePath));
        return new
        {
            enabled = true,
            directory = _fileLogger.Directory,
            activeFile = _fileLogger.ActiveFilePath,
            activeFileBytes = activeBytes,
            rotatedFiles = rotated.Count,
            totalBytes = activeBytes + rotated.Sum(FileLoggerProvider.LengthOf),
            maxFileBytes = _fileLogger.MaxFileSizeBytes,
            maxTotalBytes = _fileLogger.MaxTotalSizeBytes,
            maxAgeDays = _fileLogger.MaxAge.TotalDays,
            queueLength = _fileLogger.QueueLength,
            queueCapacity = _fileLogger.QueueCapacity,
            droppedTotal = _fileLogger.TotalDropped,
            lastError = _fileLogger.LastError,
        };
    }
}
