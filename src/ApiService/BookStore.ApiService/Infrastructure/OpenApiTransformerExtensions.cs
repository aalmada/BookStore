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
                Description = "API version to use. Defaults to 1.0 if not specified. Example: `1.0`"
            });

            // Add localization header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept-Language",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Preferred language for response content and error messages. Supported values: `en` (English, default), `pt` (Portuguese), `es` (Spanish), `fr` (French), `de` (German)"
            });

            // Add correlation ID header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Correlation-ID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Correlation ID for distributed tracing. Tracks the entire business transaction across services. If not provided, one will be generated and returned in the response. Example: `01234567-89ab-cdef-0123-456789abcdef`"
            });

            // Add causation ID header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Causation-ID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Causation ID for distributed tracing. Tracks the immediate cause of this request (e.g., ID of the previous event or command). Use the `X-Event-ID` from a previous response. Example: `01234567-89ab-cdef-0123-456789abcdef`"
            });

            return Task.CompletedTask;
        });

        return options;
    }

    static string BuildApiDescription()
    {
        return """
            Event-sourced book store management system with book search, author, category, and publisher management.

            ## API Versioning
            This API uses header-based versioning. Include the `api-version` header in your requests:
            - **Current Version**: 1.0
            - **Default Behavior**: If no version is specified, v1.0 is assumed
            - **Version Header**: `api-version: 1.0`

            ## Localization
            The API supports multiple languages for content and error messages:
            - **Supported Languages**: English (en), Portuguese (pt), Spanish (es), French (fr), German (de)
            - **Default Language**: English (en)
            - **Language Header**: `Accept-Language: pt` (use ISO 639-1 language codes)

            ## Distributed Tracing
            The API implements correlation and causation tracking for distributed tracing:

            ### Request Headers
            - **X-Correlation-ID**: Tracks the entire business transaction across services. If not provided, one will be generated.
            - **X-Causation-ID**: Tracks the immediate cause of this request (e.g., the ID of the command or event that triggered this call).

            ### Response Headers
            - **X-Correlation-ID**: Returns the correlation ID used for this request (either provided or generated).
            - **X-Event-ID**: Returns the unique ID of any event created by this request (for POST, PUT, DELETE operations).

            ### Best Practices
            - Always include `X-Correlation-ID` when making related API calls to track the full transaction flow
            - Use the previous response's `X-Event-ID` as the `X-Causation-ID` for subsequent related requests
            - Store correlation IDs in your logs to enable end-to-end tracing
            """;
    }
}
