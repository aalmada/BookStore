namespace BookStore.Shared.Models;

public record DeliveryAddressDto(
    string FullName,
    string Street,
    string City,
    string PostalCode,
    string Country);

public record PaymentInfoDto(
    string CardHolderName,
    string CardNumberLast4,
    int ExpiryMonth,
    int ExpiryYear);

public record OrderItemDto(
    Guid BookId,
    string Title,
    int Quantity,
    decimal UnitPrice);

public record PlaceOrderRequest(
    string? CustomerEmail,
    List<OrderItemDto> Items,
    DeliveryAddressDto DeliveryAddress,
    PaymentInfoDto PaymentInfo);

public record OrderSummaryDto(
    Guid OrderId,
    string CustomerEmail,
    List<OrderItemDto> Items,
    DeliveryAddressDto DeliveryAddress,
    PaymentInfoDto PaymentInfo,
    decimal TotalAmount,
    string Status,
    DateTimeOffset PlacedAt);
