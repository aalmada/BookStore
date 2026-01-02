using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookStore.Web.Services;

/// <summary>
/// Custom authentication state provider for Blazor
/// </summary>
public class CustomAuthenticationStateProvider(AuthenticationService authService) : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await authService.GetAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var user = GetUserFromToken(token);
            return new AuthenticationState(user);
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop not available during prerendering
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// Notify that authentication state has changed
    /// </summary>
    public void NotifyAuthenticationStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    static ClaimsPrincipal GetUserFromToken(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "Unknown")
            };

            // Add email claim
            var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            if (email != null)
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            // Add role claims
            var roles = jwtToken.Claims.Where(c => c.Type is "role" or ClaimTypes.Role);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.Value)));

            var identity = new ClaimsIdentity(claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
