using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using App.Backend.Api.Dtos;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Services;

/// <summary>Entity kinds the global search covers.</summary>
public static class SearchKinds
{
    public const string Stations = "stations";
    public const string Controllers = "controllers";
    public const string Tickets = "tickets";
    public const string Users = "users";
    public const string Inventory = "inventory";

    public static readonly IReadOnlyList<string> All = new[] { Stations, Controllers, Tickets, Users, Inventory };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stations"] = Stations, ["station"] = Stations, ["machines"] = Stations, ["machine"] = Stations,
        ["controllers"] = Controllers, ["controller"] = Controllers, ["clients"] = Controllers, ["nodes"] = Controllers,
        ["tickets"] = Tickets, ["ticket"] = Tickets,
        ["users"] = Users, ["user"] = Users, ["people"] = Users,
        ["inventory"] = Inventory, ["parts"] = Inventory, ["part"] = Inventory
    };

    /// <summary>Canonical kind for a name or alias, or null when it is unknown.</summary>
    public static string? Normalize(string? kind) =>
        kind != null && Aliases.TryGetValue(kind.Trim(), out var canonical) ? canonical : null;
}

public sealed class GlobalSearchQuery
{
    public string? Text { get; init; }

    /// <summary>Canonical kinds to search; null or empty means all of them.</summary>
    public IReadOnlyCollection<string>? Kinds { get; init; }

    /// <summary>Maximum hits per kind.</summary>
    public int Limit { get; init; } = GlobalSearchService.DefaultLimit;

    /// <summary>Whether the caller may see the user directory (decided by the controller).</summary>
    public bool CanSearchUsers { get; init; }
}

public sealed class GlobalSearchOptions
{
    /// <summary>Literal matches fetched per kind before they are ranked.</summary>
    public int CandidateLimit { get; init; } = 200;

    /// <summary>
    /// Rows per kind that are checked for typo matches in memory. A kind with more rows needs
    /// pg_trgm to narrow the candidates in SQL first.
    /// </summary>
    public int TypoScanLimit { get; init; } = 2000;

    /// <summary>pg_trgm word-similarity threshold used to narrow typo candidates in SQL.</summary>
    public double TrigramThreshold { get; init; } = 0.3;

    /// <summary>Overrides the pg_trgm availability probe (tests).</summary>
    public bool? TrigramsAvailable { get; init; }
}

public interface IGlobalSearchService
{
    Task<GlobalSearchResponse> SearchAsync(GlobalSearchQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Server-side search over stations, controllers, tickets, users and inventory.
///
/// Every query runs through the request-scoped <see cref="AppDbContext"/>, so the multi-tenant
/// global query filters apply: a caller only ever gets rows of the organization the context is
/// scoped to (plus organization-less rows), and platform administrators stay unscoped. Users
/// have no query filter (the table belongs to Better-Auth), so they are scoped here through
/// their organization memberships.
///
/// Matching: every query token must be contained in at least one searchable column
/// (case-insensitive substring, <c>ILIKE</c> on PostgreSQL). Candidates are ranked in memory by
/// <see cref="SearchRanker"/>. Only when a kind has no literal match at all, digit-free tokens
/// are matched with typo tolerance: by scanning the kind when it is small, or, for large
/// tables, by letting pg_trgm narrow the candidates first. Without pg_trgm the search still
/// works with plain <c>ILIKE</c>; only typo matching on large tables becomes partial.
/// </summary>
public sealed class GlobalSearchService : IGlobalSearchService
{
    public const int DefaultLimit = 6;
    public const int MaxLimit = 50;
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 200;

    private static readonly TimeSpan TrigramProbeLifetime = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, (bool Available, DateTime CheckedUtc)> TrigramProbes = new();

    private readonly AppDbContext _db;
    private readonly GlobalSearchOptions _options;
    private readonly bool _isNpgsql;
    private readonly MethodInfo _likeMethod;

    public GlobalSearchService(AppDbContext db, GlobalSearchOptions? options = null)
    {
        _db = db;
        _options = options ?? new GlobalSearchOptions();
        _isNpgsql = db.Database.IsNpgsql();

        // ILIKE is PostgreSQL-only; other providers (the in-memory test provider) match LIKE case-insensitively.
        var arguments = new[] { typeof(DbFunctions), typeof(string), typeof(string), typeof(string) };
        _likeMethod = _isNpgsql
            ? typeof(NpgsqlDbFunctionsExtensions).GetMethod(nameof(NpgsqlDbFunctionsExtensions.ILike), arguments)!
            : typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), arguments)!;
    }

