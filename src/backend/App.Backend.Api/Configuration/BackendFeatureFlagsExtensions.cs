namespace App.Backend.Api.Configuration;

using App.Contracts.Configuration;
using Microsoft.Extensions.Configuration;

public static class BackendFeatureFlagsExtensions
{
    public static BackendFeatureFlags ToBackendFeatureFlags(this IConfiguration configuration, string? environmentName = null)
    {
        return BackendFeatureFlags.FromProvider(key => configuration[key], environmentName);
    }
}
