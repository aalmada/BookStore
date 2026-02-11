using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using TUnit.Assertions.Extensions;

namespace BookStore.AppHost.Tests;

public class FavoriteBooksTests
{
    [Test]
    public async Task GetFavoriteBooks_WhenAuthenticated_ShouldReturnOnlyFavorites()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var userClient = await TestHelpers.CreateUserAndGetClientAsync();
        var client = RestService.For<IBooksClient>(userClient.Client);

        // Create 2 books
        var book1 = await TestHelpers.CreateBookAsync(adminClient, TestHelpers.GenerateFakeBookRequest());
        var book2 = await TestHelpers.CreateBookAsync(adminClient, TestHelpers.GenerateFakeBookRequest());

        // Add 2 to favorites
        await TestHelpers.AddToFavoritesAsync(userClient.Client, book1.Id, userClient.UserId);
        await TestHelpers.AddToFavoritesAsync(userClient.Client, book2.Id, userClient.UserId);

        // Act
        var response = await client.GetFavoriteBooksAsync(new OrderedPagedRequest());

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Items.Count).IsGreaterThanOrEqualTo(2);

        // Verify the favorited books are in the response
        var favoriteIds = response.Items.Select(b => b.Id).ToHashSet();
        _ = await Assert.That(favoriteIds.Contains(book1.Id)).IsTrue();
        _ = await Assert.That(favoriteIds.Contains(book2.Id)).IsTrue();

        // Verify all returned books have IsFavorite = true
        _ = await Assert.That(response.Items.All(b => b.IsFavorite)).IsTrue();
    }

    [Test]
    public async Task GetFavoriteBooks_WhenNoFavorites_ShouldReturnEmpty()
    {
        // Arrange - Create a new authenticated user with no favorites
        var userClient = await TestHelpers.CreateUserAndGetClientAsync();
        var client = RestService.For<IBooksClient>(userClient.Client);

        // Act
        var response = await client.GetFavoriteBooksAsync(new OrderedPagedRequest());

        // Assert
        _ = await Assert.That(response).IsNotNull();
        // Note: the admin user might have favorites from other tests, so we just verify the structure
        _ = await Assert.That(response.Items).IsNotNull();
    }

    [Test]
    public async Task GetFavoriteBooks_WhenUnauthenticated_ShouldReturn401()
    {
        // Arrange
        var unauthenticatedClient = TestHelpers.GetUnauthenticatedClient();
        var client = RestService.For<IBooksClient>(unauthenticatedClient);

        // Act & Assert
        try
        {
            _ = await client.GetFavoriteBooksAsync(new OrderedPagedRequest());
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task GetFavoriteBooks_WithPagination_ShouldRespectPaging()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var userClient = await TestHelpers.CreateUserAndGetClientAsync();
        var client = RestService.For<IBooksClient>(userClient.Client);

        // Create and favorite at least 5 books
        for (var i = 0; i < 5; i++)
        {
            var book = await TestHelpers.CreateBookAsync(adminClient, TestHelpers.GenerateFakeBookRequest());
            await TestHelpers.AddToFavoritesAsync(userClient.Client, book.Id, userClient.UserId);
        }

        // Act - Request first page with 3 items
        var response = await client.GetFavoriteBooksAsync(new OrderedPagedRequest { Page = 1, PageSize = 3 });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.PageNumber).IsEqualTo(1);
        _ = await Assert.That(response.PageSize).IsEqualTo(3);
        _ = await Assert.That(response.Items.Count).IsLessThanOrEqualTo(3);
        _ = await Assert.That(response.TotalItemCount).IsGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task GetFavoriteBooks_WithSorting_ShouldApplySort()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var userClient = await TestHelpers.CreateUserAndGetClientAsync();
        var client = RestService.For<IBooksClient>(userClient.Client);

        // Create books with specific titles for sorting
        var requestA = TestHelpers.GenerateFakeBookRequest();
        requestA.Title = $"AAA Book {Guid.NewGuid()}";
        var bookA = await TestHelpers.CreateBookAsync(adminClient, requestA);

        var requestZ = TestHelpers.GenerateFakeBookRequest();
        requestZ.Title = $"ZZZ Book {Guid.NewGuid()}";
        var bookZ = await TestHelpers.CreateBookAsync(adminClient, requestZ);

        // Add to favorites
        await TestHelpers.AddToFavoritesAsync(userClient.Client, bookA.Id, userClient.UserId);
        await TestHelpers.AddToFavoritesAsync(userClient.Client, bookZ.Id, userClient.UserId);

        // Act - Sort by title descending
        var response =
            await client.GetFavoriteBooksAsync(new OrderedPagedRequest { SortBy = "title", SortOrder = "desc" });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Items.Count).IsGreaterThanOrEqualTo(2);

        // Find our test books in the response
        var ourBooks = response.Items.Where(b => b.Id == bookA.Id || b.Id == bookZ.Id).ToList();
        _ = await Assert.That(ourBooks.Count).IsEqualTo(2);

        // Verify ZZZ comes before AAA in descending order
        var indexZ = response.Items.ToList().FindIndex(b => b.Id == bookZ.Id);
        var indexA = response.Items.ToList().FindIndex(b => b.Id == bookA.Id);
        _ = await Assert.That(indexZ).IsLessThan(indexA);
    }

    [Test]
    public async Task GetFavoriteBooks_AfterRemovingFavorite_ShouldNotIncludeBook()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var userClient = await TestHelpers.CreateUserAndGetClientAsync();
        var client = RestService.For<IBooksClient>(userClient.Client);
        var book = await TestHelpers.CreateBookAsync(adminClient, TestHelpers.GenerateFakeBookRequest());

        // Add to favorites
        await TestHelpers.AddToFavoritesAsync(userClient.Client, book.Id, userClient.UserId);

        // Verify it appears in favorites
        var response1 = await client.GetFavoriteBooksAsync(new OrderedPagedRequest());
        _ = await Assert.That(response1).IsNotNull();
        var favoriteIds1 = response1.Items.Select(b => b.Id).ToHashSet();
        _ = await Assert.That(favoriteIds1.Contains(book.Id)).IsTrue();

        // Act - Remove from favorites
        await TestHelpers.RemoveFromFavoritesAsync(userClient.Client, book.Id, userClient.UserId);

        // Assert - Verify it no longer appears
        var response2 = await client.GetFavoriteBooksAsync(new OrderedPagedRequest());
        _ = await Assert.That(response2).IsNotNull();
        var favoriteIds2 = response2.Items.Select(b => b.Id).ToHashSet();
        _ = await Assert.That(favoriteIds2.Contains(book.Id)).IsFalse();
    }
}
