namespace App.Backend.Api.Services;

/// <summary>
/// Defines a resilient multi-tier distributed caching interface.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached item by key, checking L1 Memory cache first then L2 Distributed cache.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Stores an item into both L1 Memory and L2 Distributed cache.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

    /// <summary>
    /// Gets a cached item, or generates it using the factory function and caches the result.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    /// <summary>
    /// Removes an item from all cache tiers.
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes all keys matching a wildcard pattern (e.g. "inventory:*", "tickets:*").
    /// </summary>
    Task RemoveByPatternAsync(string pattern);
}
