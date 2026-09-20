namespace App.Contracts.Inventory;

using System;
using System.Collections.Generic;
using System.Text.Json;

public class InventoryFilterRequest
{
    public string? Query { get; set; }
    public string? Type { get; set; } = "all";
    public string? Classification { get; set; } = "all";
    public string? EquipmentStatus { get; set; }
    public string? Technology { get; set; }
    public string? StorageLocation { get; set; }
    public string? ManufacturerId { get; set; }
    public string? TeamId { get; set; }
    public string? SortBy { get; set; } = "name";
    public string? SortOrder { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class InventoryFilterResultDto
{
    public List<InventoryItemSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public double TotalCostHuf { get; set; }
    public InventoryKpisDto Kpis { get; set; } = new();
}

public class InventoryKpisDto
{
    public int TotalGlobalCount { get; set; }
    public int TotalGlobalHardware { get; set; }
    public int TotalGlobalSoftware { get; set; }
    public int TotalGlobalParts { get; set; }
    public int TotalGlobalStock { get; set; }
    public double TotalGlobalCost { get; set; }
    public int InStorage { get; set; }
    public int InMachine { get; set; }
    public int UnderRepair { get; set; }
    public int Decommissioned { get; set; }
    public int LowStockCount { get; set; }
}

public class InventoryItemSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? SerialNumber { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? CustomIdentifier { get; set; }
    public double? CostInHUF { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public InventoryRelationSummaryDto? Manufacturer { get; set; }
    public List<InventoryRelationSummaryDto> ResponsibleTeams { get; set; } = new();
    public bool IsStockItem { get; set; }
    public string? EquipmentStatus { get; set; }
    public string? StorageLocation { get; set; }
    public int? StockQuantity { get; set; }
    public int? MinStockThreshold { get; set; }
    public string? Technology { get; set; }
    public JsonDocument? Metadata { get; set; }
    public Guid? MachineId { get; set; }
}

public class InventoryRelationSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
