// Inject ITenantContext, IOptions<PaginationOptions>, IOptions<LocalizationOptions>.
// Include tenant ID and all query parameters in the cache key.
// Use TypedResults.Ok (not Results.Ok).
// Use named method handlers instead of inline lambdas.

static async Task<Ok<PagedListDto<{Resource}Dto>>> Get{Resource}s(
    [AsParameters] {Resource}SearchRequest request,
    [FromServices] IDocumentStore store,
    [FromServices] ITenantContext tenantContext,
    [FromServices] IOptions<PaginationOptions> paginationOptions,
    [FromServices] IOptions<LocalizationOptions> localizationOptions,
    [FromServices] HybridCache cache,
    CancellationToken cancellationToken)
{
    var paging = request.Normalize(paginationOptions.Value);
    var normalizedSort = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
    var normalizedSortBy = request.SortBy?.ToLowerInvariant();

    var cacheKey = $"{resource.ToLower()}s:tenant={tenantContext.TenantId}:search={request.Search}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSort}";

    var response = await cache.GetOrCreateLocalizedAsync(
        cacheKey,
        async cancel =>
        {
            await using var session = store.QuerySession(tenantContext.TenantId);

            var query = session.Query<{Resource}Projection>()
                .Where(x => !x.Deleted);

            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(x => x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase));

            query = (normalizedSortBy, normalizedSort) switch
            {
                ("name", "desc") => query.OrderByDescending(x => x.Name),
                _ => query.OrderBy(x => x.Name)
            };

            var pagedList = await query
                .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancel);

            var dtos = pagedList.Select(x => new {Resource}Dto(x.Id, x.Name /*, ... */)).ToList();

            return new PagedListDto<{Resource}Dto>(dtos, pagedList.PageNumber, pagedList.PageSize, pagedList.TotalItemCount);
        },
        options: new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(2)
        },
        tags: [CacheTags.{Resource}List],
        token: cancellationToken);

    return TypedResults.Ok(response);
}
