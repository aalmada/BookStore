using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookStore.Web.Tests.Infrastructure;

public class ForwardedHeadersExtensionsTests
{
    [Test]
    public async Task ConfigureSecureForwardedHeaders_ShouldApplySecureDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.ConfigureSecureForwardedHeaders();
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // Assert
        _ = await Assert.That(options.ForwardedHeaders)
            .IsEqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        _ = await Assert.That(options.ForwardLimit).IsEqualTo(1);
        _ = await Assert.That(options.RequireHeaderSymmetry).IsTrue();
        _ = await Assert.That(options.KnownIPNetworks.Count + options.KnownProxies.Count).IsGreaterThan(0);
    }
}
