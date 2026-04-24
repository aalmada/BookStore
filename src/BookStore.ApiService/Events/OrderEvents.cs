namespace BookStore.ApiService.Events;

public record DeliveryAddressData(
    string FullName,
    string Street,
    string City,
    string PostalCode,
    string Country);

public record PaymentInfoData(
    string CardHolderName,
    string CardNumberLast4,
    int ExpiryMonth,
    int ExpiryYear);

public record OrderItemData(
    Guid BookId,
    string Title,
    int Quantity,
    decimal UnitPrice);

public record OrderPlaced(
    Guid OrderId,
    string TenantId,
    Guid? UserId,
    string CustomerEmail,
    List<OrderItemData> Items,
    DeliveryAddressData DeliveryAddress,
    PaymentInfoData PaymentInfo,
    decimal TotalAmount,
    DateTimeOffset PlacedAt);

public record PaymentSimulated(
    Guid OrderId,
    DateTimeOffset SimulatedAt);
