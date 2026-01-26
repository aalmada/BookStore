group.MapGet("/{id}", async Task<IResult> (
    Guid id,
    IDocumentStore store,
    HybridCache cache,
    CancellationToken cancellationToken) =>
{
    var key = $"{resource.ToLower()}s:{id}";
    
    var result = await cache.GetOrCreateLocalizedAsync(
        key,
        async (cancel) =>
        {
            await using var session = store.QuerySession();
            var item = await session.LoadAsync<{Resource}Projection>(id, cancel);
            
            if (item == null) return null; // Handle in outer scope

            // return MapToDto(item);
            return item;
        },
        tags: [CacheTags.{Resource}List, $"{{resource.ToLower()}}:{id}"], // Tag with list to invalidate on update if needed, and specific ID
        cancellationToken: cancellationToken
    );

    return result is not null ? Results.Ok(result) : Results.NotFound();
})
.WithSummary("Get {resource}")
.WithDescription("Gets a specific {resource} by ID.");
