using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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

    static async Task<Ok<PagedListAdapter<PublisherDto>>> GetPublishers(
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [AsParameters] OrderedPagedRequest request)
    {
        var paging = request.Normalize(paginationOptions.Value);

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        IQueryable<PublisherProjection> query = session.Query<PublisherProjection>();

        query = (normalizedSortBy, normalizedSortOrder) switch
        {
            ("name", "desc") => query
                .OrderByDescending(p => p.Name),
            _ => query.OrderBy(p => p.Name)
        };

        // Use Marten's native pagination for optimal performance
        var pagedList = await query
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);

        // Map to DTOs using LINQ's lazy Select (no intermediate collection)
        var mappedList = pagedList.Select(p => new PublisherDto(p.Id, p.Name)).ToList();

        // Wrap in adapter for zero-allocation serialization
        var adapter = new PagedListAdapter<PublisherDto>(
            new PagedListWrapper<PublisherDto>(
                mappedList,
                pagedList.PageNumber,
                pagedList.PageSize,
                pagedList.TotalItemCount));

        return TypedResults.Ok(adapter);
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<PublisherDto>, NotFound>> GetPublisher(
        Guid id,
        [FromServices] IQuerySession session)
    {
        var publisher = await session.LoadAsync<PublisherProjection>(id);
        if (publisher == null)
        {
            return TypedResults.NotFound();
        }

        var publisherDto = new PublisherDto(publisher.Id, publisher.Name);
        return TypedResults.Ok(publisherDto);
    }
}
