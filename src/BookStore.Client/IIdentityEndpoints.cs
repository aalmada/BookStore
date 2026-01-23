using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Identity API endpoints for authentication
/// </summary>
public interface IIdentityLoginEndpoint
{
    /// <summary>
    /// Login with email and password
    /// </summary>
    [Post("/account/login")]
    Task<LoginResponse> LoginAsync(
        [Body] LoginRequest request,
        [Query] bool? useCookies = null,
        [Query] bool? useSessionCookies = null,
        CancellationToken cancellationToken = default);
}

public interface IIdentityRegisterEndpoint
{
    /// <summary>
    /// Register a new user
    /// </summary>
    [Post("/account/register")]
    Task<LoginResponse> RegisterAsync(
        [Body] RegisterRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityConfirmEmailEndpoint
{
    /// <summary>
    /// Confirm user email
    /// </summary>
    [Post("/account/confirm-email")]
    Task ConfirmEmailAsync(
        [Query] string userId,
        [Query] string code,
        CancellationToken cancellationToken = default);
}

public interface IIdentityRefreshEndpoint
{
    /// <summary>
    /// Refresh access token
    /// </summary>
    [Post("/account/refresh-token")]
    Task<LoginResponse> RefreshTokenAsync(
        [Body] RefreshRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityLogoutEndpoint
{
    /// <summary>
    /// Logout and invalidate refresh token
    /// </summary>
    [Post("/account/logout")]
    Task LogoutAsync(
        [Body] LogoutRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityChangePasswordEndpoint
{
    /// <summary>
    /// Change user password
    /// </summary>
    [Post("/account/change-password")]
    Task ChangePasswordAsync(
        [Body] ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityAddPasswordEndpoint
{
    /// <summary>
    /// Set a password for a user without one
    /// </summary>
    [Post("/account/add-password")]
    Task AddPasswordAsync(
        [Body] AddPasswordRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityGetPasswordStatusEndpoint
{
    /// <summary>
    /// Check if the user has a password set
    /// </summary>
    [Get("/account/password-status")]
    Task<PasswordStatusResponse> GetPasswordStatusAsync(
        CancellationToken cancellationToken = default);
}

