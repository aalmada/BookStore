using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Tests for tenant isolation of user-profile features (ratings, favorites, cart).
/// These verify that user data is correctly scoped to tenants.
/// </summary>
public class TenantUserIsolationTests
{
    [Test]
    public async Task RateBook_InSpecificTenant_ShouldUpdateRating()
    {
        // Arrange - Setup tenant and user
        var tenantId = "acme";
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var loginRes = await TestHelpers.LoginAsAdminAsync(adminClient, tenantId);
        _ = await Assert.That(loginRes).IsNotNull();

        var tenantAdminClient = await TestHelpers.GetTenantClientAsync(tenantId, loginRes!.AccessToken);
        var book = await TestHelpers.CreateBookAsync(tenantAdminClient);
        var userClient = await TestHelpers.CreateUserAndGetClientAsync(tenantId);

        // Act - Rate the book
        var rating = 5;
        await TestHelpers.RateBookAsync(userClient, book.Id, rating, book.Id, "BookUpdated");

        // Assert - Verify User Rating
        var bookDto = await userClient.GetFromJsonAsync<BookDto>($"/api/books/{book.Id}");
        _ = await Assert.That(bookDto!.UserRating).IsEqualTo(rating);
        _ = await Assert.That(bookDto.AverageRating).IsEqualTo((float)rating);
    }

    [Test]
    public async Task AddToFavorites_InSpecificTenant_ShouldUpdateFavorites()
    {
        // Arrange
        var tenantId = "contoso";
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var loginRes = await TestHelpers.LoginAsAdminAsync(adminClient, tenantId);
        _ = await Assert.That(loginRes).IsNotNull();

        var tenantAdminClient = await TestHelpers.GetTenantClientAsync(tenantId, loginRes!.AccessToken);
        var book = await TestHelpers.CreateBookAsync(tenantAdminClient);
        var userClient = await TestHelpers.CreateUserAndGetClientAsync(tenantId);

        // Act
        await TestHelpers.AddToFavoritesAsync(userClient, book.Id);

        // Assert - Verify via Get Favorites endpoint
        var favorites = await userClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites");
        _ = await Assert.That(favorites).IsNotNull();
        _ = await Assert.That(favorites!.Items.Any(b => b.Id == book.Id)).IsTrue();

        // Verify IsFavorite flag on book details
        var bookDetails = await userClient.GetFromJsonAsync<BookDto>($"/api/books/{book.Id}");
        _ = await Assert.That(bookDetails!.IsFavorite).IsTrue();
    }

    [Test]
    public async Task AddToCart_InSpecificTenant_ShouldPersistInTenant()
    {
        // Arrange - Setup tenant-specific context
        var tenantId = "acme";
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var loginRes = await TestHelpers.LoginAsAdminAsync(adminClient, tenantId);
        _ = await Assert.That(loginRes).IsNotNull();

        var tenantAdminClient = await TestHelpers.GetTenantClientAsync(tenantId, loginRes!.AccessToken);
        var book = await TestHelpers.CreateBookAsync(tenantAdminClient);
        var userClient = await TestHelpers.CreateUserAndGetClientAsync(tenantId);

        // Act - Add to cart
        await TestHelpers.AddToCartAsync(userClient, book.Id, 2);

        // Assert - Cart should contain the item
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await userClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
            return cart?.TotalItems == 2;
        }, TimeSpan.FromSeconds(5), "Cart was not populated after AddToCart");

        var finalCart = await userClient.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(finalCart).IsNotNull();
        _ = await Assert.That(finalCart!.TotalItems).IsEqualTo(2);
        _ = await Assert.That(finalCart.Items.Any(i => i.BookId == book.Id)).IsTrue();
    }

    [Test]
    public async Task UserData_ShouldBeIsolatedBetweenTenants()
    {
        // Arrange - Create users in two different tenants
        var tenant1 = "acme";
        var tenant2 = "contoso";

        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Tenant 1 setup
        var login1 = await TestHelpers.LoginAsAdminAsync(adminClient, tenant1);
        _ = await Assert.That(login1).IsNotNull();
        var admin1Client = await TestHelpers.GetTenantClientAsync(tenant1, login1!.AccessToken);
        var book1 = await TestHelpers.CreateBookAsync(admin1Client);
        var user1Client = await TestHelpers.CreateUserAndGetClientAsync(tenant1);

        // Tenant 2 setup
        var login2 = await TestHelpers.LoginAsAdminAsync(adminClient, tenant2);
        _ = await Assert.That(login2).IsNotNull();
        var admin2Client = await TestHelpers.GetTenantClientAsync(tenant2, login2!.AccessToken);
        var book2 = await TestHelpers.CreateBookAsync(admin2Client);
        var user2Client = await TestHelpers.CreateUserAndGetClientAsync(tenant2);

        // Act - User1 rates book1, User2 rates book2
        await TestHelpers.RateBookAsync(user1Client, book1.Id, 5, book1.Id, "BookUpdated");
        await TestHelpers.RateBookAsync(user2Client, book2.Id, 3, book2.Id, "BookUpdated");

        // Assert - Each user sees only their own tenant's data
        var user1Book = await user1Client.GetFromJsonAsync<BookDto>($"/api/books/{book1.Id}");
        _ = await Assert.That(user1Book!.UserRating).IsEqualTo(5);

        var user2Book = await user2Client.GetFromJsonAsync<BookDto>($"/api/books/{book2.Id}");
        _ = await Assert.That(user2Book!.UserRating).IsEqualTo(3);

        // User1 should not see book2 (different tenant)
        var user1SeesBook2 = await user1Client.GetAsync($"/api/books/{book2.Id}");
        _ = await Assert.That(user1SeesBook2.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        // User2 should not see book1 (different tenant)
        var user2SeesBook1 = await user2Client.GetAsync($"/api/books/{book1.Id}");
        _ = await Assert.That(user2SeesBook1.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
