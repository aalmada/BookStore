using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetSales)
            .WithName("GetSales")
            .WithSummary("Get all scheduled book sales");

        return group.RequireAuthorization("Admin");
    }

    static async Task<IResult> GetSales(
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [AsParameters] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var paging = request.Normalize(paginationOptions.Value);
        var now = DateTimeOffset.UtcNow;

        var books = await session.Query<BookSearchProjection>()
            .Where(b => !b.Deleted)
            .ToListAsync(cancellationToken);

        var allSales = books
            .Where(b => b.Sales.Count > 0)
            .SelectMany(b => b.Sales.Select(sale => new SaleDto
            {
                Id = b.Id,
                BookTitle = b.Title,
                BuyerName = string.Empty,
                Date = sale.Start,
                EndDate = sale.End,
                Amount = sale.Percentage,
                Status = now >= sale.Start && now < sale.End ? "Active"
                       : now < sale.Start ? "Upcoming"
                       : "Expired",
                ETag = ETagHelper.GenerateETag(b.Version)
            }))
            .OrderByDescending(s => s.Date)
            .ToList();

        var totalCount = allSales.Count;
        var page = paging.Page!.Value;
        var pageSize = paging.PageSize!.Value;
        var items = allSales.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Results.Ok(new PagedListDto<SaleDto>(items, page, pageSize, totalCount));
    }
}
