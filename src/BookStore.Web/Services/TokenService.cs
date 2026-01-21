namespace BookStore.Web.Services;

/// <summary>
/// Service for storing authentication tokens in memory (per-circuit).
/// Tokens are stored per-tenant to support multi-tenant sessions.
/// </summary>
public class TokenService
{
    readonly Dictionary<string, (string AccessToken, string? RefreshToken)> _tokens = [];

    /// <summary>
    /// Store authentication tokens for a specific tenant
    /// </summary>
    public void SetTokens(string tenantId, string accessToken, string? refreshToken = null) => _tokens[tenantId] = (accessToken, refreshToken);

    /// <summary>
    /// Get the current access token for a specific tenant
    /// </summary>
    public string? GetAccessToken(string tenantId)
        => _tokens.TryGetValue(tenantId, out var tokens) ? tokens.AccessToken : null;

    /// <summary>
    /// Get the current refresh token for a specific tenant
    /// </summary>
    public string? GetRefreshToken(string tenantId)
        => _tokens.TryGetValue(tenantId, out var tokens) ? tokens.RefreshToken : null;

    /// <summary>
    /// Check if user is authenticated for a specific tenant
    /// </summary>
    public bool IsAuthenticated(string tenantId)
        => _tokens.TryGetValue(tenantId, out var tokens) && !string.IsNullOrEmpty(tokens.AccessToken);

    /// <summary>
    /// Clear tokens for a specific tenant (logout)
    /// </summary>
    public void ClearTokens(string tenantId) => _tokens.Remove(tenantId);

    /// <summary>
    /// Clear all tokens for all tenants
    /// </summary>
    public void ClearAllTokens() => _tokens.Clear();
}
