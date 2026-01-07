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

public static class AuthorEndpoints
{
    public static RouteGroupBuilder MapAuthorEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetAuthors)
            .WithName("GetAuthors")
            .WithSummary("Get all authors")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("Accept-Language"));

        _ = group.MapGet("/{id:guid}", GetAuthor)
            .WithName("GetAuthor")
            .WithSummary("Get author by ID")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("Accept-Language"));

        return group;
    }

    static async Task<Ok<PagedListDto<AuthorDto>>> GetAuthors(
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

        IQueryable<AuthorProjection> query = session.Query<AuthorProjection>();

        // Can sort by Name since it's not localized
        query = (normalizedSortBy, normalizedSortOrder) switch
        {
            ("name", "desc") => query.OrderByDescending(a => a.Name),
            _ => query.OrderBy(a => a.Name)
        };

        var pagedList = await query
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        // Extract localized biographies using LocalizationHelper
        var authorDtos = pagedList.Select(author => new AuthorDto(
            author.Id,
            author.Name,
            LocalizationHelper.GetLocalizedValue(author.Biographies, culture, defaultCulture, "")
        )).ToList();

        return TypedResults.Ok(new PagedListDto<AuthorDto>(
            authorDtos,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount));
    }

    static async Task<Results<Ok<AuthorDto>, NotFound>> GetAuthor(
        Guid id,
        [FromServices] IDocumentStore store,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        HttpContext context)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;
        await using var session = store.QuerySession();

        var author = await session.LoadAsync<AuthorProjection>(id);
        if (author == null)
        {
            return TypedResults.NotFound();
        }

        // Extract localized biography using LocalizationHelper
        var localizedBiography = LocalizationHelper.GetLocalizedValue(
            author.Biographies,
            culture,
            defaultCulture,
            "");

        var authorDto = new AuthorDto(
            author.Id,
            author.Name,
            localizedBiography);

        return TypedResults.Ok(authorDto);
    }
}
