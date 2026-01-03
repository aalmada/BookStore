using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookStore.Web.Services;

/// <summary>
/// Authentication state provider for JWT token-based authentication.
/// Reads authentication state from tokens stored in TokenService.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenService _tokenService;

    public JwtAuthenticationStateProvider(TokenService tokenService) => _tokenService = tokenService;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = _tokenService.GetAccessToken();

        if (string.IsNullOrEmpty(token))
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(anonymous));
        }

        try
        {
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return Task.FromResult(new AuthenticationState(user));
        }
        catch
        {
            // Invalid token
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(anonymous));
        }
    }

    /// <summary>
    /// Notify that user has authenticated with a new token
    /// </summary>
    public void NotifyUserAuthentication(string token)
    {
        _tokenService.SetTokens(token);

        try
        {
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }
        catch (Exception)
        {
            // Log the error if logger was injected, or just rethrow
            throw;
        }
    }

    /// <summary>
    /// Notify that user has logged out
    /// </summary>
    public void NotifyUserLogout()
    {
        _tokenService.ClearTokens();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }
}
