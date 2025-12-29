using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure.Json;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Daemon;
using Marten.Events.Projections;
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
            var connectionString = configuration.GetConnectionString("bookstore")!;

            var options = new StoreOptions();
            options.Connection(connectionString);

            ConfigureEventMetadata(options);
            ConfigureJsonSerialization(options);
            RegisterEventTypes(options);
            RegisterProjections(options);
            ConfigureIndexes(options);

            return options;
        })
        .UseLightweightSessions()
        .IntegrateWithWolverine(cfg => cfg.UseWolverineManagedEventSubscriptionDistribution = true);

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
    {
        // Configure JSON serialization for Marten (database storage)
        // Enums stored as strings for readability and camelCase for JSON properties
        options.UseSystemTextJsonForSerialization(
            EnumStorage.AsString,
            Casing.CamelCase,
            configure: settings =>
                // Add custom converter for PartialDate to handle nullable values properly
                settings.Converters.Add(new PartialDateJsonConverter()));

        // Enable NGram search with unaccent for multilingual text search
        // This automatically enables pg_trgm and unaccent extensions
        options.Advanced.UseNGramSearchWithUnaccent = true;
    }

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
        // Configure projections - using AddAsync for async projections managed by Wolverine
        // Register projection builders explicitly with async lifecycle
        options.Projections.Add<AuthorProjectionBuilder>(ProjectionLifecycle.Async);
        options.Projections.Add<CategoryProjectionBuilder>(ProjectionLifecycle.Async);
        options.Projections.Add<PublisherProjectionBuilder>(ProjectionLifecycle.Async);
        options.Projections.Add<BookSearchProjectionBuilder>(ProjectionLifecycle.Async);
    }

    static void ConfigureIndexes(StoreOptions options)
    {
        // Configure indexes for search performance
        // Note: Trigram indexes for fuzzy search will be created via SQL migration

        // BookSearchProjection indexes
        _ = options.Schema.For<BookSearchProjection>()
            .Index(x => x.PublisherId)  // Standard B-tree index for exact matches
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
}
