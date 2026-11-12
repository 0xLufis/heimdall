using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App.Backend.Api.Logging;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Xunit;

namespace App.Backend.Tests;

/// <summary>Rotating file logger: rotation by size and age, total cap, spool overflow, shutdown flush, runtime levels.</summary>
public sealed class FileLoggingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "heimdall-filelog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (DirectoryNotFoundException) { }
    }

    private FileLoggerOptions Options(double maxFileKb = 1024, double maxTotalKb = 10 * 1024, int capacity = 20_000, double flushSeconds = 1) => new()
    {
        Enabled = true,
        Path = _dir,
        FileName = "backend.log",
        MaxFileSizeMB = maxFileKb / 1024,
        MaxTotalSizeMB = maxTotalKb / 1024,
        MaxAgeDays = 7,
        QueueCapacity = capacity,
        FlushIntervalSeconds = flushSeconds,
        ShutdownTimeoutSeconds = 5,
    };

    private string[] AllLines() =>
        Directory.GetFiles(_dir, "*.log").OrderBy(File.GetLastWriteTimeUtc).ThenBy(f => f, StringComparer.Ordinal)
            .SelectMany(File.ReadAllLines).ToArray();

    private long DirectoryBytes() => Directory.GetFiles(_dir).Sum(f => new FileInfo(f).Length);

    [Fact]
    public void SizeRotation_RollsTheActiveFileBeforeItExceedsTheLimit_AndKeepsEveryLine()
    {
        var provider = new FileLoggerProvider(Options(maxFileKb: 4), baseDirectory: null, startWriter: false);
        var logger = provider.CreateLogger("Heimdall.Test.Rotation");
        for (var i = 0; i < 300; i++)
        {
            logger.LogInformation("rotation line {Index:D4} with some padding to make it about a hundred bytes long", i);
            if (i % 25 == 0) provider.ProcessQueue();
        }
        provider.Dispose();

        var rotated = provider.GetRotatedFiles();
        Assert.True(rotated.Count >= 4, $"expected several rotated files, got {rotated.Count}");
        Assert.All(Directory.GetFiles(_dir), f => Assert.True(new FileInfo(f).Length <= 4 * 1024, $"{f} is {new FileInfo(f).Length} bytes"));
        Assert.All(rotated, f => Assert.Matches(@"^backend-\d{8}-\d{6}-\d{3}\.log$", f.Name));

        var lines = AllLines().Where(l => l.Contains("rotation line")).ToArray();
        Assert.Equal(300, lines.Length);
        Assert.Contains("[INF] Heimdall.Test.Rotation: rotation line 0299", lines[^1]);
    }

    [Fact]
    public void AgeRetention_DeletesRotatedFilesOlderThanMaxAge_AndLeavesOtherFilesAlone()
    {
        Directory.CreateDirectory(_dir);
        var old = Path.Combine(_dir, "backend-20200101-000000-000.log");
        var recent = Path.Combine(_dir, "backend-20200102-000000-000.log");
        var foreign = Path.Combine(_dir, "frontend.log");
        foreach (var f in new[] { old, recent, foreign }) File.WriteAllText(f, "x\n");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(foreign, DateTime.UtcNow.AddDays(-30));

        var provider = new FileLoggerProvider(Options(), baseDirectory: null, startWriter: false);
        provider.CreateLogger("Heimdall.Test.Age").LogWarning("first line opens the file and applies retention");
        provider.ProcessQueue(flush: true);

        Assert.False(File.Exists(old), "a rotated file older than MaxAgeDays must be deleted");
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(foreign), "files that do not belong to this logger are never touched");
        provider.Dispose();
    }

    [Fact]
    public void AgeRotation_ArchivesAnActiveFileLeftFromAnEarlierDay()
    {
        Directory.CreateDirectory(_dir);
        var active = Path.Combine(_dir, "backend.log");
        File.WriteAllText(active, "yesterday\n");
        File.SetLastWriteTime(active, DateTime.Now.AddDays(-1));

        var provider = new FileLoggerProvider(Options(), baseDirectory: null, startWriter: false);
        provider.CreateLogger("Heimdall.Test.Day").LogInformation("today");
        provider.Dispose();

        Assert.DoesNotContain("yesterday", File.ReadAllText(active));
        var archived = Assert.Single(provider.GetRotatedFiles());
        Assert.Equal("yesterday\n", File.ReadAllText(archived.FullName));
    }

    [Fact]
    public void TotalCap_DeletesTheOldestRotatedFiles_SoTheDirectoryStaysWithinTheCap()
    {
        var provider = new FileLoggerProvider(Options(maxFileKb: 2, maxTotalKb: 8), baseDirectory: null, startWriter: false);
        var logger = provider.CreateLogger("Heimdall.Test.Cap");
        for (var i = 0; i < 1000; i++)
        {
            logger.LogInformation("cap line {Index:D4} ------------------------------------------------------------", i);
            if (i % 10 == 0)
            {
                provider.ProcessQueue();
                Assert.True(DirectoryBytes() <= 8 * 1024, $"directory grew to {DirectoryBytes()} bytes");
            }
        }
        provider.Dispose();

        Assert.True(DirectoryBytes() <= 8 * 1024, $"directory grew to {DirectoryBytes()} bytes");
        var lines = AllLines();
        Assert.Contains(lines, l => l.Contains("cap line 0999"));
        Assert.DoesNotContain(lines, l => l.Contains("cap line 0000"));
    }

    [Fact]
    public void Spool_WhenFull_DropsTheOldestLowLevelEntriesFirst()
    {
        var spool = new LogSpool(16);
        LogEntry E(LogLevel level, string text) => new(DateTimeOffset.Now, level, "c", 0, text, null);

        for (var i = 0; i < 10; i++) spool.Enqueue(E(LogLevel.Information, $"I{i}"));
        for (var i = 0; i < 6; i++) spool.Enqueue(E(LogLevel.Warning, $"W{i}"));
        spool.Enqueue(E(LogLevel.Error, "E0"));       // full: evicts I0
        spool.Enqueue(E(LogLevel.Debug, "D0"));       // full: evicts I1

        var batch = new List<LogEntry>();
        Assert.Equal(2, spool.DrainTo(batch));
        Assert.Equal(
            new[] { "I2", "I3", "I4", "I5", "I6", "I7", "I8", "I9", "W0", "W1", "W2", "W3", "W4", "W5", "E0", "D0" },
            batch.Select(e => e.Message));

        // Only warnings and errors queued: a new low-level entry yields, a new error evicts the oldest warning.
        batch.Clear();
        for (var i = 0; i < 16; i++) spool.Enqueue(E(LogLevel.Warning, $"W{i}"));
        spool.Enqueue(E(LogLevel.Information, "late info"));
        spool.Enqueue(E(LogLevel.Critical, "crit"));
        Assert.Equal(2, spool.DrainTo(batch));
        Assert.DoesNotContain(batch, e => e.Message == "late info" || e.Message == "W0");
        Assert.Equal("crit", batch[^1].Message);
        Assert.Equal(4, spool.TotalDropped);
        Assert.Equal(0, spool.DrainTo(new List<LogEntry>()));
    }

    [Fact]
    public void QueueOverflow_KeepsErrors_AndWritesHowManyEntriesWereDropped()
    {
        var provider = new FileLoggerProvider(Options(capacity: 16), baseDirectory: null, startWriter: false);
        var logger = provider.CreateLogger("Heimdall.Test.Overflow");
        logger.LogError("early error");
        for (var i = 0; i < 20; i++) logger.LogInformation("info {Index}", i);
        logger.LogError("late error");
        provider.Dispose();

        var lines = AllLines();
        var dropped = Assert.Single(lines, l => l.Contains("log entries dropped"));
        Assert.Contains("6 log entries dropped", dropped);
        Assert.Contains("[WRN]", dropped);
        Assert.Contains(lines, l => l.Contains("early error"));
        Assert.Contains(lines, l => l.Contains("late error"));
        Assert.DoesNotContain(lines, l => l.EndsWith("info 5"));
        Assert.Contains(lines, l => l.EndsWith("info 6"));
        Assert.Equal(6, provider.TotalDropped);
    }

    [Fact]
    public void Dispose_DrainsAndFlushesEverythingStillSpooled_WithinTheDeadline()
    {
        // A one-hour flush interval: nothing reaches the disk unless shutdown flushes it.
        var provider = new FileLoggerProvider(Options(flushSeconds: 3600));
        var logger = provider.CreateLogger("Heimdall.Test.Shutdown");
        for (var i = 0; i < 500; i++) logger.LogInformation("pending {Index}", i);

        var watch = Stopwatch.StartNew();
        provider.Dispose();
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"dispose took {watch.Elapsed}");
        var lines = File.ReadAllLines(provider.ActiveFilePath);
        Assert.Equal(500, lines.Count(l => l.Contains("pending ")));
        Assert.EndsWith("pending 499", lines[^1]);

        logger.LogInformation("after dispose"); // ignored, must not throw
        provider.Dispose();                    // idempotent
    }

    [Fact]
    public void RuntimeLevelChange_AppliesToExistingLoggers_PerOutput_AndResets()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
            // A provider-specific Default rule beats every global category rule for that provider.
            ["Logging:Console:LogLevel:Default"] = "Information",
        });
        var source = new RuntimeLogLevelSource();
        ((IConfigurationBuilder)configuration).Add(source);

        var fileProvider = new FileLoggerProvider(Options(), baseDirectory: null, startWriter: false);
        using var services = new ServiceCollection()
            .AddLogging(b => b.AddConfiguration(configuration.GetSection("Logging")).AddProvider(fileProvider))
            .BuildServiceProvider();
        var factory = services.GetRequiredService<ILoggerFactory>();
        var filters = services.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>();
        var levels = new RuntimeLogLevelSwitch(source);
        const string Sql = "Microsoft.EntityFrameworkCore.Database.Command";
        var sqlLogger = factory.CreateLogger(Sql);
        var consoleType = typeof(ConsoleLoggerProvider).FullName!;
        var fileType = typeof(FileLoggerProvider).FullName!;

        Assert.False(sqlLogger.IsEnabled(LogLevel.Information));
        sqlLogger.LogInformation("SELECT 1 (hidden)");

        levels.Set(Sql, LogLevel.Information, "File");
        Assert.True(sqlLogger.IsEnabled(LogLevel.Information), "the existing logger picks up the change without a restart");
        Assert.Equal(LogLevel.Information, EffectiveLogLevel.For(filters.CurrentValue, fileType, "File", Sql));
        Assert.Equal(LogLevel.Information, EffectiveLogLevel.For(filters.CurrentValue, consoleType, "Console", Sql)); // console: its own Default rule
        sqlLogger.LogInformation("SELECT 2 (visible)");

        levels.Set("Default", LogLevel.Warning, "Console");
        Assert.Equal(LogLevel.Warning, EffectiveLogLevel.For(filters.CurrentValue, consoleType, "Console", "App.Backend.Api.Something"));
        Assert.Equal(LogLevel.Information, EffectiveLogLevel.For(filters.CurrentValue, fileType, "File", "App.Backend.Api.Something"));

        levels.Set(Sql, LogLevel.Debug); // All: global key plus both provider sections
        Assert.Equal(LogLevel.Debug, EffectiveLogLevel.For(filters.CurrentValue, consoleType, "Console", Sql));
        Assert.Equal(LogLevel.Debug, EffectiveLogLevel.For(filters.CurrentValue, fileType, "File", Sql));
        Assert.Contains(levels.Overrides, o => o.Provider == "All" && o.Category == Sql && o.Level == "Debug");

        levels.ResetAll();
        Assert.Empty(levels.Overrides);
        Assert.False(sqlLogger.IsEnabled(LogLevel.Information), "reset restores the configured level");
        Assert.Equal(LogLevel.Warning, EffectiveLogLevel.For(filters.CurrentValue, fileType, "File", Sql));
        sqlLogger.LogInformation("SELECT 3 (hidden again)");

        fileProvider.Dispose();
        var text = File.ReadAllText(fileProvider.ActiveFilePath);
        Assert.DoesNotContain("SELECT 1", text);
        Assert.Contains("SELECT 2 (visible)", text);
        Assert.DoesNotContain("SELECT 3", text);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", true)]
    [InlineData("Default", true)]
    [InlineData("App.*Service", true)]
    [InlineData("Logging:LogLevel", false)]
    [InlineData("a*b*c", false)]
    [InlineData("with space", false)]
    [InlineData("", false)]
    public void CategoryValidation_RejectsNamesThatWouldEscapeTheConfigurationKey(string category, bool valid) =>
        Assert.Equal(valid, RuntimeLogLevelSwitch.IsValidCategory(category));
}

