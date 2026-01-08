using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure.Json;
using BookStore.Shared.Models;
using Marten;
using Marten.Events.Daemon;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Microsoft.Extensions.Options;
using Weasel.Core;
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
            var connectionString = configuration.GetConnectionString("bookstore");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'bookstore' not found. Please ensure the 'postgres' resource is correctly referenced.");
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

            return options;
        })
        .UseLightweightSessions()
        .AddAsyncDaemon(DaemonMode.Solo)
        .IntegrateWithWolverine();

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
        _ = options.Events.AddEventType<Events.PublisherRestored>();
    }

    static void RegisterProjections(StoreOptions options)
    {
        // Configure projections using SingleStreamProjection pattern
        // Simple projections use Inline lifecycle for immediate consistency (important for UI updates/SSE)
        // BookSearchProjection remains Async as it's more complex (indexes, heavy processing)
        _ = options.Projections.Snapshot<CategoryProjection>(SnapshotLifecycle.Inline);
        _ = options.Projections.Snapshot<AuthorProjection>(SnapshotLifecycle.Inline);
        _ = options.Projections.Snapshot<BookSearchProjection>(SnapshotLifecycle.Async);
        _ = options.Projections.Snapshot<PublisherProjection>(SnapshotLifecycle.Inline);
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
            .NgramIndex(x => x.AuthorNames);    // NGram search on authors

        // AuthorProjection indexes
        _ = options.Schema.For<AuthorProjection>()
            .Index(x => x.Name)         // B-tree index for sorting
            .NgramIndex(x => x.Name);           // NGram search on author name

        // Note: CategoryProjection no longer has a Name field - uses Translations dictionary
        // No indexes configured for CategoryProjection

        // PublisherProjection indexes
        _ = options.Schema.For<PublisherProjection>()
            .Index(x => x.Name)         // B-tree index for sorting
            .NgramIndex(x => x.Name);           // NGram search on publisher name
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
