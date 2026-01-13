using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging;
using Projects;

[assembly: Retry(3)]
[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace BookStore.AppHost.Tests;

public static class GlobalHooks
{
    public static DistributedApplication? App { get; private set; }
    public static ResourceNotificationService? NotificationService { get; private set; }
    public static string? AdminAccessToken { get; private set; }
    public static HttpClient? AdminHttpClient { get; private set; }

    [Before(TestSession)]
    public static async Task SetUp()
    {
        try
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>();
            _ = builder.Services.AddLogging(logging =>
            {
                _ = logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                _ = logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "[HH:mm:ss] ";
                });
            });

            App = await builder.BuildAsync();

            NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();

            await App.StartAsync();

            // Authenticate once and cache the token for all tests
            await AuthenticateAdminAsync();

        }
        catch
        {
            throw;
        }
    }

    static async Task AuthenticateAdminAsync()
    {
        if (App == null || NotificationService == null)
        {
            throw new InvalidOperationException("App or NotificationService is not initialized");
        }

        var httpClient = App.CreateHttpClient("apiservice");

        try
        {
            using var healthCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            _ = await NotificationService.WaitForResourceHealthyAsync("apiservice", healthCts.Token);

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("apiservice failed to become healthy", ex);
        }

        // Retry login mechanism to handle potential Seeding race condition
        HttpResponseMessage? loginResponse = null;
        for (var i = 0; i < 15; i++)
        {

            try
            {
                loginResponse = await httpClient.PostAsJsonAsync("/account/login", new
                {
                    Email = "admin@bookstore.com",
                    Password = "Admin123!"
                });

                if (loginResponse.IsSuccessStatusCode)
                {
                    break;
                }

                var errorContent = await loginResponse.Content.ReadAsStringAsync();

            }
#pragma warning disable RCS1075 // Avoid empty catch clause
            catch (Exception)
            {
                // Ignore login failures during retry loop
            }
#pragma warning restore RCS1075

            await Task.Delay(TestConstants.DefaultRetryDelay);
        }

        if (loginResponse == null || !loginResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to authenticate admin user after 15 attempts");
        }

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResult == null || string.IsNullOrEmpty(loginResult.AccessToken))
        {
            throw new InvalidOperationException("Failed to retrieve access token");
        }

        AdminAccessToken = loginResult.AccessToken;

        // Configure the shared HttpClient with the admin token
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminAccessToken);
        AdminHttpClient = httpClient;
    }

    [After(TestSession)]
    public static async Task CleanUp()
    {

        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }

    record LoginResponse(string AccessToken, string RefreshToken);
}
