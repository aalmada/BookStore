using System.Net.Http.Headers;
using BookStore.Client.Services;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Http;

namespace BookStore.Web.Services;

/// <summary>
/// HTTP message handler that attaches the JWT access token to outgoing requests
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    readonly TokenService _tokenService;
    readonly IHttpContextAccessor _httpContextAccessor;
    readonly CorrelationService _correlationService;

    public AuthorizationMessageHandler(
        TokenService tokenService,
        IHttpContextAccessor httpContextAccessor,
        CorrelationService correlationService)
    {
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
        _correlationService = correlationService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add Correlation and Causation IDs
        request.Headers.Add("X-Correlation-ID", _correlationService.CorrelationId);
        request.Headers.Add("X-Causation-ID", _correlationService.CausationId);

        var token = _tokenService.GetAccessToken();
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
                _correlationService.UpdateCausationId(eventId);
            }
        }

        return response;
    }
}
