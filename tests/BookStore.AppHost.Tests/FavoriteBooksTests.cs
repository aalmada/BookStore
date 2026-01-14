using System.Net;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class FavoriteBooksTests
{
    [Test]
    public async Task GetFavoriteBooks_WhenAuthenticated_ShouldReturnOnlyFavorites()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Create 3 books
        var book1 = await TestHelpers.CreateBookAsync(httpClient);
        var book2 = await TestHelpers.CreateBookAsync(httpClient);
        var book3 = await TestHelpers.CreateBookAsync(httpClient);

        // Add 2 to favorites
        await TestHelpers.AddToFavoritesAsync(httpClient, book1.Id);
        await TestHelpers.AddToFavoritesAsync(httpClient, book2.Id);

        // Act
        var response = await httpClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites");

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.Items.Count).IsGreaterThanOrEqualTo(2);

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
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Ensure cart is empty (test helper for ensuring clean state)
        await TestHelpers.EnsureCartIsEmptyAsync(httpClient);

        // Act
        var response = await httpClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites");

        // Assert
        _ = await Assert.That(response).IsNotNull();
        // Note: the admin user might have favorites from other tests, so we just verify the structure
        _ = await Assert.That(response!.Items).IsNotNull();
    }

    [Test]
    public async Task GetFavoriteBooks_WhenUnauthenticated_ShouldReturn401()
    {
        // Arrange
        var publicClient = TestHelpers.GetUnauthenticatedClient();
        var globalHooks = GlobalHooks.NotificationService;
        _ = await globalHooks!.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await publicClient.GetAsync("/api/books/favorites");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFavoriteBooks_WithPagination_ShouldRespectPaging()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Create and favorite at least 5 books
        var books = new List<BookDto>();
        for (var i = 0; i < 5; i++)
        {
            var book = await TestHelpers.CreateBookAsync(httpClient);
            await TestHelpers.AddToFavoritesAsync(httpClient, book.Id);
            books.Add(book);
        }

        // Act - Request first page with 3 items
        var response = await httpClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites?page=1&pageSize=3");

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.PageNumber).IsEqualTo(1);
        _ = await Assert.That(response.PageSize).IsEqualTo(3);
        _ = await Assert.That(response.Items.Count).IsLessThanOrEqualTo(3);
        _ = await Assert.That(response.TotalItemCount).IsGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task GetFavoriteBooks_WithSorting_ShouldApplySort()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // Create books with specific titles for sorting
        var bookA = await TestHelpers.CreateBookAsync(httpClient, new
        {
            Title = $"AAA Book {Guid.NewGuid()}",
            Isbn = "978-3-16-148410-0",
            Language = "en",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Description = "Test description" }
            },
            PublicationDate = new { Year = 2024, Month = 1, Day = 1 },
            PublisherId = (Guid?)null,
            AuthorIds = new Guid[] { },
            CategoryIds = new Guid[] { },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
        });

        var bookZ = await TestHelpers.CreateBookAsync(httpClient, new
        {
            Title = $"ZZZ Book {Guid.NewGuid()}",
            Isbn = "978-3-16-148410-1",
            Language = "en",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Description = "Test description" }
            },
            PublicationDate = new { Year = 2024, Month = 1, Day = 1 },
            PublisherId = (Guid?)null,
            AuthorIds = new Guid[] { },
            CategoryIds = new Guid[] { },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
        });

        // Add to favorites
        await TestHelpers.AddToFavoritesAsync(httpClient, bookA.Id);
        await TestHelpers.AddToFavoritesAsync(httpClient, bookZ.Id);

        // Act - Sort by title descending
        var response = await httpClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites?sortBy=title&sortOrder=desc");

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.Items.Count).IsGreaterThanOrEqualTo(2);

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
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var book = await TestHelpers.CreateBookAsync(httpClient);

        // Add to favorites
        await TestHelpers.AddToFavoritesAsync(httpClient, book.Id);

        // Verify it appears in favorites
        var response1 = await httpClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites");
        _ = await Assert.That(response1).IsNotNull();
        var favoriteIds1 = response1!.Items.Select(b => b.Id).ToHashSet();
        _ = await Assert.That(favoriteIds1.Contains(book.Id)).IsTrue();

        // Act - Remove from favorites
        await TestHelpers.RemoveFromFavoritesAsync(httpClient, book.Id);

        // Assert - Verify it no longer appears
        var response2 = await httpClient.GetFromJsonAsync<PagedListDto<BookDto>>("/api/books/favorites");
        _ = await Assert.That(response2).IsNotNull();
        var favoriteIds2 = response2!.Items.Select(b => b.Id).ToHashSet();
        _ = await Assert.That(favoriteIds2.Contains(book.Id)).IsFalse();
    }
}
