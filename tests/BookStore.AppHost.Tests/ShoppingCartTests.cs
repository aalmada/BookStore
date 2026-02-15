using System.Net.Http.Json;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class ShoppingCartTests
{
    [Test]
    public async Task AddToCart_ShouldAddItemToCart()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first (cart needs real books to display)
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);

        // Act - Add item to cart and wait for async projection
        await ShoppingCartHelpers.AddToCartAsync(client, createdBook.Id, 2);

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
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);

        // Act - Add same book twice
        await ShoppingCartHelpers.AddToCartAsync(client, createdBook.Id, 2);

        await ShoppingCartHelpers.AddToCartAsync(client, createdBook.Id, 3);

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
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);

        var bookId = createdBook.Id;

        // Add item first
        await ShoppingCartHelpers.AddToCartAsync(client, bookId, 2);

        // Act - Update quantity
        await ShoppingCartHelpers.UpdateCartItemQuantityAsync(client, bookId, 5);

        // Assert
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart.Items[0].Quantity).IsEqualTo(5);
        _ = await Assert.That(cart.TotalItems).IsEqualTo(5);
    }

    [Test]
    public async Task RemoveFromCart_ShouldRemoveItem()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create a book first
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);

        var bookId = createdBook.Id;

        // Add item first
        await ShoppingCartHelpers.AddToCartAsync(client, bookId, 2);

        // Verify it exists
        var cartBefore = await client.GetShoppingCartAsync();
        _ = await Assert.That(cartBefore.Items.Count).IsEqualTo(1);

        // Act - Remove item
        await ShoppingCartHelpers.RemoveFromCartAsync(client, bookId);

        // Assert - Cart should be empty
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart.Items).IsEmpty();
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task ClearCart_ShouldRemoveAllItems()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Create 3 books first
        var book1 = await BookHelpers.CreateBookAsync(adminClient);
        var book2 = await BookHelpers.CreateBookAsync(adminClient);
        var book3 = await BookHelpers.CreateBookAsync(adminClient);

        // Add multiple items
        await ShoppingCartHelpers.AddToCartAsync(client, book1.Id, 2);
        await ShoppingCartHelpers.AddToCartAsync(client, book2.Id, 3);
        await ShoppingCartHelpers.AddToCartAsync(client, book3.Id);

        // Verify items exist
        var cartBefore = await client.GetShoppingCartAsync();
        _ = await Assert.That(cartBefore.Items.Count).IsEqualTo(3);

        //Act - Clear cart
        await ShoppingCartHelpers.ClearCartAsync(client);

        // Assert - Cart should be empty
        var cart = await client.GetShoppingCartAsync();
        _ = await Assert.That(cart.Items).IsEmpty();
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task GetCart_WhenEmpty_ShouldReturnEmptyCart()
    {
        // Arrange
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Act
        var cart = await client.GetShoppingCartAsync();

        // Assert
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart.Items).IsEmpty();
        _ = await Assert.That(cart.TotalItems).IsEqualTo(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(-100)]
    public async Task AddToCart_WithInvalidQuantity_ShouldReturnBadRequest(int quantity)
    {
        // Arrange
        var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IShoppingCartClient>();

        // Act & Assert
        var exception = await Assert
            .That(() => client.AddToCartAsync(new AddToCartClientRequest(Guid.NewGuid(), quantity)))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error = await exception.GetContentAsAsync<AuthenticationHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Cart.InvalidQuantity);
    }

    [Test]
    public async Task CartOperations_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var unauthenticatedHttpClient = HttpClientHelpers.GetUnauthenticatedClient();
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

