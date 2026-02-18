using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Shared;
using BookStore.Shared.Models;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events;
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
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>([
                "--RateLimit:Disabled=true",
                "--Seeding:Enabled=false",
                "--Email:DeliveryMethod=None",
                "--Jwt:ExpirationMinutes=240"
            ]);
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
        httpClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

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
                   opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);

                   opts.Connection(connectionString);
                   _ = opts.Policies.AllDocumentsAreMultiTenanted();

                   opts.Events.AppendMode = EventAppendMode.Quick;
                   opts.Events.UseArchivedStreamPartitioning = true;
                   opts.Events.EnableEventSkippingInProjectionsOrSubscriptions = true;

                   opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                   opts.Events.MetadataConfig.CausationIdEnabled = true;
                   opts.Events.MetadataConfig.HeadersEnabled = true;
                   opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
               }))
        {
            // Tenant documents use Marten's native default tenant bucket
            await using var tenantSession = store.LightweightSession();
            var defaultTenantId = StorageConstants.DefaultTenantId;

            var existingDefault = await tenantSession.LoadAsync<BookStore.ApiService.Models.Tenant>(defaultTenantId);
            if (existingDefault == null)
            {
                tenantSession.Store(new BookStore.ApiService.Models.Tenant
                {
                    Id = defaultTenantId,
                    Name = "BookStore",
                    IsEnabled = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existingDefault.Name = "BookStore";
                tenantSession.Update(existingDefault);
            }

            await tenantSession.SaveChangesAsync();

            // Seed minimal books for testing (default tenant)
            await SeedBooksAsync(store, StorageConstants.DefaultTenantId);

            // Only seed the default tenant admin directly.
            // Other tenant admins are created via the API as part of tenant creation (full tenant isolation).
            await SeedTenantAdminAsync(store, StorageConstants.DefaultTenantId);
        }

        // Retry login mechanism (less aggressive now that we control seeding)
        HttpResponseMessage? loginResponse = null;
        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            try
            {
                loginResponse = await httpClient.PostAsJsonAsync("/account/login",
                    new { Email = $"admin@{MultiTenancyConstants.DefaultTenantAlias}.com", Password = "Admin123!" });
                return loginResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }, TimeSpan.FromSeconds(60), "Failed to authenticate admin user after startup");

        var loginResult = await loginResponse!.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResult == null || string.IsNullOrEmpty(loginResult.AccessToken))
        {
            throw new InvalidOperationException("Failed to retrieve access token");
        }

        AdminAccessToken = loginResult.AccessToken;

        // Configure the shared HttpClient with the admin token
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminAccessToken);
        AdminHttpClient = httpClient;
    }

    static async Task SeedTenantAdminAsync(IDocumentStore store, string tenantId)
    {
        await using var session = store.LightweightSession(tenantId);
        var tenantAlias = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? MultiTenancyConstants.DefaultTenantAlias
            : tenantId;
        var adminEmail = $"admin@{tenantAlias}.com";

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

            var hasher =
                new Microsoft.AspNetCore.Identity.PasswordHasher<BookStore.ApiService.Models.ApplicationUser>();
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

            session.Store(adminUser);
            await session.SaveChangesAsync();
        }
    }

    static async Task SeedBooksAsync(IDocumentStore store, string tenantId)
    {
        await using var session = store.LightweightSession(tenantId);

        // Check if already seeded
        if (await session.Events.QueryRawEventDataOnly<BookAdded>().AnyAsync())
        {
            return;
        }

        var publisherId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        // Seed Publisher
        var publisherEvent = new PublisherAdded(publisherId, "Test Publisher", DateTimeOffset.UtcNow);
        _ = session.Events.StartStream<PublisherAggregate>(publisherId, publisherEvent);

        // Seed Author
        var authorEvent = new AuthorAdded(authorId, "Test Author",
            new Dictionary<string, AuthorTranslation> { ["en"] = new("Test Bio") }, DateTimeOffset.UtcNow);
        _ = session.Events.StartStream<AuthorAggregate>(authorId, authorEvent);

        // Seed Category
        var categoryEvent = new CategoryAdded(categoryId,
            new Dictionary<string, CategoryTranslation> { ["en"] = new("Test Category") }, DateTimeOffset.UtcNow);
        _ = session.Events.StartStream<CategoryAggregate>(categoryId, categoryEvent);

        // Seed Books
        for (var i = 1; i <= 5; i++)
        {
            var bookId = Guid.NewGuid();
            var bookEvent = new BookAdded(
                bookId,
                $"Test Book {i}",
                null,
                "en",
                new Dictionary<string, BookTranslation> { ["en"] = new($"Description for Test Book {i}") },
                new PartialDate(2023),
                publisherId,
                [authorId],
                [categoryId],
                new Dictionary<string, decimal> { ["USD"] = 10m + i }
            );
            _ = session.Events.StartStream<BookAggregate>(bookId, bookEvent);
        }

        await session.SaveChangesAsync();
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
