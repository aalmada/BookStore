using System.Globalization;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
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
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [AsParameters] PagedRequest request,
        HttpContext context)
    {
        var paging = request.Normalize(paginationOptions.Value);

        // Use Marten's native pagination for optimal performance
        // Note: We can't sort by localized name directly in the query, so we sort by ID
        var pagedList = await session.Query<CategoryProjection>()
            .OrderBy(c => c.Id)
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        // Map to localized responses
        var items = pagedList.Select(c => LocalizeCategory(c, context, localizationOptions.Value)).ToList();
        
        var response = new PagedListDto<CategoryDto>(
            items,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount);

        return TypedResults.Ok(response);
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<CategoryDto>, NotFound>> GetCategory(
        Guid id,
        [FromServices] IQuerySession session,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        HttpContext context)
    {
        var category = await session.LoadAsync<CategoryProjection>(id);
        if (category == null)
        {
            return TypedResults.NotFound();
        }

        var response = LocalizeCategory(category, context, localizationOptions.Value);
        return TypedResults.Ok(response);
    }

    static CategoryDto LocalizeCategory(
        CategoryProjection category,
        HttpContext context,
        LocalizationOptions options)
    {
        var localizedName = LocalizationHelper.GetLocalizedValue(
            context,
            options,
            category.Translations,
            translation => translation.Name,
            defaultValue: "Unknown");

        return new CategoryDto(category.Id, localizedName);
    }
}