    public async Task<GlobalSearchResponse> SearchAsync(GlobalSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var text = (query.Text ?? string.Empty).Trim();
        if (text.Length > MaxQueryLength) text = text[..MaxQueryLength];

        var requested = query.Kinds is { Count: > 0 } ? query.Kinds : SearchKinds.All;
        var kinds = SearchKinds.All
            .Where(kind => requested.Contains(kind) && (kind != SearchKinds.Users || query.CanSearchUsers))
            .ToList();

        var response = new GlobalSearchResponse { Query = text, Kinds = kinds };
        var tokens = SearchRanker.Tokenize(text);
        if (text.Length < MinQueryLength || tokens.Length == 0)
        {
            response.TookMs = stopwatch.Elapsed.TotalMilliseconds;
            return response;
        }

        var run = new SearchRun(tokens, Math.Clamp(query.Limit, 1, MaxLimit));

        if (kinds.Contains(SearchKinds.Stations)) response.Stations = await SearchStationsAsync(run, cancellationToken);
        if (kinds.Contains(SearchKinds.Controllers)) response.Controllers = await SearchControllersAsync(run, cancellationToken);
        if (kinds.Contains(SearchKinds.Tickets)) response.Tickets = await SearchTicketsAsync(run, cancellationToken);
        if (kinds.Contains(SearchKinds.Users)) response.Users = await SearchUsersAsync(run, cancellationToken);
        if (kinds.Contains(SearchKinds.Inventory)) response.Inventory = await SearchInventoryAsync(run, cancellationToken);

        response.TypoTolerance = run.TypoTolerance;
        response.TookMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
        return response;
    }

    // ------------------------------------------------------------------ kinds

    private Task<List<StationSearchHit>> SearchStationsAsync(SearchRun run, CancellationToken ct)
    {
        var spec = new KindSpec<Machine, StationSearchHit>
        {
            Source = _db.Machines.AsNoTracking(),
            Columns = Columns<Machine>(m => m.Name, m => m.DisplayName, m => m.CustomIdentifier, m => m.PinnedObjectHandle, m => m.MachineType, m => m.GroupId, m => m.Technology),
            TextColumns = Columns<Machine>(m => m.Name, m => m.DisplayName, m => m.MachineType, m => m.GroupId, m => m.Technology),
            ThenBy = q => q.ThenBy(m => m.Name),
            Projection = m => new StationSearchHit
            {
                Id = m.Id,
                Name = m.Name,
                DisplayName = m.DisplayName,
                CustomIdentifier = m.CustomIdentifier,
                PinnedObjectHandle = m.PinnedObjectHandle,
                MachineType = m.MachineType,
                GroupId = m.GroupId,
                Technology = m.Technology,
                Status = m.Status
            },
            RankFields = h => new[] { h.Name, h.DisplayName, h.CustomIdentifier, h.PinnedObjectHandle, h.MachineType, h.GroupId, h.Technology }
        };
        return SearchKindAsync(spec, run, ct);
    }

    private Task<List<ControllerSearchHit>> SearchControllersAsync(SearchRun run, CancellationToken ct)
    {
        var spec = new KindSpec<ClientPc, ControllerSearchHit>
        {
            Source = _db.ClientPcs.AsNoTracking(),
            Columns = Columns<ClientPc>(c => c.Hostname, c => c.Name, c => c.IpAddress, c => c.MacAddress, c => c.MachineIdentifier),
            TextColumns = Columns<ClientPc>(c => c.Hostname, c => c.Name),
            ThenBy = q => q.ThenBy(c => c.Hostname).ThenBy(c => c.Name),
            Projection = c => new ControllerSearchHit
            {
                Id = c.Id,
                Hostname = c.Hostname,
                Name = c.Name,
                IpAddress = c.IpAddress,
                MacAddress = c.MacAddress,
                MachineIdentifier = c.MachineIdentifier,
                LastOnline = c.LastOnline
            },
            RankFields = h => new[] { h.Hostname, h.Name, h.IpAddress, h.MacAddress, h.MachineIdentifier }
        };
        return SearchKindAsync(spec, run, ct);
    }

