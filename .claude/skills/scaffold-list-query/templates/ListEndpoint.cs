group.MapGet("/", async Task<IResult> (
    [AsParameters] {Resource}Filter requests, // Define Filter record with Page, PageSize, Sort, etc.
    IDocumentStore store,
    HybridCache cache,
    CancellationToken cancellationToken) =>
{
    var key = $"{resource.ToLower()}s:page:{requests.Page}:size:{requests.PageSize}";
    
    var result = await cache.GetOrCreateLocalizedAsync(
        key,
        async (cancel) =>
        {
            await using var session = store.QuerySession();
            var query = session.Query<{Resource}Projection>()
                .Where(x => !x.Deleted);
            
            // Apply other filters
            // if (!string.IsNullOrEmpty(requests.Search)) ...

            return await query.ToPagedListAsync(requests.Page, requests.PageSize, cancel);
        },
        tags: [CacheTags.{Resource}List],
        cancellationToken: cancellationToken
    );

    return Results.Ok(result);
})
.WithSummary("List {resource}s")
.WithDescription("Gets a paged list of {resource}s.");
