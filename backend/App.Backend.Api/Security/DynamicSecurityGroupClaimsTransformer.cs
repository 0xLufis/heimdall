using System.Security.Claims;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace App.Backend.Api.Security;

public class DynamicSecurityGroupClaimsTransformer : IClaimsTransformation
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DynamicSecurityGroupClaimsTransformer> _logger;
    
    private const string CacheKey = "heimdall:security_group_mappings:active";
    private const string DelegationCacheKey = "heimdall:admin_delegation:active";

    public DynamicSecurityGroupClaimsTransformer(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMemoryCache memoryCache,
        ILogger<DynamicSecurityGroupClaimsTransformer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity == null || !principal.Identity.IsAuthenticated)
        {
            return principal;
        }

        var identity = (ClaimsIdentity)principal.Identity;
        var existingRoles = principal.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Evaluate Dynamic Admin Role Delegation (e.g. Heimdall Admin as Pseudo-IT Admin)
        try
        {
            var isPseudoItAdminEnabled = await _memoryCache.GetOrCreateAsync(DelegationCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var setting = await db.SystemSettings
                    .FirstOrDefaultAsync(s => s.Category == "AdminRoleDelegation" || s.Key == "AdminRoleDelegation");
                if (setting != null && !string.IsNullOrEmpty(setting.ValueJson))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(setting.ValueJson);
                        if (doc.RootElement.TryGetProperty("heimdallAdminIsPseudoItAdmin", out var prop))
                        {
                            return prop.GetBoolean();
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            });

            if (isPseudoItAdminEnabled && (existingRoles.Contains("heimdall_admin") || existingRoles.Contains("admin")))
            {
                if (!existingRoles.Contains("it_admin"))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, "it_admin"));
                    existingRoles.Add("it_admin");
                }
                if (!existingRoles.Contains("it_site_admin"))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, "it_site_admin"));
                    existingRoles.Add("it_site_admin");
                }
                _logger.LogDebug("Injected pseudo IT-Admin claims for Heimdall Admin user.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating dynamic admin role delegation.");
        }

        // 2. Collect all incoming group claims from Entra ID / Active Directory / OIDC tokens
        var groupClaims = principal.Claims
            .Where(c => c.Type == "groups" || 
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/groupsid" ||
                        c.Type == "wids" ||
                        c.Type == "roles")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (groupClaims.Count == 0)
        {
            return principal;
        }

        try
        {
            var mappings = await _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                return await db.SecurityGroupMappings
                    .Where(m => m.IsEnabled)
                    .AsNoTracking()
                    .ToListAsync();
            }) ?? new List<SecurityGroupMapping>();

            var matchedMappings = mappings
                .Where(m => groupClaims.Contains(m.GroupIdentifier))
                .ToList();

            if (matchedMappings.Count == 0)
            {
                return principal;
            }

            foreach (var match in matchedMappings)
            {
                if (!string.IsNullOrEmpty(match.MappedRole) && !existingRoles.Contains(match.MappedRole))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, match.MappedRole));
                    existingRoles.Add(match.MappedRole);
                    _logger.LogInformation("Dynamically mapped security group {Group} to role {Role}", match.GroupIdentifier, match.MappedRole);
                }

                if (!string.IsNullOrEmpty(match.OrganizationId) && !principal.HasClaim(c => c.Type == "OrgId"))
                {
                    identity.AddClaim(new Claim("OrgId", match.OrganizationId));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating dynamic security group claims transformation.");
        }

        return principal;
    }

    public static void InvalidateCache(IMemoryCache cache)
    {
        cache.Remove(CacheKey);
        cache.Remove(DelegationCacheKey);
    }
}
