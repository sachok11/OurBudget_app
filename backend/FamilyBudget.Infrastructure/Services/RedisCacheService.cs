using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FamilyBudget.Core.Configuration;

namespace FamilyBudget.Infrastructure.Services;

/// <summary>
/// Interface for cache operations
/// This allows us to swap implementations (e.g., for testing)
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key
    /// </summary>
    /// <typeparam name="T">Type of the cached object</typeparam>
    /// <param name="key">Cache key</param>
    /// <returns>Cached value or default if not found</returns>
    Task<T?> GetAsync<T>(string key);
    
    /// <summary>
    /// Set a value in cache
    /// </summary>
    /// <typeparam name="T">Type of the object to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiry">Optional expiration time (uses default if null)</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    
    /// <summary>
    /// Remove a value from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Check if a key exists in cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>True if key exists, false otherwise</returns>
    Task<bool> ExistsAsync(string key);
    
    /// <summary>
    /// Remove all keys matching a pattern
    /// Useful for invalidating related cache entries
    /// </summary>
    /// <param name="pattern">Pattern to match (e.g., "family:123:*")</param>
    Task RemoveByPatternAsync(string pattern);
    
    /// <summary>
    /// Get multiple values at once (more efficient than individual gets)
    /// </summary>
    Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys);
    
    /// <summary>
    /// Set multiple values at once (more efficient than individual sets)
    /// </summary>
    Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null);
}

/// <summary>
/// Redis implementation of cache service
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisSettings> settings,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _settings = settings.Value;
        _logger = logger;
        
        // Get the database instance (database 0 by default)
        _db = redis.GetDatabase();
        
        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false, // Compact JSON for smaller cache size
        };
    }

    /// <summary>
    /// Add instance name prefix to keys
    /// This helps organize data and prevents key collisions
    /// Example: "family:123:summary" becomes "FamilyBudget:family:123:summary"
    /// </summary>
    private string GetFullKey(string key)
    {
        return $"{_settings.InstanceName}{key}";
    }

    // ============================================
    // GET: Retrieve from Cache
    // ============================================
    
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            // Add prefix to key
            var fullKey = GetFullKey(key);
            
            // Try to get value from Redis
            var value = await _db.StringGetAsync(fullKey);
            
            // If value doesn't exist, return default
            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache MISS for key: {Key}", key);
                return default;
            }
            
            _logger.LogDebug("Cache HIT for key: {Key}", key);
            
            // Deserialize JSON string back to object
            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return default;
        }
    }

    // ============================================
    // SET: Store in Cache
    // ============================================
    
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var fullKey = GetFullKey(key);
            
            // Serialize object to JSON string
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);
            
            // Use provided expiry or default from settings
            var expiryTime = expiry ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
            
            // Store in Redis with expiration
            await _db.StringSetAsync(fullKey, serialized, expiryTime);
            
            _logger.LogDebug("Cache SET for key: {Key} with expiry: {Expiry}", 
                key, expiryTime);
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
        }
    }

    // ============================================
    // REMOVE: Delete from Cache
    // ============================================
    
    public async Task RemoveAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            await _db.KeyDeleteAsync(fullKey);
            
            _logger.LogDebug("Cache REMOVE for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from cache for key: {Key}", key);
        }
    }

    // ============================================
    // EXISTS: Check if Key Exists
    // ============================================
    
    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return await _db.KeyExistsAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists: {Key}", key);
            return false;
        }
    }

    // ============================================
    // REMOVE BY PATTERN: Bulk Delete
    // ============================================
    
    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            var fullPattern = GetFullKey(pattern);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            // Find all keys matching pattern
            var keys = server.Keys(pattern: fullPattern).ToArray();
            
            if (keys.Length > 0)
            {
                await _db.KeyDeleteAsync(keys);
                _logger.LogDebug("Cache REMOVE {Count} keys matching pattern: {Pattern}", 
                    keys.Length, pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing keys by pattern: {Pattern}", pattern);
        }
    }

    // ============================================
    // GET MANY: Batch Retrieve
    // ============================================
    
    public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys)
    {
        var result = new Dictionary<string, T?>();
        
        try
        {
            var fullKeys = keys.Select(k => (RedisKey)GetFullKey(k)).ToArray();
            var values = await _db.StringGetAsync(fullKeys);
            
            var keyArray = keys.ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].IsNullOrEmpty)
                {
                    var deserialized = JsonSerializer.Deserialize<T>(values[i]!, _jsonOptions);
                    result[keyArray[i]] = deserialized;
                }
                else
                {
                    result[keyArray[i]] = default;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple values from cache");
        }
        
        return result;
    }

    // ============================================
    // SET MANY: Batch Store
    // ============================================
    
    public async Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null)
    {
        try
        {
            var expiryTime = expiry ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
            var batch = _db.CreateBatch();
            
            var tasks = items.Select(item =>
            {
                var fullKey = GetFullKey(item.Key);
                var serialized = JsonSerializer.Serialize(item.Value, _jsonOptions);
                return batch.StringSetAsync(fullKey, serialized, expiryTime);
            });
            
            batch.Execute();
            await Task.WhenAll(tasks);
            
            _logger.LogDebug("Cache SET MANY {Count} items", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting multiple values in cache");
        }
    }

    
}