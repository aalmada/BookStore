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
        _ = options.AddDocumentTransformer((document, context, cancellationToken) =>
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
        _ = options.AddOperationTransformer((operation, context, cancellationToken) =>
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
                Description = "Preferred language for localized content (configurable via LocalizationOptions)"
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

    static string BuildApiDescription() => """
            Book store management system with search, authors, categories, and publishers.

            ## Features
            - Multi-language support (English, Portuguese, Spanish, French, German)
            - Request tracking with correlation IDs
            - Optimistic concurrency control for updates
            - Real-time notifications via SignalR

            ## SignalR Hub

            **WebSocket Endpoint**: `/hub/bookstore`

            ### Events (Server → Client)

            The server broadcasts the following events to all connected clients:

            - **BookCreatedNotification** - Sent when a book is created
              ```json
              {
                "entityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                "title": "Clean Code",
                "eventType": "BookCreated",
                "timestamp": "2025-12-28T16:00:00Z"
              }
              ```

            - **BookUpdatedNotification** - Sent when a book is updated
            - **BookDeletedNotification** - Sent when a book is deleted

            ### Connection Example

            **JavaScript/TypeScript**:
            ```javascript
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/hub/bookstore")
                .withAutomaticReconnect()
                .build();

            connection.on("BookCreatedNotification", (notification) => {
                console.log("Book created:", notification);
            });

            await connection.start();
            ```

            **C# (.NET)**:
            ```csharp
            var connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7001/hub/bookstore")
                .WithAutomaticReconnect()
                .Build();

            connection.On<BookNotification>("BookCreatedNotification", notification =>
            {
                Console.WriteLine($"Book created: {notification.Title}");
            });

            await connection.StartAsync();
            ```

            For more details, see the [SignalR Guide](https://github.com/yourusername/bookstore/blob/main/docs/signalr-guide.md).
            """;
}
