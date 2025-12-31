using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using Marten;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add Azure Blob Storage client (Azurite locally, Azure in production)
builder.AddAzureBlobServiceClient("blobs");

// Configure services
builder.Services.AddJsonConfiguration(builder.Environment);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddMartenEventStore(builder.Configuration);
builder.Services.AddWolverineMessaging();

var app = builder.Build();

// Seed database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

    var seeder = new DatabaseSeeder(store);
    await seeder.SeedAsync();

    // Wait for async projections to process the seeded events
    // In production, projections run continuously in the background
    await WaitForProjectionsAsync(store, logger);
}

static async Task WaitForProjectionsAsync(IDocumentStore store, ILogger logger)
{
    Log.Infrastructure.WaitingForProjections(logger);

    var timeout = TimeSpan.FromSeconds(30);
    var checkInterval = TimeSpan.FromMilliseconds(100);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    while (stopwatch.Elapsed < timeout)
    {
        await using var session = store.QuerySession();

        // Check if projections have data by querying the projection tables
        var bookCount = await session.Query<BookSearchProjection>().CountAsync();
        var authorCount = await session.Query<AuthorProjection>().CountAsync();
        var categoryCount = await session.Query<CategoryProjection>().CountAsync();
        var publisherCount = await session.Query<PublisherProjection>().CountAsync();

        // If all projections have data, we're ready
        if (bookCount > 0 && authorCount > 0 && categoryCount > 0 && publisherCount > 0)
        {
            Log.Infrastructure.ProjectionsReady(logger, bookCount, authorCount, categoryCount, publisherCount);
            return;
        }

        await Task.Delay(checkInterval);
    }

    Log.Infrastructure.ProjectionTimeout(logger, timeout.TotalSeconds);
}

// Configure the HTTP request pipeline
app.UseExceptionHandler();

// Add request localization middleware
app.UseRequestLocalization();

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
            // .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.UseResponseCaching();
app.UseOutputCache();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