    private async Task<List<TicketSearchHit>> SearchTicketsAsync(SearchRun run, CancellationToken ct)
    {
        // The ticket number is derived from the id ("TKT-" + first eight hex digits), as in the ticket list.
        var spec = new KindSpec<MaintenanceTicket, TicketRow>
        {
            Source = _db.MaintenanceTickets.AsNoTracking(),
            Columns = Columns<MaintenanceTicket>(
                t => "TKT-" + t.Id.ToString().Substring(0, 8),
                t => t.Title,
                t => t.Machine!.Name,
                t => t.AssignedTo,
                t => t.Tags,
                t => t.AlarmCode,
                t => t.Description),
            TextColumns = Columns<MaintenanceTicket>(t => t.Title, t => t.AssignedTo),
            ThenBy = q => q.ThenByDescending(t => t.CreatedAt),
            Projection = t => new TicketRow
            {
                Id = t.Id,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                StationName = t.Machine != null ? t.Machine.Name : null,
                AssignedTo = t.AssignedTo,
                Tags = t.Tags,
                AlarmCode = t.AlarmCode,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            },
            // The typo scan reads many rows: leave the (potentially long) description out of it.
            ScanProjection = t => new TicketRow
            {
                Id = t.Id,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                StationName = t.Machine != null ? t.Machine.Name : null,
                AssignedTo = t.AssignedTo,
                Tags = t.Tags,
                AlarmCode = t.AlarmCode,
                CreatedAt = t.CreatedAt
            },
            RankFields = r => new[] { TicketNumber(r.Id), r.Title, r.StationName, r.AssignedTo, r.Tags, r.AlarmCode, r.Description }
        };

        var rows = await SearchKindAsync(spec, run, ct);
        return rows.Select(r => new TicketSearchHit
        {
            Id = r.Id,
            TicketNumber = TicketNumber(r.Id),
            Title = r.Title,
            Status = r.Status,
            Priority = r.Priority,
            StationName = r.StationName,
            AssignedTechnicianName = r.AssignedTo,
            CreatedAt = r.CreatedAt,
            Score = r.Score
        }).ToList();
    }

    private Task<List<UserSearchHit>> SearchUsersAsync(SearchRun run, CancellationToken ct)
    {
        // The user directory is global (owner decision 2026-11-12): every signed-in caller may find
        // every user, across organizations. Callers that may not search users at all are filtered
        // out before this runs (GlobalSearchQuery.CanSearchUsers).
        var users = _db.AuthUsers.AsNoTracking();

        var spec = new KindSpec<AuthUser, UserSearchHit>
        {
            Source = users,
            Columns = Columns<AuthUser>(u => u.Name, u => u.Email),
            TextColumns = Columns<AuthUser>(u => u.Name, u => u.Email),
            ThenBy = q => q.ThenBy(u => u.Name),
            Projection = u => new UserSearchHit { Id = u.Id, Name = u.Name, Email = u.Email, Role = u.Role },
            RankFields = h => new[] { h.Name, h.Email }
        };
        return SearchKindAsync(spec, run, ct);
    }

