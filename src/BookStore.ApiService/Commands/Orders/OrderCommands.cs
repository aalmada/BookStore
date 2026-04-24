using BookStore.ApiService.Events;

namespace BookStore.ApiService.Commands;

public record PlaceOrder(
    Guid OrderId,
    Guid? UserId,
    string CustomerEmail,
    List<OrderItemData> Items,
    DeliveryAddressData DeliveryAddress,
    PaymentInfoData PaymentInfo);
