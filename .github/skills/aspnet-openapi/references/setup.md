# OpenAPI Setup

## Package

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.*" />
<!-- Optional: Scalar or Swagger UI for the browser UI -->
<PackageReference Include="Scalar.AspNetCore" Version="2.*" />
```

Central Package Management — add a `PackageVersion` entry in `Directory.Packages.props` instead of per-project versions.

## Registering and exposing the document

### Minimal (development only)

```csharp
// Program.cs
builder.Services.AddOpenApi();   // default document name: "v1"

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();            // serves /openapi/v1.json
}
```

### Custom document name and multiple documents

```csharp
builder.Services.AddOpenApi("public");
builder.Services.AddOpenApi("internal");

app.MapOpenApi("/openapi/{documentName}.json");
```

### Document info (title, version, description)

Use a document transformer rather than options overloads — it avoids coupling service registration to document content:

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "My API",
            Version = "v1",
            Description = "Manages widgets and gadgets."
        };
        return Task.CompletedTask;
    });
});
```

## Scalar UI (recommended over Swagger UI)

[Scalar](https://scalar.com) is the UX-first replacement for Swagger UI. Wire it up after `MapOpenApi`:

```csharp
using Scalar.AspNetCore;

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/api-reference", options => options
        .WithTitle("My API")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}
```

The Scalar route (`/api-reference`) and the OpenAPI JSON route (`/openapi/v1.json`) are separate endpoints. Both should be accessible without authentication in development. In production, secure or remove them.

## Serving in production

By default `MapOpenApi()` is called unconditionally — wrap it in `IsDevelopment()` if you don't want to expose the document in production. Alternatively protect it with `.RequireAuthorization()`.

```csharp
// Production: only admins may fetch the spec
app.MapOpenApi().RequireAuthorization("Admin");
```

## Generated document location

The document is generated at runtime from endpoint metadata (not a static file). The default URL is `/openapi/v1.json`. Use the `--output` flag of `dotnet openapi` or the Microsoft Kiota CLI to export a snapshot.
