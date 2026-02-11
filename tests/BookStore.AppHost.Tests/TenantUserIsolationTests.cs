using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using SharedModels = BookStore.Shared.Models;

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
        var tenantAdminBooksClient = Refit.RestService.For<IBooksClient>(tenantAdminClient);

        // Use Refit to create book
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = $"TenantBook-{Guid.NewGuid()}",
            Isbn = "1234567890",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Tenant Book") },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m },
            AuthorIds = [],
            CategoryIds = []
        };

        SharedModels.BookDto book = null!;
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, ["BookCreated", "BookUpdated"],
            async () => book = await tenantAdminBooksClient.CreateBookAsync(createRequest),
            TestConstants.DefaultEventTimeout);

        var userClient = await TestHelpers.CreateUserAndGetClientAsync(tenantId);
        var userBooksClient = Refit.RestService.For<IBooksClient>(userClient.Client);

        // Act - Rate the book
        var rating = 5;
        // Verify method name in IBooksClient. IRateBookEndpoint.RateBookAsync?
        // Using wait for event
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(book.Id, "BookUpdated",
            async () => await userBooksClient.RateBookAsync(book.Id, new RateBookRequest(rating)),
            TestConstants.DefaultEventTimeout);

        // Assert - Verify User Rating
        var bookDto = await userBooksClient.GetBookAsync(book.Id);
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
        var tenantAdminBooksClient = Refit.RestService.For<IBooksClient>(tenantAdminClient);

        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = $"FavBook-{Guid.NewGuid()}",
            Isbn = "1234567890",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Fav Book") },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m },
            AuthorIds = [],
            CategoryIds = []
        };

        SharedModels.BookDto book = null!;
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, ["BookCreated", "BookUpdated"],
            async () => book = await tenantAdminBooksClient.CreateBookAsync(createRequest),
            TestConstants.DefaultEventTimeout);

        var userClient = await TestHelpers.CreateUserAndGetClientAsync(tenantId);
        var userBooksClient = Refit.RestService.For<IBooksClient>(userClient.Client);

        // Act
        // Act
        // AddBookToFavoritesAsync ?
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "UserUpdated",
            async () => await userBooksClient.AddBookToFavoritesAsync(book.Id),
            TestConstants.DefaultEventTimeout);

        // Assert - Verify via Get Favorites endpoint
        var favorites = await userBooksClient.GetFavoriteBooksAsync(new SharedModels.BookSearchRequest());
        _ = await Assert.That(favorites).IsNotNull();
        _ = await Assert.That(favorites!.Items.Any(b => b.Id == book.Id)).IsTrue();

        // Verify IsFavorite flag on book details
        var bookDetails = await userBooksClient.GetBookAsync(book.Id);
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
        var tenantAdminBooksClient = Refit.RestService.For<IBooksClient>(tenantAdminClient);

        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = $"CartBook-{Guid.NewGuid()}",
            Isbn = "1234567890",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Cart Book") },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m },
            AuthorIds = [],
            CategoryIds = []
        };

        SharedModels.BookDto book = null!;
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, ["BookCreated", "BookUpdated"],
            async () => book = await tenantAdminBooksClient.CreateBookAsync(createRequest),
            TestConstants.DefaultEventTimeout);

        var userClient = await TestHelpers.CreateUserAndGetClientAsync(tenantId);
        // Need Cart Client
        var userCartClient = Refit.RestService.For<IShoppingCartClient>(userClient.Client);

        // Act - Add to cart
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(userClient.UserId, "UserUpdated",
            async () => await userCartClient.AddToCartAsync(new AddToCartClientRequest(book.Id, 2)),
            TestConstants.DefaultEventTimeout);

        var finalCart = await userCartClient.GetShoppingCartAsync();
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

        // Helper to setup tenant and create book
        async Task<(SharedModels.BookDto book, TestHelpers.UserClient userClient)> SetupTenantAsync(string tid)
        {
            var login = await TestHelpers.LoginAsAdminAsync(adminClient, tid);
            var tClient = await TestHelpers.GetTenantClientAsync(tid, login!.AccessToken);
            var tBooksClient = Refit.RestService.For<IBooksClient>(tClient);

            var createRequest = new CreateBookRequest
            {
                Id = Guid.CreateVersion7(),
                Title = $"IsoBook-{tid}-{Guid.NewGuid()}",
                Isbn = "1234567890",
                Language = "en",
                Translations =
                    new Dictionary<string, BookTranslationDto> { ["en"] = new("Iso Book") },
                PublicationDate = new SharedModels.PartialDate(2024),
                Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m },
                AuthorIds = [],
                CategoryIds = []
            };

            SharedModels.BookDto createdBook = null!;
            _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, ["BookCreated", "BookUpdated"],
                async () => createdBook = await tBooksClient.CreateBookAsync(createRequest),
                TestConstants.DefaultEventTimeout);

            var uClient = await TestHelpers.CreateUserAndGetClientAsync(tid);
            return (createdBook, uClient);
        }

        var (book1, user1ClientInfo) = await SetupTenantAsync(tenant1);
        var (book2, user2ClientInfo) = await SetupTenantAsync(tenant2);

        var user1Client = Refit.RestService.For<IBooksClient>(user1ClientInfo.Client);
        var user2Client = Refit.RestService.For<IBooksClient>(user2ClientInfo.Client);

        // Act - User1 rates book1, User2 rates book2
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(book1.Id, "BookUpdated",
            async () => await user1Client.RateBookAsync(book1.Id, new RateBookRequest(5)),
            TestConstants.DefaultEventTimeout);

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(book2.Id, "BookUpdated",
            async () => await user2Client.RateBookAsync(book2.Id, new RateBookRequest(3)),
            TestConstants.DefaultEventTimeout);

        // Assert - Each user sees only their own tenant's data
        var user1Book = await user1Client.GetBookAsync(book1.Id);
        _ = await Assert.That(user1Book!.UserRating).IsEqualTo(5);

        var user2Book = await user2Client.GetBookAsync(book2.Id);
        _ = await Assert.That(user2Book!.UserRating).IsEqualTo(3);

        // User1 should not see book2 (different tenant)
        try
        {
            _ = await user1Client.GetBookAsync(book2.Id);
            Assert.Fail("User1 should not see Book2");
        }
        catch (ApiException ex)
        {
            // Expect 404 or 403 (NotFound usually)
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        // User2 should not see book1 (different tenant)
        try
        {
            _ = await user2Client.GetBookAsync(book1.Id);
            Assert.Fail("User2 should not see Book1");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }
}
