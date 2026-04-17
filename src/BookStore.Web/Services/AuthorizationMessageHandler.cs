using System.Net.Http.Headers;
using BookStore.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookStore.Web.Services;

/// <summary>
/// HTTP message handler that attaches the JWT access token to outgoing requests
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    readonly KeycloakTokenAccessor _tokenAccessor;
    readonly AuthenticationStateProvider _authenticationStateProvider;
    readonly IHttpContextAccessor _httpContextAccessor;
    readonly ClientContextService _clientContextService;

    public AuthorizationMessageHandler(
        KeycloakTokenAccessor tokenAccessor,
        AuthenticationStateProvider authenticationStateProvider,
        IHttpContextAccessor httpContextAccessor,
        ClientContextService clientContextService)
    {
        _tokenAccessor = tokenAccessor;
        _authenticationStateProvider = authenticationStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _clientContextService = clientContextService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add Correlation and Causation IDs
        request.Headers.Add("X-Correlation-ID", _clientContextService.CorrelationId);
        request.Headers.Add("X-Causation-ID", _clientContextService.CausationId);

        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var userSub = authState.User.FindFirst("sub")?.Value;
        var token = await _tokenAccessor.GetAccessTokenAsync(userSub ?? string.Empty);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        // Forward technical metadata from the original browser request
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // Forward User-Agent
            var userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();
            if (!string.IsNullOrEmpty(userAgent))
            {
                request.Headers.UserAgent.Clear();
                _ = request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }

            // Forward Client IP (assuming ForwardedHeaders middleware has processed it)
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(ip))
            {
                _ = request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Capture Event ID from response to use as causation for next request
        if (response.Headers.TryGetValues("X-Event-ID", out var eventIds))
        {
            var eventId = eventIds.FirstOrDefault();
            if (!string.IsNullOrEmpty(eventId))
            {
                _clientContextService.UpdateCausationId(eventId);
            }
        }

        return response;
    }
}
