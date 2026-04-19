using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookStore.Client;
using BookStore.Web.Logging;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace BookStore.Web.Services;

/// <summary>
/// Authentication state provider for JWT token-based authentication.
/// Supports multi-tenant sessions by storing tokens per-tenant.
/// Automatically refreshes tokens before they expire.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    readonly TokenService _tokenService;
    readonly Blazored.LocalStorage.ILocalStorageService _localStorage;
    readonly TenantService _tenantService;
    readonly IIdentityClient _identityClient;
    readonly ILogger<JwtAuthenticationStateProvider> _logger;

    /// <summary>
    /// Time before token expiry to trigger background refresh (5 minutes)
    /// </summary>
    static readonly TimeSpan TokenRefreshThreshold = TimeSpan.FromMinutes(5);

    public JwtAuthenticationStateProvider(
        TokenService tokenService,
        Blazored.LocalStorage.ILocalStorageService localStorage,
        TenantService tenantService,
        IIdentityClient identityClient,
        ILogger<JwtAuthenticationStateProvider> logger)
    {
        _tokenService = tokenService;
        _localStorage = localStorage;
        _tenantService = tenantService;
        _identityClient = identityClient;
        _logger = logger;
        _tenantService.OnChange += HandleTenantChanged;
        _tokenService.OnTokensCleared += HandleTokensCleared;
    }

    void HandleTenantChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    void HandleTokensCleared(string tenantId)
    {
        if (string.Equals(tenantId, _tenantService.CurrentTenantId, StringComparison.OrdinalIgnoreCase))
        {
            NotifyAuthenticationStateChanged(Task.FromResult(CreateAnonymousState()));
        }
    }

    string GetStorageKey(string tenantId) => $"accessToken_{tenantId}";
    string GetRefreshStorageKey(string tenantId) => $"refreshToken_{tenantId}";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var currentTenant = _tenantService.CurrentTenantId;
        var token = _tokenService.GetAccessToken(currentTenant);

        if (string.IsNullOrEmpty(token))
        {
            // Try to hydrate from LocalStorage with tenant-specific key
            try
            {
                var storedToken = await _localStorage.GetItemAsync<string>(GetStorageKey(currentTenant));
                if (!string.IsNullOrEmpty(storedToken))
                {
                    token = storedToken;
                    var storedRefreshToken = await _localStorage.GetItemAsync<string>(GetRefreshStorageKey(currentTenant));
                    _tokenService.SetTokens(currentTenant, token, storedRefreshToken);
                }
            }
            catch (InvalidOperationException)
            {
                // JavaScript interop calls cannot be issued at this time (Prerendering)
            }
            catch (Exception ex)
            {
                Log.AuthStateStorageHydrationFailed(_logger, currentTenant, ex);
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
                // Token will expire soon - trigger background refresh on the current sync context
                // (Task.Run would leave the Blazor circuit context and break JS interop in localStorage)
                var refreshToken = _tokenService.GetRefreshToken(currentTenant);
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    _ = TryRefreshTokenAsync(currentTenant, refreshToken);
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
        catch (Exception ex)
        {
            Log.AuthStateTokenReadFailed(_logger, currentTenant, ex);
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
                    await _localStorage.SetItemAsync(GetStorageKey(tenantId), response.AccessToken);
                    if (!string.IsNullOrEmpty(response.RefreshToken))
                    {
                        await _localStorage.SetItemAsync(GetRefreshStorageKey(tenantId), response.RefreshToken);
                    }
                }
                catch (Exception ex)
                {
                    Log.AuthStateStorageWriteFailed(_logger, tenantId, ex);
                }

                NotifyAuthenticationStateChanged(Task.FromResult(
                    new AuthenticationState(new ClaimsPrincipal(
                        new ClaimsIdentity(ParseClaimsFromJwt(response.AccessToken), "jwt")))));

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.AuthStateRefreshFailed(_logger, tenantId, ex);
        }

        return false;
    }

    static AuthenticationState CreateAnonymousState()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    public void Dispose()
    {
        _tenantService.OnChange -= HandleTenantChanged;
        _tokenService.OnTokensCleared -= HandleTokensCleared;
    }

    /// <summary>
    /// Notify that user has authenticated with new tokens for the current tenant
    /// </summary>
    public async Task NotifyUserAuthentication(string accessToken, string? refreshToken = null)
    {
        var currentTenant = _tenantService.CurrentTenantId;
        _tokenService.SetTokens(currentTenant, accessToken, refreshToken);

        try
        {
            await _localStorage.SetItemAsync(GetStorageKey(currentTenant), accessToken);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _localStorage.SetItemAsync(GetRefreshStorageKey(currentTenant), refreshToken);
            }
        }
        catch (Exception ex)
        {
            Log.AuthStateAuthenticationPersistFailed(_logger, currentTenant, ex);
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
            await _localStorage.RemoveItemAsync(GetStorageKey(currentTenant));
            await _localStorage.RemoveItemAsync(GetRefreshStorageKey(currentTenant));
        }
        catch (Exception ex)
        {
            Log.AuthStateStorageDeleteFailed(_logger, currentTenant, ex);
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
