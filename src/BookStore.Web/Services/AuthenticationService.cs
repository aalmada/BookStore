using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Shared.Validation;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing user authentication using JWT token-based authentication
/// </summary>
public class AuthenticationService(
    IIdentityClient identityClient,
    TokenService tokenService,
    TenantService tenantService)
{
    public async Task<bool> ConfirmEmailAsync(string userId, string code)
    {
        try
        {
            await identityClient.ConfirmEmailAsync(userId, code);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        try
        {
            await identityClient.ResendVerificationAsync(new ResendVerificationRequest(email));
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
            var response = await identityClient.LoginAsync(request, useCookies: false);

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
            _ = await identityClient.RegisterAsync(request);
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
                if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == System.Text.Json.JsonValueKind.Array)
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
    /// Logout the current user and invalidate refresh token on server
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            var currentTenant = tenantService.CurrentTenantId;
            var refreshToken = tokenService.GetRefreshToken(currentTenant);
            await identityClient.LogoutAsync(new LogoutRequest(refreshToken));
        }
        catch
        {
            // Logout failures are non-critical - local tokens will still be cleared
        }
    }

    /// <summary>
    /// Validate password strength using shared validator
    /// </summary>
    static string? ValidatePassword(string password) => PasswordValidator.GetFirstError(password);

    public async Task<PasswordOperationResult> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var validationError = ValidatePassword(newPassword);
        if (validationError != null)
        {
            return new PasswordOperationResult(false, validationError);
        }

        if (currentPassword == newPassword)
        {
            return new PasswordOperationResult(false, "New password cannot be the same as the current password.");
        }

        try
        {
            await identityClient.ChangePasswordAsync(new ChangePasswordRequest(currentPassword, newPassword));
            return new PasswordOperationResult(true, null);
        }
        catch (Refit.ApiException ex)
        {
            return new PasswordOperationResult(false, ParseError(ex.Content));
        }
    }

    public async Task<PasswordOperationResult> AddPasswordAsync(string newPassword)
    {
        var validationError = ValidatePassword(newPassword);
        if (validationError != null)
        {
            return new PasswordOperationResult(false, validationError);
        }

        try
        {
            await identityClient.AddPasswordAsync(new AddPasswordRequest(newPassword));
            return new PasswordOperationResult(true, null);
        }
        catch (Refit.ApiException ex)
        {
            return new PasswordOperationResult(false, ParseError(ex.Content));
        }
    }

    public async Task<bool> HasPasswordAsync()
    {
        try
        {
            var response = await identityClient.GetPasswordStatusAsync();
            return response.HasPassword;
        }
        catch
        {
            return false;
        }
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

public record PasswordOperationResult(bool Success, string? Error);
