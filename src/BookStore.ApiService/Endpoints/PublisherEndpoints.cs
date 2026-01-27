using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

public static class PublisherEndpoints
{
    public static RouteGroupBuilder MapPublisherEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetPublishers)
            .WithName("GetPublishers")
            .WithSummary("Get all publishers");

        _ = group.MapGet("/{id:guid}", GetPublisher)
            .WithName("GetPublisher")
            .WithSummary("Get publisher by ID");

        return group;
    }

    static async Task<Ok<PagedListAdapter<PublisherDto>>> GetPublishers(
        [FromServices] IDocumentStore store,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] HybridCache cache,
        [AsParameters] OrderedPagedRequest request,
        CancellationToken cancellationToken)
    {
        var paging = request.Normalize(paginationOptions.Value);

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        // Create cache key based on pagination, sorting, AND Tenant
        var cacheKey = $"publishers:tenant={tenantContext.TenantId}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

        var response = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                await using var session = store.QuerySession(tenantContext.TenantId);

                var query = session.Query<PublisherProjection>()
                    .Where(p => !p.Deleted);

                query = (normalizedSortBy, normalizedSortOrder) switch
                {
                    ("name", "desc") => query
                        .OrderByDescending(p => p.Name),
                    _ => query.OrderBy(p => p.Name)
                };

                // Use Marten's native pagination for optimal performance
                var pagedList = await query
                    .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancel);

                // Map to DTOs using LINQ's lazy Select (no intermediate collection)
                var mappedList = pagedList.Select(p => new PublisherDto(p.Id, p.Name)).ToList();

                // Wrap in adapter for zero-allocation serialization
                return new PagedListAdapter<PublisherDto>(
                    new PagedListWrapper<PublisherDto>(
                        mappedList,
                        pagedList.PageNumber,
                        pagedList.PageSize,
                        pagedList.TotalItemCount));
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheTags.PublisherList],
            cancellationToken: cancellationToken);

        return TypedResults.Ok(response);
    }

    static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Ok<PublisherDto>, NotFound>> GetPublisher(
        Guid id,
        [FromServices] IDocumentStore store,
        [FromServices] ITenantContext tenantContext,
        [FromServices] HybridCache cache,
        CancellationToken cancellationToken)
    {
        var response = await cache.GetOrCreateAsync(
            $"publisher:{id}:tenant={tenantContext.TenantId}",
            async cancel =>
            {
                await using var session = store.QuerySession(tenantContext.TenantId);
                var publisher = await session.LoadAsync<PublisherProjection>(id, cancel);
                if (publisher == null || publisher.Deleted)
                {
                    return (PublisherDto?)null;
                }

                return new PublisherDto(publisher.Id, publisher.Name);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheTags.ForItem(CacheTags.PublisherItemPrefix, id)],
            cancellationToken: cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }
}

