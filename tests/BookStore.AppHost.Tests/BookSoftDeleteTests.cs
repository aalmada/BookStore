using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class BookSoftDeleteTests
{
    [Test]
    public async Task SoftDeleteFlow_FullLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // We also need a raw client to fetch ETag, as Refit IBooksClient returns DTOs without headers
        // var rawAdminClient = await HttpClientHelpers.GetAuthenticatedClientAsync();
        var publicClient = Refit.RestService.For<IBooksClient>(HttpClientHelpers.GetUnauthenticatedClient());

        // 1. Create a book
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        // Verify visible in public API
        var initialGet = await publicClient.GetBookAsync(bookId);
        _ = await Assert.That(initialGet).IsNotNull();

        // 2. Soft Delete via Admin API

        // Perform Soft Delete
        var deletedBook = await BookHelpers.DeleteBookAsync(adminClient, createdBook);

        // 3. Verify Public API returns 404
        try
        {
            _ = await publicClient.GetBookAsync(bookId);
            Assert.Fail("Book should have been deleted (404)");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        // 4. Verify Admin API still returns it
        // The Admin Get All endpoint should return it
        var adminGetAll = await adminClient.GetAllBooksAdminAsync();
        var adminBook = adminGetAll!.FirstOrDefault(b => b.Id == bookId);
        _ = await Assert.That(adminBook).IsNotNull();
        _ = await Assert.That(adminBook!.IsDeleted).IsTrue();

        // 5. Restore via Admin API
        createdBook = await BookHelpers.RestoreBookAsync(adminClient, bookId);

        var restoredGet = await publicClient.GetBookAsync(bookId);
        _ = await Assert.That(restoredGet).IsNotNull();
    }

    [Test]
    public async Task SoftDeletedBook_ShouldBeVisibleToAdmin_ButNotPublic()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // var rawAdminClient = await HttpClientHelpers.GetAuthenticatedClientAsync(); // For ETag
        var publicClient = Refit.RestService.For<IBooksClient>(HttpClientHelpers.GetUnauthenticatedClient());

        var createdBook = await BookHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        // Soft Delete
        var deletedBook = await BookHelpers.DeleteBookAsync(adminClient, createdBook);

        // Act & Assert

        // 1. Single Book Endpoint

        // Public -> 404
        try
        {
            _ = await publicClient.GetBookAsync(bookId);
            Assert.Fail("Book should be not found");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        // Admin should see the book (Get)
        var book = await adminClient.GetBookAdminAsync(bookId);
        _ = await Assert.That(book).IsNotNull();
        _ = await Assert.That(book!.Id).IsEqualTo(bookId);
        _ = await Assert.That(book.IsDeleted).IsTrue();

        // 2. Search/List Endpoint

        // Public -> Should NOT contain the book
        var publicSearch = await publicClient.GetBooksAsync(new BookSearchRequest { Search = createdBook.Title });
        _ = await Assert.That(publicSearch!.Items.Any(b => b.Id == bookId)).IsFalse();

        // Admin -> Should contain the book with IsDeleted=true
        var adminSearch = await adminClient.GetBooksAsync(new BookSearchRequest { Search = createdBook.Title });

        var foundBook = adminSearch!.Items.FirstOrDefault(b => b.Id == bookId);

        _ = await Assert.That(foundBook).IsNotNull();
        _ = await Assert.That(foundBook!.IsDeleted).IsTrue();
    }
}
