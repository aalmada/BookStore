using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging;
using Projects;
using System.Net.Http.Json;

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
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>();
        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            logging.AddSimpleConsole(options => 
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
        });

        // Disable resilience handler for tests to avoid circuit breaker issues during parallel execution
        // builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        // {
        //     clientBuilder.AddStandardResilienceHandler();
        // });

        App = await builder.BuildAsync();
        
        NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();
        await App.StartAsync();

        // Authenticate once and cache the token for all tests
        await AuthenticateAdminAsync();
    }

    private static async Task AuthenticateAdminAsync()
    {
        if (App == null || NotificationService == null)
        {
            throw new InvalidOperationException("App or NotificationService is not initialized");
        }

        var httpClient = App.CreateHttpClient("apiservice");
        await NotificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Retry login mechanism to handle potential Seeding race condition
        HttpResponseMessage? loginResponse = null;
        for (int i = 0; i < 15; i++)
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

            await Task.Delay(1000);
        }

        if (loginResponse == null || !loginResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to authenticate admin user");
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

    private record LoginResponse(string AccessToken, string RefreshToken);
}
