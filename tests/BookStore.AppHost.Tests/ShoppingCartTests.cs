using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class ShoppingCartTests
{
    [Test]
    public async Task AddToCart_ShouldAddItemToCart()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Create a book first (cart needs real books to display)
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act - Add item to cart and wait for async projection
        await TestHelpers.AddToCartAsync(httpClient, createdBook.Id, 2);

        // Assert - Verify cart contains item
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart!.TotalItems).IsEqualTo(2);
        _ = await Assert.That(cart.Items.Count).IsEqualTo(1);
        _ = await Assert.That(cart.Items[0].BookId).IsEqualTo(createdBook.Id);
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(2);
    }

    [Test]
    public async Task AddToCart_MultipleTimes_ShouldAccumulateQuantity()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Create a book first
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act - Add same book twice
        await TestHelpers.AddToCartAsync(httpClient, createdBook.Id, 2);

        await TestHelpers.AddToCartAsync(httpClient, createdBook.Id, 3);

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
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Create a book first
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        var bookId = createdBook.Id;

        // Add item first
        await TestHelpers.AddToCartAsync(httpClient, bookId, 2);

        // Act - Update quantity
        await TestHelpers.UpdateCartItemQuantityAsync(httpClient, bookId, 5);

        // Assert
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart!.Items[0].Quantity).IsEqualTo(5);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(5);
    }

    [Test]
    public async Task RemoveFromCart_ShouldRemoveItem()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Create a book first
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        var bookId = createdBook.Id;

        // Add item first
        await TestHelpers.AddToCartAsync(httpClient, bookId, 2);

        // Verify it exists
        var cartBefore = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cartBefore!.Items.Count).IsEqualTo(1);

        // Act - Remove item
        await TestHelpers.RemoveFromCartAsync(httpClient, bookId);

        // Assert - Cart should be empty
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart!.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task ClearCart_ShouldRemoveAllItems()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Create 3 books first
        var book1 = await TestHelpers.CreateBookAsync(adminClient);
        var book2 = await TestHelpers.CreateBookAsync(adminClient);
        var book3 = await TestHelpers.CreateBookAsync(adminClient);

        // Add multiple items
        await TestHelpers.AddToCartAsync(httpClient, book1.Id, 2);
        await TestHelpers.AddToCartAsync(httpClient, book2.Id, 3);
        await TestHelpers.AddToCartAsync(httpClient, book3.Id);

        // Verify items exist
        var cartBefore = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cartBefore!.Items.Count).IsEqualTo(3);

        //Act - Clear cart
        await TestHelpers.ClearCartAsync(httpClient);

        // Assert - Cart should be empty
        var cart = await httpClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart!.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task GetCart_WhenEmpty_ShouldReturnEmptyCart()
    {
        // Arrange
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

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
        var httpClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Act & Assert - Zero quantity
        var request1 = new AddToCartClientRequest(Guid.NewGuid(), 0);
        var response1 = await httpClient.PostAsJsonAsync("/api/cart/items", request1);
        _ = await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error1 = await response1.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error1?.Error).IsEqualTo(ErrorCodes.Cart.InvalidQuantity);

        // Act & Assert - Negative quantity
        var request2 = new AddToCartClientRequest(Guid.NewGuid(), -1);
        var response2 = await httpClient.PostAsJsonAsync("/api/cart/items", request2);
        _ = await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error2 = await response2.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error2?.Error).IsEqualTo(ErrorCodes.Cart.InvalidQuantity);
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
        var addResponse =
            await unauthenticatedClient.PostAsJsonAsync("/api/cart/items",
                new AddToCartClientRequest(Guid.NewGuid(), 1));
        _ = await Assert.That(addResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Update cart item
        var updateResponse = await unauthenticatedClient.PutAsJsonAsync($"/api/cart/items/{Guid.NewGuid()}",
            new UpdateCartItemClientRequest(5));
        _ = await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Remove from cart
        var removeResponse = await unauthenticatedClient.DeleteAsync($"/api/cart/items/{Guid.NewGuid()}");
        _ = await Assert.That(removeResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Act & Assert - Clear cart
        var clearResponse = await unauthenticatedClient.DeleteAsync("/api/cart");
        _ = await Assert.That(clearResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

