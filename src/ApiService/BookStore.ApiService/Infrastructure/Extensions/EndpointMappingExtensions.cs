using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Endpoints.Admin;
using Scalar.AspNetCore;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for mapping API endpoints
/// </summary>
public static class EndpointMappingExtensions
{
    /// <summary>
    /// Maps all API endpoints including public, admin, and system endpoints
    /// </summary>
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        // Root endpoint
        _ = app.MapGet("/", () => "Book Store API is running. Visit /api-reference for API documentation.")
            .ExcludeFromDescription();

        // Create API version set for v1
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new Asp.Versioning.ApiVersion(1))
            .ReportApiVersions()
            .Build();

        // Map public and admin endpoints
        MapPublicEndpoints(app, apiVersionSet);
        MapAdminEndpoints(app, apiVersionSet);

        // Map default endpoints (health checks, metrics, etc.)
        _ = app.MapDefaultEndpoints();

        return app;
    }

    static void MapPublicEndpoints(WebApplication app, Asp.Versioning.Builder.ApiVersionSet apiVersionSet)
    {
        // Public API endpoints (v1)
        var publicApi = app.MapGroup("/api")
            .WithApiVersionSet(apiVersionSet);

        _ = publicApi.MapGroup("/books")
            .MapBookEndpoints()
            .WithTags("Books");

        _ = publicApi.MapGroup("/authors")
            .MapAuthorEndpoints()
            .WithTags("Authors");

        _ = publicApi.MapGroup("/categories")
            .MapCategoryEndpoints()
            .WithTags("Categories");

        _ = publicApi.MapGroup("/publishers")
            .MapPublisherEndpoints()
            .WithTags("Publishers");

        _ = publicApi.MapGroup("/notifications")
            .MapNotificationEndpoints()
            .WithTags("Notifications");
    }

    static void MapAdminEndpoints(WebApplication app, Asp.Versioning.Builder.ApiVersionSet apiVersionSet)
    {
        // Admin API endpoints (v1)
        var adminApi = app.MapGroup("/api/admin")
            .WithApiVersionSet(apiVersionSet);

        _ = adminApi.MapGroup("/books")
            .MapAdminBookEndpoints()
            .WithTags("Admin - Books");

        _ = adminApi.MapGroup("/authors")
            .MapAdminAuthorEndpoints()
            .WithTags("Admin - Authors");

        _ = adminApi.MapGroup("/categories")
            .MapAdminCategoryEndpoints()
            .WithTags("Admin - Categories");

        _ = adminApi.MapGroup("/publishers")
            .MapAdminPublisherEndpoints()
            .WithTags("Admin - Publishers");

        _ = adminApi.MapGroup("/projections")
            .MapProjectionEndpoints()
            .WithTags("Admin - System");
    }
}
