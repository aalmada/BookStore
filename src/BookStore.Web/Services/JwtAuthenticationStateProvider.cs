using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookStore.Web.Services;

/// <summary>
/// Authentication state provider for JWT token-based authentication.
/// Supports multi-tenant sessions by storing tokens per-tenant.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    readonly TokenService _tokenService;
    readonly Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage _localStorage;
    readonly TenantService _tenantService;

    public JwtAuthenticationStateProvider(
        TokenService tokenService,
        Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage localStorage,
        TenantService tenantService)
    {
        _tokenService = tokenService;
        _localStorage = localStorage;
        _tenantService = tenantService;
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
                _tokenService.ClearTokens(currentTenant);
                return CreateAnonymousState();
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

    static AuthenticationState CreateAnonymousState()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    public void Dispose() => _tenantService.OnChange -= HandleTenantChanged;

    /// <summary>
    /// Notify that user has authenticated with a new token for the current tenant
    /// </summary>
    public async Task NotifyUserAuthentication(string token)
    {
        var currentTenant = _tenantService.CurrentTenantId;
        _tokenService.SetTokens(currentTenant, token);

        try
        {
            await _localStorage.SetAsync(GetStorageKey(currentTenant), token);
        }
        catch
        {
            /* Storage write error */
        }

        var claims = ParseClaimsFromJwt(token);
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
