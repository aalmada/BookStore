using BookStore.ApiService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Infrastructure.Extensions;

public class RateLimitingExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_WithDisabledFlagInDevelopment_ShouldNotThrow()
    {
        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Development");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Disabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert – must not throw
        _ = await Assert.That(() => _ = services.AddCustomRateLimiting(configuration, environment)).ThrowsNothing();
    }

    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_WithDisabledFlagInProduction_ShouldNotThrow_ButLogs()
    {
        // The guard logs a Critical warning rather than throwing, so the call still succeeds.
        // This test verifies the method completes without exception in production.

        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Production");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Disabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert – must not throw (log-only guard)
        _ = await Assert.That(() => _ = services.AddCustomRateLimiting(configuration, environment)).ThrowsNothing();
    }

    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_WithDisabledFlagInTestEnvironment_ShouldNotThrow()
    {
        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Testing");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Disabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        _ = await Assert.That(() => _ = services.AddCustomRateLimiting(configuration, environment)).ThrowsNothing();
    }
}
