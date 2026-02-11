using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for user identity and account management.
/// </summary>
public interface IIdentityClient
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

    /// <summary>
    /// Register a new user
    /// </summary>
    [Post("/account/register")]
    Task<LoginResponse> RegisterAsync(
        [Body] RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resend email verification link
    /// </summary>
    [Post("/account/resend-verification")]
    Task ResendVerificationAsync(
        [Body] ResendVerificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirm user email
    /// </summary>
    [Post("/account/confirm-email")]
    Task ConfirmEmailAsync(
        [Query] string userId,
        [Query] string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh access token
    /// </summary>
    [Post("/account/refresh-token")]
    Task<LoginResponse> RefreshTokenAsync(
        [Body] RefreshRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logout and invalidate refresh token
    /// </summary>
    [Post("/account/logout")]
    Task LogoutAsync(
        [Body] LogoutRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Change user password
    /// </summary>
    [Post("/account/change-password")]
    Task ChangePasswordAsync(
        [Body] ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a password for a user without one
    /// </summary>
    [Post("/account/add-password")]
    Task AddPasswordAsync(
        [Body] AddPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove password from a user account (must have passkeys)
    /// </summary>
    [Post("/account/remove-password")]
    Task RemovePasswordAsync(
        [Body] RemovePasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the user has a password set
    /// </summary>
    [Get("/account/password-status")]
    Task<PasswordStatusResponse> GetPasswordStatusAsync(
        CancellationToken cancellationToken = default);
}
