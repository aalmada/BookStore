using System.Net.Http.Headers;
using Blazored.LocalStorage;
using BookStore.Shared;
using BookStore.Web.Services;

namespace BookStore.Web.Infrastructure;

/// <summary>
/// Auth handler for <see cref="BookStore.Client.ITenantsClient"/>.
/// Uses the default tenant's token to avoid the circular dependency:
/// TenantService → ITenantsClient → TenantHeaderHandler → TenantService.
/// Admin tenant endpoints always require a default-tenant admin, so the
/// default-tenant token is the correct one to use.
/// Falls back to localStorage hydration after a server restart that cleared
/// the in-memory TokenService.
/// </summary>
public class DefaultTenantAuthHandler(TokenService tokenService, ILocalStorageService localStorage) : DelegatingHandler
{
    static readonly string StorageKey = $"accessToken_{MultiTenancyConstants.DefaultTenantId}";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenService.GetAccessToken(MultiTenancyConstants.DefaultTenantId);

        if (string.IsNullOrEmpty(token))
        {
            try
            {
                var stored = await localStorage.GetItemAsync<string>(StorageKey, cancellationToken);
                if (!string.IsNullOrEmpty(stored))
                {
                    token = stored;
                    tokenService.SetTokens(MultiTenancyConstants.DefaultTenantId, token);
                }
            }
            catch (InvalidOperationException)
            {
                // JS interop not yet available during prerendering — skip.
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