    private async Task<List<InventorySearchHit>> SearchInventoryAsync(SearchRun run, CancellationToken ct)
    {
        // Manufacturers are a small reference table: resolve the matching ones up front so the
        // item query stays a single-table OR (which trigram indexes can serve).
        var manufacturers = await _db.Manufacturers.AsNoTracking().Select(m => new { m.Id, m.Name }).ToListAsync(ct);

        var spec = new KindSpec<BaseInventoryItem, InventorySearchHit>
        {
            // Stations share the inventory table (TPT); they are their own search kind.
            Source = _db.InventoryItems.AsNoTracking().Where(i => !(i is Machine)),
            Columns = Columns<BaseInventoryItem>(i => i.Name, i => i.DisplayName, i => i.SerialNumber, i => i.StorageLocation),
            TextColumns = Columns<BaseInventoryItem>(i => i.Name, i => i.DisplayName),
            ExtraMatch = (token, fuzzy) =>
            {
                var ids = manufacturers
                    .Where(m => SearchRanker.ScoreToken(token, m.Name, fuzzy) > 0)
                    .Select(m => (Guid?)m.Id)
                    .ToList();
                return ids.Count == 0 ? null : i => ids.Contains(i.ManufacturerId);
            },
            ThenBy = q => q.ThenBy(i => i.Name),
            Projection = i => new InventorySearchHit
            {
                Id = i.Id,
                Name = i.Name,
                DisplayName = i.DisplayName,
                ItemType = i is HardwareComponent ? nameof(HardwareComponent)
                    : i is SoftwareComponent ? nameof(SoftwareComponent)
                    : i is SoftwareAsset ? nameof(SoftwareAsset)
                    : i is PcHardware ? nameof(PcHardware)
                    : "Asset",
                SerialNumber = i.SerialNumber,
                ManufacturerName = i.Manufacturer != null ? i.Manufacturer.Name : null,
                StorageLocation = i.StorageLocation,
                EquipmentStatus = i.EquipmentStatus,
                IsStockItem = i.IsStockItem,
                StockQuantity = i.StockQuantity
            },
            RankFields = h => new[] { h.Name, h.DisplayName, h.SerialNumber, h.ManufacturerName, h.StorageLocation }
        };
        return await SearchKindAsync(spec, run, ct);
    }

    // ------------------------------------------------------------------ engine

    private sealed class SearchRun
    {
        public SearchRun(string[] tokens, int limit)
        {
            Tokens = tokens;
            Limit = limit;
            TypoTokens = tokens.Where(SearchRanker.IsTypoEligible).ToArray();
        }

        public string[] Tokens { get; }
        public string[] TypoTokens { get; }
        public int Limit { get; }

        /// <summary>Weakest typo coverage over the searched kinds: full &gt; trigram &gt; partial.</summary>
        public string TypoTolerance { get; private set; } = "full";

        public void Downgrade(string level)
        {
            if (level == "partial" || TypoTolerance == "full") TypoTolerance = level;
        }
    }

    private sealed class KindSpec<TEntity, TRow> where TEntity : class where TRow : class
    {
        /// <summary>Tenant-scoped rows of the kind.</summary>
        public required IQueryable<TEntity> Source { get; init; }

        /// <summary>Columns a token may match, most important first; the first one drives the coarse SQL order.</summary>
        public required IReadOnlyList<Expression<Func<TEntity, string?>>> Columns { get; init; }

        /// <summary>Free-text columns pg_trgm compares typo-eligible tokens with.</summary>
        public required IReadOnlyList<Expression<Func<TEntity, string?>>> TextColumns { get; init; }

        /// <summary>Additional way a token can match a row (e.g. through a pre-resolved reference).</summary>
        public Func<string, bool, Expression<Func<TEntity, bool>>?>? ExtraMatch { get; init; }

        /// <summary>Order among rows of the same coarse relevance.</summary>
        public required Func<IOrderedQueryable<TEntity>, IOrderedQueryable<TEntity>> ThenBy { get; init; }

        public required Expression<Func<TEntity, TRow>> Projection { get; init; }

        /// <summary>Lighter projection for the typo scan; defaults to <see cref="Projection"/>.</summary>
        public Expression<Func<TEntity, TRow>>? ScanProjection { get; init; }

        /// <summary>Values the ranker scores, in the same order of importance as <see cref="Columns"/>.</summary>
        public required Func<TRow, IReadOnlyList<string?>> RankFields { get; init; }
    }

