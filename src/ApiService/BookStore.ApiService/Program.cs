using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add Azure Blob Storage client (Azurite locally, Azure in production)
builder.AddAzureBlobServiceClient(BookStore.ServiceDefaults.ResourceNames.Blobs);

// Add Redis distributed cache (L2 for HybridCache)
builder.AddRedisDistributedCache(BookStore.ServiceDefaults.ResourceNames.Cache);

// Configure services
builder.Services.AddJsonConfiguration(builder.Environment);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddLocalization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMartenEventStore(builder.Configuration);
builder.Services.AddWolverineMessaging();

// Add CORS to allow Web app to call API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => _ = policy.WithOrigins("https://localhost:7260", "http://localhost:7260")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    var rateLimitOptions = new RateLimitOptions();
    builder.Configuration.GetSection(RateLimitOptions.SectionName).Bind(rateLimitOptions);

    // Strict policy for Authentication endpoints (Login, Register, Passkeys)
    _ = options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = rateLimitOptions.PermitLimit;
        opt.Window = TimeSpan.FromMinutes(rateLimitOptions.WindowInMinutes);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = rateLimitOptions.QueueLimit;
    });
});

// Configure Forwarded Headers to correctly capture client IP behind proxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear known networks/proxies to trust standard proxies in the environment (Aspire/Docker)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null;
    options.RequireHeaderSymmetry = false;
});

var app = builder.Build();

// Start seeding in the background (don't block app startup)
// We need seeding in all environments for now (including tests)
if (true)
{
    _ = Task.Run(async () =>
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
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

                // Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
                await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

                var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
                var seeder = new DatabaseSeeder(store, bus);
                await seeder.SeedAsync();

                // Seed admin user
                var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<BookStore.ApiService.Models.ApplicationUser>>();
                await DatabaseSeeder.SeedAdminUserAsync(userManager);

                // Wait for async projections to process the seeded events
                // In production, projections run continuously in the background
                await WaitForProjectionsAsync(store, logger);

                Log.Infrastructure.DatabaseSeedingCompleted(logger);
                return; // Success, exit loop
            }
            catch (Exception ex)
            {
                retryCount++;
                Log.Infrastructure.DatabaseSeedingFailed(logger, ex);

                if (retryCount >= maxRetries)
                {
#pragma warning disable CA1848 // Use LoggerMessage delegates
                    logger.LogError(ex, "Database seeding failed after {RetryCount} attempts. Application may not behave correctly.", retryCount);
#pragma warning restore CA1848
                    break;
                }

#pragma warning disable CA1848 // Use LoggerMessage delegates
                logger.LogWarning(ex, "Database seeding failed (attempt {RetryCount}/{MaxRetries}). Retrying in {RetryDelay}s...", retryCount, maxRetries, retryDelay.TotalSeconds);
#pragma warning restore CA1848
                await Task.Delay(retryDelay);
            }
        }
    });
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
if (app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async context =>
    {
        var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionHandlerFeature is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var exception = exceptionHandlerFeature.Error;

            Log.Infrastructure.UnhandledException(logger, exception, exception.Message);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                title = "An error occurred while processing your request.",
                status = StatusCodes.Status500InternalServerError,
                detail = exception.Message,
                stackTrace = exception.StackTrace
            });
        }
    }));
}

// Add Forwarded Headers middleware early in the pipeline
app.UseForwardedHeaders();

// Add request localization middleware
// Add request localization middleware
var localizationOptions = new LocalizationOptions { SupportedCultures = ["en", "pt", "pt-PT", "es", "fr", "de"] }; // Default/Fallback
builder.Configuration.GetSection(LocalizationOptions.SectionName).Bind(localizationOptions);

var requestLocalizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(localizationOptions.DefaultCulture)
    .AddSupportedCultures(localizationOptions.SupportedCultures)
    .AddSupportedUICultures(localizationOptions.SupportedCultures);

app.UseRequestLocalization(requestLocalizationOptions);

// Add Marten metadata middleware to set correlation/causation IDs
app.UseMartenMetadata();

// Add logging enricher middleware to add metadata to all logs
app.UseLoggingEnricher();

// Enable CORS
app.UseCors();

// Enable Rate Limiting
app.UseRateLimiter();

// Add authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

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

// Map JWT authentication endpoints
app.MapGroup("/account").MapJwtAuthenticationEndpoints();
app.MapPasskeyEndpoints();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
