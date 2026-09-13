namespace App.Agent.Daemon.Extensions;

using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using App.Agent.Daemon.Infrastructure.Plugins;
using App.Agent.Daemon.Interfaces;
using App.Shared.Errors;
using App.Shared.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

public class ExtensionAuthFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configService = context.HttpContext.RequestServices.GetRequiredService<IConfigurationService>();
        var config = configService.Config;

        // 1. Loopback restriction check
        if (config.RequireLoopbackForExtensions)
        {
            var remoteIp = context.HttpContext.Connection.RemoteIpAddress;
            if (remoteIp != null && !IPAddress.IsLoopback(remoteIp))
            {
                return Results.Json(
                    new ApiError(ErrorCode.AccessDenied, "Extension API is restricted to loopback callers."),
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        // 2. Authentication check
        string configuredKey = config.ExtensionApiKey;
        if (string.IsNullOrEmpty(configuredKey))
        {
            configuredKey = Environment.GetEnvironmentVariable("HEIMDALL_EXTENSION_KEY") ?? string.Empty;
        }

        // If a key is configured, caller MUST provide matching token
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = context.HttpContext.Request.Headers["X-Extension-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(providedKey))
            {
                var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    providedKey = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(providedKey))
            {
                return Results.Json(
                    new ApiError(ErrorCode.AuthenticationRequired, "X-Extension-Key or Bearer token is required."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            byte[] configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
            byte[] providedBytes = Encoding.UTF8.GetBytes(providedKey);

            if (configuredBytes.Length != providedBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes))
            {
                return Results.Json(
                    new ApiError(ErrorCode.InvalidCredentials, "Invalid Extension API key."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        return await next(context);
    }
}

public static class ExtensionApiEndpoints
{
    public static IEndpointRouteBuilder MapExtensionApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1");

        // --- Status & Diagnostics ---
        group.MapGet("/agent/status", (
            ISystemInfoService sysInfoService,
            IConfigurationService configService,
            IExtensionRegistry extensionRegistry,
            IPluginManager pluginManager) =>
        {
            var info = sysInfoService.GetSystemInfo();
            var config = configService.Config;

            var status = new AgentStatus
            {
                Hostname = info.Hostname,
                MachineIdentifier = info.MachineIdentifier,
                MacAddress = info.MacAddress,
                AgentVersion = "1.0.0",
                BackendUrl = config.BackendUrl,
                AuthType = config.AuthType,
                BackendConnected = true,
                LastReportTimeUtc = info.LastOnline,
                ActiveExtensionsCount = extensionRegistry.GetActiveComponents().Count,
                ActivePluginsCount = pluginManager.GetInstalledPlugins().Count,
                Environment = config.Environment
            };

            return Results.Ok(status);
        });

        // --- Custom Component Ingestion ---
        group.MapPost("/extensions/components", async (
            CustomComponentSubmission submission,
            IExtensionRegistry registry,
            ISystemInfoReporter reporter) =>
        {
            if (submission == null)
            {
                return Results.BadRequest(new ApiError(ErrorCode.InvalidInput, "Submission payload cannot be null."));
            }

            bool success = registry.RegisterOrUpdateComponent(submission, out var error);
            if (!success)
            {
                return Results.BadRequest(new ApiError(ErrorCode.InvalidPayload, error));
            }

            if (submission.ImmediateSync)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await reporter.TriggerSyncAsync();
                    }
                    catch { }
                });
            }

            return Results.Ok(new { Message = $"Component '{submission.ComponentName}' registered successfully." });
        }).AddEndpointFilter<ExtensionAuthFilter>();

        group.MapGet("/extensions/components", (IExtensionRegistry registry) =>
        {
            return Results.Ok(registry.GetActiveComponents());
        }).AddEndpointFilter<ExtensionAuthFilter>();

        group.MapDelete("/extensions/components/{name}", (string name, IExtensionRegistry registry) =>
        {
            bool removed = registry.RemoveComponent(name);
            if (!removed)
            {
                return Results.NotFound(new ApiError(ErrorCode.EntityNotFound, $"Component '{name}' was not found."));
            }
            return Results.NoContent();
        }).AddEndpointFilter<ExtensionAuthFilter>();

        // --- Custom Telemetry Ingestion ---
        group.MapPost("/extensions/telemetry", (
            CustomTelemetrySubmission telemetry,
            IExtensionRegistry registry) =>
        {
            if (telemetry == null || telemetry.Metrics == null || telemetry.Metrics.Count == 0)
            {
                return Results.BadRequest(new ApiError(ErrorCode.InvalidInput, "Telemetry submission must contain at least one metric point."));
            }

            registry.EnqueueTelemetry(telemetry);
            return Results.Accepted(value: new { Enqueued = telemetry.Metrics.Count });
        }).AddEndpointFilter<ExtensionAuthFilter>();

        // --- Custom Event Ingestion ---
        group.MapPost("/extensions/events", (
            CustomEventSubmission customEvent,
            IExtensionRegistry registry) =>
        {
            if (customEvent == null || string.IsNullOrWhiteSpace(customEvent.Message))
            {
                return Results.BadRequest(new ApiError(ErrorCode.InvalidInput, "Event message is required."));
            }

            registry.EnqueueEvent(customEvent);
            return Results.Accepted(value: new { Status = "Enqueued" });
        }).AddEndpointFilter<ExtensionAuthFilter>();

        // --- Immediate Sync Trigger ---
        group.MapPost("/agent/sync", async (ISystemInfoReporter reporter) =>
        {
            var res = await reporter.TriggerSyncAsync();
            return Results.Ok(new { Success = res?.Success ?? false, Message = res?.Message ?? "Sync completed" });
        }).AddEndpointFilter<ExtensionAuthFilter>();

        // --- Plugins Query ---
        group.MapGet("/plugins", (IPluginManager pluginManager) =>
        {
            return Results.Ok(pluginManager.GetInstalledPlugins());
        }).AddEndpointFilter<ExtensionAuthFilter>();

        return app;
    }
}
