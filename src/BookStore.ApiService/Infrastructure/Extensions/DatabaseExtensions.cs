using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static async Task EnsureDatabaseSchemaAsync(this WebApplication app)
    {
        // Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
        // This must remain blocking to ensure schema exists before requests are handled
        using (var scope = app.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        }
    }

    public static void RunDatabaseSeedingAsync(this WebApplication app)
    {
        // Start seeding in the background (don't block app startup)
        // We need seeding in all environments for now (including tests)
        _ = Task.Run(async () =>
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            var env = app.Services.GetRequiredService<IHostEnvironment>();
            var seedingEnabled = app.Configuration.GetValue("Seeding:Enabled", true);

            Log.Infrastructure.StartupTaskRunning(logger, env.EnvironmentName, seedingEnabled);

            var retryCount = 0;
            var maxRetries = 10;
            var retryDelay = TimeSpan.FromSeconds(2);

            while (retryCount < maxRetries)
            {
                try
                {
                    // Give the app a moment to start listening for health checks
                    await Task.Delay(100);

                    using var scope = app.Services.CreateScope();
                    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

                    Log.Infrastructure.DatabaseSeedingStarted(logger);

                    if (app.Configuration.GetValue("Seeding:Enabled", true))
                    {
                        var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
                        var seederLogger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseSeeder>>();
                        var seeder = new DatabaseSeeder(store, bus, seederLogger);

                        // 1. Ensure Tenants exist in the DB
                        await seeder.SeedTenantsAsync(TenantConstants.KnownTenants);

                        // 2. Refresh the list of tenants from the store (verifying it works)
                        var tenants = TenantConstants.KnownTenants;

                        foreach (var tenantId in tenants)
                        {
                            Log.Infrastructure.SeedingTenant(logger, tenantId);

                            await seeder.SeedAsync(tenantId);

                            // Wait for async projections to process the seeded events for this tenant
                            await WaitForProjectionsAsync(store, logger, tenantId);

                            // Seed sales AFTER projections are ready
                            await seeder.SeedSalesAsync(tenantId);

                            // Wait AGAIN for projections
                            await WaitForProjectionsAsync(store, logger, tenantId, expectSales: true);
                        }
                    }

                    Log.Infrastructure.DatabaseSeedingCompleted(logger);
                    return; // Success, exit loop
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Log.Infrastructure.DatabaseSeedingFailed(logger, ex);

                    if (retryCount >= maxRetries)
                    {
                        Log.Infrastructure.SeedingFailedMaxRetries(logger, ex, retryCount);
                        break;
                    }

                    Log.Infrastructure.SeedingFailedRetrying(logger, ex, retryCount, maxRetries, retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay);
                }
            }
        });
    }

    private static async Task WaitForProjectionsAsync(IDocumentStore store, ILogger logger, string tenantId, bool expectSales = false)
    {
        Log.Infrastructure.WaitingForProjections(logger);

        var timeout = TimeSpan.FromSeconds(30);
        var checkInterval = TimeSpan.FromMilliseconds(100);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            await using var session = store.QuerySession(tenantId);

            // Check if projections have data by querying the projection tables
            var bookCount = await session.Query<BookSearchProjection>().CountAsync();
            var authorCount = await session.Query<AuthorProjection>().CountAsync();
            var categoryCount = await session.Query<CategoryProjection>().CountAsync();
            var publisherCount = await session.Query<PublisherProjection>().CountAsync();

            var projectionsReady = bookCount > 0 && authorCount > 0 && categoryCount > 0 && publisherCount > 0;

            if (expectSales)
            {
                // If we expect sales, verify that at least one book has sales
                var hasSales = await session.Query<BookSearchProjection>().AnyAsync(b => b.Sales.Count > 0);
                projectionsReady = projectionsReady && hasSales;
            }

            if (projectionsReady)
            {
                Log.Infrastructure.ProjectionsReady(logger, bookCount, authorCount, categoryCount, publisherCount);
                return;
            }

            await Task.Delay(checkInterval);
        }

        Log.Infrastructure.ProjectionTimeout(logger, timeout.TotalSeconds);
    }
}
