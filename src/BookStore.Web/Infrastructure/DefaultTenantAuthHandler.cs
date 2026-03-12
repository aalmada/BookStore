using System.Net.Http.Headers;
using BookStore.Shared;
using BookStore.Web.Services;

namespace BookStore.Web.Infrastructure;

/// <summary>
/// Auth handler for <see cref="BookStore.Client.ITenantsClient"/>.
/// Uses the default tenant's token to avoid the circular dependency:
/// TenantService → ITenantsClient → TenantHeaderHandler → TenantService.
/// Admin tenant endpoints always require a default-tenant admin, so the
/// default-tenant token is the correct one to use.
/// </summary>
public class DefaultTenantAuthHandler(TokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenService.GetAccessToken(MultiTenancyConstants.DefaultTenantId);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
