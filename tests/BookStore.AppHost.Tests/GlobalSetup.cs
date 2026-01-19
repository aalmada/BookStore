using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Marten;
using Microsoft.Extensions.Logging;
using Projects;
using Weasel.Core;
using Weasel.Postgresql;

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
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>(["--Seeding:Enabled=false"]);
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

        // Manually seed the default admin user since automatic seeding is disabled
        // This ensures tests are self-contained and don't rely on background processes
        var connectionString = await App.GetConnectionStringAsync("bookstore");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not retrieve connection string for 'bookstore' resource.");
        }

        using (var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            // Configure Multi-Tenancy (Conjoined)
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        }))
        {
            // We reuse the logic from DatabaseSeeder (or duplicate it for isolation)
            // Here we duplicate the critical part to avoid dependency on internal implementation details of DatabaseSeeder
            await using var session = store.LightweightSession("default");
            var adminEmail = "admin@bookstore.com";

            var existingAdmin = await session.Query<BookStore.ApiService.Models.ApplicationUser>()
                .Where(u => u.Email == adminEmail)
                .FirstOrDefaultAsync();

            if (existingAdmin == null)
            {
                var adminUser = new BookStore.ApiService.Models.ApplicationUser
                {
                    UserName = adminEmail,
                    NormalizedUserName = adminEmail.ToUpperInvariant(),
                    Email = adminEmail,
                    NormalizedEmail = adminEmail.ToUpperInvariant(),
                    EmailConfirmed = true,
                    Roles = ["Admin"],
                    SecurityStamp = Guid.CreateVersion7().ToString("D"),
                    ConcurrencyStamp = Guid.CreateVersion7().ToString("D")
                };

                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<BookStore.ApiService.Models.ApplicationUser>();
                adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

                session.Store(adminUser);
                await session.SaveChangesAsync();
            }
        }

        // Retry login mechanism (less aggressive now that we control seeding)
        HttpResponseMessage? loginResponse = null;
        for (var i = 0; i < 30; i++)
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
