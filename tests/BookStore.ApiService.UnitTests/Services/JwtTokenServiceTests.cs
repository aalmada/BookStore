using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using BookStore.ApiService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

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
        var configuration = CreateMockConfiguration(expirationMinutes: 45);
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

        var expectedExpiration = DateTimeOffset.UtcNow.AddMinutes(45).UtcDateTime;
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
    public async Task GenerateAccessToken_WithHs256SecretShorterThan32Bytes_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var configuration = CreateMockConfiguration(secretKey: "short-hs256-secret");
        var service = new JwtTokenService(configuration);
        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act + Assert
        _ = await Assert.That(() => service.GenerateAccessToken(claims))
            .Throws<InvalidOperationException>()
            .WithMessage("JWT HS256 SecretKey must be at least 32 bytes when UTF-8 encoded");
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithRs256Algorithm_ShouldGenerateTokenWithRs256Header()
    {
        // Arrange
        var (privateKeyPem, publicKeyPem) = CreateRsaPemKeyPair();
        var configuration = CreateMockConfiguration(
            algorithm: "RS256",
            secretKey: string.Empty,
            rs256PrivateKeyPem: privateKeyPem,
            rs256PublicKeyPem: publicKeyPem);
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
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        _ = await Assert.That(jwtToken.Header.Alg).IsEqualTo(SecurityAlgorithms.RsaSha256);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithAlgorithmOmittedAndRs256KeysPresent_ShouldGenerateTokenWithRs256Header()
    {
        // Arrange
        var (privateKeyPem, publicKeyPem) = CreateRsaPemKeyPair();
        var configuration = CreateMockConfiguration(
            algorithm: null,
            secretKey: "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
            rs256PrivateKeyPem: privateKeyPem,
            rs256PublicKeyPem: publicKeyPem);
        var service = new JwtTokenService(configuration);
        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        _ = await Assert.That(jwtToken.Header.Alg).IsEqualTo(SecurityAlgorithms.RsaSha256);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithAlgorithmOmittedAndRs256KeysAbsent_ShouldGenerateTokenWithHs256Header()
    {
        // Arrange
        var configuration = CreateMockConfiguration(
            algorithm: null,
            secretKey: "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
            rs256PrivateKeyPem: null,
            rs256PublicKeyPem: null);
        var service = new JwtTokenService(configuration);
        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        _ = await Assert.That(jwtToken.Header.Alg).IsEqualTo(SecurityAlgorithms.HmacSha256);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithExplicitHs256AndRs256KeysPresent_ShouldUseExplicitHs256()
    {
        // Arrange
        var (privateKeyPem, publicKeyPem) = CreateRsaPemKeyPair();
        var configuration = CreateMockConfiguration(
            algorithm: "HS256",
            secretKey: "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
            rs256PrivateKeyPem: privateKeyPem,
            rs256PublicKeyPem: publicKeyPem);
        var service = new JwtTokenService(configuration);
        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act
        var token = service.GenerateAccessToken(claims);

        // Assert
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        _ = await Assert.That(jwtToken.Header.Alg).IsEqualTo(SecurityAlgorithms.HmacSha256);
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithRs256MissingPrivateKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var configuration = CreateMockConfiguration(
            algorithm: "RS256",
            secretKey: string.Empty,
            rs256PrivateKeyPem: string.Empty,
            rs256PublicKeyPem: "-----BEGIN PUBLIC KEY-----test-----END PUBLIC KEY-----");
        var service = new JwtTokenService(configuration);
        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };

        // Act + Assert
        _ = await Assert.That(() => service.GenerateAccessToken(claims))
            .Throws<InvalidOperationException>()
            .WithMessage("JWT RS256:PrivateKeyPem not configured");
    }

    [Test]
    [Category("Unit")]
    public async Task GenerateAccessToken_WithMissingExpirationMinutes_ShouldDefaultTo15Minutes()
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

        var expectedExpiration = DateTimeOffset.UtcNow.AddMinutes(15).UtcDateTime;
        var tolerance = TimeSpan.FromMinutes(1);

        _ = await Assert.That(jwtToken.ValidTo).IsGreaterThan(expectedExpiration - tolerance);
        _ = await Assert.That(jwtToken.ValidTo).IsLessThan(expectedExpiration + tolerance);
    }

    [Test]
    [Category("Unit")]
    [Arguments(15, 900)]
    [Arguments(90, 5400)]
    [Arguments(240, 14400)]
    public async Task GetAccessTokenExpiresInSeconds_WithConfiguredValue_ShouldReturnMatchingSeconds(int expirationMinutes, int expectedSeconds)
    {
        // Arrange
        var configuration = CreateMockConfiguration(expirationMinutes: expirationMinutes);
        var service = new JwtTokenService(configuration);

        // Act
        var expiresIn = service.GetAccessTokenExpiresInSeconds();

        // Assert
        _ = await Assert.That(expiresIn).IsEqualTo(expectedSeconds);
    }

    [Test]
    [Category("Unit")]
    public async Task GetAccessTokenExpiresInSeconds_WithInvalidConfiguredValue_ShouldDefaultTo900()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:ExpirationMinutes"] = "invalid"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var logger = Substitute.For<ILogger<JwtTokenService>>();
        _ = logger.IsEnabled(LogLevel.Warning).Returns(true);

        var service = new JwtTokenService(configuration, logger);

        // Act
        var expiresIn = service.GetAccessTokenExpiresInSeconds();

        // Assert
        _ = await Assert.That(expiresIn).IsEqualTo(900);

        var warningCalls = logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => call.GetArguments())
            .Where(args => args.Length >= 3 && args[0] is LogLevel level && level == LogLevel.Warning)
            .ToList();

        _ = await Assert.That(warningCalls.Count).IsEqualTo(1);

        var eventId = (EventId)warningCalls[0][1]!;
        _ = await Assert.That(eventId.Name).IsEqualTo("InvalidJwtExpirationMinutes");
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

    #region Refresh Token Hashing Tests

    [Test]
    [Category("Unit")]
    public async Task HashRefreshToken_ShouldBeDeterministicAndNotEqualToRawToken()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);
        var rawToken = service.GenerateRefreshToken();

        // Act
        var firstHash = service.HashRefreshToken(rawToken);
        var secondHash = service.HashRefreshToken(rawToken);

        // Assert
        _ = await Assert.That(firstHash).IsEqualTo(secondHash);
        _ = await Assert.That(firstHash).IsNotEqualTo(rawToken);
    }

    [Test]
    [Category("Unit")]
    public async Task RotateRefreshToken_ShouldPersistOnlyTokenHash()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Email = "test@example.com",
            UserName = "test@example.com",
            SecurityStamp = Guid.CreateVersion7().ToString()
        };

        // Act
        var rawToken = service.RotateRefreshToken(user, "default");

        // Assert
        _ = await Assert.That(user.RefreshTokens.Count).IsEqualTo(1);
        var stored = user.RefreshTokens.Single();
        _ = await Assert.That(stored.Token).IsNotEqualTo(rawToken);
        _ = await Assert.That(stored.Token).IsEqualTo(service.HashRefreshToken(rawToken));
    }

    [Test]
    [Category("Unit")]
    public async Task RotateRefreshToken_WithOldRawToken_ShouldMarkPreviousHashAsUsed()
    {
        // Arrange
        var configuration = CreateMockConfiguration();
        var service = new JwtTokenService(configuration);

        var user = new BookStore.ApiService.Models.ApplicationUser
        {
            Email = "test@example.com",
            UserName = "test@example.com",
            SecurityStamp = Guid.CreateVersion7().ToString()
        };

        var firstRawToken = service.RotateRefreshToken(user, "default");

        // Act
        var secondRawToken = service.RotateRefreshToken(user, "default", firstRawToken);

        // Assert
        var firstHash = service.HashRefreshToken(firstRawToken);
        var firstStored = user.RefreshTokens.First(t => t.Token == firstHash);
        _ = await Assert.That(firstStored.IsUsed).IsTrue();

        var secondHash = service.HashRefreshToken(secondRawToken);
        _ = await Assert.That(user.RefreshTokens.Any(t => t.Token == secondHash)).IsTrue();
    }

    #endregion

    #region Helper Methods

    static IConfiguration CreateMockConfiguration(
        string? algorithm = "HS256",
        string secretKey = "super-secret-key-that-is-long-enough-for-hmacsha256-algorithm",
        string issuer = "test-issuer",
        string audience = "test-audience",
        int expirationMinutes = 15,
        string? rs256PrivateKeyPem = null,
        string? rs256PublicKeyPem = null)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = secretKey,
            ["Jwt:Issuer"] = issuer,
            ["Jwt:Audience"] = audience,
            ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString(),
            ["Jwt:RS256:PrivateKeyPem"] = rs256PrivateKeyPem,
            ["Jwt:RS256:PublicKeyPem"] = rs256PublicKeyPem
        };

        if (!string.IsNullOrWhiteSpace(algorithm))
        {
            configDict["Jwt:Algorithm"] = algorithm;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    static (string privateKeyPem, string publicKeyPem) CreateRsaPemKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }

    #endregion
}
