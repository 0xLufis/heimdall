namespace App.Backend.Api.Dtos;

/// <summary>
/// Result of <c>GET /api/v1/search</c>. One list per entity kind, each ordered by relevance.
/// Property names follow the list endpoints of each entity so clients can reuse their mappers.
/// </summary>
public sealed class GlobalSearchResponse
{
    /// <summary>The query as it was searched (trimmed, length-capped).</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Kinds that were searched. A requested kind the caller may not search is left out.</summary>
    public List<string> Kinds { get; set; } = new();

    /// <summary>
    /// How typo matching was applied to the kinds that needed it: <c>full</c> (every row was
    /// considered), <c>trigram</c> (pg_trgm narrowed a large table first) or <c>partial</c>
    /// (a large table without pg_trgm: only the first rows were checked for typos).
    /// </summary>
    public string TypoTolerance { get; set; } = "full";

    public double TookMs { get; set; }

    public List<StationSearchHit> Stations { get; set; } = new();
    public List<ControllerSearchHit> Controllers { get; set; } = new();
    public List<TicketSearchHit> Tickets { get; set; } = new();
    public List<UserSearchHit> Users { get; set; } = new();
    public List<InventorySearchHit> Inventory { get; set; } = new();
}

public abstract class SearchHit
{
    /// <summary>Relevance in (0, 1]: exact &gt; prefix &gt; whole word &gt; word prefix &gt; substring &gt; typo.</summary>
    public double Score { get; set; }
}

public sealed class StationSearchHit : SearchHit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? CustomIdentifier { get; set; }
    public string? PinnedObjectHandle { get; set; }
    public string? MachineType { get; set; }
    public string? GroupId { get; set; }
    public string? Technology { get; set; }
    public string? Status { get; set; }
}

public sealed class ControllerSearchHit : SearchHit
{
    public Guid Id { get; set; }
    public string? Hostname { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? MachineIdentifier { get; set; }
    public DateTimeOffset? LastOnline { get; set; }
}

public sealed class TicketSearchHit : SearchHit
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? StationName { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserSearchHit : SearchHit
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Role { get; set; }
}

public sealed class InventorySearchHit : SearchHit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? ManufacturerName { get; set; }
    public string? StorageLocation { get; set; }
    public string? EquipmentStatus { get; set; }
    public bool IsStockItem { get; set; }
    public int? StockQuantity { get; set; }
}
