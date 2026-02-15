using System.Net.Http.Headers;
using Aspire.Hosting;
using BookStore.ApiService.Infrastructure.Tenant;
using JasperFx;
using Refit;

namespace BookStore.AppHost.Tests.Helpers;

public static class HttpClientHelpers
{
    public static HttpClient GetAuthenticatedClient(string accessToken)
    {
        var client = GetUnauthenticatedClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static HttpClient GetAuthenticatedClient(string accessToken, string tenantId)
    {
        var client = GetUnauthenticatedClient(tenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        return await Task.FromResult(client);
    }

    /// <summary>
    /// Gets an authenticated HTTP client for the API service using the global admin token.
    /// </summary>
    /// <typeparam name="T">The Refit interface type to create a client for.</typeparam>
    /// <returns>A Refit client instance configured with admin authentication.</returns>
    public static async Task<T> GetAuthenticatedClientAsync<T>()
    {
        var httpClient = await GetAuthenticatedClientAsync();
        return RestService.For<T>(httpClient);
    }

    public static HttpClient GetUnauthenticatedClient()
        => GetUnauthenticatedClient(StorageConstants.DefaultTenantId);

    public static HttpClient GetUnauthenticatedClient(string tenantId)
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        return client;
    }

    public static T GetUnauthenticatedClient<T>()
    {
        var httpClient = GetUnauthenticatedClient();
        return RestService.For<T>(httpClient);
    }

    public static T GetUnauthenticatedClientWithLanguage<T>(string language)
    {
        var httpClient = GetUnauthenticatedClient();
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        return RestService.For<T>(httpClient);
    }

    public static async Task<HttpClient> GetTenantClientAsync(string tenantId, string accessToken)
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        return await Task.FromResult(client);
    }
}
