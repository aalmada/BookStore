using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.UCP;
using BookStore.ApiService.Models.Ucp;
using BookStore.Shared.Models;
using Marten;

namespace BookStore.ApiService.Handlers.CheckoutSession;

public static class CheckoutSessionHandlers
{
    // ------------------------------------------------------------------
    // Create
    // ------------------------------------------------------------------
    public static async Task<IResult> Handle(
        CreateCheckoutSession command,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var created = new CheckoutSessionCreated(
            command.SessionId,
            command.TenantId,
            command.Currency,
            command.LineItems,
            now.AddHours(24),
            now);

        _ = session.Events.StartStream<CheckoutSessionAggregate>(command.SessionId, created);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/ucp/checkout-sessions/{command.SessionId}",
            BuildResponse(
                command.SessionId,
                CheckoutSessionStatus.Incomplete,
                command.Currency,
                command.LineItems,
                buyer: null,
                orderId: null,
                created.ExpiresAt));
    }

    // ------------------------------------------------------------------
    // Update (full replacement / PUT)
    // ------------------------------------------------------------------
    public static async Task<IResult> Handle(
        UpdateCheckoutSession command,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(
            command.SessionId, token: cancellationToken);

        // Session existence and terminal state are validated in the endpoint before dispatching
        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{command.SessionId}' not found")).ToProblemDetails();
        }

        var buyer = command.Buyer;
        var lineItems = command.LineItems;
        var newStatus = buyer?.Email is not null
            ? CheckoutSessionStatus.ReadyForComplete
            : CheckoutSessionStatus.Incomplete;

        var updated = new CheckoutSessionUpdated(command.SessionId, lineItems, buyer);
        _ = session.Events.Append(command.SessionId, updated);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(BuildResponse(
            aggregate.Id,
            newStatus,
            aggregate.Currency,
            lineItems,
            buyer,
            orderId: null,
            aggregate.ExpiresAt));
    }

    // ------------------------------------------------------------------
    // Complete
    // ------------------------------------------------------------------
    public static async Task<IResult> Handle(
        CompleteCheckoutSession command,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(
            command.SessionId, token: cancellationToken);

        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{command.SessionId}' not found")).ToProblemDetails();
        }

        if (aggregate.IsTerminal)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionTerminal,
                "Cannot complete a completed or cancelled checkout session")).ToProblemDetails();
        }

        if (aggregate.Status != CheckoutSessionStatus.ReadyForComplete)
        {
            // UCP: business error as HTTP 200 with messages
            return TypedResults.Ok(BuildResponse(
                aggregate.Id,
                aggregate.Status,
                aggregate.Currency,
                aggregate.LineItems,
                aggregate.Buyer,
                orderId: null,
                aggregate.ExpiresAt,
                [new UcpMessage(
                    "error",
                    "session_not_ready",
                    null,
                    "Checkout session is not ready to complete. Please provide buyer information.",
                    "requires_buyer_input")]));
        }

        var orderId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var customerEmail = aggregate.Buyer?.Email ?? string.Empty;
        var totalCents = aggregate.LineItems.Sum(li => li.UnitPriceCents * li.Quantity);

        var orderPlaced = new OrderPlaced(
            orderId,
            aggregate.TenantId,
            null,
            customerEmail,
            [.. aggregate.LineItems.Select(li => new OrderItemData(
                Guid.TryParse(li.BookId, out var bid) ? bid : Guid.Empty,
                li.Title,
                li.Quantity,
                li.UnitPriceCents / 100m))],
            new DeliveryAddressData(
                $"{aggregate.Buyer?.FirstName} {aggregate.Buyer?.LastName}".Trim(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty),
            new PaymentInfoData("UCP", "0000", 12, 99),
            totalCents / 100m,
            now);

        var paymentSimulated = new PaymentSimulated(orderId, now);
        _ = session.Events.StartStream<OrderAggregate>(orderId, orderPlaced, paymentSimulated);

        var completed = new CheckoutSessionCompleted(command.SessionId, orderId, BuildOrderLabel(orderId), now);
        _ = session.Events.Append(command.SessionId, completed);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(BuildResponse(
            aggregate.Id,
            CheckoutSessionStatus.Completed,
            aggregate.Currency,
            aggregate.LineItems,
            aggregate.Buyer,
            orderId,
            aggregate.ExpiresAt));
    }

    // ------------------------------------------------------------------
    // Cancel
    // ------------------------------------------------------------------
    public static async Task<IResult> Handle(
        CancelCheckoutSession command,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(
            command.SessionId, token: cancellationToken);

        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{command.SessionId}' not found")).ToProblemDetails();
        }

        if (aggregate.IsTerminal)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionTerminal,
                "Cannot cancel an already completed or cancelled checkout session")).ToProblemDetails();
        }

        var cancelled = new CheckoutSessionCancelled(command.SessionId, DateTimeOffset.UtcNow);
        _ = session.Events.Append(command.SessionId, cancelled);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(BuildResponse(
            aggregate.Id,
            CheckoutSessionStatus.Cancelled,
            aggregate.Currency,
            aggregate.LineItems,
            aggregate.Buyer,
            orderId: null,
            aggregate.ExpiresAt));
    }

    // ------------------------------------------------------------------
    // Helpers (internal for GET endpoint)
    // ------------------------------------------------------------------

    internal static CheckoutSessionResponse MapToResponse(
        CheckoutSessionAggregate aggregate,
        List<UcpMessage>? extraMessages = null,
        UcpFulfillmentResponse? fulfillmentResponse = null)
    {
        // Build fulfillment response from aggregate state if not explicitly provided
        fulfillmentResponse ??= BuildFulfillmentResponse(aggregate.Fulfillment, aggregate.Currency);

        return BuildResponse(
            aggregate.Id,
            aggregate.Status,
            aggregate.Currency,
            aggregate.LineItems,
            aggregate.Buyer,
            aggregate.OrderId,
            aggregate.ExpiresAt,
            extraMessages,
            aggregate.Fulfillment,
            fulfillmentResponse);
    }

    internal static CheckoutSessionResponse BuildResponse(
        Guid sessionId,
        string status,
        string currency,
        List<CheckoutLineItemData> lineItems,
        UcpBuyer? buyer,
        Guid? orderId,
        DateTimeOffset expiresAt,
        List<UcpMessage>? messages = null,
        FulfillmentData? fulfillmentData = null,
        UcpFulfillmentResponse? fulfillmentResponse = null)
    {
        var responseLineItems = lineItems.Select(li =>
        {
            var lineTotal = li.UnitPriceCents * li.Quantity;
            return new UcpLineItem(
                li.LineItemId,
                new UcpLineItemProduct(li.BookId, li.Title, li.UnitPriceCents),
                li.Quantity,
                [new UcpTotal("subtotal", lineTotal)]);
        }).ToList();

        var subtotal = lineItems.Sum(li => li.UnitPriceCents * li.Quantity);
        var shippingCents = fulfillmentData?.ShippingCostCents ?? 0;
        var total = subtotal + shippingCents;

        List<UcpTotal> totals = [new UcpTotal("subtotal", subtotal)];
        if (shippingCents > 0)
        {
            totals.Add(new UcpTotal("fulfillment", shippingCents, "Shipping"));
        }

        totals.Add(new UcpTotal("total", total));

        UcpOrder? order = null;
        if (orderId.HasValue)
        {
            order = new UcpOrder(
                orderId.Value.ToString(),
                BuildOrderLabel(orderId.Value),
                string.Empty);
        }

        return new CheckoutSessionResponse(
            new UcpMeta("2026-04-08", status),
            sessionId.ToString(),
            status,
            currency,
            responseLineItems,
            totals,
            messages ?? [],
            ContinueUrl: null,
            expiresAt.ToString("O"),
            buyer,
            order,
            fulfillmentResponse);
    }

    internal static UcpFulfillmentResponse? BuildFulfillmentResponse(FulfillmentData? data, string currency)
    {
        if (data is null)
        {
            return null;
        }

        List<UcpFulfillmentGroupResponse> groups;
        if (data.SelectedOptionId is not null)
        {
            groups = [new("pkg_1", Options: null, SelectedOptionId: data.SelectedOptionId)];
        }
        else
        {
            var options = UcpFulfillmentService.ShippingOptions
                .Select(o => new UcpFulfillmentOptionResponse(o.Id, o.Title, o.PriceCents, currency))
                .ToList();
            groups = [new("pkg_1", options, SelectedOptionId: null)];
        }

        var dest = new UcpFulfillmentDestinationResponse(
            data.DestinationId,
            data.ShippingAddress.StreetAddress,
            data.ShippingAddress.AddressLocality,
            data.ShippingAddress.AddressRegion,
            data.ShippingAddress.PostalCode,
            data.ShippingAddress.AddressCountry);

        return new UcpFulfillmentResponse(
            [new(data.MethodId, "shipping", [dest], data.DestinationId, groups)]);
    }

    internal static string BuildOrderLabel(Guid orderId)
        => $"#ORD-{orderId.ToString("N")[..8].ToUpperInvariant()}";
}
