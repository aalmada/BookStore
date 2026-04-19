using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class SecurityHeadersMiddlewareTests
{
    [Test]
    [Category("Unit")]
    public async Task HstsValue_ShouldIncludePreloadAndSubDomainsAndMaxAge()
    {
        var value = SecurityHeadersMiddleware.HstsValue;
        _ = await Assert.That(value).Contains("preload");
        _ = await Assert.That(value).Contains("includeSubDomains");
        _ = await Assert.That(value).Contains("max-age=31536000");
    }
}
