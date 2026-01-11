using System.Net.Http.Headers;
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

    public AuthorizationMessageHandler(TokenService tokenService, IHttpContextAccessor httpContextAccessor)
    {
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
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

        return await base.SendAsync(request, cancellationToken);
    }
}