/// <summary>The runtime log-level endpoint through the real pipeline: administrators only, changes apply at once.</summary>
public sealed class LoggingAdminEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Sql = "Microsoft.EntityFrameworkCore.Database.Command";
    private readonly CustomWebApplicationFactory _factory;

    public LoggingAdminEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.AuthSessions.Any(s => s.Token == "logging-tok-technician")) return;
        db.AuthUsers.Add(new AuthUser { Id = "usr-logging-tech", Name = "Log Technician", Email = "log-tech@fake-factory.internal", Role = HeimdallRoles.Technician });
        db.AuthSessions.Add(new AuthSession { Id = "s-logging-tech", Token = "logging-tok-technician", UserId = "usr-logging-tech", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        db.SaveChanges();
    }

    private static string? Level(JsonElement view, string category, string output) =>
        view.GetProperty("levels").EnumerateArray()
            .First(l => l.GetProperty("category").GetString() == category)
            .GetProperty(output).GetString();

    [Fact]
    public async Task Administrator_ReadsAndChangesTheLevel_AndNonAdministratorIsForbidden()
    {
        var admin = _factory.CreateClient(); // the Test environment authenticates anonymous calls as the dev administrator

        var before = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/logging?category={Sql}");
        Assert.Equal("Warning", Level(before, Sql, "console"));

        var put = await admin.PutAsJsonAsync("/api/v1/admin/logging/levels", new { category = Sql, level = "Information", provider = "All" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var after = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Information", Level(after, Sql, "console"));
        Assert.Contains(after.GetProperty("overrides").EnumerateArray(), o => o.GetProperty("category").GetString() == Sql);

        var logger = _factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Sql);
        Assert.True(logger.IsEnabled(LogLevel.Information));

        var bad = await admin.PutAsJsonAsync("/api/v1/admin/logging/levels", new { category = Sql, level = "Verbose" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var technician = _factory.CreateClient();
        technician.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "logging-tok-technician");
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync("/api/v1/admin/logging")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PutAsJsonAsync("/api/v1/admin/logging/levels", new { category = Sql, level = "Trace" })).StatusCode);

        var reset = await admin.DeleteAsync($"/api/v1/admin/logging/levels?category={Sql}");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal("Warning", Level(await reset.Content.ReadFromJsonAsync<JsonElement>(), Sql, "console"));
        Assert.False(logger.IsEnabled(LogLevel.Information));
    }
}
