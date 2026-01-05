using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add Azure Blob Storage client (Azurite locally, Azure in production)
builder.AddAzureBlobServiceClient("blobs");

// Add Redis distributed cache
// Add Redis distributed cache
// builder.AddRedisDistributedCache("cache");
builder.Services.AddDistributedMemoryCache();

// Add HybridCache (L1 + L2)
builder.Services.AddHybridCache();

// Configure services
builder.Services.AddJsonConfiguration(builder.Environment);
builder.Services.AddApplicationServices(builder.Configuration);
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

    // Strict policy for Authentication endpoints (Login, Register, Passkeys)
    _ = options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Configure cookie authentication security
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; // Lax for Aspire same-domain
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;

    // API-friendly responses (no redirects)
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

var app = builder.Build();

// Start seeding in the background (don't block app startup)
if (app.Environment.IsDevelopment())
{
    _ = Task.Run(async () =>
    {
        // Give the app a moment to start listening for health checks
        await Task.Delay(100);

        using var scope = app.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var seeder = new DatabaseSeeder(store);
        await seeder.SeedAsync();

        // Seed admin user
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<BookStore.ApiService.Models.ApplicationUser>>();
        await DatabaseSeeder.SeedAdminUserAsync(userManager);

        // Wait for async projections to process the seeded events
        // In production, projections run continuously in the background
        await WaitForProjectionsAsync(store, logger);
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

            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

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
else
{
    _ = app.UseExceptionHandler();
}

// Add request localization middleware
app.UseRequestLocalization();

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

app.UseResponseCaching();
app.UseOutputCache();

// Map JWT authentication endpoints
app.MapGroup("/account").MapJwtAuthenticationEndpoints();
app.MapPasskeyEndpoints();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
