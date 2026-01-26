using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.ApiService.Services;

/// <summary>
/// Service for generating and validating JWT tokens
/// </summary>
public class JwtTokenService
{
    readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

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
            new("tenant_id", tenantId)
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
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).UtcDateTime,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate a cryptographically secure refresh token
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Rotates refresh tokens for a user, maintaining the latest 5 tokens.
    /// </summary>
    public string RotateRefreshToken(BookStore.ApiService.Models.ApplicationUser user, string tenantId, string? oldToken = null)
    {
        // 1. Remove old token if provided
        if (!string.IsNullOrEmpty(oldToken))
        {
            var existing = user.RefreshTokens.FirstOrDefault(rt => rt.Token == oldToken);
            if (existing != null)
            {
                _ = user.RefreshTokens.Remove(existing);
            }
        }

        // 2. Generate new token
        var newToken = GenerateRefreshToken();

        // 3. Add to collection
        user.RefreshTokens.Add(new BookStore.ApiService.Models.RefreshTokenInfo(
            newToken,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            tenantId));

        // 4. Prune old tokens (keep latest 5)
        if (user.RefreshTokens.Count > 5)
        {
            user.RefreshTokens = [.. user.RefreshTokens.OrderByDescending(r => r.Created).Take(5)];
        }

        return newToken;
    }
}
