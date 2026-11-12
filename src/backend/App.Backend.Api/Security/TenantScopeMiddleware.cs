using App.Contracts.Security;
using App.Shared.Data;
using Microsoft.AspNetCore.Authorization;

namespace App.Backend.Api.Security;

/// <summary>
/// Activates the multi-tenant EF Core global query filters for every authenticated request by
/// setting <see cref="AppDbContext.CurrentOrganizationId"/> on the request-scoped context.
///
/// Before this middleware existed the property was never assigned, so every
/// <c>HasQueryFilter(... CurrentOrganizationId == null || ...)</c> evaluated to "no filter" and
/// all tenants' tickets, inventory, client PCs, audit logs, etc. were visible to every user.
///
/// Rules:
/// <list type="bullet">
///   <item>Platform administrators (<see cref="AuthorizationPolicies.SystemAdministration"/>) stay unscoped.</item>
///   <item>Other authenticated callers are scoped to their <c>OrgId</c> claim.</item>
///   <item>Authenticated non-admins with no <c>OrgId</c> fail closed: they only see global
///   (organization-less) rows, via a sentinel that never matches a real organization.</item>
///   <item>Anonymous requests are left untouched; [Authorize] rejects them before data access,
///   and machine-to-machine endpoints apply their own credential checks.</item>
/// </list>
/// Contexts created through <c>IDbContextFactory&lt;AppDbContext&gt;</c> are NOT covered and must
/// set the property explicitly.
/// </summary>
public sealed class TenantScopeMiddleware
{
    public const string OrgClaimType = "OrgId";
    public const string NoOrganizationSentinel = "__heimdall_no_organization__";

    /// <summary><see cref="HttpContext.Items"/> key holding the tenant scope resolved for the request.</summary>
    public const string ScopeItemKey = "Heimdall.TenantScope";
    /// <summary>Cache scope of unscoped (platform administrator) requests.</summary>
    public const string UnscopedCacheScope = "all";

    private readonly RequestDelegate _next;

    public TenantScopeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db, IAuthorizationService authorization)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var scope = await ResolveScopeAsync(user, authorization);
            db.CurrentOrganizationId = scope;
            context.Items[ScopeItemKey] = scope ?? UnscopedCacheScope;
        }

        await _next(context);
    }

    /// <summary>
    /// Qualifies a cache key with the caller's tenant scope. Results of tenant-filtered queries
    /// must be cached per scope: under a shared key the first caller's rows (for example a platform
    /// administrator's unfiltered list) are served to every other tenant until the entry expires.
    /// </summary>
    public static string ScopedCacheKey(HttpContext? context, string key) =>
        $"{key}:tenant:{(context?.Items[ScopeItemKey] as string) ?? UnscopedCacheScope}";

    public static async Task<string?> ResolveScopeAsync(
        System.Security.Claims.ClaimsPrincipal user, IAuthorizationService authorization)
    {
        var isPlatformAdmin =
            (await authorization.AuthorizeAsync(user, AuthorizationPolicies.SystemAdministration)).Succeeded;
        if (isPlatformAdmin) return null;

        var orgId = user.FindFirst(OrgClaimType)?.Value;
        return string.IsNullOrWhiteSpace(orgId) ? NoOrganizationSentinel : orgId;
    }
}
