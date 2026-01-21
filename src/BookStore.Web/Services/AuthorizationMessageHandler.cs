using System.Net.Http.Headers;
using BookStore.Client.Services;

namespace BookStore.Web.Services;

/// <summary>
/// HTTP message handler that attaches the JWT access token to outgoing requests
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    readonly TokenService _tokenService;
    readonly TenantService _tenantService;
    readonly IHttpContextAccessor _httpContextAccessor;
    readonly ClientContextService _clientContextService;

    public AuthorizationMessageHandler(
        TokenService tokenService,
        TenantService tenantService,
        IHttpContextAccessor httpContextAccessor,
        ClientContextService clientContextService)
    {
        _tokenService = tokenService;
        _tenantService = tenantService;
        _httpContextAccessor = httpContextAccessor;
        _clientContextService = clientContextService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add Correlation and Causation IDs
        request.Headers.Add("X-Correlation-ID", _clientContextService.CorrelationId);
        request.Headers.Add("X-Causation-ID", _clientContextService.CausationId);

        var token = _tokenService.GetAccessToken(_tenantService.CurrentTenantId);
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
