using Marten;
using BookStore.ApiService.Models;

using Microsoft.AspNetCore.Http.HttpResults;
using Marten.Pagination;


using BookStore.ApiService.Projections;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class AuthorEndpoints
{
    public static RouteGroupBuilder MapAuthorEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAuthors)
            .WithName("GetAuthors")
            .WithSummary("Get all active authors")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

        group.MapGet("/{id:guid}", GetAuthor)
            .WithName("GetAuthor")
            .WithSummary("Get author by ID")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

        return group;
    }

    static async Task<Ok<IPagedList<AuthorProjection>>> GetAuthors(
        [FromServices] IQuerySession session,
        [AsParameters] PagedRequest request)
    {
        var paging = request.Normalize();
        
        // Use Marten's native pagination for optimal performance
        var pagedList = await session.Query<AuthorProjection>()
            .OrderBy(a => a.Name)
            .ToPagedListAsync(paging.Page, paging.PageSize);

        return TypedResults.Ok(pagedList);
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<AuthorProjection>, NotFound>> GetAuthor(
        Guid id,
        [FromServices] IQuerySession session)
    {
        var author = await session.LoadAsync<AuthorProjection>(id);
        if (author == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(author);
    }
}
