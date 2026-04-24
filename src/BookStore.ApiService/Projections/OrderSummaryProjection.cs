using BookStore.ApiService.Events;
using JasperFx.Events;
using Marten.Events;

namespace BookStore.ApiService.Projections;

public class OrderSummaryProjection
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItemData> Items { get; set; } = [];
    public DeliveryAddressData DeliveryAddress { get; set; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    public PaymentInfoData PaymentInfo { get; set; } = new(string.Empty, string.Empty, 0, 0);
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset PlacedAt { get; set; }
    public long Version { get; set; }

    public static OrderSummaryProjection Create(IEvent<OrderPlaced> @event) => new()
    {
        Id = @event.Data.OrderId,
        TenantId = @event.Data.TenantId,
        UserId = @event.Data.UserId,
        CustomerEmail = @event.Data.CustomerEmail,
        Items = @event.Data.Items,
        DeliveryAddress = @event.Data.DeliveryAddress,
        PaymentInfo = @event.Data.PaymentInfo,
        TotalAmount = @event.Data.TotalAmount,
        Status = "Placed",
        PlacedAt = @event.Data.PlacedAt,
        Version = @event.Version
    };

    public void Apply(IEvent<PaymentSimulated> @event)
    {
        Status = "PaymentSimulated";
        Version = @event.Version;
    }
}
