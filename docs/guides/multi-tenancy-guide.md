# Multi-Tenancy Guide

This guide details the **Enterprise-grade Multi-tenancy** implementation in the BookStore application with comprehensive performance optimizations, security features, and user experience enhancements.

## Overview

The application uses **Conjoined Tenancy** where all tenants share the same database and schema, but data is logically isolated using a `tenant_id` column.

### Key Features

✅ **Data Isolation**: Marten-based conjoined multi-tenancy  
✅ **Event Sourcing**: Full multi-tenant event store support  
✅ **Performance**: Redis caching reduces DB queries by ~99%  
✅ **Security**: Audit logging and per-tenant rate limiting  
✅ **UX**: Tenant switcher UI with localStorage persistence  

---

## Marten Compliance

Our implementation follows [Marten's official event store multi-tenancy recommendations](https://martendb.io/events/multitenancy.html):

✅ **Event Store**: `TenancyStyle.Conjoined` configured  
✅ **Documents**: `AllDocumentsAreMultiTenanted()` policy applied  
✅ **Global Documents**: `[DoNotPartition]` for `Tenant` model (Marten 8.5+)  
✅ **Sessions**: Properly scoped per-tenant  
✅ **Projections**: Automatically tenant-aware  
✅ **Async Daemon**: Processes events per-tenant  

---

## Architecture

### 1. Event Store Multi-Tenancy (Marten)

```csharp
// MartenConfigurationExtensions.cs
options.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
```

**What this means:**
- All events are automatically captured with `tenant_id`
- Event streams are isolated per tenant
- Projections respect tenant boundaries
- Async daemon processes events per-tenant

### 2. Document Multi-Tenancy

```csharp
options.Policies.AllDocumentsAreMultiTenanted();
```

- Every document includes a `tenant_id` column
- All sessions are scoped to a specific tenant
- Queries automatically filter by tenant ID

**Exception**: `Tenant` documents use `[DoNotPartition]` attribute to be globally accessible:

```csharp
[Marten.Schema.DoNotPartition]
public class Tenant
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsEnabled { get; set; }
}
```

### 3. API Isolation (Middleware)

`TenantResolutionMiddleware` intercepts every HTTP request:
1. Extracts `X-Tenant-ID` header
2. Validates tenant (with Redis caching)
3. Sets `ITenantContext` for request scope
4. Logs tenant access for audit trail
5. Returns `400 Bad Request` if invalid

### 4. Cache Isolation

All caching operations automatically include tenant context. Cache keys are tenant-scoped to prevent data leakage:

```csharp
// Tenant ID is automatically appended by GetOrCreateLocalizedAsync
var cacheKey = $"book:{id}";  // Becomes: book:123|en-US|acme
```

> [!NOTE]
> For complete caching implementation details including tenant-aware patterns, see the [Caching Guide](caching-guide.md#tenant-aware-caching).
---

## Blob Storage Multi-Tenancy

### Isolation Strategy: Folder Prefixes

Unlike Marten (database) which has built-in support, Azure Blob Storage (and Azurite) uses a flat namespace. We implement logical isolation by **prefixing all blobs with the tenant ID**.

- **Structure**: `{tenantId}/{bookId}.{extension}`
- **Example**: `acme/book-123.png`, `contoso/book-456.png`

### 1. Upload Isolation
The `BlobStorageService` accepts a `tenantId` (resolved from `ITenantContext`) and prefixes the blob path during upload. This ensures `acme` data never ends up in `default` folders.

### 2. Download Isolation & The "Browser Context" Problem

**The Challenge**:
When a browser loads an image via an `<img>` tag, it makes a standard GET request. It does **not** send custom headers (like `X-Tenant-ID`) and, in a development environment (localhost), the domain is shared. The API cannot automatically distinguish which tenant the request belongs to.

**The Solution: Proxy Endpoint with Explicit Context**
We serve images via a proxy endpoint that carries the tenant context in the URL:

- **URL**: `/api/books/{id}/cover?tenantId=acme`
- **Mechanism**:
  1. `BookCoverHandlers` saves this parameterized URL in the database.
  2. The browser requests this URL.
  3. The API reads `tenantId` from the query string.
  4. The API reconstructs the path (`acme/{bookId}.png`) and streams the blob.

This guarantees robust isolation and correct image loading regardless of the domain or network environment (Docker/Host).

---

## Performance Optimizations

### Tenant Validation Caching

`CachedTenantStore` wraps `MartenTenantStore` with Redis:
- **Cache Duration**: 5 minutes
- **Impact**: ~99% reduction in DB queries
- **Invalidation**: `InvalidateCacheAsync()` on tenant updates

### Per-Tenant Rate Limiting

1000 requests/minute per tenant prevents noisy neighbor problem:

```csharp
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
    return RateLimitPartition.GetFixedWindowLimiter(tenantId, 
        new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1000,
            Window = TimeSpan.FromMinutes(1)
        });
});
```

---

## Database Indexes

For optimal multi-tenancy performance in production, proper database indexing is critical. 

### Quick Reference

```sql
-- Core tenant indexes (verify these exist)
CREATE INDEX IF NOT EXISTS idx_book_tenant_id ON mt_doc_book(tenant_id);
CREATE INDEX IF NOT EXISTS idx_author_tenant_id ON mt_doc_author(tenant_id);
CREATE INDEX IF NOT EXISTS idx_user_tenant_id ON mt_doc_user(tenant_id);

-- Event store indexes
CREATE INDEX idx_events_tenant_stream ON mt_events(tenant_id, stream_id);
```

### Expected Performance

With proper indexing:
- Tenant-filtered queries: < 10ms for up to 1M records per tenant
- Event queries: < 5ms for recent events

**For comprehensive indexing strategies, monitoring queries, and maintenance schedules, see [Database Indexes Guide](database-indexes-guide.md).**

---

## Security Features

### Audit Logging

All tenant access is logged:

```csharp
_logger.LogInformation(
    "Tenant {TenantId} accessing {Method} {Path} from {RemoteIp}",
    tenantContext.TenantId,
    context.Request.Method,
    context.Request.Path,
    context.Connection.RemoteIpAddress);
```

### Rate Limiting

- **Global**: 1000 req/min per tenant
- **Auth**: 10 req/min (stricter)
- **Response**: `429 Too Many Requests` with `retryAfter` seconds

---

## Frontend Integration

### Tenant Service

Three-tier priority:
1. URL parameter (`?tenant=xxx`)
2. LocalStorage
3. Default tenant

### Tenant Switcher UI

```razor
<MudMenu Icon="@Icons.Material.Filled.Business">
    <MudMenuItem OnClick="@(() => SwitchTenant("acme"))">Acme Corp</MudMenuItem>
</MudMenu>
```

### LocalStorage Persistence

```csharp
await _localStorage.SetItemAsStringAsync("selected-tenant", tenantId);
```

---

## Developer Guidelines

### 1. Always Inject ITenantContext

```csharp
public class MyService(ITenantContext tenantContext)
{
    var tenantId = tenantContext.TenantId;
}
```

### 2. Verify Cache Keys

```csharp
// ✅ Correct
var key = $"book:{id}:tenant={tenantContext.TenantId}";

// ❌ Wrong - data leak!
var key = $"book:{id}";
```

### 3. Test Multi-Tenancy

```csharp
[Test]
public async Task EntitiesAreIsolatedByTenant()
{
    var acmeClient = CreateClient("acme");
    var contosoClient = CreateClient("contoso");
    
    var book = await acmeClient.PostAsync("/api/admin/books", data);
    var response = await contosoClient.GetAsync($"/api/books/{book.Id}");
    Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
}
```

---

## Production Deployment

### Checklist

- [ ] Verify all `tenant_id` indexes exist
- [ ] Configure Redis for distributed caching
- [ ] Set up audit log aggregation
- [ ] Configure rate limits per tenant tier
- [ ] Schedule monthly index rebuilds

### Environment Variables

```bash
ConnectionStrings__redis=localhost:6379
RateLimit__PermitLimit=1000
RateLimit__WindowInMinutes=1
```

### Maintenance

For index rebuild schedules and vacuum strategies, see [Database Indexes Guide - Maintenance](database-indexes-guide.md#maintenance).

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Tenant not displaying | Restart Docker |
| 404 on `/api/tenants/{id}` | Verify seeding, check `[DoNotPartition]` |
| Slow queries | Check index usage with `EXPLAIN ANALYZE` |

---

## References

- [Marten Event Store Multi-Tenancy](https://martendb.io/events/multitenancy.html)
- [Marten Document Multi-Tenancy](https://martendb.io/documents/multi-tenancy.html)
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Database Indexes Guide](database-indexes-guide.md) - Comprehensive indexing strategies for production
