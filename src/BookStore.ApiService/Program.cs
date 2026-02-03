
using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Endpoints.Admin;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;

using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;

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
// Add Rate Limiting (using extension)
builder.Services.AddCustomRateLimiting(builder.Configuration);

var app = builder.Build();

// Apply schema to create PostgreSQL extensions (pg_trgm, unaccent)
await app.EnsureDatabaseSchemaAsync();

// Start seeding in the background
app.RunDatabaseSeedingAsync();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

// Add Forwarded Headers middleware early in the pipeline
app.UseForwardedHeaders();

// Add request localization middleware
var localizationOptions = new LocalizationOptions { SupportedCultures = ["en", "pt", "pt-PT", "es", "fr", "de"] }; // Default/Fallback
builder.Configuration.GetSection(LocalizationOptions.SectionName).Bind(localizationOptions);

var requestLocalizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(localizationOptions.DefaultCulture)
    .AddSupportedCultures(localizationOptions.SupportedCultures)
    .AddSupportedUICultures(localizationOptions.SupportedCultures);

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

app.UseRouting();

// Add authentication and authorization
app.UseAuthentication();
app.UseTenantSecurity();
app.UseAuthorization();

// Map OpenAPI endpoint and configure Scalar UI
app.MapOpenApi().WithMetadata(new AllowAnonymousTenantAttribute());

if (app.Environment.IsDevelopment())
{
    _ = app.MapScalarApiReference("/api-reference",
        options => options
            .WithTitle("Book Store API")
            // .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient))
            .WithMetadata(new AllowAnonymousTenantAttribute());
}

// Map JWT authentication endpoints
app.MapGroup("/account").MapJwtAuthenticationEndpoints();
app.MapGroup("/api/admin/tenants").WithTags("Tenants").MapTenantEndpoints().RequireAuthorization("Admin"); // Require Admin role
app.MapGroup("/api/tenants").WithTags("Tenants").MapTenantInfoEndpoints(); // Public
app.MapGroup("/api/config").WithTags("Configuration").MapConfigurationEndpoints(); // Public
app.MapPasskeyEndpoints();

// Map all API endpoints
app.MapApiEndpoints();

app.Run();
