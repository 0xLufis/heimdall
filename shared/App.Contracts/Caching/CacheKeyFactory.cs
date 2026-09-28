namespace App.Contracts.Caching;

using System;

/// <summary>
/// Strongly-typed cache key factory for Redis and in-memory caching across Heimdall.
/// Eliminates raw magic strings and prevents cache key collision bugs.
/// </summary>
public static class CacheKeyFactory
{
    public static class Tickets
    {
        public const string AllPrefix = "tickets:all";
        public const string ItemPrefix = "tickets:item";

        public static string All(string? status = null) =>
            $"{AllPrefix}:{status?.ToLowerInvariant() ?? "all"}";

        public static string Item(Guid id) =>
            $"{ItemPrefix}:{id:D}";
    }

    public static class Dashboard
    {
        public const string Metrics = "dashboard:metrics";
    }

    public static class Inventory
    {
        public const string Tree = "inventory:tree";
        public const string Machines = "inventory:machines";
        public const string Teams = "inventory:teams";
        public const string Manufacturers = "inventory:manufacturers";
        public const string Suppliers = "inventory:suppliers";
        public const string ClientPcs = "inventory:client_pcs";
        public const string MetadataKeys = "inventory:metadata_keys";
        public const string Parts = "inventory:parts";
        public const string Stock = "inventory:stock";
        public const string AllPattern = "inventory:*";
    }
}
