using System.Globalization;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Projections;
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
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        [AsParameters] OrderedPagedRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;
        var paging = request.Normalize(paginationOptions.Value);

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        // Create cache key based on pagination and sorting
        var cacheKey = $"categories:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

        var response = await cache.GetOrCreateLocalizedAsync(
            cacheKey,
            async cancel =>
            {
                await using var session = store.QuerySession();

                IQueryable<CategoryProjection> query = session.Query<CategoryProjection>();

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
            tags: ["categories"],
            token: cancellationToken);

        return TypedResults.Ok(response);
    }

    static async Task<Results<Ok<CategoryDto>, NotFound>> GetCategory(
        Guid id,
        [FromServices] IDocumentStore store,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        var response = await cache.GetOrCreateLocalizedAsync(
            $"category:{id}",
            async cancel =>
            {
                await using var session = store.QuerySession();
                var category = await session.LoadAsync<CategoryProjection>(id, cancel);
                if (category == null)
                {
                    return (CategoryDto?)null;
                }

                // Extract localized name using LocalizationHelper
                var localizedName = LocalizationHelper.GetLocalizedValue(
                    category.Names,
                    culture,
                    defaultCulture,
                    "Unknown");

                return new CategoryDto(category.Id, localizedName);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [$"category:{id}"],
            token: cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }
}

