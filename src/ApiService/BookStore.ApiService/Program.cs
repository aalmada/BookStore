using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Endpoints.Admin;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Projections;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Daemon;
using Marten.Events.Projections;
using Scalar.AspNetCore;
using Weasel.Core;
using Wolverine;
using Wolverine.Marten;
using Wolverine.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Azure Blob Storage client (Azurite locally, Azure in production)
builder.AddAzureBlobServiceClient("blobs");

// Configure JSON serialization for consistent API responses
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Use web defaults (camelCase properties)
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

    // Serialize enums as strings (not integers) for better readability and API evolution
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));

    // Pretty print in development for easier debugging
    options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();

    // ISO 8601 date/time format is default in System.Text.Json
    // DateTimeOffset automatically serializes as: "2025-12-26T17:16:09.123Z"
});

// Add services to the container.
builder.Services.AddProblemDetails();

// Configure pagination options
builder.Services.Configure<BookStore.ApiService.Models.PaginationOptions>(
    builder.Configuration.GetSection(BookStore.ApiService.Models.PaginationOptions.SectionName));

// Configure OpenAPI with metadata
builder.Services.AddOpenApi(options => options.AddBookStoreApiDocumentation());

// Configure API Versioning (header-based)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new Asp.Versioning.HeaderApiVersionReader("api-version");
});

// Add localization services
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en", "pt", "es", "fr", "de" };
    _ = options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Configure Marten for event sourcing
builder.Services.AddMarten(sp =>
{
    // Get connection string from Aspire
    var connectionString = builder.Configuration.GetConnectionString("bookstore")!;

    var options = new StoreOptions();
    options.Connection(connectionString);

    // Enable metadata storage for correlation/causation tracking
    options.Events.MetadataConfig.CorrelationIdEnabled = true;
    options.Events.MetadataConfig.CausationIdEnabled = true;
    options.Events.MetadataConfig.HeadersEnabled = true;

    // Configure JSON serialization for Marten (database storage)
    // Enums stored as strings for readability and camelCase for JSON properties
    options.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);

    // Enable NGram search with unaccent for multilingual text search
    // This automatically enables pg_trgm and unaccent extensions
    options.Advanced.UseNGramSearchWithUnaccent = true;

    // Register event types
    _ = options.Events.AddEventType<BookStore.ApiService.Events.BookAdded>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.BookUpdated>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.BookSoftDeleted>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.BookRestored>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.BookCoverUpdated>();

    _ = options.Events.AddEventType<BookStore.ApiService.Events.AuthorAdded>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.AuthorUpdated>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.AuthorSoftDeleted>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.AuthorRestored>();

    _ = options.Events.AddEventType<BookStore.ApiService.Events.CategoryAdded>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.CategoryUpdated>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.CategorySoftDeleted>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.CategoryRestored>();

    _ = options.Events.AddEventType<BookStore.ApiService.Events.PublisherAdded>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.PublisherUpdated>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.PublisherSoftDeleted>();
    _ = options.Events.AddEventType<BookStore.ApiService.Events.PublisherRestored>();

    // Configure projections - using AddAsync for async projections managed by Wolverine
    // Register projection builders explicitly with async lifecycle
    options.Projections.Add<AuthorProjectionBuilder>(ProjectionLifecycle.Async);
    options.Projections.Add<CategoryProjectionBuilder>(ProjectionLifecycle.Async);
    options.Projections.Add<PublisherProjectionBuilder>(ProjectionLifecycle.Async);
    options.Projections.Add<BookSearchProjectionBuilder>(ProjectionLifecycle.Async);

    // Configure indexes for search performance
    // Note: Trigram indexes for fuzzy search will be created via SQL migration
    _ = options.Schema.For<BookSearchProjection>()
        .Index(x => x.PublisherId)  // Standard B-tree index for exact matches
        .Index(x => x.Title)        // B-tree index for sorting
        .GinIndexJsonData();        // GIN index for JSON fields

    // Indexes for AuthorProjection
    _ = options.Schema.For<AuthorProjection>()
        .Index(x => x.Name);         // B-tree index for sorting

    // Indexes for CategoryProjection
    _ = options.Schema.For<CategoryProjection>()
        .Index(x => x.Name);         // B-tree index for sorting

    // Indexes for PublisherProjection
    _ = options.Schema.For<PublisherProjection>()
        .Index(x => x.Name);         // B-tree index for sorting

    // Configure NGram indexes for text search (accent-insensitive)
    _ = options.Schema.For<BookSearchProjection>()
        .NgramIndex(x => x.Title)           // NGram search on title
        .NgramIndex(x => x.AuthorNames);    // NGram search on authors

    _ = options.Schema.For<AuthorProjection>()
        .NgramIndex(x => x.Name);           // NGram search on author name

    _ = options.Schema.For<CategoryProjection>()
        .NgramIndex(x => x.Name);           // NGram search on category name

    _ = options.Schema.For<PublisherProjection>()
        .NgramIndex(x => x.Name);           // NGram search on publisher name

    return options;
})
.UseLightweightSessions()
.IntegrateWithWolverine(cfg => cfg.UseWolverineManagedEventSubscriptionDistribution = true);

