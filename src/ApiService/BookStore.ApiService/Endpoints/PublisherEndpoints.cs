using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class PublisherEndpoints
{
    public static RouteGroupBuilder MapPublisherEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetPublishers)
            .WithName("GetPublishers")
            .WithSummary("Get all publishers")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

        _ = group.MapGet("/{id:guid}", GetPublisher)
            .WithName("GetPublisher")
            .WithSummary("Get publisher by ID")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

        return group;
    }

    static async Task<Ok<IPagedList<PublisherProjection>>> GetPublishers(
        [FromServices] IQuerySession session,
        [AsParameters] PagedRequest request)
    {
        var paging = request.Normalize();

        // Use Marten's native pagination for optimal performance
        var pagedList = await session.Query<PublisherProjection>()
            .OrderBy(p => p.Name)
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        return TypedResults.Ok(pagedList);
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<PublisherProjection>, NotFound>> GetPublisher(
        Guid id,
        [FromServices] IQuerySession session)
    {
        var publisher = await session.LoadAsync<PublisherProjection>(id);
        if (publisher == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(publisher);
    }
}
