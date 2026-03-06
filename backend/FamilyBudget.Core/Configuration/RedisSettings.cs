namespace FamilyBudget.Core.Configuration;

/// <summary>
/// Configuration settings for Redis cache
/// </summary>
public class RedisSettings
{
    /// <summary>
    /// Redis server connection string (e.g., "localhost:6379")
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";
    
    /// <summary>
    /// Prefix for all cache keys (e.g., "FamilyBudget:")
    /// Helps organize and identify your app's data in Redis
    /// </summary>
    public string InstanceName { get; set; } = "FamilyBudget:";
    
    /// <summary>
    /// Default cache expiration time in minutes
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 60;
    
    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;
    
    /// <summary>
    /// Whether to abort connection on connect failure
    /// Set to false for more resilient behavior
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;
}