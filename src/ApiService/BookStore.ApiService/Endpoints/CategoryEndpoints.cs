using System.Globalization;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

public static class CategoryEndpoints
{

    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetCategories)
            .WithName("GetCategories")
            .WithSummary("Get all categories")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("Accept-Language"));

        _ = group.MapGet("/{id:guid}", GetCategory)
            .WithName("GetCategory")
            .WithSummary("Get category by ID")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("Accept-Language"));

        return group;
    }

    static async Task<Ok<PagedListDto<CategoryDto>>> GetCategories(
        [FromServices] IDocumentStore store,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [AsParameters] OrderedPagedRequest request,
        HttpContext context)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;
        await using var session = store.QuerySession();
        var paging = request.Normalize(paginationOptions.Value);

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        IQueryable<CategoryProjection> query = session.Query<CategoryProjection>();

        // Note: Cannot sort by localized name since it's in a dictionary
        // Sorting by ID only
        query = (normalizedSortBy, normalizedSortOrder) switch
        {
            ("id", "desc") => query.OrderByDescending(c => c.Id),
            _ => query.OrderBy(c => c.Id) // Default to ID asc
        };

        var pagedList = await query
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        // Extract localized names using LocalizationHelper
        var items = pagedList.Select(c => new CategoryDto(
            c.Id,
            LocalizationHelper.GetLocalizedValue(c.Names, culture, defaultCulture, "Unknown")
        )).ToList();

        var response = new PagedListDto<CategoryDto>(
            items,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount);

        return TypedResults.Ok(response);
    }

    static async Task<Results<Ok<CategoryDto>, NotFound>> GetCategory(
        Guid id,
        [FromServices] IDocumentStore store,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        HttpContext context)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;
        await using var session = store.QuerySession();

        var category = await session.LoadAsync<CategoryProjection>(id);
        if (category == null)
        {
            return TypedResults.NotFound();
        }

        // Extract localized name using LocalizationHelper
        var localizedName = LocalizationHelper.GetLocalizedValue(
            category.Names,
            culture,
            defaultCulture,
            "Unknown");

        var response = new CategoryDto(category.Id, localizedName);
        return TypedResults.Ok(response);
    }
}
