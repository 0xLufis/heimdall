using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Backend.Tests;

public class DependencyInjectionIntegrityTests : IClassFixture<WebApplicationFactory<App.Backend.Api.Program>>
{
    private readonly WebApplicationFactory<App.Backend.Api.Program> _factory;

    public DependencyInjectionIntegrityTests(WebApplicationFactory<App.Backend.Api.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=dummy;Username=test;Password=test");
            builder.UseSetting("REDIS_CONNECTION_STRING", "localhost:6379");
        });
    }

    [Fact]
    public void ServiceProvider_ValidatesAllDescriptors_WithoutCaptiveDependencies()
    {
        // Act: Resolving Services triggers full DI descriptor and scope validation
        var services = _factory.Services;
        Assert.NotNull(services);

        // Verify key services can be resolved without runtime exceptions
        using var scope = services.CreateScope();
        var pluginService = scope.ServiceProvider.GetService<App.Backend.Api.Services.Plugins.IPluginService>();
        Assert.NotNull(pluginService);
    }
}
