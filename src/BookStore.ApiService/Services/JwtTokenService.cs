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
    const int DefaultAccessTokenExpirationMinutes = 60;
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
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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

    public int GetAccessTokenExpiresInSeconds()
    {
        var expirationMinutes = GetAccessTokenExpirationMinutes();
        return checked(expirationMinutes * 60);
    }

    int GetAccessTokenExpirationMinutes()
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var configuredMinutes = jwtSettings["ExpirationMinutes"];

        if (int.TryParse(configuredMinutes, out var minutes) && minutes > 0)
        {
            return minutes;
        }

        return DefaultAccessTokenExpirationMinutes;
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
    /// Rotates refresh tokens for a user, maintaining the latest 5 active tokens.
    /// Marks the old token as used (for replay detection) rather than removing it immediately.
    /// </summary>
    public string RotateRefreshToken(BookStore.ApiService.Models.ApplicationUser user, string tenantId, string? oldToken = null)
    {
        string familyId;

        // 1. Mark the old token as used (do NOT remove it — presence of a used token enables replay detection)
        if (!string.IsNullOrEmpty(oldToken))
        {
            var existing = user.RefreshTokens.FirstOrDefault(rt => rt.Token == oldToken);
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

        // 3. Add to collection (same family as parent)
        user.RefreshTokens.Add(new BookStore.ApiService.Models.RefreshTokenInfo(
            newToken,
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
