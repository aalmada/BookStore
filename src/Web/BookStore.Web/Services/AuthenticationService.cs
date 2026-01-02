using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing user authentication
/// </summary>
public class AuthenticationService(
    ILocalStorageService localStorage,
    IIdentityLoginEndpoint loginEndpoint,
    IIdentityRegisterEndpoint registerEndpoint,
    IIdentityRefreshEndpoint refreshEndpoint)
{
    const string AccessTokenKey = "accessToken";
    const string RefreshTokenKey = "refreshToken";
    const string TokenExpiryKey = "tokenExpiry";

    /// <summary>
    /// Login with email and password
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest(email, password);
            var response = await loginEndpoint.Execute(request);

            // Store tokens
            await localStorage.SetItemAsync(AccessTokenKey, response.AccessToken);
            await localStorage.SetItemAsync(RefreshTokenKey, response.RefreshToken);
            await localStorage.SetItemAsync(TokenExpiryKey, DateTime.UtcNow.AddSeconds(response.ExpiresIn));

            return new LoginResult(true, null, GetUserFromToken(response.AccessToken));
        }
        catch (Refit.ApiException ex)
        {
            return new LoginResult(false, ex.Content ?? "Login failed", null);
        }
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    public async Task<RegisterResult> RegisterAsync(string email, string password)
    {
        // Validate password strength
        var validationError = ValidatePassword(password);
        if (validationError != null)
        {
            return new RegisterResult(false, validationError);
        }

        try
        {
            var request = new RegisterRequest(email, password);
            await registerEndpoint.Execute(request);
            return new RegisterResult(true, null);
        }
        catch (Refit.ApiException ex)
        {
            return new RegisterResult(false, ex.Content ?? "Registration failed");
        }
    }

    /// <summary>
    /// Logout the current user
    /// </summary>
    public async Task LogoutAsync()
    {
        await localStorage.RemoveItemAsync(AccessTokenKey);
        await localStorage.RemoveItemAsync(RefreshTokenKey);
        await localStorage.RemoveItemAsync(TokenExpiryKey);
    }

    /// <summary>
    /// Get the current access token
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            // Check if token is expired
            var expiry = await localStorage.GetItemAsync<DateTime?>(TokenExpiryKey);
            if (expiry.HasValue && expiry.Value <= DateTime.UtcNow.AddMinutes(5))
            {
                // Token expired or expiring soon, try to refresh
                await RefreshTokenAsync();
            }

            return await localStorage.GetItemAsync<string>(AccessTokenKey);
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop not available during prerendering
            return null;
        }
    }

    /// <summary>
    /// Refresh the access token
    /// </summary>
    async Task RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await localStorage.GetItemAsync<string>(RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return;
            }

            var request = new RefreshRequest(refreshToken);
            var response = await refreshEndpoint.Execute(request);

            // Update tokens
            await localStorage.SetItemAsync(AccessTokenKey, response.AccessToken);
            await localStorage.SetItemAsync(RefreshTokenKey, response.RefreshToken);
            await localStorage.SetItemAsync(TokenExpiryKey, DateTime.UtcNow.AddSeconds(response.ExpiresIn));
        }
        catch
        {
            // Refresh failed, clear tokens
            await LogoutAsync();
        }
    }

    /// <summary>
    /// Get user information from JWT token
    /// </summary>
    static UserClaims? GetUserFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var email = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            var roles = jwtToken.Claims
                .Where(c => c.Type is ClaimTypes.Role or "role")
                .Select(c => c.Value)
                .ToList();

            return email != null ? new UserClaims(email, roles) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validate password strength
    /// </summary>
    static string? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required";
        }

        if (password.Length < 8)
        {
            return "Password must be at least 8 characters long";
        }

        if (!password.Any(char.IsUpper))
        {
            return "Password must contain at least one uppercase letter";
        }

        if (!password.Any(char.IsLower))
        {
            return "Password must contain at least one lowercase letter";
        }

        if (!password.Any(char.IsDigit))
        {
            return "Password must contain at least one number";
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return "Password must contain at least one special character";
        }

        return null;
    }
}

public record LoginResult(bool Success, string? Error, UserClaims? User);
public record RegisterResult(bool Success, string? Error);
public record UserClaims(string Email, IReadOnlyList<string> Roles);
