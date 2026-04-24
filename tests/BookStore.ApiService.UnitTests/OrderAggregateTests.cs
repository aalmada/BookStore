using Bogus;
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;

namespace BookStore.ApiService.UnitTests;

public class OrderAggregateTests
{
    readonly Faker _faker = new();

    [Test]
    [Category("Unit")]
    public async Task ApplyOrderPlaced_ShouldSetPlacedState()
    {
        // Arrange
        var orderId = Guid.CreateVersion7();
        var tenantId = _faker.Internet.DomainWord();
        var userId = Guid.CreateVersion7();
        var customerEmail = _faker.Internet.Email();
        var address = new DeliveryAddressData(
            _faker.Name.FullName(),
            _faker.Address.StreetAddress(),
            _faker.Address.City(),
            _faker.Address.ZipCode(),
            _faker.Address.Country());
        var payment = new PaymentInfoData(_faker.Name.FullName(), "4242", _faker.Random.Int(1, 12), _faker.Date.Future().Year);
        var items = new List<OrderItemData>
        {
            new(Guid.CreateVersion7(), _faker.Commerce.ProductName(), _faker.Random.Int(1, 3), _faker.Random.Decimal(5, 40)),
            new(Guid.CreateVersion7(), _faker.Commerce.ProductName(), _faker.Random.Int(1, 3), _faker.Random.Decimal(5, 40))
        };
        var totalAmount = items.Sum(item => item.Quantity * item.UnitPrice);
        var placedAt = DateTimeOffset.UtcNow;
        var orderPlaced = new OrderPlaced(orderId, tenantId, userId, customerEmail, items, address, payment, totalAmount, placedAt);

        // Act
        var aggregate = AggregateFactory.Hydrate<OrderAggregate>(orderPlaced);

        // Assert
        _ = await Assert.That(aggregate.Id).IsEqualTo(orderId);
        _ = await Assert.That(aggregate.TenantId).IsEqualTo(tenantId);
        _ = await Assert.That(aggregate.UserId).IsEqualTo(userId);
        _ = await Assert.That(aggregate.CustomerEmail).IsEqualTo(customerEmail);
        _ = await Assert.That(aggregate.Items.Count).IsEqualTo(items.Count);
        _ = await Assert.That(aggregate.Items[0].BookId).IsEqualTo(items[0].BookId);
        _ = await Assert.That(aggregate.DeliveryAddress.City).IsEqualTo(address.City);
        _ = await Assert.That(aggregate.PaymentInfo.CardNumberLast4).IsEqualTo(payment.CardNumberLast4);
        _ = await Assert.That(aggregate.TotalAmount).IsEqualTo(totalAmount);
        _ = await Assert.That(aggregate.PlacedAt).IsEqualTo(placedAt);
        _ = await Assert.That(aggregate.Status).IsEqualTo("Placed");
        _ = await Assert.That(aggregate.Version).IsEqualTo(1);
    }

    [Test]
    [Category("Unit")]
    public async Task ApplyPaymentSimulated_ShouldTransitionStatus()
    {
        // Arrange
        var orderId = Guid.CreateVersion7();
        var orderPlaced = new OrderPlaced(
            orderId,
            _faker.Internet.DomainWord(),
            Guid.CreateVersion7(),
            _faker.Internet.Email(),
            [new OrderItemData(Guid.CreateVersion7(), _faker.Commerce.ProductName(), 1, 10m)],
            new DeliveryAddressData(
                _faker.Name.FullName(),
                _faker.Address.StreetAddress(),
                _faker.Address.City(),
                _faker.Address.ZipCode(),
                _faker.Address.Country()),
            new PaymentInfoData(_faker.Name.FullName(), "1234", 12, _faker.Date.Future().Year),
            10m,
            DateTimeOffset.UtcNow);
        var paymentSimulated = new PaymentSimulated(orderId, DateTimeOffset.UtcNow);

        // Act
        var aggregate = AggregateFactory.Hydrate<OrderAggregate>(orderPlaced, paymentSimulated);

        // Assert
        _ = await Assert.That(aggregate.Status).IsEqualTo("PaymentSimulated");
        _ = await Assert.That(aggregate.Version).IsEqualTo(2);
    }
}
