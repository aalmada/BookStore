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
    /// Generate a JWT access token for the given user with tenant context.
    /// This is the preferred method for generating tokens as it includes the tenant_id claim.
    /// </summary>
    /// <param name="user">The user to generate a token for</param>
    /// <param name="tenantId">The tenant identifier to include in the token</param>
    /// <param name="roles">Optional roles to include. If null, uses user.Roles</param>
    public string GenerateAccessToken(BookStore.ApiService.Models.ApplicationUser user, string tenantId, IEnumerable<string>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new("tenant_id", tenantId),
        };

        foreach (var role in roles ?? user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return GenerateAccessToken(claims);
    }

    /// <summary>
    /// Generate a JWT access token for the given user.
    /// </summary>
    /// <remarks>
    /// This method is deprecated. Use <see cref="GenerateAccessToken(BookStore.ApiService.Models.ApplicationUser, string, IEnumerable{string}?)"/> 
    /// which includes tenant_id for proper multi-tenancy support.
    /// </remarks>
    [Obsolete("Use GenerateAccessToken(user, tenantId, roles) overload for multi-tenancy support")]
    public string GenerateAccessToken(BookStore.ApiService.Models.ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

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
}
