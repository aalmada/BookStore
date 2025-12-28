using Marten;
using BookStore.ApiService.Models;

using Microsoft.AspNetCore.Http.HttpResults;
using Marten.Pagination;


using BookStore.ApiService.Projections;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace BookStore.ApiService.Endpoints;

public static class BookEndpoints
{
    public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", SearchBooks)
            .WithName("GetBooks")
            .WithSummary("Get all books")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(2)));

        group.MapGet("/{id:guid}", GetBook)
            .WithName("GetBook")
            .WithSummary("Get book by ID")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("If-None-Match"));

        return group;
    }

    static async Task<Ok<IPagedList<BookSearchProjection>>> SearchBooks(
        [FromServices] IQuerySession session,
        [AsParameters] PagedRequest request,
        [FromQuery] string? q = null)
    {
        var paging = request.Normalize();

        if (string.IsNullOrWhiteSpace(q))
        {
            // Return all books if no search query - use Marten's native pagination
            var pagedList = await session.Query<BookSearchProjection>()
                .OrderBy(b => b.Title)
                .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

            return TypedResults.Ok(pagedList);
        }

        // Use NGram search for fuzzy, accent-insensitive matching
        // This leverages the pg_trgm indexes we configured
        var searchQuery = q.Trim();
        
        var query = session.Query<BookSearchProjection>()
            .Where(b => 
                b.Title.NgramSearch(searchQuery) ||
                (b.Description != null && b.Description.NgramSearch(searchQuery)) ||
                (b.Isbn != null && b.Isbn.Contains(searchQuery)) ||  // Exact match for ISBN
                (b.PublisherName != null && b.PublisherName.NgramSearch(searchQuery)) ||
                b.AuthorNames.NgramSearch(searchQuery))
                // Note: CategoryNames excluded - use filtering instead of text search
            .OrderBy(b => b.Title);
        
        // Use Marten's native pagination for optimal performance
        var searchResults = await query.ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        return TypedResults.Ok(searchResults);
    }

    static async Task<IResult> GetBook(
        Guid id,
        [FromServices] IQuerySession session,
        HttpContext context)
    {
        var book = await session.LoadAsync<BookSearchProjection>(id);
        
        if (book == null)
            return TypedResults.NotFound();

        // Get stream state for ETag
        var streamState = await session.Events.FetchStreamStateAsync(id);
        if (streamState != null)
        {
            var etag = Infrastructure.ETagHelper.GenerateETag(streamState.Version);
            
            // Check If-None-Match for caching
            if (Infrastructure.ETagHelper.CheckIfNoneMatch(context, etag))
            {
                return Infrastructure.ETagHelper.NotModified(etag);
            }
            
            Infrastructure.ETagHelper.AddETagHeader(context, etag);
        }

        return TypedResults.Ok(book);
    }
}
