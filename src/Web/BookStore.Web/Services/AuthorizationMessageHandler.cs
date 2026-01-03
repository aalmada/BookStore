using System.Net.Http.Headers;
using BookStore.Web.Services;

namespace BookStore.Web.Services;

/// <summary>
/// HTTP message handler that attaches the JWT access token to outgoing requests
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly TokenService _tokenService;

    public AuthorizationMessageHandler(TokenService tokenService) => _tokenService = tokenService;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokenService.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