    private async Task<List<TRow>> SearchKindAsync<TEntity, TRow>(KindSpec<TEntity, TRow> spec, SearchRun run, CancellationToken ct)
        where TEntity : class
        where TRow : SearchHit
    {
        // 1. Literal pass: every token is a substring of some column.
        var literal = spec.Source;
        foreach (var token in run.Tokens)
        {
            literal = literal.Where(TokenPredicate(spec, token, fuzzy: false));
        }

        var candidates = await spec.ThenBy(literal.OrderBy(CoarseRank(spec.Columns[0], run.Tokens[0])))
            .Take(_options.CandidateLimit)
            .Select(spec.Projection)
            .ToListAsync(ct);

        var ranked = Rank(spec, candidates, run.Tokens);
        if (ranked.Count > 0 || run.TypoTokens.Length == 0)
        {
            return ranked.Take(run.Limit).ToList();
        }

        // 2. Typo pass, only for a kind without any literal match (it reads many more rows):
        //    tokens with digits still have to match literally; the others may match with a typo.
        var loose = spec.Source;
        foreach (var token in run.Tokens.Except(run.TypoTokens))
        {
            loose = loose.Where(TokenPredicate(spec, token, fuzzy: false));
        }

        var scanProjection = spec.ScanProjection ?? spec.Projection;
        var scan = await spec.ThenBy(loose.OrderBy(CoarseRank(spec.Columns[0], run.Tokens[0])))
            .Take(_options.TypoScanLimit + 1)
            .Select(scanProjection)
            .ToListAsync(ct);

        if (scan.Count > _options.TypoScanLimit)
        {
            // Too many rows to check one by one.
            List<TRow>? narrowed = null;
            if (await TrigramsAvailableAsync(ct))
            {
                try
                {
                    narrowed = await TrigramCandidatesAsync(spec, loose, run, scanProjection, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Typo matching is best effort: never fail a search because the trigram query could not run.
                    narrowed = null;
                }
            }

            if (narrowed != null)
            {
                scan = narrowed;
                run.Downgrade("trigram");
            }
            else
            {
                scan.RemoveAt(scan.Count - 1);
                run.Downgrade("partial");
            }
        }

        return Rank(spec, scan, run.Tokens).Take(run.Limit).ToList();
    }

    /// <summary>Rows that match every typo-eligible token literally or by pg_trgm word similarity.</summary>
    private async Task<List<TRow>> TrigramCandidatesAsync<TEntity, TRow>(
        KindSpec<TEntity, TRow> spec,
        IQueryable<TEntity> loose,
        SearchRun run,
        Expression<Func<TEntity, TRow>> projection,
        CancellationToken ct)
        where TEntity : class
        where TRow : SearchHit
    {
        var narrowed = loose;
        foreach (var token in run.TypoTokens)
        {
            narrowed = narrowed.Where(TokenPredicate(spec, token, fuzzy: true));
        }
        var query = spec.ThenBy(narrowed.OrderBy(CoarseRank(spec.Columns[0], run.Tokens[0])))
            .Take(_options.TypoScanLimit)
            .Select(projection);

        // The `<%` operator reads its threshold from a setting; scope the change to one transaction.
        var ownsTransaction = _db.Database.CurrentTransaction == null;
        var transaction = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var threshold = _options.TrigramThreshold.ToString(CultureInfo.InvariantCulture);
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('pg_trgm.word_similarity_threshold', {0}, true)", new object[] { threshold }, ct);
            var rows = await query.ToListAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return rows;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private static List<TRow> Rank<TEntity, TRow>(KindSpec<TEntity, TRow> spec, List<TRow> rows, string[] tokens)
        where TEntity : class
        where TRow : SearchHit
    {
        var ranked = new List<TRow>(rows.Count);
        foreach (var row in rows)
        {
            row.Score = Math.Round(SearchRanker.ScoreRecord(tokens, spec.RankFields(row)), 4);
            if (row.Score > 0) ranked.Add(row);
        }
        // Stable: equally relevant rows keep the SQL order.
        return ranked.OrderByDescending(row => row.Score).ToList();
    }

    private async Task<bool> TrigramsAvailableAsync(CancellationToken ct)
    {
        if (_options.TrigramsAvailable is bool forced) return forced;
        if (!_isNpgsql) return false;

        var connection = _db.Database.GetDbConnection();
        var key = $"{connection.DataSource}/{connection.Database}";
        if (TrigramProbes.TryGetValue(key, out var probe) && DateTime.UtcNow - probe.CheckedUtc < TrigramProbeLifetime)
        {
            return probe.Available;
        }

        bool available;
        try
        {
            available = await _db.Database
                .SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') AS \"Value\"")
                .SingleAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            available = false;
        }
        TrigramProbes[key] = (available, DateTime.UtcNow);
        return available;
    }

    // ------------------------------------------------------------------ expressions

    private static string TicketNumber(Guid id) => "TKT-" + id.ToString()[..8].ToUpperInvariant();

    private static IReadOnlyList<Expression<Func<T, string?>>> Columns<T>(params Expression<Func<T, string?>>[] columns) => columns;

    /// <summary>LIKE pattern matching <paramref name="token"/> anywhere (or at the start) of a column.</summary>
    private static string LikePattern(string token, bool prefixOnly = false)
    {
        var escaped = token.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return prefixOnly ? escaped + "%" : "%" + escaped + "%";
    }

    /// <summary>
    /// Wraps a value so EF sends it as a SQL parameter. A constant in the expression tree would be
    /// inlined into the SQL text and give every search term its own compiled query.
    /// </summary>
    private static Expression AsParameter<TValue>(TValue value)
    {
        Expression<Func<TValue>> captured = () => value;
        return captured.Body;
    }

    private static readonly Expression EfFunctions = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

    private static readonly MethodInfo WordSimilarMethod = typeof(NpgsqlTrigramsDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlTrigramsDbFunctionsExtensions.TrigramsAreWordSimilar),
        new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

    /// <summary>A token matches a row when any column contains it (or, with <paramref name="fuzzy"/>, is word-similar to it).</summary>
    private Expression<Func<TEntity, bool>> TokenPredicate<TEntity, TRow>(KindSpec<TEntity, TRow> spec, string token, bool fuzzy)
        where TEntity : class
        where TRow : class
    {
        var row = Expression.Parameter(typeof(TEntity), "row");
        var pattern = AsParameter(LikePattern(token));
        var escape = Expression.Constant("\\");

        Expression? body = null;
        foreach (var column in spec.Columns)
        {
            var like = Expression.Call(_likeMethod, EfFunctions, Rebind(column, row), pattern, escape);
            body = body == null ? like : Expression.OrElse(body, like);
        }

        if (fuzzy)
        {
            var tokenParameter = AsParameter(token);
            foreach (var column in spec.TextColumns)
            {
                body = Expression.OrElse(body!, Expression.Call(WordSimilarMethod, EfFunctions, tokenParameter, Rebind(column, row)));
            }
        }

        var extra = spec.ExtraMatch?.Invoke(token, fuzzy);
        if (extra != null) body = Expression.OrElse(body!, Rebind(extra, row));

        return Expression.Lambda<Func<TEntity, bool>>(body!, row);
    }

    /// <summary>0 when the main column starts with the first token, 1 when it contains it, 2 otherwise.</summary>
    private Expression<Func<TEntity, int>> CoarseRank<TEntity>(Expression<Func<TEntity, string?>> column, string token)
    {
        var row = Expression.Parameter(typeof(TEntity), "row");
        var value = Rebind(column, row);
        var escape = Expression.Constant("\\");
        var startsWith = Expression.Call(_likeMethod, EfFunctions, value, AsParameter(LikePattern(token, prefixOnly: true)), escape);
        var contains = Expression.Call(_likeMethod, EfFunctions, value, AsParameter(LikePattern(token)), escape);
        var body = Expression.Condition(startsWith, Expression.Constant(0),
            Expression.Condition(contains, Expression.Constant(1), Expression.Constant(2)));
        return Expression.Lambda<Func<TEntity, int>>(body, row);
    }

    /// <summary>Body of <paramref name="lambda"/> with its parameter replaced by <paramref name="parameter"/>.</summary>
    private static Expression Rebind(LambdaExpression lambda, ParameterExpression parameter) =>
        new ParameterReplacer(lambda.Parameters[0], parameter).Visit(lambda.Body);

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) => node == _from ? _to : base.VisitParameter(node);
    }

    private sealed class TicketRow : SearchHit
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? StationName { get; set; }
        public string? AssignedTo { get; set; }
        public string? Tags { get; set; }
        public string? AlarmCode { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
