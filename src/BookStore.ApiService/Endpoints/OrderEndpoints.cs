using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Wolverine;

namespace BookStore.ApiService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders")
            .WithTags("Orders")
            .WithMetadata(new AllowAnonymousTenantAttribute());

        // safe: checkout must allow anonymous shoppers to place an order with explicit email provided in the request body.
        _ = group.MapPost("/", PlaceOrder)
            .WithName("PlaceOrder")
            .AllowAnonymous();

        _ = group.MapGet("/", GetOrders)
            .WithName("GetOrders")
            .RequireAuthorization();
    }

    static async Task<IResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        var isAuthenticated = userId != Guid.Empty;

        var customerEmail = isAuthenticated
            ? context.User.GetEmail() ?? request.CustomerEmail
            : request.CustomerEmail;

        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.EmailRequired, "Customer email is required")).ToProblemDetails();
        }

        if (!customerEmail.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidEmail, "Customer email is invalid")).ToProblemDetails();
        }

        var cardNumberLast4 = ExtractCardLast4(request.PaymentInfo.CardNumberLast4);
        if (cardNumberLast4 is null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidPayment, "Card number must contain at least four digits")).ToProblemDetails();
        }

        var command = new BookStore.ApiService.Commands.PlaceOrder(
            Guid.CreateVersion7(),
            isAuthenticated ? userId : null,
            customerEmail,
            [.. request.Items.Select(item => new OrderItemData(item.BookId, item.Title, item.Quantity, item.UnitPrice))],
            new DeliveryAddressData(
                request.DeliveryAddress.FullName,
                request.DeliveryAddress.Street,
                request.DeliveryAddress.City,
                request.DeliveryAddress.PostalCode,
                request.DeliveryAddress.Country),
            new PaymentInfoData(
                request.PaymentInfo.CardHolderName,
                cardNumberLast4,
                request.PaymentInfo.ExpiryMonth,
                request.PaymentInfo.ExpiryYear));

        return await bus.InvokeAsync<IResult>(
            command,
            new DeliveryOptions { TenantId = tenantContext.TenantId },
            cancellationToken);
    }

    static async Task<IResult> GetOrders(
        [FromServices] IQuerySession session,
        [FromServices] HybridCache cache,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not authenticated.")).ToProblemDetails();
        }

        var tenantId = session.TenantId;
        var cacheKey = $"orders:{tenantId}:{userId}";

        var response = await cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var projections = await session.Query<OrderSummaryProjection>()
                    .Where(order => order.UserId == userId)
                    .OrderByDescending(order => order.PlacedAt)
                    .ToListAsync(ct);

                return (IReadOnlyList<OrderSummaryDto>)[.. projections.Select(MapToSummaryDto)];
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(2),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            },
            tags: [CacheTags.OrderList],
            cancellationToken: cancellationToken);

        return TypedResults.Ok(response);
    }

    static string? ExtractCardLast4(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return null;
        }

        var digits = new string([.. cardNumber.Where(char.IsDigit)]);
        if (digits.Length < 4)
        {
            return null;
        }

        return digits[^4..];
    }

    static OrderSummaryDto MapToSummaryDto(OrderSummaryProjection projection)
        => new(
            projection.Id,
            projection.CustomerEmail,
            [.. projection.Items.Select(item => new OrderItemDto(item.BookId, item.Title, item.Quantity, item.UnitPrice))],
            new DeliveryAddressDto(
                projection.DeliveryAddress.FullName,
                projection.DeliveryAddress.Street,
                projection.DeliveryAddress.City,
                projection.DeliveryAddress.PostalCode,
                projection.DeliveryAddress.Country),
            new PaymentInfoDto(
                projection.PaymentInfo.CardHolderName,
                projection.PaymentInfo.CardNumberLast4,
                projection.PaymentInfo.ExpiryMonth,
                projection.PaymentInfo.ExpiryYear),
            projection.TotalAmount,
            projection.Status,
            projection.PlacedAt);
}
