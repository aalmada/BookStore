using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Extensions for configuring OpenAPI document transformers with comprehensive API metadata
/// </summary>
public static class OpenApiTransformerExtensions
{
    /// <summary>
    /// Adds Book Store API documentation with versioning, localization, and correlation tracking metadata
    /// </summary>
    public static OpenApiOptions AddBookStoreApiDocumentation(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            // Configure API information
            document.Info = new()
            {
                Title = "Book Store API",
                Version = "v1",
                Description = BuildApiDescription(),
                Contact = new()
                {
                    Name = "Book Store Support"
                }
            };

            return Task.CompletedTask;
        });

        // Add operation transformer to add global headers to all operations
        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            // Initialize Parameters collection if null
            operation.Parameters ??= [];
            
            // Add API versioning header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "api-version",
                In = ParameterLocation.Header,
                Required = false,
                Description = "API version. Example: `1.0`"
            });

            // Add localization header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept-Language",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Preferred language. Supported: `en`, `pt`, `es`, `fr`, `de`"
            });

            // Add correlation ID header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Correlation-ID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Correlation ID for tracking requests across the system"
            });

            // Add causation ID header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Causation-ID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Causation ID linking this request to its trigger"
            });

            return Task.CompletedTask;
        });

        return options;
    }

    static string BuildApiDescription()
    {
        return """
            Book store management system with search, authors, categories, and publishers.

            ## Features
            - Multi-language support (English, Portuguese, Spanish, French, German)
            - Request tracking with correlation IDs
            - Optimistic concurrency control for updates
            """;
    }
}
