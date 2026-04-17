using System.Net.Http.Headers;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.AppHost.Tests.Helpers;
using BookStore.ServiceDefaults;
using BookStore.Shared;
using BookStore.Shared.Models;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.Configuration;
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
    public static string KeycloakAdminUsername { get; private set; } = string.Empty;
    public static string KeycloakAdminPassword { get; private set; } = string.Empty;

    [Before(TestSession)]
    public static async Task SetUp()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>([
            "--RateLimit:Disabled=true",
            "--Seeding:Enabled=false",
            "--Email:DeliveryMethod=None",
            "--Jwt:ExpirationMinutes=240"
        ]);

        _ = builder.Services.AddLogging(logging =>
        {
            _ = logging.SetMinimumLevel(LogLevel.Information);
            _ = logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
        });

        App = await builder.BuildAsync();
        NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();
        var configuration = App.Services.GetRequiredService<IConfiguration>();

        KeycloakAdminUsername = ResolveConfigurationValue(
            configuration,
            [
                "Keycloak:Admin:AdminUsername",
                "Parameters:keycloak-admin"
            ])
            // safe: Aspire Keycloak defaults the master admin username to "admin" in local dev/test.
            ?? "admin";

        KeycloakAdminPassword = ResolveConfigurationValue(
            configuration,
            [
                "Keycloak:Admin:AdminPassword",
                "Parameters:keycloak-password",
                "Parameters:keycloak-admin-password"
            ]) ?? throw new InvalidOperationException(
            "Could not resolve the Keycloak admin password from runtime configuration.");

        await App.StartAsync();
        await AuthenticateAdminAsync();
    }

    static async Task AuthenticateAdminAsync()
    {
        if (App == null || NotificationService == null)
        {
            throw new InvalidOperationException("App or NotificationService is not initialized");
        }

        try
        {
            using var healthCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            _ = await NotificationService.WaitForResourceHealthyAsync(ResourceNames.ApiService, healthCts.Token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{ResourceNames.ApiService} failed to become healthy", ex);
        }

        using var keycloakClient = App.CreateHttpClient(ResourceNames.Keycloak);
        await WaitForKeycloakReadyAsync(keycloakClient);

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
            await SeedBooksAsync(store, StorageConstants.DefaultTenantId);
        }

        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);
        var loginResult = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);
        if (loginResult == null || string.IsNullOrEmpty(loginResult.AccessToken))
        {
            throw new InvalidOperationException("Failed to retrieve access token");
        }

        AdminAccessToken = loginResult.AccessToken;

        var httpClient = App.CreateHttpClient(ResourceNames.ApiService);
        httpClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminAccessToken);
        AdminHttpClient = httpClient;
    }

    static async Task WaitForKeycloakReadyAsync(HttpClient keycloakClient)
    {
        Exception? lastException = null;
        try
        {
            await SseEventHelpers.WaitForConditionAsync(
                async () =>
                {
                    try
                    {
                        using var response = await keycloakClient.GetAsync(
                            "/realms/bookstore/.well-known/openid-configuration");

                        if (response.IsSuccessStatusCode)
                        {
                            return true;
                        }

                        lastException = new InvalidOperationException(
                            $"Keycloak readiness probe returned {(int)response.StatusCode}.");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        return false;
                    }
                },
                TimeSpan.FromSeconds(120),
                "Keycloak failed to become ready within 120 seconds.");
        }
        catch (Exception) when (lastException is not null)
        {
            throw new InvalidOperationException("Keycloak failed to become ready within 120 seconds.", lastException);
        }
    }

    static string? ResolveConfigurationValue(IConfiguration configuration, string[] keys)
    {
        foreach (var key in keys)
        {
            var configurationValue = configuration[key];
            if (!string.IsNullOrWhiteSpace(configurationValue))
            {
                return configurationValue;
            }

            var environmentKey = key.Replace(':', '_').Replace('-', '_').ToUpperInvariant();
            var environmentValue = Environment.GetEnvironmentVariable(environmentKey);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue;
            }
        }

        return null;
    }

    static async Task SeedBooksAsync(IDocumentStore store, string tenantId)
    {
        await using var session = store.LightweightSession(tenantId);

        if (await session.Events.QueryRawEventDataOnly<BookAdded>().AnyAsync())
        {
            return;
        }

        var publisherId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();

        var publisherEvent = new PublisherAdded(publisherId, "Test Publisher", DateTimeOffset.UtcNow);
        _ = session.Events.StartStream<PublisherAggregate>(publisherId, publisherEvent);

        var authorEvent = new AuthorAdded(authorId, "Test Author",
            new Dictionary<string, AuthorTranslation> { ["en"] = new("Test Bio") }, DateTimeOffset.UtcNow);
        _ = session.Events.StartStream<AuthorAggregate>(authorId, authorEvent);

        var categoryEvent = new CategoryAdded(categoryId,
            new Dictionary<string, CategoryTranslation> { ["en"] = new("Test Category") }, DateTimeOffset.UtcNow);
        _ = session.Events.StartStream<CategoryAggregate>(categoryId, categoryEvent);

        for (var i = 1; i <= 5; i++)
        {
            var bookId = Guid.CreateVersion7();
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
                new Dictionary<string, decimal> { ["GBP"] = 10m + i }
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
}
