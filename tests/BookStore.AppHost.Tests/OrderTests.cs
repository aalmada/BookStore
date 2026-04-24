using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class OrderTests
{
    readonly Faker _faker = new();

    [Test]
    [Category("Integration")]
    [Category("Checkout")]
    public async Task PostOrders_AuthenticatedUser_ShouldCreateOrderReturnHistoryAndClearCart()
    {
        // Arrange
        var adminBooksClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync();
        var shoppingCartClient = RestService.For<IShoppingCartClient>(userClient.Client);
        var createdBook = await BookHelpers.CreateBookAsync(adminBooksClient, FakeDataGenerators.GenerateFakeBookRequest());
        var quantity = _faker.Random.Int(1, 3);

        await ShoppingCartHelpers.AddToCartAsync(shoppingCartClient, createdBook.Id, quantity, userClient.UserId);

        var cartBeforeCheckout = await shoppingCartClient.GetShoppingCartAsync();
        var request = CreatePlaceOrderRequest(cartBeforeCheckout.Items, includeEmail: false);

        HttpResponseMessage? placeOrderResponse = null;
        var minTimestamp = DateTimeOffset.UtcNow;

        // Act
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "OrderPlaced",
            async () => placeOrderResponse = await userClient.Client.PostAsJsonAsync("/api/orders", request),
            TestConstants.DefaultEventTimeout,
            minTimestamp: minTimestamp);

        // Assert
        _ = await Assert.That(received).IsTrue();
        _ = await Assert.That(placeOrderResponse).IsNotNull();

        using var createdResponse = placeOrderResponse!;
        _ = await Assert.That(createdResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var createdOrder = await createdResponse.Content.ReadFromJsonAsync<OrderSummaryDto>();
        _ = await Assert.That(createdOrder).IsNotNull();
        _ = await Assert.That(createdOrder!.OrderId).IsNotEqualTo(Guid.Empty);

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            using var ordersResponse = await userClient.Client.GetAsync("/api/orders");
            if (ordersResponse.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            var orders = await ordersResponse.Content.ReadFromJsonAsync<List<OrderSummaryDto>>();
            return orders is not null && orders.Any(order => order.OrderId == createdOrder.OrderId);
        }, TestConstants.DefaultTimeout, "Placed order did not appear in GET /api/orders.");

        using var ordersGetResponse = await userClient.Client.GetAsync("/api/orders");
        _ = await Assert.That(ordersGetResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var orderHistory = await ordersGetResponse.Content.ReadFromJsonAsync<List<OrderSummaryDto>>();
        _ = await Assert.That(orderHistory).IsNotNull();
        _ = await Assert.That(orderHistory!.Any(order => order.OrderId == createdOrder.OrderId)).IsTrue();

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await shoppingCartClient.GetShoppingCartAsync();
            return cart.TotalItems == 0 && cart.Items.Count == 0;
        }, TestConstants.DefaultTimeout, "Shopping cart was not cleared after checkout.");

        var cartAfterCheckout = await shoppingCartClient.GetShoppingCartAsync();
        _ = await Assert.That(cartAfterCheckout.TotalItems).IsEqualTo(0);
        _ = await Assert.That(cartAfterCheckout.Items.Count).IsEqualTo(0);
    }

    [Test]
    [Category("Integration")]
    [Category("Checkout")]
    public async Task PostOrders_AnonymousUser_ShouldCreateOrder()
    {
        // Arrange
        var adminBooksClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(adminBooksClient, FakeDataGenerators.GenerateFakeBookRequest());
        var prices = createdBook.Prices ?? new Dictionary<string, decimal> { ["GBP"] = _faker.Random.Decimal(5, 100) };

        using var anonymousClient = HttpClientHelpers.GetUnauthenticatedClient();
        var request = CreatePlaceOrderRequest(
            [
                new ShoppingCartItemResponse(
                    createdBook.Id,
                    createdBook.Title,
                    createdBook.Isbn,
                    _faker.Random.Int(1, 2),
                    prices)
            ],
            includeEmail: true);

        HttpResponseMessage? placeOrderResponse = null;

        // Act
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "OrderPlaced",
            async () => placeOrderResponse = await anonymousClient.PostAsJsonAsync("/api/orders", request),
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        // Assert
        _ = await Assert.That(received).IsTrue();
        _ = await Assert.That(placeOrderResponse).IsNotNull();

        using var createdResponse = placeOrderResponse!;
        _ = await Assert.That(createdResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var createdOrder = await createdResponse.Content.ReadFromJsonAsync<OrderSummaryDto>();
        _ = await Assert.That(createdOrder).IsNotNull();
        _ = await Assert.That(createdOrder!.OrderId).IsNotEqualTo(Guid.Empty);
        _ = await Assert.That(createdOrder.CustomerEmail).IsEqualTo(request.CustomerEmail);
    }

    [Test]
    [Category("Integration")]
    [Category("Checkout")]
    public async Task PostOrders_AnonymousUserWithMissingEmail_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        using var anonymousClient = HttpClientHelpers.GetUnauthenticatedClient();
        var request = CreatePlaceOrderRequest(
            [CreateRandomCartItem()],
            includeEmail: false);

        // Act
        using var response = await anonymousClient.PostAsJsonAsync("/api/orders", request);
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationHelpers.ValidationProblemDetails>();

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
        _ = await Assert.That(problem).IsNotNull();
        _ = await Assert.That(problem!.Error).IsEqualTo(ErrorCodes.Orders.EmailRequired);
    }

    [Test]
    [Category("Integration")]
    [Category("Checkout")]
    public async Task PostOrders_WithMissingAddressFields_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        using var anonymousClient = HttpClientHelpers.GetUnauthenticatedClient();
        var request = CreatePlaceOrderRequest(
            [CreateRandomCartItem()],
            includeEmail: true);
        request = request with
        {
            DeliveryAddress = request.DeliveryAddress with
            {
                City = string.Empty
            }
        };

        // Act
        using var response = await anonymousClient.PostAsJsonAsync("/api/orders", request);
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationHelpers.ValidationProblemDetails>();

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
        _ = await Assert.That(problem).IsNotNull();
        _ = await Assert.That(problem!.Error).IsEqualTo(ErrorCodes.Orders.InvalidAddress);
    }

    [Test]
    [Category("Integration")]
    [Category("Checkout")]
    public async Task GetOrders_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using var anonymousClient = HttpClientHelpers.GetUnauthenticatedClient();

        // Act
        using var response = await anonymousClient.GetAsync("/api/orders");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    PlaceOrderRequest CreatePlaceOrderRequest(List<ShoppingCartItemResponse> cartItems, bool includeEmail)
        => new(
            includeEmail ? _faker.Internet.Email() : null,
            [.. cartItems.Select(item => new OrderItemDto(item.BookId, item.Title, item.Quantity, ResolveUnitPrice(item)))],
            new DeliveryAddressDto(
                _faker.Name.FullName(),
                _faker.Address.StreetAddress(),
                _faker.Address.City(),
                _faker.Address.ZipCode(),
                _faker.Address.Country()),
            new PaymentInfoDto(
                _faker.Name.FullName(),
                _faker.Random.Replace("####"),
                _faker.Random.Int(1, 12),
                _faker.Date.Future().Year));

    ShoppingCartItemResponse CreateRandomCartItem()
        => new(
            Guid.CreateVersion7(),
            _faker.Commerce.ProductName(),
            _faker.Commerce.Ean13(),
            _faker.Random.Int(1, 3),
            new Dictionary<string, decimal>
            {
                ["GBP"] = _faker.Random.Decimal(5, 100)
            });

    static decimal ResolveUnitPrice(ShoppingCartItemResponse item)
    {
        if (item.Prices.TryGetValue("GBP", out var gbpPrice))
        {
            return gbpPrice;
        }

        return item.Prices.Values.FirstOrDefault();
    }
}
