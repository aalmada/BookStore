using System.Globalization;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetCategories)
            .WithName("GetCategories")
            .WithSummary("Get all categories");

        _ = group.MapGet("/{id:guid}", GetCategory)
            .WithName("GetCategory")
            .WithSummary("Get category by ID");

        return group;
    }

    static async Task<Ok<PagedListDto<CategoryDto>>> GetCategories(
        [FromServices] IDocumentStore store,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        [AsParameters] OrderedPagedRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;
        var paging = request.Normalize(paginationOptions.Value);

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        // Create cache key based on pagination, sorting, AND Tenant
        var cacheKey = $"categories:tenant={tenantContext.TenantId}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

        var response = await cache.GetOrCreateLocalizedAsync(
            cacheKey,
            async cancel =>
            {
                await using var session = store.QuerySession(tenantContext.TenantId);

                var query = session.Query<CategoryProjection>()
                    .Where(c => !c.Deleted);

                // Note: Cannot sort by localized name since it's in a dictionary
                // Sorting by ID only
                query = (normalizedSortBy, normalizedSortOrder) switch
                {
                    ("id", "desc") => query.OrderByDescending(c => c.Id),
                    _ => query.OrderBy(c => c.Id) // Default to ID asc
                };

                var pagedList = await query
                    .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancel);

                // Extract localized names using LocalizationHelper
                var items = pagedList.Select(c => new CategoryDto(
                    c.Id,
                    LocalizationHelper.GetLocalizedValue(c.Names, culture, defaultCulture, "Unknown")
                )).ToList();

                return new PagedListDto<CategoryDto>(
                    items,
                    pagedList.PageNumber,
                    pagedList.PageSize,
                    pagedList.TotalItemCount);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheTags.CategoryList],
            token: cancellationToken);

        return TypedResults.Ok(response);
    }

    static async Task<Results<Ok<CategoryDto>, NotFound>> GetCategory(
        Guid id,
        [FromServices] IDocumentStore store,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        var projection = await cache.GetOrCreateLocalizedAsync(
            $"category:projection:{id}:tenant={tenantContext.TenantId}",
            async cancel =>
            {
                await using var session = store.QuerySession(tenantContext.TenantId);
                var category = await session.LoadAsync<CategoryProjection>(id, cancel);
                if (category == null || category.Deleted)
                {
                    return (CategoryProjection?)null;
                }

                return category;
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheTags.ForItem(CacheTags.CategoryItemPrefix, id)],
            token: cancellationToken);

        if (projection == null)
        {
            return TypedResults.NotFound();
        }

        // Extract localized name using LocalizationHelper
        var localizedName = LocalizationHelper.GetLocalizedValue(
            projection.Names,
            culture,
            defaultCulture,
            "Unknown");

        return TypedResults.Ok(new CategoryDto(projection.Id, localizedName));
    }
}
