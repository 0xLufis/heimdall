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

    private readonly RequestDelegate _next;

    public TenantScopeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db, IAuthorizationService authorization)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            db.CurrentOrganizationId = await ResolveScopeAsync(user, authorization);
        }

        await _next(context);
    }

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
