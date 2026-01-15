---
name: scaffold-projection
description: Create a new Marten read model projection for CQRS queries. Use this when you need to create an optimized read model from event streams.
license: MIT
---

Follow this guide to create a new **Marten projection** (read model) for efficient queries in the ApiService.

1. **Define the Projection Class**
   - Create a `class` in `src/BookStore.ApiService/Projections/`
   - **Naming**: `{Resource}Projection` (e.g., `AuthorProjection`)
   - **Inheritance**: Inherit from appropriate Marten base or use `[ViewProjection]`
   - **Template**:
     ```csharp
     namespace BookStore.ApiService.Projections;
     
     public class AuthorProjection
     {
         public Guid Id { get; set; }
         public string Name { get; set; } = string.Empty;
         public string Biography { get; set; } = string.Empty;
         public bool Deleted { get; set; }
         public int Version { get; set; }
         public DateTimeOffset CreatedAt { get; set; }
         public DateTimeOffset? UpdatedAt { get; set; }
         
         // For localized content, use Dictionary<string, string>
         public Dictionary<string, string> Biographies { get; set; } = new();
     }
     ```

2. **Implement Create Method**
   - Handle the initial event to create the projection
   - **Pattern**: Static method returning projection instance
   - **Example**:
     ```csharp
     public static AuthorProjection Create(AuthorCreated @event)
     {
         return new AuthorProjection
         {
             Id = @event.Id,
             Name = @event.Name,
             Biography = @event.Biography,
             CreatedAt = @event.CreatedAt,
             Version = 1
         };
     }
     ```

3. **Implement Apply Methods**
   - Handle subsequent events to update the projection
   - **Pattern**: Instance method, returns void, single event parameter
   - **Example**:
     ```csharp
     public void Apply(AuthorUpdated @event)
     {
         Name = @event.Name;
         Biography = @event.Biography;
         UpdatedAt = @event.UpdatedAt;
         Version++;
     }
     
     public void Apply(AuthorDeleted @event)
     {
         Deleted = true;
         UpdatedAt = @event.DeletedAt;
         Version++;
     }
     
     public void Apply(AuthorRestored @event)
     {
         Deleted = false;
         UpdatedAt = @event.RestoredAt;
         Version++;
     }
     ```

4. **Configure Marten Projection**
   - Open `src/BookStore.ApiService/Program.cs`
   - Register the projection:
     ```csharp
     builder.Services.AddMarten(options =>
     {
         // Existing configuration...
         
         // Add inline projection
         options.Projections.Add<AuthorProjection>(ProjectionLifecycle.Inline);
         
         // OR for async projection (recommended for production)
         options.Projections.Add<AuthorProjection>(ProjectionLifecycle.Async);
     });
     ```

5. **Add Indexing (Optional but Recommended)**
   - For search and filtering performance:
     ```csharp
     builder.Services.AddMarten(options =>
     {
         options.Schema.For<AuthorProjection>()
             .Index(x => x.Name)          // For name searches
             .Index(x => x.Deleted)       // For filtering deleted items
             .GinIndexJsonData();         // For full-text search (if using JSONB)
     });
     ```

6. **Create Queries Using the Projection**
   - In endpoints, query the projection (not the aggregate):
     ```csharp
     public static async Task<IResult> GetAuthors(
         IDocumentStore store,
         int page = 1,
         int pageSize = 20,
         CancellationToken cancellationToken = default)
     {
         await using var session = store.QuerySession();
         
         var authors = await session.Query<AuthorProjection>()
             .Where(x => !x.Deleted)
             .OrderBy(x => x.Name)
             .ToPagedListAsync(page, pageSize, cancellationToken);
         
         return Results.Ok(authors);
     }
     ```

7. **Add Caching (Recommended)**
   - Wrap projection queries with HybridCache:
     ```csharp
     public static async Task<IResult> GetAuthors(
         IDocumentStore store,
         HybridCache cache,
         int page = 1,
         int pageSize = 20,
         CancellationToken cancellationToken = default)
     {
         var cacheKey = $"authors:page:{page}:size:{pageSize}";
         
         var authors = await cache.GetOrCreateAsync(
             cacheKey,
             async entry =>
             {
                 entry.SetOptions(new HybridCacheEntryOptions
                 {
                     Expiration = TimeSpan.FromMinutes(5),
                     LocalCacheExpiration = TimeSpan.FromMinutes(1)
                 });
                 
                 await using var session = store.QuerySession();
                 return await session.Query<AuthorProjection>()
                     .Where(x => !x.Deleted)
                     .OrderBy(x => x.Name)
                     .ToPagedListAsync(page, pageSize, cancellationToken);
             },
             tags: [CacheTags.AuthorList],
             cancellationToken: cancellationToken
         );
         
         return Results.Ok(authors);
     }
     ```

8. **Rebuild Projections (Development)**
   - To rebuild all projections from events:
     ```bash
     curl -X POST http://localhost:5000/api/admin/projections/rebuild
     ```

## Projection Types

### Inline Projections
- **When**: Real-time updates required
- **Pro**: Always up-to-date
- **Con**: Slower writes (blocks until projection updated)
- **Use**: Small datasets, critical consistency

### Async Projections
- **When**: High write volume
- **Pro**: Non-blocking writes, better performance
- **Con**: Eventually consistent (slight delay)
- **Use**: Large datasets, acceptable eventual consistency

## Localized Content Pattern

For multi-language support:

```csharp
public class AuthorProjection
{
    // Store all translations
    public Dictionary<string, string> Biographies { get; set; } = new();
    
    // Helper method to get localized value
    public string GetBiography(string culture, string defaultCulture = "en")
    {
        return LocalizationHelper.GetLocalizedValue(
            Biographies,
            culture,
            defaultCulture,
            ""
        );
    }
}
```

## Performance Tips

- ✅ **Index frequently queried fields** (Name, Deleted, etc.)
- ✅ **Use GIN indexes for JSONB search** (full-text)
- ✅ **Limit projection size** (only include query-required fields)
- ✅ **Use async projections** for high-volume events
- ✅ **Cache projection queries** with appropriate invalidation

## Troubleshooting

**Projection Not Updating**
1. Check Marten configuration includes the projection
2. Verify Apply method signatures match events exactly
3. For async projections, check daemon is running
4. Run projection rebuild: `POST /api/admin/projections/rebuild`

**Missing Data in Queries**
1. Ensure projection includes all required fields
2. Check Apply methods handle all relevant events
3. Verify indexes exist for queried fields

## Related Skills

**Prerequisites**:
- `/scaffold-aggregate` - Ensure events and aggregates exist first
- Events should be defined before creating projections

**Next Steps**:
- `/scaffold-read` - Create query endpoints using this projection
- `/scaffold-write` - Ensure MartenCommitListener sends SSE notifications
- `/scaffold-test` - Create integration tests
- `/verify-feature` - Complete verification

**Related**:
- `/debug-cache` - If cached projection data is stale

**See Also**:
- [scaffold-aggregate](../scaffold-aggregate/SKILL.md) - Event and aggregate patterns
- [scaffold-read](../scaffold-read/SKILL.md) - Query endpoint implementation
- [scaffold-write](../scaffold-write/SKILL.md) - SSE notification setup
- ApiService AGENTS.md - Projection patterns and Marten configuration
