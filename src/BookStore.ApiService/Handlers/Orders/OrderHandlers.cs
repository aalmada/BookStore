using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Projections;
using BookStore.Shared.Messages.Events;
using BookStore.Shared.Models;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.Orders;

public static partial class OrderHandlers
{
    public static async Task<IResult> Handle(
        PlaceOrder command,
        IDocumentSession session,
        HybridCache cache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var validationResult = Validate(command);
        if (validationResult.IsFailure)
        {
            Log.Orders.OrderValidationFailed(logger, command.OrderId, validationResult.Error.Code);
            return validationResult.ToProblemDetails();
        }

        var placedAt = DateTimeOffset.UtcNow;
        var simulatedAt = DateTimeOffset.UtcNow;
        var totalAmount = command.Items.Sum(item => item.Quantity * item.UnitPrice);

        var orderPlaced = new OrderPlaced(
            command.OrderId,
            session.TenantId,
            command.UserId,
            command.CustomerEmail,
            command.Items,
            command.DeliveryAddress,
            command.PaymentInfo,
            totalAmount,
            placedAt);

        var paymentSimulated = new PaymentSimulated(command.OrderId, simulatedAt);

        _ = session.Events.StartStream<OrderAggregate>(command.OrderId, orderPlaced, paymentSimulated);

        if (command.UserId is Guid userId)
        {
            var userProfile = await session.Events.AggregateStreamAsync<UserProfile>(userId, token: cancellationToken);
            if (userProfile is not null && userProfile.ShoppingCartItems.Count > 0)
            {
                _ = session.Events.Append(userId, new ShoppingCartCleared());
            }
        }

        await cache.RemoveByTagAsync([CacheTags.OrderList], cancellationToken);

        Log.Orders.OrderPlaced(logger, command.OrderId, command.CustomerEmail);

        var summary = MapToSummaryDto(command, totalAmount, placedAt, "PaymentSimulated");
        return TypedResults.Created($"/api/orders/{command.OrderId}", summary);
    }

    static Result Validate(PlaceOrder command)
    {
        if (command.Items.Count == 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.EmptyItems, "Order must contain at least one item"));
        }

        if (string.IsNullOrWhiteSpace(command.CustomerEmail))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.EmailRequired, "Customer email is required"));
        }

        if (!command.CustomerEmail.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidEmail, "Customer email is invalid"));
        }

        if (command.Items.Any(item => item.Quantity <= 0))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidQuantity, "Order item quantity must be greater than zero"));
        }

        if (command.Items.Any(item => item.UnitPrice <= 0))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidUnitPrice, "Order item unit price must be greater than zero"));
        }

        if (!IsValidAddress(command.DeliveryAddress))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidAddress, "Delivery address is invalid"));
        }

        if (!IsValidPayment(command.PaymentInfo))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Orders.InvalidPayment, "Payment information is invalid"));
        }

        return Result.Success();
    }

    static bool IsValidAddress(DeliveryAddressData address)
        => !string.IsNullOrWhiteSpace(address.FullName)
           && !string.IsNullOrWhiteSpace(address.Street)
           && !string.IsNullOrWhiteSpace(address.City)
           && !string.IsNullOrWhiteSpace(address.PostalCode)
           && !string.IsNullOrWhiteSpace(address.Country);

    static bool IsValidPayment(PaymentInfoData payment)
    {
        if (string.IsNullOrWhiteSpace(payment.CardHolderName))
        {
            return false;
        }

        if (payment.CardNumberLast4.Length != 4)
        {
            return false;
        }

        return payment.CardNumberLast4.All(char.IsDigit);
    }

    static OrderSummaryDto MapToSummaryDto(
        PlaceOrder command,
        decimal totalAmount,
        DateTimeOffset placedAt,
        string status)
        => new(
            command.OrderId,
            command.CustomerEmail,
            [.. command.Items.Select(item => new OrderItemDto(item.BookId, item.Title, item.Quantity, item.UnitPrice))],
            new DeliveryAddressDto(
                command.DeliveryAddress.FullName,
                command.DeliveryAddress.Street,
                command.DeliveryAddress.City,
                command.DeliveryAddress.PostalCode,
                command.DeliveryAddress.Country),
            new PaymentInfoDto(
                command.PaymentInfo.CardHolderName,
                command.PaymentInfo.CardNumberLast4,
                command.PaymentInfo.ExpiryMonth,
                command.PaymentInfo.ExpiryYear),
            totalAmount,
            status,
            placedAt);
}
