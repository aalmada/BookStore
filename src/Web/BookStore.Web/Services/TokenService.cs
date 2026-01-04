namespace BookStore.Web.Services;

/// <summary>
/// Service for storing authentication tokens in memory (per-circuit).
/// Tokens are cleared when the circuit ends, providing better security than LocalStorage.
/// </summary>
public class TokenService
{
    string? _accessToken;
    string? _refreshToken;

    /// <summary>
    /// Store authentication tokens
    /// </summary>
    public void SetTokens(string accessToken, string? refreshToken = null)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
    }

    /// <summary>
    /// Get the current access token
    /// </summary>
    public string? GetAccessToken() => _accessToken;

    /// <summary>
    /// Get the current refresh token
    /// </summary>
    public string? GetRefreshToken() => _refreshToken;

    /// <summary>
    /// Check if user is authenticated (has valid token)
    /// </summary>
    public bool IsAuthenticated() => !string.IsNullOrEmpty(_accessToken);

    /// <summary>
    /// Clear all tokens (logout)
    /// </summary>
    public void ClearTokens()
    {
        _accessToken = null;
        _refreshToken = null;
    }
}
