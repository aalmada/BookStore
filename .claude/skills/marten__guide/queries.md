# Query Endpoints

## Quick Decision Guide

| Scenario | Use |
|----------|-----|
| Fetch a single resource by ID | [Get-by-ID endpoint](#get-by-id-endpoint) |
| Fetch a paged, filterable list | [List query endpoint](#list-query-endpoint) |

Ensure the corresponding Projection exists before implementing an endpoint. See [`projections.md`](projections.md).

---

## Get-by-ID endpoint

Follow this guide to implement a **Get By ID** endpoint in the ApiService.

1.  **Prerequisites**
    -   Ensure the Projection exists (see [`projections.md`](projections.md) — POCO snapshot section).

2.  **Create Endpoint**
    -   Create/Update `src/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`
    -   Use **named method handlers** (not inline lambdas) for testability
    -   Inject `IDocumentStore` (not `IDocumentSession`) — create a `QuerySession` per request scoped to the current tenant
    -   Inject `ITenantContext` for the tenant ID
    -   Include the tenant ID in the cache key to prevent cross-tenant data leakage
    -   Return `TypedResults.Ok(...)` / `TypedResults.NotFound()` (not `Results.Ok`)
    -   **Template**: `templates/GetByIdEndpoint.cs`

3.  **Client Integration**
    -   Create `IGet{Resource}Endpoint.cs` in Client project.

---

## List query endpoint

Follow this guide to implement a **List Query** endpoint in the ApiService.

1.  **Prerequisites**
    -   Ensure the Projection exists.
    -   Ensure indexes are configured for filtered/sorted fields.

2.  **Create Endpoint**
    -   Create/Update `src/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`
    -   Inject `IDocumentStore`, `ITenantContext`, `IOptions<PaginationOptions>`, `IOptions<LocalizationOptions>`, `HybridCache`
    -   Open a `QuerySession` scoped to the tenant: `store.QuerySession(tenantContext.TenantId)`
    -   Include tenant ID and all filter/sort parameters in the cache key
    -   Cache with explicit `HybridCacheEntryOptions` (Expiration + LocalCacheExpiration)
    -   Use `TypedResults.Ok(...)` (not `Results.Ok`)
    -   **Template**: `templates/ListEndpoint.cs`

3.  **Client Integration**
    -   Create `IGet{Resource}sEndpoint.cs` in Client project.
