using System.Net.Http.Headers;

namespace BookStore.Web.Services;

/// <summary>
/// HTTP message handler that adds JWT token to outgoing requests
/// </summary>
public class AuthorizingHttpMessageHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Lazily resolve AuthenticationService to avoid circular dependency
        var authService = serviceProvider.GetService<AuthenticationService>();

        // Get the access token
        var token = authService != null ? await authService.GetAccessTokenAsync() : null;

        // Add authorization header if token exists
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Send the request
        var response = await base.SendAsync(request, cancellationToken);

        // If unauthorized, token might be expired - try to refresh and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && authService != null)
        {
            // Token might be expired, get a fresh one
            token = await authService.GetAccessTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                // Retry with new token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }
}
