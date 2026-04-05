# Marten Multi-Tenancy

## Mental Model

Marten supports three isolation strategies. The right choice depends on compliance and operational requirements:

| Strategy | Isolation level | Complexity | Best for |
|----------|----------------|------------|---------|
| **Conjoined** | Row-level (`tenant_id` column) | Low | Most SaaS apps, shared infra |
| **Separate schemas** | Schema-level | Medium | Stricter isolation |
| **Separate databases** | Database-level | High | Compliance, very high isolation |

Conjoined tenancy is almost always the right default. Marten automatically filters every query by `tenant_id` — no manual filtering needed.

---

## Configuration

### Enable conjoined tenancy

```csharp
// Events AND documents both use conjoined tenancy
options.Events.TenancyStyle = TenancyStyle.Conjoined;
options.Policies.AllDocumentsAreMultiTenanted();
```

`AllDocumentsAreMultiTenanted()` is shorthand for:
```csharp
options.Policies.ForAllDocuments(x => x.TenancyStyle = TenancyStyle.Conjoined);
```

### Opt specific documents out of tenancy

Documents that should be global (e.g., a tenant registry, reference data) must be excluded:

```csharp
// Option 1: Attribute (Marten 8.5+)
[DoNotPartition]
public class Tenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

// Option 2: Fluent API
options.Schema.For<Tenant>().SingleTenanted();
```

> Without exclusion, a document marked as multi-tenanted cannot be queried without a tenant ID — global lookups fail.

### Make a specific document tenanted when the global policy is single-tenant

```csharp
options.Policies.ForAllDocuments(x => x.TenancyStyle = TenancyStyle.Single);
options.Schema.For<Invoice>().MultiTenanted(); // override for this type
```

---

## Opening Tenant-Scoped Sessions

Sessions **must** be scoped to the correct tenant ID at construction time. All queries and writes on that session are automatically filtered or tagged.

```csharp
// Lightweight session scoped to a tenant
await using var session = store.LightweightSession("acme");

// Read-only session scoped to a tenant
await using var query = store.QuerySession("acme");
```

Calling `store.LightweightSession()` without a tenant ID uses Marten's built-in default tenant (`StorageConstants.DefaultTenantId = "*DEFAULT*"`).

### DI registration pattern (recommended)

Register a scoped `IDocumentSession` that resolves the tenant from the current execution context — either from a Wolverine message envelope or from an ASP.NET Core middleware:

```csharp
services.AddScoped<IDocumentSession>(sp =>
{
    var store = sp.GetRequiredService<IDocumentStore>();

    // Wolverine message handlers: use the envelope's tenant ID
    var messageContext = sp.GetService<IMessageContext>();
    if (messageContext?.TenantId != null)
        return store.LightweightSession(messageContext.TenantId);

    // ASP.NET Core requests: use the request-scoped tenant context
    var tenantContext = sp.GetRequiredService<ITenantContext>();
    return store.LightweightSession(tenantContext.TenantId);
});

// IQuerySession just delegates to the already-scoped IDocumentSession
services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentSession>());
```

This way, handlers and endpoints receive a session already scoped to the right tenant without any manual wiring:

```csharp
// Handler automatically receives the right tenant's session
public static IResult Handle(CreateBook command, IDocumentSession session)
{
    session.Events.StartStream<BookAggregate>(command.Id, @event);
    return Results.Created(...);
}
```

---

## Resolving the Tenant per Request (Middleware)

The tenant ID typically comes from an HTTP header. A middleware reads it, validates it, and sets it on a request-scoped `ITenantContext` service:

```csharp
public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantStore tenantStore)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var values))
        {
            var tenantId = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                if (await tenantStore.IsValidTenantAsync(tenantId))
                    tenantContext.Initialize(tenantId);
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid tenant" });
                    return;
                }
            }
        }
        // Falls through to default tenant if no header present
        await next(context);
    }
}
```

Register before authentication/authorization middleware so the tenant is available to all downstream handlers.

---

## Querying Global Documents from a Tenanted Store

Documents marked `[DoNotPartition]` / `SingleTenanted()` must be queried via a session opened without a specific tenant — use `IDocumentStore` directly rather than the injected `IDocumentSession`:

```csharp
// Correct: open a global (default-tenant) session to read tenant registry
await using var session = store.LightweightSession();
var tenant = await session.LoadAsync<Tenant>(tenantId);
```

Never inject `IDocumentSession` for global queries — the injected session is already scoped to the current tenant and cannot see non-tenanted data.

---

## Cross-Tenant Queries (Advanced)

Marten provides a LINQ extension to query across multiple tenants in a single session:

```csharp
// Find all documents matching tenant "Green" or "Red" in a single query
var results = await session.Query<Order>()
    .Where(x => x.TenantIsOneOf("Green", "Red"))
    .ToListAsync();
```

