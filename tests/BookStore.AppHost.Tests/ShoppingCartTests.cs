using System.Net;
using System.Net.Http.Json;
using BookStore.Client;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class ShoppingCartTests
{
    [Test]
    public async Task AddToCart_ShouldAddItemToCart()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Act - Add item to cart
        var addRequest = new AddToCartClientRequest(Guid.NewGuid(), 2);
        var addResponse = await httpClient.PostAsJsonAsync("/api/cart/items", addRequest);
        _ = await Assert.That(addResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Assert - Verify cart contains item
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart!.TotalItems).IsEqualTo(2);
        _ = await Assert.That(cart.Items.Count).IsEqualTo(1);
        _ = await Assert.That(cart.Items[0].BookId).IsEqualTo(addRequest.BookId);
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(2);
    }

    [Test]
    public async Task AddToCart_MultipleTimes_ShouldAccumulateQuantity()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var bookId = Guid.NewGuid();

        // Act - Add same book twice
        var addRequest1 = new AddToCartClientRequest(bookId, 2);
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", addRequest1);

        var addRequest2 = new AddToCartClientRequest(bookId, 3);
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", addRequest2);

        // Assert - Quantity should be accumulated (2 + 3 = 5)
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart!.TotalItems).IsEqualTo(5);
        _ = await Assert.That(cart.Items.Count).IsEqualTo(1);
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(5);
    }

    [Test]
    public async Task UpdateCartItemQuantity_ShouldUpdateQuantity()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var bookId = Guid.NewGuid();

        // Add item first
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(bookId, 2));

        // Act - Update quantity
        var updateRequest = new UpdateCartItemClientRequest(5);
        var updateResponse = await httpClient.PutAsJsonAsync($"/api/cart/items/{bookId}", updateRequest);
        _ = await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Assert
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart!.Items[0].Quantity).IsEqualTo(5);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(5);
    }

    [Test]
    public async Task RemoveFromCart_ShouldRemoveItem()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var bookId = Guid.NewGuid();

        // Add item first
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(bookId, 2));

        // Verify it exists
        var cartBefore = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cartBefore!.Items.Count).IsEqualTo(1);

        // Act - Remove item
        var removeResponse = await httpClient.DeleteAsync($"/api/cart/items/{bookId}");
        _ = await Assert.That(removeResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Assert - Cart should be empty
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart!.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task ClearCart_ShouldRemoveAllItems()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Add multiple items
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(Guid.NewGuid(), 2));
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(Guid.NewGuid(), 3));
        _ = await httpClient.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(Guid.NewGuid(), 1));

        // Verify items exist
        var cartBefore = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cartBefore!.Items.Count).IsEqualTo(3);

        //Act - Clear cart
        var clearResponse = await httpClient.DeleteAsync("/api/cart");
        _ = await Assert.That(clearResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Assert - Cart should be empty
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart!.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task GetCart_WhenEmpty_ShouldReturnEmptyCart()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Act
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");

        // Assert
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart!.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task AddToCart_WithInvalidQuantity_ShouldReturnBadRequest()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Act & Assert - Zero quantity
        var request1 = new AddToCartClientRequest(Guid.NewGuid(), 0);
        var response1 = await httpClient.PostAsJsonAsync("/api/cart/items", request1);
        _ = await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        // Act & Assert - Negative quantity
        var request2 = new AddToCartClientRequest(Guid.NewGuid(), -1);
        var response2 = await httpClient.PostAsJsonAsync("/api/cart/items", request2);
        _ = await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CartOperations_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = TestHelpers.GetUnauthenticatedClient();

        // Act & Assert - Get cart
        var getResponse = await unauthenticatedClient.GetAsync("/api/cart");
        _ = await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Add to cart
        var addResponse = await unauthenticatedClient.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(Guid.NewGuid(), 1));
        _ = await Assert.That(addResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Update cart item
        var updateResponse = await unauthenticatedClient.PutAsJsonAsync($"/api/cart/items/{Guid.NewGuid()}", new UpdateCartItemClientRequest(5));
        _ = await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Remove from cart
        var removeResponse = await unauthenticatedClient.DeleteAsync($"/api/cart/items/{Guid.NewGuid()}");
        _ = await Assert.That(removeResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Clear cart
        var clearResponse = await unauthenticatedClient.DeleteAsync("/api/cart");
        _ = await Assert.That(clearResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
