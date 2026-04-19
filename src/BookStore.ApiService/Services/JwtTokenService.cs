using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.ApiService.Services;

/// <summary>
/// Service for generating and validating JWT tokens
/// </summary>
public class JwtTokenService
{
    const int DefaultAccessTokenExpirationMinutes = 15;
    const int MinimumHs256SecretKeyBytes = 32;
    readonly IConfiguration _configuration;
    readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration)
        : this(configuration, NullLogger<JwtTokenService>.Instance)
    {
    }

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Builds the standard set of claims for a user token
    /// </summary>
    public List<Claim> BuildUserClaims(BookStore.ApiService.Models.ApplicationUser user, string tenantId, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new("tenant_id", tenantId),
            new("security_stamp", user.SecurityStamp ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : role));
        }

        return claims;
    }

    /// <summary>
    /// Generate a JWT access token for the given user with tenant context.
    /// </summary>
    public string GenerateAccessToken(BookStore.ApiService.Models.ApplicationUser user, string tenantId, IEnumerable<string> roles)
    {
        var claims = BuildUserClaims(user, tenantId, roles);
        return GenerateAccessToken(claims);
    }

    /// <summary>
    /// Generate a JWT access token with the provided claims
    /// </summary>
    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var credentials = CreateSigningCredentials(jwtSettings);

        var expirationMinutes = GetAccessTokenExpirationMinutes();

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).UtcDateTime,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    static SigningCredentials CreateSigningCredentials(IConfigurationSection jwtSettings)
    {
        var algorithm = JwtAlgorithmResolver.Resolve(jwtSettings);

        return algorithm switch
        {
            JwtAlgorithmResolver.JwtAlgorithmHs256 => CreateHs256SigningCredentials(jwtSettings),
            JwtAlgorithmResolver.JwtAlgorithmRs256 => CreateRs256SigningCredentials(jwtSettings),
            _ => throw new InvalidOperationException($"Unsupported JWT algorithm: {algorithm}")
        };
    }

    static SigningCredentials CreateHs256SigningCredentials(IConfigurationSection jwtSettings)
    {
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");

        if (Encoding.UTF8.GetByteCount(secretKey) < MinimumHs256SecretKeyBytes)
        {
            throw new InvalidOperationException("JWT HS256 SecretKey must be at least 32 bytes when UTF-8 encoded");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    static SigningCredentials CreateRs256SigningCredentials(IConfigurationSection jwtSettings)
    {
        var privateKeyPem = jwtSettings["RS256:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(privateKeyPem))
        {
            throw new InvalidOperationException("JWT RS256:PrivateKeyPem not configured");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.ToCharArray());
        var key = new RsaSecurityKey(rsa);
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public int GetAccessTokenExpiresInSeconds()
    {
        var expirationMinutes = GetAccessTokenExpirationMinutes();
        return checked(expirationMinutes * 60);
    }

    int GetAccessTokenExpirationMinutes()
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var configuredMinutes = jwtSettings["ExpirationMinutes"];

        if (string.IsNullOrWhiteSpace(configuredMinutes))
        {
            return DefaultAccessTokenExpirationMinutes;
        }

        if (int.TryParse(configuredMinutes, out var minutes) && minutes > 0)
        {
            return minutes;
        }

        Log.Infrastructure.InvalidJwtExpirationMinutes(
            _logger,
            configuredMinutes,
            DefaultAccessTokenExpirationMinutes);

        return DefaultAccessTokenExpirationMinutes;
    }

    /// <summary>
    /// Generate a cryptographically secure refresh token.
    /// The returned value is plaintext and should only be sent to the client;
    /// persisted values must use <see cref="HashRefreshToken"/>.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Computes a deterministic SHA-256 hash for refresh-token storage and lookup.
    /// </summary>
    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Rotates refresh tokens for a user, maintaining the latest 5 active tokens.
    /// Marks the old token as used (for replay detection) rather than removing it immediately.
    /// </summary>
    public string RotateRefreshToken(BookStore.ApiService.Models.ApplicationUser user, string tenantId, string? oldToken = null)
    {
        string familyId;

        // 1. Mark the old token as used (do NOT remove it — presence of a used token enables replay detection)
        if (!string.IsNullOrEmpty(oldToken))
        {
            var hashedOldToken = HashRefreshToken(oldToken);
            var existing = user.RefreshTokens.FirstOrDefault(rt => rt.Token == hashedOldToken);
            if (existing != null)
            {
                familyId = existing.FamilyId;
                var index = user.RefreshTokens.IndexOf(existing);
                user.RefreshTokens[index] = existing with { IsUsed = true };
            }
            else
            {
                // Old token not found — start a new family
                familyId = Guid.CreateVersion7().ToString();
            }
        }
        else
        {
            // No old token supplied — start a new token family
            familyId = Guid.CreateVersion7().ToString();
        }

        // 2. Generate new token
        var newToken = GenerateRefreshToken();
        var hashedNewToken = HashRefreshToken(newToken);

        // 3. Add to collection (same family as parent)
        user.RefreshTokens.Add(new BookStore.ApiService.Models.RefreshTokenInfo(
            hashedNewToken,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            tenantId,
            user.SecurityStamp ?? string.Empty,
            familyId,
            IsUsed: false));

        // 4. Prune expired tokens but keep recently-used ones for replay detection (up to 24 h)
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        user.RefreshTokens = [.. user.RefreshTokens
            .Where(r => !r.IsUsed || r.Created >= cutoff)
            .OrderByDescending(r => r.Created)
            .Take(20)];

        return newToken;
    }
}
