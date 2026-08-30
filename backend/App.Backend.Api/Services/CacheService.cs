using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace App.Backend.Api.Services;

/// <summary>
/// Hybrid L1 (Memory) + L2 (Redis Distributed) caching service with graceful offline fallback.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer? _redisConnection;
    private readonly ILogger<CacheService> _logger;

    // Track known L1 keys for local pattern matching
    private static readonly ConcurrentDictionary<string, byte> LocalKeys = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CacheService(
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<CacheService> logger,
        IConnectionMultiplexer? redisConnection = null)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
        _redisConnection = redisConnection;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        if (string.IsNullOrEmpty(key)) return default;

        // 1. Try L1 Memory Cache
        if (_memoryCache.TryGetValue(key, out var cachedVal))
        {
            if (cachedVal is T typedVal) return typedVal;
            if (cachedVal is string strVal)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(strVal, JsonOptions);
                }
                catch
                {
                    // Ignore deserialization error
                }
            }
        }

        // 2. Try L2 Distributed Cache (Redis)
        try
        {
            var data = await _distributedCache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(data))
            {
                var deserialized = JsonSerializer.Deserialize<T>(data, JsonOptions);
                if (deserialized != null)
                {
                    // Populate L1 cache with short sliding window
                    _memoryCache.Set(key, deserialized, TimeSpan.FromMinutes(1));
                    LocalKeys.TryAdd(key, 0);
                    return deserialized;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis L2 Cache unavailable for key '{Key}': {Message}. Continuing with L1 fallback.", key, ex.Message);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        if (string.IsNullOrEmpty(key) || value == null) return;

        var absExp = absoluteExpiration ?? TimeSpan.FromMinutes(15);

        // 1. Set L1 Memory Cache
        var memOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absExp,
            SlidingExpiration = slidingExpiration
        };
        _memoryCache.Set(key, value, memOptions);
        LocalKeys.TryAdd(key, 0);

        // 2. Set L2 Distributed Cache (Redis)
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var distOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absExp,
                SlidingExpiration = slidingExpiration
            };
            await _distributedCache.SetStringAsync(key, json, distOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis L2 Cache unavailable to store key '{Key}': {Message}. Retained in L1.", key, ex.Message);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        var existing = await GetAsync<T>(key);
        if (existing != null && !EqualityComparer<T>.Default.Equals(existing, default))
        {
            return existing;
        }

        var result = await factory();
        if (result != null)
        {
            await SetAsync(key, result, expiration);
        }
        return result;
    }

    public async Task RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        _memoryCache.Remove(key);
        LocalKeys.TryRemove(key, out _);

        try
        {
            await _distributedCache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis L2 Cache unavailable to remove key '{Key}': {Message}", key, ex.Message);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return;

        // Clean from L1 local keys
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var k in LocalKeys.Keys)
        {
            if (regex.IsMatch(k))
            {
                _memoryCache.Remove(k);
                LocalKeys.TryRemove(k, out _);
            }
        }

        // Clean from Redis L2 using StackExchange.Redis if connected
        if (_redisConnection != null && _redisConnection.IsConnected)
        {
            try
            {
                var endpoints = _redisConnection.GetEndPoints();
                foreach (var ep in endpoints)
                {
                    var server = _redisConnection.GetServer(ep);
                    if (!server.IsReplica)
                    {
                        var redisPattern = $"heimdall:{pattern}";
                        var keys = server.Keys(pattern: redisPattern).ToArray();
                        if (keys.Length > 0)
                        {
                            var db = _redisConnection.GetDatabase();
                            await db.KeyDeleteAsync(keys);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to purge pattern '{Pattern}' from Redis: {Message}", pattern, ex.Message);
            }
        }
    }
}
