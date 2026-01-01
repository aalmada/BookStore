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
        [AsParameters] PagedRequest request,
        HttpContext context)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        await using var session = store.QuerySession(culture);
        var paging = request.Normalize(paginationOptions.Value);

        var pagedList = await session.Query<AuthorProjection>()
            .OrderBy(a => a.Name)
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        var authorDtos = pagedList.Select(author => new AuthorDto(
            author.Id,
            author.Name,
            author.Biography
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
        HttpContext context)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        await using var session = store.QuerySession(culture);

        var author = await session.LoadAsync<AuthorProjection>(id);
        if (author == null)
        {
            return TypedResults.NotFound();
        }

        var authorDto = new AuthorDto(
            author.Id,
            author.Name,
            author.Biography);

        return TypedResults.Ok(authorDto);
    }
}