// Add Wolverine with command/handler pattern
builder.Services.AddWolverine(opts =>
{
    // Auto-discover handlers in this assembly
    _ = opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // Explicitly include static handler classes for discovery
    _ = opts.Discovery.IncludeType(typeof(BookStore.ApiService.Handlers.Authors.AuthorHandlers));
    _ = opts.Discovery.IncludeType(typeof(BookStore.ApiService.Handlers.Books.BookHandlers));
    _ = opts.Discovery.IncludeType(typeof(BookStore.ApiService.Handlers.Books.BookCoverHandlers));
    _ = opts.Discovery.IncludeType(typeof(BookStore.ApiService.Handlers.Categories.CategoryHandlers));
    _ = opts.Discovery.IncludeType(typeof(BookStore.ApiService.Handlers.Publishers.PublisherHandlers));

    // Enable SignalR transport for real-time notifications
    _ = opts.UseSignalR();

    // Route domain event notifications to SignalR
    opts.Publish(x =>
    {
        x.MessagesImplementing<BookStore.ApiService.Events.Notifications.IDomainEventNotification>();
        _ = x.ToSignalR();
    });

    // Policies for automatic behavior
    opts.Policies.AutoApplyTransactions();
});

// Add SignalR for real-time notifications
builder.Services.AddSignalR();

// Add Blob Storage service
builder.Services.AddSingleton<BookStore.ApiService.Services.BlobStorageService>();

// Add Marten health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("bookstore")!);

// Add response caching for performance
builder.Services.AddResponseCaching();
builder.Services.AddOutputCache();

var app = builder.Build();

// Seed database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    
    // Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    
    var seeder = new DatabaseSeeder(store);
    await seeder.SeedAsync();
    
    // Give async projections time to process the seeded events
    // In production, projections run continuously in the background
    await Task.Delay(TimeSpan.FromSeconds(2));
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Add Marten metadata middleware to set correlation/causation IDs
app.UseMartenMetadata();

// Add logging enricher middleware to add metadata to all logs
app.UseLoggingEnricher();

// Map OpenAPI endpoint and configure Scalar UI
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    _ = app.MapScalarApiReference("/api-reference",
    options => options
            .WithTitle("Book Store API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.UseResponseCaching();
app.UseOutputCache();

// Map endpoints
app.MapGet("/", () => "Book Store API is running. Visit /scalar/v1 for API documentation.")
    .ExcludeFromDescription();

// Create API version set for v1
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new Asp.Versioning.ApiVersion(1))
    .ReportApiVersions()
    .Build();

// Public API endpoints (v1)
var publicApi = app.MapGroup("/api")
    .WithApiVersionSet(apiVersionSet);

publicApi.MapGroup("/books")
    .MapBookEndpoints()
    .WithTags("Books");

publicApi.MapGroup("/authors")
    .MapAuthorEndpoints()
    .WithTags("Authors");

publicApi.MapGroup("/categories")
    .MapCategoryEndpoints()
    .WithTags("Categories");

publicApi.MapGroup("/publishers")
    .MapPublisherEndpoints()
    .WithTags("Publishers");

// Admin API endpoints (v1)
var adminApi = app.MapGroup("/api/admin")
    .WithApiVersionSet(apiVersionSet);

adminApi.MapGroup("/books")
    .MapAdminBookEndpoints()
    .WithTags("Admin - Books");

adminApi.MapGroup("/authors")
    .MapAdminAuthorEndpoints()
    .WithTags("Admin - Authors");

adminApi.MapGroup("/categories")
    .MapAdminCategoryEndpoints()
    .WithTags("Admin - Categories");

adminApi.MapGroup("/publishers")
    .MapAdminPublisherEndpoints()
    .WithTags("Admin - Publishers");

adminApi.MapGroup("/projections")
    .MapProjectionEndpoints()
    .WithTags("Admin - System");

app.MapDefaultEndpoints();

// Map SignalR hub for real-time notifications
app.MapHub<Wolverine.SignalR.WolverineHub>("/hub/bookstore");

app.Run();
