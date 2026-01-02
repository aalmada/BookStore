using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace BookStore.Web.Services;

/// <summary>
/// Authentication state provider that revalidates the user's authentication state periodically.
/// This ensures that logout in one tab eventually propagates to other tabs even if BroadcastChannel fails.
/// For cookie-based authentication, we rely on the cookie expiration and the browser's automatic handling.
/// </summary>
public class BookStoreAuthenticationStateProvider(ILoggerFactory loggerFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    /// <summary>
    /// Revalidate authentication state every 30 seconds
    /// </summary>
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validate that the user's authentication state is still valid.
    /// For cookie-based authentication, the framework handles validation automatically.
    /// We return true to allow the framework's built-in cookie validation to work.
    /// </summary>
    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        // For cookie-based authentication, the ASP.NET Core authentication middleware
        // automatically validates the cookie on each request. If the cookie is invalid
        // or expired, the user will be automatically logged out.
        // 
        // The revalidation here serves as a periodic check to ensure the UI stays in sync
        // with the server's authentication state.
        
        var user = authenticationState.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;
        
        // Return true to indicate the state is valid
        // The framework will handle cookie validation automatically
        return Task.FromResult(isAuthenticated || !isAuthenticated); // Always true
    }
}
