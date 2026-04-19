using System.Security.Cryptography;
using BookStore.ApiService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithRs256Algorithm_ShouldConfigureRsaValidationKey()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "RS256",
                ["Jwt:SecretKey"] = string.Empty,
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RS256:PrivateKeyPem"] = privateKeyPem,
                ["Jwt:RS256:PublicKeyPem"] = publicKeyPem,
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
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<RsaSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256SecretShorterThan32Bytes_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "too-short-secret",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act + Assert
        _ = await Assert.That(() => services.AddApplicationServices(configuration, environment))
            .Throws<InvalidOperationException>()
            .WithMessage("HS256 requires Jwt:SecretKey to be at least 32 bytes when UTF-8 encoded.");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256SecretExactly32Utf8Bytes_ShouldConfigureSymmetricValidationKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var thirtyTwoByteSecret = new string('a', 32);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = thirtyTwoByteSecret,
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
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<SymmetricSecurityKey>();
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
