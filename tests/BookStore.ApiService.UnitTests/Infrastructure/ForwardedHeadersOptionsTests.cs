using BookStore.ApiService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class ForwardedHeadersOptionsTests
{
    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_ShouldConfigureSecureForwardedHeadersDefaults()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Authentication:Passkey:AllowedOrigins:0"] = "https://localhost:7260"
        });

        _ = builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);
        await using var provider = builder.Services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // Assert
        _ = await Assert.That(options.ForwardedHeaders)
            .IsEqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        _ = await Assert.That(options.ForwardLimit).IsEqualTo(1);
        _ = await Assert.That(options.RequireHeaderSymmetry).IsTrue();
        _ = await Assert.That(options.KnownIPNetworks.Count + options.KnownProxies.Count).IsGreaterThan(0);
    }
}
