using BookStore.ApiService.Events;
using BookStore.ApiService.Models.Ucp;

namespace BookStore.ApiService.Aggregates;

public class CheckoutSessionAggregate
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Status { get; private set; } = CheckoutSessionStatus.Incomplete;
    public string Currency { get; private set; } = "GBP";
    public List<CheckoutLineItemData> LineItems { get; private set; } = [];
    public UcpBuyer? Buyer { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? OrderId { get; private set; }
    public bool IsCompleted => Status == CheckoutSessionStatus.Completed;
    public bool IsCancelled => Status == CheckoutSessionStatus.Cancelled;
    public bool IsTerminal => IsCompleted || IsCancelled;
    public long Version { get; private set; }

    void Apply(CheckoutSessionCreated @event)
    {
        Id = @event.SessionId;
        TenantId = @event.TenantId;
        Status = CheckoutSessionStatus.Incomplete;
        Currency = @event.Currency;
        LineItems = @event.LineItems;
        ExpiresAt = @event.ExpiresAt;
        CreatedAt = @event.CreatedAt;
    }

    void Apply(CheckoutSessionUpdated @event)
    {
        LineItems = @event.LineItems;
        Buyer = @event.Buyer;
        Status = @event.Buyer?.Email is not null
            ? CheckoutSessionStatus.ReadyForComplete
            : CheckoutSessionStatus.Incomplete;
    }

    void Apply(CheckoutSessionCompleted @event)
    {
        Status = CheckoutSessionStatus.Completed;
        OrderId = @event.OrderId;
    }

    void Apply(CheckoutSessionCancelled _) => Status = CheckoutSessionStatus.Cancelled;
}

public static class CheckoutSessionStatus
{
    public const string Incomplete = "incomplete";
    public const string RequiresEscalation = "requires_escalation";
    public const string ReadyForComplete = "ready_for_complete";
    public const string CompleteInProgress = "complete_in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}
