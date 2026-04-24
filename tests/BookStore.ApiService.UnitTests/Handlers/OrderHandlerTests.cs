using System.Text.Json;
using Bogus;
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Handlers.Orders;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Projections;
using BookStore.Shared.Messages.Events;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.ApiService.UnitTests.Handlers;

public class OrderHandlerTests : HandlerTestBase
{
    readonly Faker _faker = new();

    [Test]
    [Category("Unit")]
    public async Task Handle_WithValidAuthenticatedCommand_ShouldStartOrderStreamClearCartAndInvalidateCache()
    {
        // Arrange
        _ = Session.TenantId.Returns(_faker.Internet.DomainWord());

        var userId = Guid.CreateVersion7();
        var command = CreateValidCommand(userId: userId);
        var userProfile = new UserProfile
        {
            Id = userId,
            ShoppingCartItems = new Dictionary<Guid, int>
            {
                [Guid.CreateVersion7()] = _faker.Random.Int(1, 4)
            }
        };
        _ = Session.Events.AggregateStreamAsync<UserProfile>(userId).Returns(userProfile);

        // Act
        var result = await OrderHandlers.Handle(command, Session, Cache, Logger, CancellationToken.None);

        // Assert
        _ = await Assert.That(result).IsTypeOf<Created<OrderSummaryDto>>();
        var created = (Created<OrderSummaryDto>)result;
        _ = await Assert.That(created.Value).IsNotNull();
        _ = await Assert.That(created.Value!.OrderId).IsEqualTo(command.OrderId);
        _ = await Assert.That(created.Value.Status).IsEqualTo("PaymentSimulated");

        _ = Session.Events.Received(1).StartStream<OrderAggregate>(
            command.OrderId,
            Arg.Is<OrderPlaced>(e =>
                e.OrderId == command.OrderId &&
                e.UserId == userId &&
                e.CustomerEmail == command.CustomerEmail),
            Arg.Is<PaymentSimulated>(e => e.OrderId == command.OrderId));

        _ = Session.Events.Received(1).Append(
            userId,
            Arg.Is<ShoppingCartCleared>(_ => true));

        await Cache.Received(1).RemoveByTagAsync(
            Arg.Is<IEnumerable<string>>(tags => tags.Contains(CacheTags.OrderList)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Category("Unit")]
    public async Task Handle_WithValidAnonymousCommand_ShouldStartOrderStreamWithoutClearingCartAndInvalidateCache()
    {
        // Arrange
        _ = Session.TenantId.Returns(_faker.Internet.DomainWord());

        var command = CreateValidCommand(userId: null);

        // Act
        var result = await OrderHandlers.Handle(command, Session, Cache, Logger, CancellationToken.None);

        // Assert
        _ = await Assert.That(result).IsTypeOf<Created<OrderSummaryDto>>();

        _ = Session.Events.Received(1).StartStream<OrderAggregate>(
            command.OrderId,
            Arg.Is<OrderPlaced>(e =>
                e.OrderId == command.OrderId &&
                e.UserId == null &&
                e.CustomerEmail == command.CustomerEmail),
            Arg.Is<PaymentSimulated>(e => e.OrderId == command.OrderId));

        _ = Session.Events.DidNotReceive().Append(
            Arg.Any<Guid>(),
            Arg.Any<ShoppingCartCleared>());

        await Cache.Received(1).RemoveByTagAsync(
            Arg.Is<IEnumerable<string>>(tags => tags.Contains(CacheTags.OrderList)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Category("Unit")]
    public async Task Handle_WithMissingEmailForAnonymousUser_ShouldReturnValidationError()
    {
        // Arrange
        var command = CreateValidCommand(userId: null, customerEmail: string.Empty);

        // Act
        var result = await OrderHandlers.Handle(command, Session, Cache, Logger, CancellationToken.None);
        var response = await ExecuteResultAsync(result);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(response.Document.RootElement.GetProperty("error").GetString())
            .IsEqualTo(ErrorCodes.Orders.EmailRequired);
    }

    [Test]
    [Category("Unit")]
    public async Task Handle_WithMissingDeliveryAddressFields_ShouldReturnValidationError()
    {
        // Arrange
        var invalidAddress = new DeliveryAddressData(
            _faker.Name.FullName(),
            _faker.Address.StreetAddress(),
            string.Empty,
            _faker.Address.ZipCode(),
            _faker.Address.Country());
        var command = CreateValidCommand(deliveryAddress: invalidAddress);

        // Act
        var result = await OrderHandlers.Handle(command, Session, Cache, Logger, CancellationToken.None);
        var response = await ExecuteResultAsync(result);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(response.Document.RootElement.GetProperty("error").GetString())
            .IsEqualTo(ErrorCodes.Orders.InvalidAddress);
    }

    [Test]
    [Category("Unit")]
    public async Task Handle_WithEmptyItems_ShouldReturnValidationError()
    {
        // Arrange
        var command = CreateValidCommand(items: []);

        // Act
        var result = await OrderHandlers.Handle(command, Session, Cache, Logger, CancellationToken.None);
        var response = await ExecuteResultAsync(result);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(response.Document.RootElement.GetProperty("error").GetString())
            .IsEqualTo(ErrorCodes.Orders.EmptyItems);
    }

    PlaceOrder CreateValidCommand(
        Guid? userId = null,
        string? customerEmail = null,
        List<OrderItemData>? items = null,
        DeliveryAddressData? deliveryAddress = null,
        PaymentInfoData? paymentInfo = null)
    {
        items ??=
        [
            new OrderItemData(
                Guid.CreateVersion7(),
                _faker.Commerce.ProductName(),
                _faker.Random.Int(1, 4),
                _faker.Random.Decimal(5, 100))
        ];

        deliveryAddress ??= new DeliveryAddressData(
            _faker.Name.FullName(),
            _faker.Address.StreetAddress(),
            _faker.Address.City(),
            _faker.Address.ZipCode(),
            _faker.Address.Country());

        paymentInfo ??= new PaymentInfoData(
            _faker.Name.FullName(),
            _faker.Random.Replace("####"),
            _faker.Random.Int(1, 12),
            _faker.Date.Future().Year);

        return new PlaceOrder(
            Guid.CreateVersion7(),
            userId,
            customerEmail ?? _faker.Internet.Email(),
            items,
            deliveryAddress,
            paymentInfo);
    }

    static async Task<(int StatusCode, JsonDocument Document)> ExecuteResultAsync(IResult result)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddProblemDetails();

        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext();
        context.RequestServices = serviceProvider;
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        body.Position = 0;
        var document = await JsonDocument.ParseAsync(body);
        return (context.Response.StatusCode, document);
    }
}
