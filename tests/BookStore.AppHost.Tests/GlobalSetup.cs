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
        Console.WriteLine("[GLOBAL-SETUP] Starting SetUp...");

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

            Console.WriteLine("[GLOBAL-SETUP] Building application...");
            App = await builder.BuildAsync();

            Console.WriteLine("[GLOBAL-SETUP] Getting ResourceNotificationService...");
            NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();

            Console.WriteLine("[GLOBAL-SETUP] Starting application...");
            await App.StartAsync();

            Console.WriteLine("[GLOBAL-SETUP] Authenticating admin...");
            // Authenticate once and cache the token for all tests
            await AuthenticateAdminAsync();
            Console.WriteLine("[GLOBAL-SETUP] SetUp completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GLOBAL-SETUP] FATAL ERROR: {ex.Message}");
            Console.WriteLine($"[GLOBAL-SETUP] Stack trace: {ex.StackTrace}");
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
        Console.WriteLine("[GLOBAL-SETUP] Waiting for apiservice to be healthy...");

        try
        {
            using var healthCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            _ = await NotificationService.WaitForResourceHealthyAsync("apiservice", healthCts.Token);
            Console.WriteLine("[GLOBAL-SETUP] apiservice is healthy.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GLOBAL-SETUP] Health check failed: {ex.Message}");
            throw new InvalidOperationException("apiservice failed to become healthy", ex);
        }

        // Retry login mechanism to handle potential Seeding race condition
        HttpResponseMessage? loginResponse = null;
        for (var i = 0; i < 15; i++)
        {
            Console.WriteLine($"[GLOBAL-SETUP] Login attempt {i + 1}/15...");
            try
            {
                loginResponse = await httpClient.PostAsJsonAsync("/account/login", new
                {
                    Email = "admin@bookstore.com",
                    Password = "Admin123!"
                });

                if (loginResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("[GLOBAL-SETUP] Login successful.");
                    break;
                }

                var errorContent = await loginResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[GLOBAL-SETUP] Login failed with status: {loginResponse.StatusCode}, body: {errorContent}");
            }
#pragma warning disable RCS1075 // Avoid empty catch clause
            catch (Exception)
            {
                // Ignore login failures during retry loop
            }
#pragma warning restore RCS1075

            await Task.Delay(1000);
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
        Console.WriteLine("[GLOBAL-SETUP] Admin access token retrieved.");

        // Configure the shared HttpClient with the admin token
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminAccessToken);
        AdminHttpClient = httpClient;
    }

    [After(TestSession)]
    public static async Task CleanUp()
    {
        Console.WriteLine("[GLOBAL-SETUP] Cleaning up...");
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }

    record LoginResponse(string AccessToken, string RefreshToken);
}
