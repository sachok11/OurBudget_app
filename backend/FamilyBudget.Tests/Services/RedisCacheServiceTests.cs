using Xunit;
using Moq;
using StackExchange.Redis;
using FamilyBudget.Infrastructure.Services;

public class RedisCacheServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();
        
        mockDb.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);
        
        var service = new RedisCacheService(mockRedis.Object /* ... */);
        
        // Act
        var result = await service.GetAsync<string>("nonexistent-key");
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_StoresValue_WithExpiration()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();
        
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);
        
        var service = new RedisCacheService(mockRedis.Object /* ... */);
        
        // Act
        await service.SetAsync("test-key", "test-value", TimeSpan.FromMinutes(5));
        
        // Assert
        mockDb.Verify(x => x.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            TimeSpan.FromMinutes(5),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}