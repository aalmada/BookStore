using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Models.Ucp;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class UcpCatalogEndpoints
{
    const string UcpAgentHeader = "UCP-Agent";

    public static void MapUcpCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ucp/catalog")
            .WithTags("UCP - Catalog")
            // safe: UCP catalog endpoints are intentionally anonymous for platform agents; the required UCP-Agent header identifies caller intent.
            .AllowAnonymous()
            .ExcludeFromDescription();

        _ = group.MapGet("/items", SearchItems)
            .WithName("SearchUcpCatalogItems");

        _ = group.MapGet("/items/{id:guid}", GetItemById)
            .WithName("GetUcpCatalogItemById");
    }

    static async Task<IResult> SearchItems(
        [FromServices] IQuerySession session,
        [FromQuery] string? q,
        [FromQuery] string? currency,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateAgentHeader(context);
        if (validationError is not null)
        {
            return validationError;
        }

        var normalizedLimit = int.Clamp(limit ?? 20, 1, 100);
        var normalizedOffset = int.Max(offset ?? 0, 0);
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

        var query = session.Query<BookSearchProjection>().Where(b => !b.Deleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(b =>
                b.Title.Contains(search) ||
                (b.Isbn != null && b.Isbn.Contains(search)) ||
                b.AuthorNames.Contains(search) ||
                (b.PublisherName != null && b.PublisherName.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);

        var books = await query
            .OrderBy(b => b.Title)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        var items = books
            .Select(book => MapCatalogItem(book, normalizedCurrency))
            .ToList();

        return TypedResults.Ok(new UcpCatalogSearchResponse(
            items,
            total,
            normalizedOffset + items.Count < total));
    }

    static async Task<IResult> GetItemById(
        Guid id,
        [FromServices] IQuerySession session,
        [FromQuery] string? currency,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateAgentHeader(context);
        if (validationError is not null)
        {
            return validationError;
        }

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

        var book = await session.LoadAsync<BookSearchProjection>(id, cancellationToken);
        if (book is null || book.Deleted)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.BookNotFound,
                $"Book '{id}' not found")).ToProblemDetails();
        }

        return TypedResults.Ok(MapCatalogItem(book, normalizedCurrency));
    }

    static UcpCatalogItem MapCatalogItem(BookSearchProjection projection, string? preferredCurrency)
    {
        var description = projection.Descriptions.Count > 0
            ? projection.Descriptions.Values.FirstOrDefault()
            : null;

        var priceEntry = !string.IsNullOrWhiteSpace(preferredCurrency)
            ? projection.CurrentPrices.FirstOrDefault(p => string.Equals(p.Currency, preferredCurrency, StringComparison.OrdinalIgnoreCase))
            : null;

        priceEntry ??= projection.CurrentPrices.FirstOrDefault();

        var price = new UcpCatalogPrice(
            priceEntry?.Currency ?? preferredCurrency ?? "GBP",
            priceEntry is not null
                ? (long)decimal.Round(priceEntry.Value * 100m, MidpointRounding.AwayFromZero)
                : 0L);

        return new UcpCatalogItem(
            projection.Id.ToString(),
            projection.Title,
            description,
            projection.Isbn,
            projection.AuthorNames,
            projection.PublisherName,
            price,
            "in_stock",
            CoverUrl: null);
    }

    static IResult? ValidateAgentHeader(HttpContext context)
    {
        var agentHeader = context.Request.Headers[UcpAgentHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.MissingAgentHeader,
                $"Required header '{UcpAgentHeader}' is missing")).ToProblemDetails();
        }

        return null;
    }
}
