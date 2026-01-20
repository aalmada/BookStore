using System.Threading.RateLimiting;
using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Endpoints.Admin;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
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

// specialized tenant services
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<MartenTenantStore>(); // Register concrete implementation
builder.Services.AddScoped<ITenantStore>(sp =>
{
    var inner = sp.GetRequiredService<MartenTenantStore>();
    var cache = sp.GetRequiredService<IDistributedCache>();
    return new CachedTenantStore(inner, cache);
});

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

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Exempt health checks and metrics
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics"))
        {
            return RateLimitPartition.GetNoLimiter("exempt");
        }

        // Per-tenant rate limiting
        var tenantId = context.Items["TenantId"]?.ToString()
            ?? JasperFx.StorageConstants.DefaultTenantId;

        return RateLimitPartition.GetFixedWindowLimiter(tenantId, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000, // 1000 requests per window
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });

    // Stricter rate limiting for authentication endpoints (login, register, etc.)
    _ = options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = rateLimitOptions.AuthPermitLimit;
        opt.Window = TimeSpan.FromSeconds(rateLimitOptions.AuthWindowSeconds);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = rateLimitOptions.AuthQueueLimit;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        double? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = retryAfter.TotalSeconds;
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            retryAfter = retryAfterSeconds
        }, cancellationToken);
    };
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

// Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
// This must remain blocking to ensure schema exists before requests are handled
// This fixes the "correlation_id column missing" error in tests
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
}

// Start seeding in the background (don't block app startup)
// We need seeding in all environments for now (including tests)
if (true)
{
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

                // Schema application moved to main thread

                if (app.Configuration.GetValue("Seeding:Enabled", true))
                {
                    var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
                    var seederLogger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseSeeder>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<BookStore.ApiService.Models.ApplicationUser>>();
                    var seeder = new DatabaseSeeder(store, bus, seederLogger, userManager);
                    var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();

                    // 1. Ensure Tenants exist in the DB
                    await seeder.SeedTenantsAsync(TenantConstants.KnownTenants);

                    // 2. Refresh the list of tenants from the store (verifying it works)
                    var tenants = TenantConstants.KnownTenants;

                    foreach (var tenantId in tenants)
                    {
                        Log.Infrastructure.SeedingTenant(logger, tenantId);

                        // Admin user is now seeded per-tenant in SeedAsync

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

static async Task WaitForProjectionsAsync(IDocumentStore store, ILogger logger, string tenantId, bool expectSales = false)
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
            var hasSales = await session.Query<BookSearchProjection>().AnyAsync(b => b.Sales.Count > 0); // Check length of JSONB array
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

app.UseRequestLocalization(requestLocalizationOptions);

// Add Tenant Resolution Middleware
app.UseTenantResolution();

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
app.UseTenantSecurity();
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
app.MapGroup("/api/admin/tenants").WithTags("Tenants").MapTenantEndpoints().RequireAuthorization("Admin"); // Require Admin role
app.MapGroup("/api/tenants").WithTags("Tenants").MapTenantInfoEndpoints(); // Public
app.MapPasskeyEndpoints();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
