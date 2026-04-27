using BookStore.ApiService.Models.Ucp;

namespace BookStore.ApiService.Events;

// ------------------------------------------------------------------
// Checkout Session Events (past-tense records)
// ------------------------------------------------------------------

public record CheckoutSessionCreated(
    Guid SessionId,
    string TenantId,
    string Currency,
    List<CheckoutLineItemData> LineItems,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public record CheckoutSessionUpdated(
    Guid SessionId,
    List<CheckoutLineItemData> LineItems,
    UcpBuyer? Buyer);

public record CheckoutSessionCompleted(
    Guid SessionId,
    Guid OrderId,
    string OrderLabel,
    DateTimeOffset CompletedAt);

public record CheckoutSessionCancelled(
    Guid SessionId,
    DateTimeOffset CancelledAt);

// ------------------------------------------------------------------
// Value objects used in events
// ------------------------------------------------------------------

public record CheckoutLineItemData(
    string LineItemId,
    string BookId,
    string Title,
    int Quantity,
    long UnitPriceCents);
