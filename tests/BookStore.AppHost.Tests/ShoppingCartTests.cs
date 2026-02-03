using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class ShoppingCartTests
{
    [Test]
    public async Task AddToCart_ShouldAddItemToCart()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first (cart needs real books to display)
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act - Add item to cart and wait for async projection
        await TestHelpers.AddToCartAsync(client, createdBook.Id, 2);

        // Assert - Verify cart contains item
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart.TotalItems).IsEqualTo(2);
        _ = await Assert.That(cart.Items.Count).IsEqualTo(1);
        _ = await Assert.That(cart.Items[0].BookId).IsEqualTo(createdBook.Id);
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(2);
    }

    [Test]
    public async Task AddToCart_MultipleTimes_ShouldAccumulateQuantity()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act - Add same book twice
        await TestHelpers.AddToCartAsync(client, createdBook.Id, 2);

        await TestHelpers.AddToCartAsync(client, createdBook.Id, 3);

        // Assert - Quantity should be accumulated (2 + 3 = 5)
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart.TotalItems).IsEqualTo(5);
        _ = await Assert.That(cart.Items.Count).IsEqualTo(1);
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(5);
    }

    [Test]
    public async Task UpdateCartItemQuantity_ShouldUpdateQuantity()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        var bookId = createdBook.Id;

        // Add item first
        await TestHelpers.AddToCartAsync(client, bookId, 2);

        // Act - Update quantity
        await TestHelpers.UpdateCartItemQuantityAsync(client, bookId, 5);

        // Assert
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(5);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(5);
    }

    [Test]
    public async Task RemoveFromCart_ShouldRemoveItem()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        var bookId = createdBook.Id;

        // Add item first
        await TestHelpers.AddToCartAsync(client, bookId, 2);

        // Verify it exists
        var cartBefore = await client.GetShoppingCartAsync();
        _ = await Assert.That(cartBefore.Items.Count).IsEqualTo(1);

        // Act - Remove item
        await TestHelpers.RemoveFromCartAsync(client, bookId);

        // Assert - Cart should be empty
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task ClearCart_ShouldRemoveAllItems()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create 3 books first
        var book1 = await TestHelpers.CreateBookAsync(adminClient);
        var book2 = await TestHelpers.CreateBookAsync(adminClient);
        var book3 = await TestHelpers.CreateBookAsync(adminClient);

        // Add multiple items
        await TestHelpers.AddToCartAsync(client, book1.Id, 2);
        await TestHelpers.AddToCartAsync(client, book2.Id, 3);
        await TestHelpers.AddToCartAsync(client, book3.Id);

        // Verify items exist
        var cartBefore = await client.GetShoppingCartAsync();
        _ = await Assert.That(cartBefore.Items.Count).IsEqualTo(3);

        //Act - Clear cart
        await TestHelpers.ClearCartAsync(client);

        // Assert - Cart should be empty
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task GetCart_WhenEmpty_ShouldReturnEmptyCart()
    {
        // Arrange
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Act
        var cart = await client.GetShoppingCartAsync();

        // Assert
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart.Items.Count).IsEqualTo(0);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(-100)]
    public async Task AddToCart_WithInvalidQuantity_ShouldReturnBadRequest(int quantity)
    {
        // Arrange
        var client = await TestHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Act & Assert
        var exception = await Assert
            .That(() => client.AddToCartAsync(new AddToCartClientRequest(Guid.NewGuid(), quantity)))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error = await exception.GetContentAsAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Cart.InvalidQuantity);
    }

    [Test]
    public async Task CartOperations_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var unauthenticatedHttpClient = TestHelpers.GetUnauthenticatedClient();
        var client = RestService.For<IShoppingCartClient>(unauthenticatedHttpClient);

        // Act & Assert - Get cart
        try
        {
            _ = await client.GetShoppingCartAsync();
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }

        // Act & Assert - Add to cart
        try
        {
            await client.AddToCartAsync(new AddToCartClientRequest(Guid.NewGuid(), 1));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }

        // Act & Assert - Update cart item
        try
        {
            await client.UpdateCartItemAsync(Guid.NewGuid(), new UpdateCartItemClientRequest(5));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }

        // Act & Assert - Remove from cart
        try
        {
            await client.RemoveFromCartAsync(Guid.NewGuid());
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }

        // Act & Assert - Clear cart
        try
        {
            await client.ClearCartAsync();
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }
}

