using System.Net.Http.Headers;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Provides common helper methods for integration tests.
/// </summary>
public static class TestHelpers
{
    public static async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        
        await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Create a new HttpClient for this test (to avoid concurrency issues)
        var httpClient = app.CreateHttpClient("apiservice");
        
        // Use the shared admin access token from GlobalSetup
        if (string.IsNullOrEmpty(GlobalHooks.AdminAccessToken))
        {
            throw new InvalidOperationException("Admin access token is not available");
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        return httpClient;
    }

    public static HttpClient GetUnauthenticatedClient()
    {
        var app = GlobalHooks.App!;
        return app.CreateHttpClient("apiservice");
    }
}
