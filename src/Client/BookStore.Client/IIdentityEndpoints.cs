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
    [Post("/identity/login")]
    Task<LoginResponse> Execute(
        [Body] LoginRequest request,
        [AliasAs("useCookies")] bool? useCookies = null,
        [AliasAs("useSessionCookies")] bool? useSessionCookies = null,
        CancellationToken cancellationToken = default);
}

public interface IIdentityRegisterEndpoint
{
    /// <summary>
    /// Register a new user
    /// </summary>
    [Post("/identity/register")]
    Task Execute(
        [Body] RegisterRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityConfirmEmailEndpoint
{
    /// <summary>
    /// Confirm user email
    /// </summary>
    [Post("/identity/confirmEmail")]
    Task Execute(
        [Query] string userId,
        [Query] string code,
        CancellationToken cancellationToken = default);
}

public interface IIdentityRefreshEndpoint
{
    /// <summary>
    /// Refresh access token
    /// </summary>
    [Post("/identity/refresh")]
    Task<LoginResponse> Execute(
        [Body] RefreshRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityManageInfoEndpoint
{
    /// <summary>
    /// Get user information
    /// </summary>
    [Get("/identity/manage/info")]
    Task<UserInfo> Execute(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update user information
    /// </summary>
    [Post("/identity/manage/info")]
    Task<UserInfo> Update(
        [Body] UpdateUserInfoRequest request,
        CancellationToken cancellationToken = default);
}

