using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class SecurityStampCacheTests
{
    [Test]
    [Category("Unit")]
    public async Task GetCacheKey_ShouldIncludeTenantAndUserId()
    {
        // Arrange
        var tenantId = "tenant-a";
        var userId = Guid.CreateVersion7();

        // Act
        var key = SecurityStampCache.GetCacheKey(tenantId, userId);

        // Assert
        _ = await Assert.That(key).IsEqualTo($"auth:security-stamp:{tenantId}:{userId:D}");
    }

    [Test]
    [Category("Unit")]
    public async Task GetCacheTag_ShouldMatchCacheTagsConvention()
    {
        // Arrange
        var tenantId = "tenant-a";
        var userId = Guid.CreateVersion7();

        // Act
        var tag = SecurityStampCache.GetCacheTag(tenantId, userId);

        // Assert
        _ = await Assert.That(tag).IsEqualTo(CacheTags.ForSecurityStamp(tenantId, userId));
    }

    [Test]
    [Category("Unit")]
    public async Task CreateEntryOptions_ShouldUseShortExpirationWindows()
    {
        // Act
        var options = SecurityStampCache.CreateEntryOptions();

        // Assert
        _ = await Assert.That(options.Expiration).IsEqualTo(TimeSpan.FromSeconds(30));
        _ = await Assert.That(options.LocalCacheExpiration).IsEqualTo(TimeSpan.FromSeconds(15));
    }
}
