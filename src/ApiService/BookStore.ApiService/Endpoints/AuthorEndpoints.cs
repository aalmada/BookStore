using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
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
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [AsParameters] PagedRequest request,
        HttpContext context)
    {
        var paging = request.Normalize(paginationOptions.Value);

        // Use Marten's native pagination for optimal performance
        var pagedList = await session.Query<AuthorProjection>()
            .OrderBy(a => a.Name)
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        // Map to DTOs with localized biographies
        var authorDtos = pagedList.Select(author => new AuthorDto(
            author.Id,
            author.Name,
            LocalizeBiography(author, context, localizationOptions.Value)
        )).ToList();

        return TypedResults.Ok(new PagedListDto<AuthorDto>(
            authorDtos,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount));
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<AuthorDto>, NotFound>> GetAuthor(
        Guid id,
        [FromServices] IQuerySession session,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        HttpContext context)
    {
        var author = await session.LoadAsync<AuthorProjection>(id);
        if (author == null)
        {
            return TypedResults.NotFound();
        }

        var authorDto = new AuthorDto(
            author.Id,
            author.Name,
            LocalizeBiography(author, context, localizationOptions.Value));

        return TypedResults.Ok(authorDto);
    }

    // Helper method for author biography localization
    static string? LocalizeBiography(
        AuthorProjection author,
        HttpContext context,
        LocalizationOptions options)
    {
        if (author.Translations.Count == 0)
        {
            return null;
        }

        return LocalizationHelper.GetLocalizedValue(
            context,
            options,
            author.Translations,
            translation => translation.Biography,
            defaultValue: string.Empty);
    }
}
