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
    readonly TokenService _tokenService;
    readonly Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage _localStorage;

    public JwtAuthenticationStateProvider(TokenService tokenService, Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage localStorage)
    {
        _tokenService = tokenService;
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = _tokenService.GetAccessToken();

        if (string.IsNullOrEmpty(token))
        {
            // Try to hydrate from LocalStorage
            try
            {
                var result = await _localStorage.GetAsync<string>("accessToken");
                if (result.Success && !string.IsNullOrEmpty(result.Value))
                {
                    token = result.Value;
                    _tokenService.SetTokens(token);
                }
            }
            catch (InvalidOperationException)
            {
                // JavaScript interop calls cannot be issued at this time (Prerendering).
                // This is expected.
            }
            catch (Exception ex)
            {
                // Storage failure, ignore
                System.Diagnostics.Debug.WriteLine($"Storage hydration failed: {ex.Message}");
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(anonymous);
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                 // Token expired
                _tokenService.ClearTokens();
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(anonymous);
            }

            var claims = jwtToken.Claims;
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }
        catch
        {
            // Invalid token
            _tokenService.ClearTokens();
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(anonymous);
        }
    }

    /// <summary>
    /// Notify that user has authenticated with a new token
    /// </summary>
    public async Task NotifyUserAuthentication(string token)
    {
        _tokenService.SetTokens(token);

        try
        {
            await _localStorage.SetAsync("accessToken", token);
        }
        catch { /* checking write permission error? */ }

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
    public async Task NotifyUserLogout()
    {
        _tokenService.ClearTokens();
        
        try
        {
             await _localStorage.DeleteAsync("accessToken");
        }
        catch { }

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
    }

    static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }
}
