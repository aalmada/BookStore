using Marten;
using Marten.Events.Daemon;
using Marten.Events.Projections;
using JasperFx.Events;
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Projections;
using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Endpoints.Admin;
using BookStore.ApiService.Infrastructure;
using Scalar.AspNetCore;
using Weasel.Core;
using Wolverine;
using Wolverine.Marten;
using JasperFx.Events.Projections;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

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

// Configure OpenAPI with metadata
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Book Store API",
            Version = "v1",
            Description = "Event-sourced book store management system with book search, author, category, and publisher management.",
            Contact = new()
            {
                Name = "Book Store Support"
            }
        };
        return Task.CompletedTask;
    });
});

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
    options.SetDefaultCulture("en")
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
    options.Events.AddEventType<BookStore.ApiService.Events.BookAdded>();
    options.Events.AddEventType<BookStore.ApiService.Events.BookUpdated>();
    options.Events.AddEventType<BookStore.ApiService.Events.BookSoftDeleted>();
    options.Events.AddEventType<BookStore.ApiService.Events.BookRestored>();

    options.Events.AddEventType<BookStore.ApiService.Events.AuthorAdded>();
    options.Events.AddEventType<BookStore.ApiService.Events.AuthorUpdated>();
    options.Events.AddEventType<BookStore.ApiService.Events.AuthorSoftDeleted>();
    options.Events.AddEventType<BookStore.ApiService.Events.AuthorRestored>();

    options.Events.AddEventType<BookStore.ApiService.Events.CategoryAdded>();
    options.Events.AddEventType<BookStore.ApiService.Events.CategoryUpdated>();
    options.Events.AddEventType<BookStore.ApiService.Events.CategorySoftDeleted>();
    options.Events.AddEventType<BookStore.ApiService.Events.CategoryRestored>();

    options.Events.AddEventType<BookStore.ApiService.Events.PublisherAdded>();
    options.Events.AddEventType<BookStore.ApiService.Events.PublisherUpdated>();
    options.Events.AddEventType<BookStore.ApiService.Events.PublisherSoftDeleted>();
    options.Events.AddEventType<BookStore.ApiService.Events.PublisherRestored>();

    // Configure projections - using AddAsync for async projections managed by Wolverine
    // Register projection builders explicitly with async lifecycle
    options.Projections.Add<AuthorProjectionBuilder>(ProjectionLifecycle.Async);
    options.Projections.Add<CategoryProjectionBuilder>(ProjectionLifecycle.Async);
    options.Projections.Add<PublisherProjectionBuilder>(ProjectionLifecycle.Async);
    options.Projections.Add<BookSearchProjectionBuilder>(ProjectionLifecycle.Async);

    // Configure indexes for search performance
    // Note: Trigram indexes for fuzzy search will be created via SQL migration
    options.Schema.For<BookSearchProjection>()
        .Index(x => x.PublisherId)  // Standard B-tree index for exact matches
        .Index(x => x.Title)        // B-tree index for sorting
        .GinIndexJsonData();        // GIN index for JSON fields

    // Indexes for AuthorProjection
    options.Schema.For<AuthorProjection>()
        .Index(x => x.Name);         // B-tree index for sorting

    // Indexes for CategoryProjection
    options.Schema.For<CategoryProjection>()
        .Index(x => x.Name);         // B-tree index for sorting

    // Indexes for PublisherProjection
    options.Schema.For<PublisherProjection>()
        .Index(x => x.Name);         // B-tree index for sorting

    // Configure NGram indexes for text search (accent-insensitive)
    options.Schema.For<BookSearchProjection>()
        .NgramIndex(x => x.Title)           // NGram search on title
        .NgramIndex(x => x.AuthorNames);    // NGram search on authors

    options.Schema.For<AuthorProjection>()
        .NgramIndex(x => x.Name);           // NGram search on author name

    options.Schema.For<CategoryProjection>()
        .NgramIndex(x => x.Name);           // NGram search on category name

    options.Schema.For<PublisherProjection>()
        .NgramIndex(x => x.Name);           // NGram search on publisher name
    return options;
})
.UseLightweightSessions()
.IntegrateWithWolverine();

// Add Wolverine with command/handler pattern
builder.Services.AddWolverine(opts =>
{
    // Auto-discover handlers in this assembly
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // Policies for automatic behavior
    opts.Policies.AutoApplyTransactions();
});

// Add Marten health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("bookstore")!);

// Add response caching for performance
builder.Services.AddResponseCaching();
builder.Services.AddOutputCache();

var app = builder.Build();

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
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Book Store API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
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

app.Run();
