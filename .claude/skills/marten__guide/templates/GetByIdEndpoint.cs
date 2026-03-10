// Inject ITenantContext to scope data and cache keys per tenant.
// Use TypedResults.Ok / TypedResults.NotFound (not Results.Ok).
// Use named method handlers instead of inline lambdas.

static async Task<IResult> Get{Resource}(
    Guid id,
    [FromServices] IDocumentStore store,
    [FromServices] ITenantContext tenantContext,
    [FromServices] IOptions<LocalizationOptions> localizationOptions,
    [FromServices] HybridCache cache,
    CancellationToken cancellationToken)
{
    var response = await cache.GetOrCreateLocalizedAsync(
        $"{resource.ToLower()}:{id}:tenant={tenantContext.TenantId}",
        async cancel =>
        {
            await using var session = store.QuerySession(tenantContext.TenantId);
            var item = await session.LoadAsync<{Resource}Projection>(id, cancel);

            if (item is null || item.Deleted)
                return null;

            // Map to DTO
            return item;
        },
        options: new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(2)
        },
        tags: [$"{resource.ToLower()}:{id}", CacheTags.{Resource}List],
        token: cancellationToken);

    return response is not null ? TypedResults.Ok(response) : TypedResults.NotFound();
}
