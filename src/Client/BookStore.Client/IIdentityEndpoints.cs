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
    Task<LoginResponse> Execute(
        [Body] LoginRequest request,
        [Query, AliasAs("useCookies")] bool? useCookies = null,
        [Query, AliasAs("useSessionCookies")] bool? useSessionCookies = null,
        CancellationToken cancellationToken = default);
}

public interface IIdentityRegisterEndpoint
{
    /// <summary>
    /// Register a new user
    /// </summary>
    [Post("/account/register")]
    Task<LoginResponse> Execute(
        [Body] RegisterRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityConfirmEmailEndpoint
{
    /// <summary>
    /// Confirm user email
    /// </summary>
    [Post("/account/confirm-email")]
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
    [Post("/account/refresh-token")]
    Task<LoginResponse> Execute(
        [Body] RefreshRequest request,
        CancellationToken cancellationToken = default);
}

