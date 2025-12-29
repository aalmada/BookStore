using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using Marten;
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

    // Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

    var seeder = new DatabaseSeeder(store);
    await seeder.SeedAsync();

    // Give async projections time to process the seeded events
    // In production, projections run continuously in the background
    await Task.Delay(TimeSpan.FromSeconds(2));
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
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.UseResponseCaching();
app.UseOutputCache();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