Use sparingly — this bypasses normal tenant isolation and is only appropriate for admin/global views.

---

## Projections and Multi-Tenancy

### Per-tenant projections (default)

When `Events.TenancyStyle = TenancyStyle.Conjoined`, all projections automatically run per-tenant. Each event is processed in the context of the tenant that appended it; the resulting read-model documents are stored with the same `tenant_id`.

### Cross-tenant (global) projections

Some projections aggregate data across all tenants (e.g., a platform-wide stats counter). Enable cross-tenant projection support and register the projection as global:

```csharp
// Step 1: Enable the feature
options.Events.EnableGlobalProjectionsForConjoinedTenancy = true;

// Step 2: Register the projection as global
options.Projections.AddGlobalProjection(new PlatformStatsProjection(), ProjectionLifecycle.Async);
```

Global projections store their read model documents under the default tenant ID, regardless of which tenant's events they process.

---

## Indexes and Query Performance

For conjoined tenancy, indexes should start with `tenant_id` to remain effective when queries always include a tenant filter:

```csharp
// Apply globally
options.Policies.ForAllDocuments(x => x.StartIndexesByTenantId = true);

// Or per document type
options.Schema.For<Order>()
    .StartIndexesByTenantId()
    .Index(x => x.Status);
```

### Per-tenant unique indexes

Unique constraints can be scoped per-tenant (e.g., unique email within a tenant):

```csharp
options.Schema.For<User>()
    .MultiTenanted()
    .UniqueIndex(
        UniqueIndexType.Computed,
        "idx_user_email_per_tenant",
        TenancyScope.PerTenant,
        x => x.Email);
```

---

## Table Partitioning (Marten 8+)

For large multi-tenant datasets, table partitioning improves query performance and simplifies data management:

```csharp
// Managed by Marten (schema stored in 'tenants' schema)
options.Policies.PartitionMultiTenantedDocumentsUsingMartenManagement("tenants");

// OR — full control via external tooling (e.g., pg_partman)
options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(x =>
{
    x.ByExternallyManagedListPartitions(); // each tenant gets a LIST partition
    // Alternatives:
    // x.ByHash("shard1", "shard2", "shard3"); // spread by hash
    // x.ByList().AddPartition("acme", "ACME"); // explicit partitions
});
```

Exempt small or reference tables from partitioning with `[DoNotPartition]` — adding partitions has overhead:

```csharp
[DoNotPartition]
public class Tenant { ... }
```

---

## Tenant Lifecycle Management

### Apply schema changes for a specific tenant

When using multi-database tenancy, apply schema changes per-tenant on demand:

```csharp
var tenant = await store.Tenancy.GetTenantAsync(tenantId);
await tenant.Database.ApplyAllConfiguredChangesToDatabaseAsync();
```

### Delete all data for a tenant

```csharp
// Removes all documents and events associated with tenantId
// Only affects conjoined-tenancy tables
await store.Advanced.DeleteAllTenantDataAsync("acme", cancellationToken);
```

Use for tenant offboarding or test isolation — this is permanent.

### Validate a tenant exists

The project wraps tenant existence checks behind an `ITenantStore` interface with a distributed-cache layer:

```csharp
public interface ITenantStore
{
    Task<bool> IsValidTenantAsync(string tenantId);
    Task<IEnumerable<string>> GetAllTenantsAsync();
    Task InvalidateCacheAsync(string tenantId); // call after create/update/delete
}
```

Register as a decorator over the Marten-backed implementation:
```csharp
services.AddScoped<MartenTenantStore>();
services.AddScoped<ITenantStore>(sp =>
    new CachedTenantStore(
        sp.GetRequiredService<MartenTenantStore>(),
        sp.GetRequiredService<IDistributedCache>()));
```

---

## Key Constants

Always use `MultiTenancyConstants` from `BookStore.Shared` — never hardcode tenant ID strings:

```csharp
// ✅ Correct
if (tenantId == MultiTenancyConstants.DefaultTenantId) { ... }

// ❌ Wrong
if (tenantId == "*DEFAULT*") { ... }
```

`StorageConstants.DefaultTenantId` (from JasperFx) is the same value — use `MultiTenancyConstants` in application code for consistency.

---

## Common Pitfalls

| Problem | Cause | Fix |
|---------|-------|-----|
| Query returns no results | Session opened under wrong tenant | Check the DI session registration |
| Global document not found | Querying via a tenanted `IDocumentSession` | Use `store.LightweightSession()` directly |
| Cross-tenant data leaked | Missing `[DoNotPartition]` / `SingleTenanted()` | Mark global documents explicitly |
| Invalid tenant rejected | Tenant lookup fails | Validate using `ITenantStore.IsValidTenantAsync` |
| Slow queries under load | Indexes don't start with `tenant_id` | Enable `StartIndexesByTenantId` |
