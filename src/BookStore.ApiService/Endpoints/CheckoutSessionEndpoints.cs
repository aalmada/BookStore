using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.CheckoutSession;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.UCP;
using BookStore.ApiService.Models.Ucp;
using BookStore.ApiService.Projections;
using BookStore.Shared;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class CheckoutSessionEndpoints
{
    const string UcpAgentHeader = "UCP-Agent";

    public static void MapCheckoutSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ucp/checkout-sessions")
            .WithTags("UCP - Checkout")
            // safe: UCP checkout endpoints are designed for anonymous shoppers; caller intent is validated via the required UCP-Agent header on every request.
            .AllowAnonymous()
            .ExcludeFromDescription();

        _ = group.MapPost("/", CreateCheckout)
            .WithName("CreateCheckoutSession");

        _ = group.MapGet("/{id:guid}", GetCheckout)
            .WithName("GetCheckoutSession");

        _ = group.MapPut("/{id:guid}", UpdateCheckout)
            .WithName("UpdateCheckoutSession");

        _ = group.MapPost("/{id:guid}/complete", CompleteCheckout)
            .WithName("CompleteCheckoutSession");

        _ = group.MapPost("/{id:guid}/cancel", CancelCheckout)
            .WithName("CancelCheckoutSession");
    }

    static async Task<IResult> CreateCheckout(
        [FromBody] UcpCreateCheckoutRequest request,
        [FromServices] IDocumentSession session,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var agentHeader = context.Request.Headers[UcpAgentHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.MissingAgentHeader,
                $"Required header '{UcpAgentHeader}' is missing")).ToProblemDetails();
        }

        if (request.LineItems.Count == 0)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.EmptyLineItems,
                "Checkout session must contain at least one line item")).ToProblemDetails();
        }

        if (request.LineItems.Any(li => li.Quantity <= 0))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.InvalidQuantity,
                "Line item quantity must be greater than zero")).ToProblemDetails();
        }

        var tenantId = GetTenantId(context);
        var currency = request.Context?.Currency ?? "GBP";
        var lineItems = new List<CheckoutLineItemData>();

        foreach (var li in request.LineItems)
        {
            var bookId = Guid.TryParse(li.Item.Id, out var bid) ? bid : Guid.Empty;
            var book = bookId != Guid.Empty
                ? await session.LoadAsync<BookSearchProjection>(bookId, cancellationToken)
                : null;

            if (book is null)
            {
                return Result.Failure(Error.Validation(
                    ErrorCodes.Checkout.BookNotFound,
                    $"Book '{li.Item.Id}' not found")).ToProblemDetails();
            }

            var priceEntry = book.CurrentPrices.Find(p =>
                string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase))
                ?? book.CurrentPrices.FirstOrDefault();

            var unitPriceCents = priceEntry is not null
                ? (long)decimal.Round(priceEntry.Value * 100, MidpointRounding.AwayFromZero)
                : 0L;

            var lineItemId = li.Id ?? $"li_{lineItems.Count + 1}";
            lineItems.Add(new CheckoutLineItemData(lineItemId, li.Item.Id, book.Title, li.Quantity, unitPriceCents));
        }

        var sessionId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var created = new CheckoutSessionCreated(sessionId, tenantId, currency, lineItems, now.AddHours(24), now);
        _ = session.Events.StartStream<CheckoutSessionAggregate>(sessionId, created);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/ucp/checkout-sessions/{sessionId}",
            CheckoutSessionHandlers.BuildResponse(
                sessionId,
                CheckoutSessionStatus.Incomplete,
                currency,
                lineItems,
                buyer: null,
                orderId: null,
                created.ExpiresAt));
    }

    static async Task<IResult> GetCheckout(
        Guid id,
        [FromServices] IDocumentSession session,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var agentHeader = context.Request.Headers[UcpAgentHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.MissingAgentHeader,
                $"Required header '{UcpAgentHeader}' is missing")).ToProblemDetails();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(
            id, token: cancellationToken);

        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{id}' not found")).ToProblemDetails();
        }

        return TypedResults.Ok(CheckoutSessionHandlers.MapToResponse(aggregate));
    }

    static async Task<IResult> UpdateCheckout(
        Guid id,
        [FromBody] UcpUpdateCheckoutRequest request,
        [FromServices] IDocumentSession session,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var agentHeader = context.Request.Headers[UcpAgentHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.MissingAgentHeader,
                $"Required header '{UcpAgentHeader}' is missing")).ToProblemDetails();
        }

        if (request.LineItems.Count == 0)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.EmptyLineItems,
                "Checkout session must contain at least one line item")).ToProblemDetails();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(id, token: cancellationToken);
        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{id}' not found")).ToProblemDetails();
        }

        if (aggregate.IsTerminal)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionTerminal,
                "Cannot update a completed or cancelled checkout session")).ToProblemDetails();
        }

        var lineItems = new List<CheckoutLineItemData>();
        foreach (var li in request.LineItems)
        {
            var bookId = Guid.TryParse(li.Item.Id, out var bid) ? bid : Guid.Empty;
            var book = bookId != Guid.Empty
                ? await session.LoadAsync<BookSearchProjection>(bookId, cancellationToken)
                : null;

            if (book is null)
            {
                return Result.Failure(Error.Validation(
                    ErrorCodes.Checkout.BookNotFound,
                    $"Book '{li.Item.Id}' not found")).ToProblemDetails();
            }

            var priceEntry = book.CurrentPrices.Find(p =>
                string.Equals(p.Currency, aggregate.Currency, StringComparison.OrdinalIgnoreCase))
                ?? book.CurrentPrices.FirstOrDefault();

            var unitPriceCents = priceEntry is not null
                ? (long)decimal.Round(priceEntry.Value * 100, MidpointRounding.AwayFromZero)
                : 0L;

            var lineItemId = li.Id ?? $"li_{lineItems.Count + 1}";
            lineItems.Add(new CheckoutLineItemData(lineItemId, li.Item.Id, book.Title, li.Quantity, unitPriceCents));
        }

        var buyer = request.Buyer;

        // ------------------------------------------------------------------
        // Fulfillment extension processing
        // ------------------------------------------------------------------
        FulfillmentData? fulfillmentData = null;
        UcpFulfillmentResponse? fulfillmentResponse = null;

        if (request.Fulfillment?.Methods is { Count: > 0 } methods)
        {
            var method = methods[0];
            var destination = method.Destinations?.FirstOrDefault();
            var group = method.Groups?.FirstOrDefault();
            var selectedOptionId = group?.SelectedOptionId;

            var methodId = method.Id ?? "ship_1";
            var destId = destination?.Id ?? "dest_1";

            long shippingCostCents = 0;
            List<UcpFulfillmentGroupResponse> groups;

            if (selectedOptionId is not null)
            {
                var option = UcpFulfillmentService.FindOption(selectedOptionId);
                shippingCostCents = option?.PriceCents ?? 0;
                groups = [new("pkg_1", Options: null, SelectedOptionId: selectedOptionId)];
            }
            else
            {
                var options = UcpFulfillmentService.ShippingOptions
                    .Select(o => new UcpFulfillmentOptionResponse(o.Id, o.Title, o.PriceCents, aggregate.Currency))
                    .ToList();
                groups = [new("pkg_1", options, SelectedOptionId: null)];
            }

            var destResponse = new UcpFulfillmentDestinationResponse(
                destId,
                destination?.StreetAddress,
                destination?.AddressLocality,
                destination?.AddressRegion,
                destination?.PostalCode,
                destination?.AddressCountry);

            fulfillmentResponse = new UcpFulfillmentResponse(
                [new(methodId, "shipping", [destResponse], destId, groups)]);

            if (destination is not null)
            {
                var shippingAddress = new UcpAddress(
                    destination.StreetAddress ?? string.Empty,
                    destination.AddressLocality ?? string.Empty,
                    destination.AddressRegion ?? string.Empty,
                    destination.PostalCode ?? string.Empty,
                    destination.AddressCountry ?? string.Empty);
                fulfillmentData = new FulfillmentData(methodId, destId, shippingAddress, selectedOptionId, shippingCostCents);
            }
        }

        var hasFulfillmentOption = fulfillmentData is null || fulfillmentData.SelectedOptionId is not null;
        var newStatus = buyer?.Email is not null && hasFulfillmentOption
            ? CheckoutSessionStatus.ReadyForComplete
            : CheckoutSessionStatus.Incomplete;

        var updated = new CheckoutSessionUpdated(id, lineItems, buyer, fulfillmentData);
        _ = session.Events.Append(id, updated);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(CheckoutSessionHandlers.BuildResponse(
            aggregate.Id,
            newStatus,
            aggregate.Currency,
            lineItems,
            buyer,
            orderId: null,
            aggregate.ExpiresAt,
            fulfillmentData: fulfillmentData,
            fulfillmentResponse: fulfillmentResponse));
    }

    static async Task<IResult> CompleteCheckout(
        Guid id,
        [FromBody] UcpCompleteCheckoutRequest? request,
        [FromServices] IDocumentSession session,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var agentHeader = context.Request.Headers[UcpAgentHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.MissingAgentHeader,
                $"Required header '{UcpAgentHeader}' is missing")).ToProblemDetails();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(id, token: cancellationToken);
        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{id}' not found")).ToProblemDetails();
        }

        if (aggregate.IsTerminal)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionTerminal,
                "Cannot complete a completed or cancelled checkout session")).ToProblemDetails();
        }

        if (aggregate.Status != CheckoutSessionStatus.ReadyForComplete)
        {
            return TypedResults.Ok(CheckoutSessionHandlers.BuildResponse(
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
        var itemsCents = aggregate.LineItems.Sum(li => li.UnitPriceCents * li.Quantity);
        var shippingCents = aggregate.Fulfillment?.ShippingCostCents ?? 0;
        var totalCents = itemsCents + shippingCents;

        var deliveryAddress = aggregate.Fulfillment is not null
            ? new DeliveryAddressData(
                $"{aggregate.Buyer?.FirstName} {aggregate.Buyer?.LastName}".Trim(),
                aggregate.Fulfillment.ShippingAddress.StreetAddress,
                aggregate.Fulfillment.ShippingAddress.AddressLocality,
                aggregate.Fulfillment.ShippingAddress.PostalCode,
                aggregate.Fulfillment.ShippingAddress.AddressCountry)
            : new DeliveryAddressData(
                $"{aggregate.Buyer?.FirstName} {aggregate.Buyer?.LastName}".Trim(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

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
            deliveryAddress,
            new PaymentInfoData("UCP", "0000", 12, 99),
            totalCents / 100m,
            now);

        var paymentSimulated = new PaymentSimulated(orderId, now);
        _ = session.Events.StartStream<OrderAggregate>(orderId, orderPlaced, paymentSimulated);

        var completed = new CheckoutSessionCompleted(id, orderId, CheckoutSessionHandlers.BuildOrderLabel(orderId), now);
        _ = session.Events.Append(id, completed);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(CheckoutSessionHandlers.BuildResponse(
            aggregate.Id,
            CheckoutSessionStatus.Completed,
            aggregate.Currency,
            aggregate.LineItems,
            aggregate.Buyer,
            orderId,
            aggregate.ExpiresAt,
            fulfillmentData: aggregate.Fulfillment));
    }

    static async Task<IResult> CancelCheckout(
        Guid id,
        [FromServices] IDocumentSession session,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var agentHeader = context.Request.Headers[UcpAgentHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.MissingAgentHeader,
                $"Required header '{UcpAgentHeader}' is missing")).ToProblemDetails();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CheckoutSessionAggregate>(id, token: cancellationToken);
        if (aggregate is null)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionNotFound,
                $"Checkout session '{id}' not found")).ToProblemDetails();
        }

        if (aggregate.IsTerminal)
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Checkout.SessionTerminal,
                "Cannot cancel an already completed or cancelled checkout session")).ToProblemDetails();
        }

        var cancelled = new CheckoutSessionCancelled(id, DateTimeOffset.UtcNow);
        _ = session.Events.Append(id, cancelled);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(CheckoutSessionHandlers.BuildResponse(
            aggregate.Id,
            CheckoutSessionStatus.Cancelled,
            aggregate.Currency,
            aggregate.LineItems,
            aggregate.Buyer,
            orderId: null,
            aggregate.ExpiresAt));
    }

    static string GetTenantId(HttpContext context)
        => context.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? MultiTenancyConstants.DefaultTenantId;
}
