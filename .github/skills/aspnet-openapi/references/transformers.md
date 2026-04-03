# Transformers

Transformers let you customise the generated OpenAPI document, operations, or schemas after the automatic pass. They run at document-generation time (on each request to `/openapi/v1.json`), not at build time.

> **API note:** The transformer API changed significantly in .NET 9. `IOpenApiDocumentTransformer`, `IOpenApiOperationTransformer`, and `IOpenApiSchemaTransformer` are the correct interfaces. The lambda overloads (inline delegates) are syntactic sugar over these.

## Document transformers

Use a document transformer to set document-level metadata or add global security schemes.

### Inline lambda

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "My API",
            Version = "v1",
            Description = "Short description here."
        };
        return Task.CompletedTask;
    });
});
```

### DI-injected class (preferred for complex logic)

```csharp
internal sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    private readonly IHostEnvironment _env;

    public ApiInfoTransformer(IHostEnvironment env) => _env = env;

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Description += _env.IsDevelopment()
            ? "\n\n_Development environment — may contain unstable endpoints._"
            : string.Empty;
        return Task.CompletedTask;
    }
}

// Registration
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ApiInfoTransformer>();
});
```

### Adding a Bearer security scheme

```csharp
options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    return Task.CompletedTask;
});
```

## Operation transformers

Use operation transformers to inject global parameters, add common responses, or remove endpoints from the document based on runtime conditions.

### Add a global header to every operation

```csharp
options.AddOperationTransformer((operation, context, cancellationToken) =>
{
    operation.Parameters ??= [];
    operation.Parameters.Add(new OpenApiParameter
    {
        Name = "X-Correlation-ID",
        In = ParameterLocation.Header,
        Required = false,
        Description = "Optional correlation ID for request tracing."
    });
    return Task.CompletedTask;
});
```

### Add a common 4XX error response

```csharp
options.AddOperationTransformer(async (operation, context, cancellationToken) =>
{
    var problemSchema = await context.GetOrCreateSchemaAsync(
        typeof(ProblemDetails), null, cancellationToken);
    context.Document?.AddComponent("Error", problemSchema);

    operation.Responses ??= new OpenApiResponses();
    operation.Responses["4XX"] = new OpenApiResponse
    {
        Description = "Client error",
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchemaReference("Error", context.Document)
            }
        }
    };
});
```

### Add Bearer security requirement to protected routes

```csharp
options.AddOperationTransformer((operation, context, cancellationToken) =>
{
    var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
        .OfType<IAuthorizeData>().Any();

    if (requiresAuth)
    {
        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = "Bearer",
                        Type = ReferenceType.SecurityScheme
                    }
                }] = []
            }
        ];
    }

    return Task.CompletedTask;
});
```

## Schema transformers

Use schema transformers to adjust how C# types are represented in the JSON Schema (e.g., add descriptions, set formats, or enforce nullable rules).

```csharp
options.AddSchemaTransformer((schema, context, cancellationToken) =>
{
    // Make all DateTime properties carry the "date-time" format
    if (context.JsonTypeInfo.Type == typeof(DateTimeOffset))
    {
        schema.Format = "date-time";
    }
    return Task.CompletedTask;
});
```

### Adding XML comments to schemas

Install `Microsoft.Extensions.ApiDescription.Server` and enable XML documentation output in the project file:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Then configure the schema transformer to read the XML file:

```csharp
// .NET 9+ — built-in XML comment support via AddSchemaTransformer is not yet automatic;
// use Swashbuckle or OpenAPI.Extensions NuGet for this if needed.
```

## Transformer ordering

Transformers run in registration order. Document transformers run before operation and schema transformers. Register shared setup (like security schemes) in a document transformer first, then reference those components from operation transformers.

## GetOrCreateSchemaAsync

Available on `OpenApiOperationTransformerContext` in .NET 10+. Generates a schema for an arbitrary C# type using the same logic as the automatic pass:

```csharp
var schema = await context.GetOrCreateSchemaAsync(typeof(MyType), null, cancellationToken);
```

Use it to generate reusable component schemas instead of duplicating inline definitions.
