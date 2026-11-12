using Microsoft.Extensions.Configuration;

namespace App.Backend.Api.Logging;

public static class HeimdallLoggingExtensions
{
    /// <summary>
    /// Adds the runtime log-level switch (must run after the last configuration source that should be
    /// overridable at runtime) and, when <c>Logging:File:Enabled</c> is true, the rotating file logger.
    /// The console logger stays as configured by the host (<c>Logging:Console</c>).
    /// </summary>
    public static WebApplicationBuilder AddHeimdallLogging(this WebApplicationBuilder builder)
    {
        var runtimeLevels = new RuntimeLogLevelSource();
        ((IConfigurationBuilder)builder.Configuration).Add(runtimeLevels);
        builder.Services.AddSingleton(runtimeLevels);
        builder.Services.AddSingleton<RuntimeLogLevelSwitch>();

        if (builder.Configuration.GetValue<bool>($"{FileLoggerOptions.SectionName}:Enabled"))
        {
            // Created and disposed by the container: disposal drains the spool and flushes on shutdown.
            builder.Services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IConfiguration>().GetSection(FileLoggerOptions.SectionName).Get<FileLoggerOptions>()
                    ?? new FileLoggerOptions();
                return new FileLoggerProvider(options, sp.GetRequiredService<IHostEnvironment>().ContentRootPath);
            });
            builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<FileLoggerProvider>());
        }

        return builder;
    }
}
