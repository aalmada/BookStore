using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

public class OrderAggregate
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public List<OrderItemData> Items { get; private set; } = [];
    public DeliveryAddressData DeliveryAddress { get; private set; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    public PaymentInfoData PaymentInfo { get; private set; } = new(string.Empty, string.Empty, 0, 0);
    public decimal TotalAmount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset PlacedAt { get; private set; }
    public long Version { get; private set; }

    void Apply(OrderPlaced @event)
    {
        Id = @event.OrderId;
        TenantId = @event.TenantId;
        UserId = @event.UserId;
        CustomerEmail = @event.CustomerEmail;
        Items = @event.Items;
        DeliveryAddress = @event.DeliveryAddress;
        PaymentInfo = @event.PaymentInfo;
        TotalAmount = @event.TotalAmount;
        PlacedAt = @event.PlacedAt;
        Status = "Placed";
    }

    void Apply(PaymentSimulated _) => Status = "PaymentSimulated";
}
