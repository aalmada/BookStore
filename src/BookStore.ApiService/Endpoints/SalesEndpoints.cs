using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetSales)
            .WithName("GetSales")
            .WithSummary("Get all scheduled sales across all books (Admin only)")
            .RequireAuthorization("Admin");

        return group;
    }

    static async Task<Ok<PagedListDto<SaleDto>>> GetSales(
        [FromServices] IDocumentStore store,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] HybridCache cache,
        [AsParameters] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var paging = request.Normalize(paginationOptions.Value);
        var cacheKey = $"sales:page={paging.Page}:size={paging.PageSize}:tenant={tenantContext.TenantId}";

        var response = await cache.GetOrCreateLocalizedAsync(
            cacheKey,
            async cancel =>
            {
                await using var session = store.QuerySession(tenantContext.TenantId);

                var books = await session.Query<BookSearchProjection>()
                    .Where(b => !b.Deleted)
                    .ToListAsync(cancel);

                var now = DateTimeOffset.UtcNow;

                var allSales = books
                    .Where(b => b.Sales.Count > 0)
                    .SelectMany(b => b.Sales.Select(s => new SaleDto
                    {
                        BookId = b.Id,
                        BookTitle = b.Title,
                        Percentage = s.Percentage,
                        Start = s.Start,
                        End = s.End,
                        Status = ComputeStatus(s.Start, s.End, now),
                        BookETag = ETagHelper.GenerateETag(b.Version)
                    }))
                    .OrderByDescending(s => s.Start)
                    .ToList();

                var totalItems = allSales.Count;
                var skip = (paging.Page!.Value - 1) * paging.PageSize!.Value;
                var items = allSales.Skip(skip).Take(paging.PageSize.Value).ToList();

                return new PagedListDto<SaleDto>(items, paging.Page.Value, paging.PageSize.Value, totalItems);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(2),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            },
            tags: [CacheTags.BookList],
            token: cancellationToken);

        return TypedResults.Ok(response);
    }

    static string ComputeStatus(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now) => end < now ? "Expired" : start <= now ? "Active" : "Scheduled";
}
