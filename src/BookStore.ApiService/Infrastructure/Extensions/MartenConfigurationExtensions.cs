using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure.Json;
using BookStore.Shared.Models;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Daemon;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Weasel.Core;
using Weasel.Postgresql;
using Wolverine;
using Wolverine.Marten;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring Marten event store
/// </summary>
public static class MartenConfigurationExtensions
{
    /// <summary>
    /// Configures Marten for event sourcing with projections and indexes
    /// </summary>
    public static IServiceCollection AddMartenEventStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = services.AddMarten(sp =>
        {
            // Get connection string from Aspire
            var connectionString = configuration.GetConnectionString(BookStore.ServiceDefaults.ResourceNames.BookStoreDb);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{BookStore.ServiceDefaults.ResourceNames.BookStoreDb}' not found. Please ensure the '{BookStore.ServiceDefaults.ResourceNames.Postgres}' resource is correctly referenced.");
            }

            var env = sp.GetRequiredService<IHostEnvironment>();

            var options = new StoreOptions();
            options.Connection(connectionString);

            // Configure automatic schema creation/updates based on environment
            // Development: All - creates/updates/drops schema objects as needed
            // Production: CreateOnly - only creates missing objects, never modifies existing
            options.AutoCreateSchemaObjects = env.IsDevelopment()
                ? AutoCreate.All
                : AutoCreate.CreateOnly;

            ConfigureEventMetadata(options);
            ConfigureJsonSerialization(options);
            RegisterEventTypes(options);
            RegisterProjections(options);
            ConfigureIndexes(options);
            RegisterChangeListeners(options, sp);

            // Enable Multi-Tenancy (Conjoined - same DB, different tenant_id column)
            options.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;

            // Enforce multi-tenancy on all documents
            _ = options.Policies.AllDocumentsAreMultiTenanted();

            // Tenant documents are excluded from multi-tenancy via [DoNotPartition] attribute

            return options;
        })
        .AddAsyncDaemon(DaemonMode.Solo)
        .IntegrateWithWolverine();

        // Register IDocumentSession with proper tenant scoping
        // Since we use Marten's "*DEFAULT*" for the default tenant,
        // we can pass tenant ID directly to LightweightSession
        _ = services.AddScoped<IDocumentSession>(sp =>
        {
            var store = sp.GetRequiredService<IDocumentStore>();

            // If running within a Wolverine message handler, use the envelope's tenant ID
            var messageContext = sp.GetService<IMessageContext>();
            if (messageContext?.TenantId != null)
            {
                return store.LightweightSession(messageContext.TenantId);
            }

            // Otherwise fall back to ASP.NET Core tenant context
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            return store.LightweightSession(tenantContext.TenantId);
        });

        // Also register IQuerySession just in case
        _ = services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentSession>());

        return services;
    }

    static void ConfigureEventMetadata(StoreOptions options)
    {
        // Enable metadata storage for correlation/causation tracking
        options.Events.MetadataConfig.CorrelationIdEnabled = true;
        options.Events.MetadataConfig.CausationIdEnabled = true;
        options.Events.MetadataConfig.HeadersEnabled = true;
    }

    static void ConfigureJsonSerialization(StoreOptions options)
        // Configure JSON serialization for Marten (database storage)
        // Enums stored as strings for readability and camelCase for JSON properties
        => options.UseSystemTextJsonForSerialization(
            EnumStorage.AsString,
            Casing.CamelCase,
            configure: settings =>
                // Add custom converter for PartialDate to handle nullable values properly
                settings.Converters.Add(new PartialDateJsonConverter()));

    static void RegisterEventTypes(StoreOptions options)
    {
        // Book events
        _ = options.Events.AddEventType<Events.BookAdded>();
        _ = options.Events.AddEventType<Events.BookUpdated>();
        _ = options.Events.AddEventType<Events.BookSoftDeleted>();
        _ = options.Events.AddEventType<Events.BookRestored>();
        _ = options.Events.AddEventType<Events.BookCoverUpdated>();

        // Author events
        _ = options.Events.AddEventType<Events.AuthorAdded>();
        _ = options.Events.AddEventType<Events.AuthorUpdated>();
        _ = options.Events.AddEventType<Events.AuthorSoftDeleted>();
        _ = options.Events.AddEventType<Events.AuthorRestored>();

        // Category events
        _ = options.Events.AddEventType<Events.CategoryAdded>();
        _ = options.Events.AddEventType<Events.CategoryUpdated>();
        _ = options.Events.AddEventType<Events.CategorySoftDeleted>();
        _ = options.Events.AddEventType<Events.CategoryRestored>();

        // Publisher events
        _ = options.Events.AddEventType<Events.PublisherAdded>();
        _ = options.Events.AddEventType<Events.PublisherUpdated>();
        _ = options.Events.AddEventType<Events.PublisherSoftDeleted>();
        _ = options.Events.AddEventType<Events.PublisherSoftDeleted>();
        _ = options.Events.AddEventType<Events.PublisherRestored>();

        // User events
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.UserProfileCreated>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.BookAddedToFavorites>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.BookRemovedFromFavorites>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.BookRated>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.BookRatingRemoved>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.BookAddedToCart>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.BookRemovedFromCart>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.CartItemQuantityUpdated>();
        _ = options.Events.AddEventType<BookStore.Shared.Messages.Events.ShoppingCartCleared>();
    }

    static void RegisterProjections(StoreOptions options)
    {
        // Configure projections using SingleStreamProjection pattern
        // Simple projections use Async lifecycle for eventual consistency
        _ = options.Projections.Snapshot<CategoryProjection>(SnapshotLifecycle.Async);
        _ = options.Projections.Snapshot<AuthorProjection>(SnapshotLifecycle.Async);
        _ = options.Projections.Snapshot<BookSearchProjection>(SnapshotLifecycle.Async);
        _ = options.Projections.Snapshot<PublisherProjection>(SnapshotLifecycle.Async);
        _ = options.Projections.Snapshot<UserProfile>(SnapshotLifecycle.Inline);
        options.Projections.Add<BookStatisticsProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<AuthorStatisticsProjectionBuilder>(ProjectionLifecycle.Async);
        options.Projections.Add<CategoryStatisticsProjectionBuilder>(ProjectionLifecycle.Async);
        options.Projections.Add<PublisherStatisticsProjectionBuilder>(ProjectionLifecycle.Async);
    }

    static void ConfigureIndexes(StoreOptions options)
    {
        // Configure indexes for search performance
        // Note: Trigram indexes for fuzzy search will be created via SQL migration

        // BookSearchProjection indexes
        _ = options.Schema.For<BookSearchProjection>()
            .Index(x => x.PublisherId!)  // Standard B-tree index for exact matches
            .Index(x => x.Title)        // B-tree index for sorting
            .GinIndexJsonData()        // GIN index for JSON fields
            .NgramIndex(x => x.Title)           // NGram search on title
            .NgramIndex(x => x.AuthorNames)    // NGram search on authors
            .Index(x => x.Deleted);             // Index for soft-delete filtering

        // AuthorProjection indexes
        _ = options.Schema.For<AuthorProjection>()
            .Index(x => x.Name)         // B-tree index for sorting
            .NgramIndex(x => x.Name)    // NGram search on author name
            .Index(x => x.Deleted);     // Index for soft-delete filtering

        // Note: CategoryProjection no longer has a Name field - uses Translations dictionary
        _ = options.Schema.For<CategoryProjection>()
             .Index(x => x.Deleted);    // Index for soft-delete filtering

        // PublisherProjection indexes
        _ = options.Schema.For<PublisherProjection>()
            .Index(x => x.Name)         // B-tree index for sorting
            .NgramIndex(x => x.Name)    // NGram search on publisher name
            .Index(x => x.Deleted);     // Index for soft-delete filtering

        // ApplicationUser indexes (Identity)
        _ = options.Schema.For<ApplicationUser>()
            .UniqueIndex(x => x.Email!)
            .Index(x => x.NormalizedEmail)
            .Index(x => x.NormalizedUserName)
            .GinIndexJsonData()
            .NgramIndex(x => x.Email!);
    }

    static void RegisterChangeListeners(StoreOptions options, IServiceProvider sp)
    {
        // Register commit listener for cache invalidation and SSE notifications
        // This ensures actions are taken AFTER read models are updated
        var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
        var notificationService = sp.GetRequiredService<Notifications.INotificationService>();
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProjectionCommitListener>>();
        var listener = new ProjectionCommitListener(cache, notificationService, logger);
        options.Listeners.Add(listener);
        options.Projections.AsyncListeners.Add(listener);
    }
}
