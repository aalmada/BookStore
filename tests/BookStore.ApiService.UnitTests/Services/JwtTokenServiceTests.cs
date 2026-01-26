using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookStore.ApiService.Services;
using Microsoft.Extensions.Configuration;

namespace BookStore.ApiService.UnitTests.Services;

public class JwtTokenServiceTests
{
    #region GenerateAccessToken (ApplicationUser) Tests

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithApplicationUser_ShouldGenerateValidToken()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "testuser",
            Email = "test@example.com",
            Roles = ["User"]
        };

        // Act
        var token = service.GenerateAccessToken(user, "default-tenant", user.Roles);

        // Assert
        _ = await Assert.That(token).IsNotNull();
        _ = await Assert.That(token).IsNotEmpty();

        // Verify it's a valid JWT
        var handler = new JwtSecurityTokenHandler();
        _ = await Assert.That(handler.CanReadToken(token)).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithApplicationUser_ShouldIncludeCorrectClaims()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var userId = Guid.CreateVersion7();
        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            Roles = ["User", "Admin"]
        };

        // Act
        var token = service.GenerateAccessToken(user, "default-tenant", user.Roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Check standard claims
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId.ToString())).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == ClaimTypes.Name && c.Value == "testuser")).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com")).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString())).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com")).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Jti)).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithApplicationUser_ShouldIncludeRoleClaims()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "adminuser",
            Email = "admin@example.com",
            Roles = ["User", "Admin", "SuperAdmin"]
        };

        // Act
        var token = service.GenerateAccessToken(user, "default-tenant", user.Roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        _ = await Assert.That(roleClaims).Contains("User");
        _ = await Assert.That(roleClaims).Contains("Admin");
        _ = await Assert.That(roleClaims).Contains("SuperAdmin");
        _ = await Assert.That(roleClaims.Count).IsEqualTo(3);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithApplicationUser_ShouldNotBeExpiredImmediately()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "testuser",
            Email = "test@example.com",
            Roles = []
        };

        // Act
        var token = service.GenerateAccessToken(user, "default-tenant", user.Roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        _ = await Assert.That(jwtToken.ValidTo).IsGreaterThan(DateTimeOffset.UtcNow.UtcDateTime);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithApplicationUser_ShouldExpireAfterConfiguredTime()
    {
        // Arrange
        var configuration = CreateMockConfiguration(expirationMinutes: 30);
        var service = new JwtTokenService(configuration);

        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "testuser",
            Email = "test@example.com",
            Roles = []
        };

        // Act
        var token = service.GenerateAccessToken(user, "default-tenant", user.Roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpiration = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime;
        var tolerance = TimeSpan.FromMinutes(1); // Allow 1 minute tolerance

        _ = await Assert.That(jwtToken.ValidTo).IsGreaterThan(expectedExpiration - tolerance);
        _ = await Assert.That(jwtToken.ValidTo).IsLessThan(expectedExpiration + tolerance);
    }

    #endregion

    #region GenerateAccessToken (Claims collection) Tests

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithClaims_ShouldGenerateValidToken()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()),
            new(ClaimTypes.Name, "customuser"),
            new("custom-claim", "custom-value")
        };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        _ = await Assert.That(token).IsNotNull();
        _ = await Assert.That(token).IsNotEmpty();

        var handler = new JwtSecurityTokenHandler();
        _ = await Assert.That(handler.CanReadToken(token)).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithClaims_ShouldIncludeProvidedClaims()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-123"),
            new("custom-claim", "custom-value"),
            new(ClaimTypes.Role, "CustomRole")
        };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user-123")).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == "custom-claim" && c.Value == "custom-value")).IsTrue();
        _ = await Assert.That(jwtToken.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "CustomRole")).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithClaims_ShouldUseConfiguredIssuerAndAudience()
    {
        // Arrange
        var configuration = CreateMockConfiguration(issuer: "test-issuer", audience: "test-audience");
        var service = new JwtTokenService(configuration);

        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        _ = await Assert.That(jwtToken.Issuer).IsEqualTo("test-issuer");
        _ = await Assert.That(jwtToken.Audiences).Contains("test-audience");
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithMissingSecretKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:ExpirationMinutes"] = "60"
            // Missing SecretKey
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var service = new JwtTokenService(configuration);

        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act & Assert
        _ = await Assert.That(() => service.GenerateAccessToken(claims))
            .Throws<InvalidOperationException>()
            .WithMessage("JWT SecretKey not configured");
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithMissingExpirationMinutes_ShouldDefaultTo60Minutes()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience"
            // Missing ExpirationMinutes
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var service = new JwtTokenService(configuration);

        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpiration = DateTimeOffset.UtcNow.AddMinutes(60).UtcDateTime;
        var tolerance = TimeSpan.FromMinutes(1);

        _ = await Assert.That(jwtToken.ValidTo).IsGreaterThan(expectedExpiration - tolerance);
        _ = await Assert.That(jwtToken.ValidTo).IsLessThan(expectedExpiration + tolerance);
    }

    #endregion

    #region GenerateRefreshToken Tests

    [Test]
    [Category("Unit")]
    public async Task GenerateRefreshToken_ShouldGenerateBase64String()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        // Act
        var refreshToken = service.GenerateRefreshToken();

        // Assert
        _ = await Assert.That(refreshToken).IsNotNull();
        _ = await Assert.That(refreshToken).IsNotEmpty();

        // Verify it's valid base64 by converting it
        var bytes = Convert.FromBase64String(refreshToken);
        _ = await Assert.That(bytes).IsNotNull();
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateRefreshToken_ShouldGenerate64ByteToken()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        // Act
        var refreshToken = service.GenerateRefreshToken();
        var bytes = Convert.FromBase64String(refreshToken);

        // Assert
        _ = await Assert.That(bytes.Length).IsEqualTo(64);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateRefreshToken_ShouldGenerateUniqueTokens()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        // Act
        var token1 = service.GenerateRefreshToken();
        var token2 = service.GenerateRefreshToken();
        var token3 = service.GenerateRefreshToken();

        // Assert
        _ = await Assert.That(token1).IsNotEqualTo(token2);
        _ = await Assert.That(token1).IsNotEqualTo(token3);
        _ = await Assert.That(token2).IsNotEqualTo(token3);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateRefreshToken_MultipleCalls_ShouldAllBeCryptographicallySecure()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        // Act - Generate multiple tokens
        var tokens = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            _ = tokens.Add(service.GenerateRefreshToken());
        }

        // Assert - All tokens should be unique (no duplicates)
        _ = await Assert.That(tokens.Count).IsEqualTo(100);

        // All tokens should be properly base64 encoded
        foreach (var token in tokens)
        {
            var bytes = Convert.FromBase64String(token);
            _ = await Assert.That(bytes.Length).IsEqualTo(64);
        }
    }

    #endregion

    #region Helper Methods

    static IConfiguration CreateMockConfiguration(
        string secretKey = "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
        string issuer = "test-issuer",
        string audience = "test-audience",
        int expirationMinutes = 60)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = secretKey,
            ["Jwt:Issuer"] = issuer,
            ["Jwt:Audience"] = audience,
            ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    #endregion
}
