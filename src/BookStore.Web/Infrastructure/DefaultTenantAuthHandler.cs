using System.Net.Http.Headers;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookStore.Web.Infrastructure;

/// <summary>
/// Auth handler for <see cref="BookStore.Client.ITenantsClient"/>.
/// Uses the default tenant's token to avoid the circular dependency:
/// TenantService -> ITenantsClient -> TenantHeaderHandler -> TenantService.
/// The token is read from a cache keyed by the authenticated user sub claim.
/// </summary>
public class DefaultTenantAuthHandler(
    KeycloakTokenAccessor tokenAccessor,
    AuthenticationStateProvider authenticationStateProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userSub = authState.User.FindFirst("sub")?.Value;
        var token = await tokenAccessor.GetAccessTokenAsync(userSub ?? string.Empty);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
