using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Global search over stations, controllers, tickets, users and inventory.
/// Stations, controllers, tickets and inventory are limited to the caller's organization by the
/// tenant-scoped <see cref="AppDbContext"/>. The user directory is global: every signed-in caller
/// finds every user (name, e-mail, role), across organizations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IGlobalSearchService _search;

    /// <remarks><paramref name="authorization"/> is no longer consulted (the user directory used to be
    /// limited to administration policies); it stays in the signature for existing callers.</remarks>
    public SearchController(AppDbContext db, IAuthorizationService? authorization = null, IGlobalSearchService? search = null)
    {
        _search = search ?? new GlobalSearchService(db);
    }

    /// <summary>
    /// Searches the requested entity kinds for <paramref name="q"/>.
    /// </summary>
    /// <param name="q">Free text; every whitespace-separated token has to match. At least two characters.</param>
    /// <param name="kinds">Comma-separated subset of stations, controllers, tickets, users, inventory. Default: all.</param>
    /// <param name="limit">Maximum hits per kind (1-50, default 6).</param>
    [HttpGet]
    public async Task<ActionResult<GlobalSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] string? kinds = null,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        List<string>? requestedKinds = null;
        if (!string.IsNullOrWhiteSpace(kinds))
        {
            requestedKinds = new List<string>();
            foreach (var raw in kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kind = SearchKinds.Normalize(raw);
                if (kind == null)
                {
                    return BadRequest(new { error = $"Unknown search kind '{raw}'. Valid kinds: {string.Join(", ", SearchKinds.All)}." });
                }
                if (!requestedKinds.Contains(kind)) requestedKinds.Add(kind);
            }
        }

        var response = await _search.SearchAsync(new GlobalSearchQuery
        {
            Text = q,
            Kinds = requestedKinds,
            Limit = limit ?? GlobalSearchService.DefaultLimit,
            // The user directory is open to every signed-in user ([Authorize] keeps anonymous callers out).
            CanSearchUsers = User?.Identity?.IsAuthenticated == true
        }, cancellationToken);

        return Ok(response);
    }
}
