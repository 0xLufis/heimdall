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

        // Collect all incoming group claims from Entra ID / Active Directory / OIDC tokens
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

            var identity = (ClaimsIdentity)principal.Identity;
            var existingRoles = principal.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
    }
}
