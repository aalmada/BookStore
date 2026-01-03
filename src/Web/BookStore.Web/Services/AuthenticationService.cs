using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing user authentication using cookie-based authentication
/// </summary>
public class AuthenticationService(
    IIdentityLoginEndpoint loginEndpoint,
    IIdentityRegisterEndpoint registerEndpoint)
{
    /// <summary>
    /// Login with email and password (JWT token-based)
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest(email, password);
            // useCookies=false - we want JWT tokens, not cookies
            var response = await loginEndpoint.Execute(request, useCookies: false);

            // Return the access token so caller can store it
            return new LoginResult(true, null, response.AccessToken, response.RefreshToken);
        }
        catch (Refit.ApiException ex)
        {
            return new LoginResult(false, ex.Content ?? "Login failed", null, null);
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
        try
        {
            // Call logout endpoint to clear the cookie on the server
            // Note: This requires adding IIdentityLogoutEndpoint to BookStore.Client
            // For now, the cookie will expire naturally or on browser close
            await Task.CompletedTask;
        }
        catch
        {
            // Logout failures are non-critical
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

        if (!password.Any(char.IsDigit))
        {
            return "Password must contain at least one digit";
        }

        if (!password.Any(char.IsLower))
        {
            return "Password must contain at least one lowercase letter";
        }

        if (!password.Any(char.IsUpper))
        {
            return "Password must contain at least one uppercase letter";
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return "Password must contain at least one special character";
        }

        return null;
    }
}

/// <summary>
/// Result of a login attempt
/// </summary>
public record LoginResult(bool Success, string? Error, string? AccessToken = null, string? RefreshToken = null);

/// <summary>
/// Result of a registration attempt
/// </summary>
public record RegisterResult(bool Success, string? Error);
