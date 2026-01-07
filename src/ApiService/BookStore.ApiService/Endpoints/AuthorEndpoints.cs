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

public static class AuthorEndpoints
{
    public static RouteGroupBuilder MapAuthorEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetAuthors)
            .WithName("GetAuthors")
            .WithSummary("Get all authors");

        _ = group.MapGet("/{id:guid}", GetAuthor)
            .WithName("GetAuthor")
            .WithSummary("Get author by ID");

        return group;
    }

    static async Task<Ok<PagedListDto<AuthorDto>>> GetAuthors(
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
        var cacheKey = $"authors:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

        var response = await cache.GetOrCreateLocalizedAsync(
            cacheKey,
            async cancel =>
            {
                await using var session = store.QuerySession();

                IQueryable<AuthorProjection> query = session.Query<AuthorProjection>();

                // Can sort by Name since it's not localized
                query = (normalizedSortBy, normalizedSortOrder) switch
                {
                    ("name", "desc") => query.OrderByDescending(a => a.Name),
                    _ => query.OrderBy(a => a.Name)
                };

                var pagedList = await query
                    .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancel);

                // Extract localized biographies using LocalizationHelper
                var authorDtos = pagedList.Select(author => new AuthorDto(
                    author.Id,
                    author.Name,
                    LocalizationHelper.GetLocalizedValue(author.Biographies, culture, defaultCulture, "")
                )).ToList();

                return new PagedListDto<AuthorDto>(
                    authorDtos,
                    pagedList.PageNumber,
                    pagedList.PageSize,
                    pagedList.TotalItemCount);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: ["authors"],
            token: cancellationToken);

        return TypedResults.Ok(response);
    }

    static async Task<Results<Ok<AuthorDto>, NotFound>> GetAuthor(
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
            $"author:{id}",
            async cancel =>
            {
                await using var session = store.QuerySession();
                var author = await session.LoadAsync<AuthorProjection>(id, cancel);
                if (author == null)
                {
                    return (AuthorDto?)null;
                }

                // Extract localized biography using LocalizationHelper
                var localizedBiography = LocalizationHelper.GetLocalizedValue(
                    author.Biographies,
                    culture,
                    defaultCulture,
                    "");

                return new AuthorDto(
                    author.Id,
                    author.Name,
                    localizedBiography);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [$"author:{id}"],
            token: cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }
}

