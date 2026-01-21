using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookStore.Client;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookStore.Web.Services;

/// <summary>
/// Authentication state provider for JWT token-based authentication.
/// Supports multi-tenant sessions by storing tokens per-tenant.
/// Automatically refreshes tokens before they expire.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    readonly TokenService _tokenService;
    readonly Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage _localStorage;
    readonly TenantService _tenantService;
    readonly IIdentityClient _identityClient;

    /// <summary>
    /// Time before token expiry to trigger background refresh (5 minutes)
    /// </summary>
    static readonly TimeSpan TokenRefreshThreshold = TimeSpan.FromMinutes(5);

    public JwtAuthenticationStateProvider(
        TokenService tokenService,
        Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage localStorage,
        TenantService tenantService,
        IIdentityClient identityClient)
    {
        _tokenService = tokenService;
        _localStorage = localStorage;
        _tenantService = tenantService;
        _identityClient = identityClient;
        _tenantService.OnChange += HandleTenantChanged;
    }

    void HandleTenantChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    string GetStorageKey(string tenantId) => $"accessToken_{tenantId}";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var currentTenant = _tenantService.CurrentTenantId;
        var token = _tokenService.GetAccessToken(currentTenant);

        if (string.IsNullOrEmpty(token))
        {
            // Try to hydrate from LocalStorage with tenant-specific key
            try
            {
                var result = await _localStorage.GetAsync<string>(GetStorageKey(currentTenant));
                if (result.Success && !string.IsNullOrEmpty(result.Value))
                {
                    token = result.Value;
                    _tokenService.SetTokens(currentTenant, token);
                }
            }
            catch (InvalidOperationException)
            {
                // JavaScript interop calls cannot be issued at this time (Prerendering)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Storage hydration failed: {ex.Message}");
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return CreateAnonymousState();
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                // Token expired - try to refresh
                var refreshToken = _tokenService.GetRefreshToken(currentTenant);
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var refreshed = await TryRefreshTokenAsync(currentTenant, refreshToken);
                    if (refreshed)
                    {
                        // Re-read the new token
                        token = _tokenService.GetAccessToken(currentTenant);
                        jwtToken = handler.ReadJwtToken(token!);
                    }
                    else
                    {
                        _tokenService.ClearTokens(currentTenant);
                        return CreateAnonymousState();
                    }
                }
                else
                {
                    _tokenService.ClearTokens(currentTenant);
                    return CreateAnonymousState();
                }
            }
            else if (jwtToken.ValidTo < DateTime.UtcNow.Add(TokenRefreshThreshold))
            {
                // Token will expire soon - trigger background refresh
                var refreshToken = _tokenService.GetRefreshToken(currentTenant);
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    _ = Task.Run(async () => await TryRefreshTokenAsync(currentTenant, refreshToken));
                }
            }

            // Verify token belongs to current tenant
            var tokenTenant = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
            if (string.IsNullOrEmpty(tokenTenant) ||
                !string.Equals(tokenTenant, currentTenant, StringComparison.OrdinalIgnoreCase))
            {
                return CreateAnonymousState();
            }

            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            _tokenService.ClearTokens(currentTenant);
            return CreateAnonymousState();
        }
    }

    /// <summary>
    /// Attempts to refresh the access token using the refresh token
    /// </summary>
    async Task<bool> TryRefreshTokenAsync(string tenantId, string refreshToken)
    {
        try
        {
            var response = await _identityClient.RefreshTokenAsync(new RefreshRequest(refreshToken));
            if (!string.IsNullOrEmpty(response.AccessToken))
            {
                _tokenService.SetTokens(tenantId, response.AccessToken, response.RefreshToken);

                try
                {
                    await _localStorage.SetAsync(GetStorageKey(tenantId), response.AccessToken);
                }
                catch
                {
                    /* Storage write error is non-critical */
                }

                NotifyAuthenticationStateChanged(Task.FromResult(
                    new AuthenticationState(new ClaimsPrincipal(
                        new ClaimsIdentity(ParseClaimsFromJwt(response.AccessToken), "jwt")))));

                return true;
            }
        }
        catch
        {
            /* Refresh failed - caller will handle */
        }

        return false;
    }

    static AuthenticationState CreateAnonymousState()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    public void Dispose() => _tenantService.OnChange -= HandleTenantChanged;

    /// <summary>
    /// Notify that user has authenticated with new tokens for the current tenant
    /// </summary>
    public async Task NotifyUserAuthentication(string accessToken, string? refreshToken = null)
    {
        var currentTenant = _tenantService.CurrentTenantId;
        _tokenService.SetTokens(currentTenant, accessToken, refreshToken);

        try
        {
            await _localStorage.SetAsync(GetStorageKey(currentTenant), accessToken);
        }
        catch
        {
            /* Storage write error */
        }

        var claims = ParseClaimsFromJwt(accessToken);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    /// <summary>
    /// Notify that user has logged out of the current tenant
    /// </summary>
    public async Task NotifyUserLogout()
    {
        var currentTenant = _tenantService.CurrentTenantId;
        _tokenService.ClearTokens(currentTenant);

        try
        {
            await _localStorage.DeleteAsync(GetStorageKey(currentTenant));
        }
        catch
        {
            /* Storage delete error */
        }

        NotifyAuthenticationStateChanged(Task.FromResult(CreateAnonymousState()));
    }

    static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }
}
