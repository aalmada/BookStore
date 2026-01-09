using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing user authentication using cookie-based authentication
/// </summary>
public class AuthenticationService(IIdentityClient identityClient)
{
    public async Task<bool> ConfirmEmailAsync(string userId, string code)
    {
        try
        {
            await ((IIdentityConfirmEmailEndpoint)identityClient).Execute(userId, code);
            return true;
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Login with email and password (JWT token-based)
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest(email, password);
            // useCookies=false - we want JWT tokens, not cookies
            var response = await ((IIdentityLoginEndpoint)identityClient).Execute(request, useCookies: false);

            // Return the access token so caller can store it
            return new LoginResult(true, null, response.AccessToken, response.RefreshToken);
        }
        catch (Refit.ApiException ex)
        {
            var errorMessage = ParseError(ex.Content);
            return new LoginResult(false, errorMessage, null, null);
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
            _ = await ((IIdentityRegisterEndpoint)identityClient).Execute(request);
            return new RegisterResult(true, null);
        }
        catch (Refit.ApiException ex)
        {
            var errorMessage = ParseError(ex.Content);
            return new RegisterResult(false, errorMessage);
        }
    }

    static string ParseError(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "Operation failed";
        }

        try
        {
            // Try to parse standard { "errors": [ { "description": "..." } ] }
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var error in errors.EnumerateArray())
                    {
                        if (error.TryGetProperty("description", out var desc))
                        {
                            return desc.GetString() ?? "Unknown error";
                        }
                    }
                }
                // Try to parse standard ProblemDetails "detail"
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "Operation failed";
                }
            }
            // If it's a direct array [ { "description": "..." } ] (used in some endpoints)
            else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var error in doc.RootElement.EnumerateArray())
                {
                    if (error.TryGetProperty("description", out var desc))
                    {
                        return desc.GetString() ?? "Unknown error";
                    }
                }
            }
        }
        catch
        {
            // Fallback to raw content if not JSON
        }

        // Clean up quotes if it's a simple string
        if (content.StartsWith("\"") && content.EndsWith("\""))
        {
            return content.Trim('"');
        }

        return content;
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
