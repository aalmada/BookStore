using BookStore.ApiService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Infrastructure.Extensions;

public class ApplicationServicesExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_ShouldConfigureJwtClockSkewToThirtySeconds()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.ClockSkew).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BookStore.ApiService.UnitTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
