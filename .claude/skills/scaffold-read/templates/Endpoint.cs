public static async Task<Ok<PagedListDto<Dto>>> GetItems(
    [FromServices] IDocumentStore store,
    [FromServices] HybridCache cache,
    [FromServices] IOptions<LocalizationOptions> locOptions,
    [AsParameters] OrderedPagedRequest request,
    CancellationToken cancellationToken)
{
    // ... cache and query implementation
}
