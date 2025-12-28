using System.Globalization;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class CategoryEndpoints
{
    public record CategoryResponse(Guid Id, string Name);

    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetCategories)
            .WithName("GetCategories")
            .WithSummary("Get all categories")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

        _ = group.MapGet("/{id:guid}", GetCategory)
            .WithName("GetCategory")
            .WithSummary("Get category by ID")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

        return group;
    }

    static async Task<IResult> GetCategories(
        [FromServices] IQuerySession session,
        [AsParameters] PagedRequest request,
        HttpContext context)
    {
        var paging = request.Normalize();
        var language = GetPreferredLanguage(context);

        // Use Marten's native pagination for optimal performance
        var pagedList = await session.Query<CategoryProjection>()
            .OrderBy(c => c.Name)
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        // Return the paged list - localization will be handled by the client or in a response DTO
        // For now, we'll map to localized responses inline
        var localizedResponse = new
        {
            Items = pagedList.Select(c => LocalizeCategory(c, language)).ToList(),
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount,
            pagedList.PageCount,
            pagedList.HasPreviousPage,
            pagedList.HasNextPage
        };

        return TypedResults.Ok(localizedResponse);
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<CategoryResponse>, NotFound>> GetCategory(
        Guid id,
        [FromServices] IQuerySession session,
        HttpContext context)
    {
        var language = GetPreferredLanguage(context);
        var category = await session.LoadAsync<CategoryProjection>(id);
        if (category == null)
        {
            return TypedResults.NotFound();
        }

        var response = LocalizeCategory(category, language);
        return TypedResults.Ok(response);
    }

    static string GetPreferredLanguage(HttpContext context)
    {
        // Get Accept-Language header
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();

        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return "en"; // Default to English
        }

        // Parse the first language from Accept-Language header
        // Format: "en-US,en;q=0.9,pt;q=0.8"
        var languages = acceptLanguage.Split(',')
            .Select(lang => lang.Split(';')[0].Trim())
            .ToList();

        // Return the first two-letter language code
        var primaryLanguage = languages.FirstOrDefault() ?? "en";
        return primaryLanguage.Length >= 2 ? primaryLanguage[..2].ToLower() : "en";
    }

    static CategoryResponse LocalizeCategory(CategoryProjection category, string language)
    {
        // Try to get translation for the requested language
        if (category.Translations.TryGetValue(language, out var translation))
        {
            return new CategoryResponse(
                category.Id,
                translation.Name);
        }

        // Fallback to default (English or original)
        return new CategoryResponse(
            category.Id,
            category.Name);
    }
}
