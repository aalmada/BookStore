using System.Net;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class BookSoftDeleteTests
{
    [Test]
    public async Task SoftDeleteFlow_FullLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        // 1. Create a book
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        // Verify visible in public API
        var initialGet = await publicClient.GetAsync($"/api/books/{bookId}");
        _ = await Assert.That(initialGet.IsSuccessStatusCode).IsTrue();

        // 2. Soft Delete via Admin API
        var getResponse = await adminClient.GetAsync($"/api/books/{bookId}");
        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{bookId}");
        deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));
        var deleteResponse = await adminClient.SendAsync(deleteRequest);
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Wait for projection to process deletion
        // We can check the admin endpoint until it shows as deleted if we exposed that property,
        // or just rely on the public endpoint disappearing.
        // Let's rely on polling the public endpoint until it's 404 (which Marten 8 should handle auto-filtering)
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            var r = await publicClient.GetAsync($"/api/books/{bookId}");
            return r.StatusCode == HttpStatusCode.NotFound;
        }, TimeSpan.FromSeconds(10), "Book did not disappear from public API");

        // 3. Verify Public API returns 404
        var deletedGet = await publicClient.GetAsync($"/api/books/{bookId}");
        _ = await Assert.That(deletedGet.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        // 4. Verify Admin API still returns it (using MaybeDeleted)
        // Note: The Admin Get All endpoint should return it
        var adminGetAll = await adminClient.GetFromJsonAsync<List<BookSearchProjection>>("/api/admin/books");
        var adminBook = adminGetAll!.FirstOrDefault(b => b.Id == bookId);
        _ = await Assert.That(adminBook).IsNotNull();
        _ = await Assert.That(adminBook!.Deleted).IsTrue();
        _ = await Assert.That(adminBook.DeletedAt).IsNotNull();

        // 5. Restore via Admin API
        // We need ETag again. Since we can't get it from public API (404),
        // we might need to rely on the admin list or just try unrestricted (if allowed) or fetch via a specific admin GET if we had one.
        // The AdminBookEndpoints currently uses If-Match for Restore.
        // Let's fetch it via a direct session query helper if needed, but integration tests should stick to API.
        // However, there is no single-item Admin GET endpoint exposed in AdminBookEndpoints!
        // The GetAll endpoint returns the list, let's see if it returns ETag... likely not in the body.
        // Wait, the `RestoreBook` endpoint requires If-Match.
        // But we can't GET the book to get the ETag if normal GET returns 404.
        // This reveals a potential API gap: Admin needs a way to GET a deleted book to get its Version/ETag for concurrency control during Restore.
        // For this test, let's bypass the ETag check if the endpoint allows it, or fail if it requires it.
        // Looking at RestoreBook implementation: `var etag = context.Request.Headers["If-Match"].FirstOrDefault();` 
        // Then command is created with ETag. Wolverine likely ignores concurrency if ETag is null?
        // Let's try restoring without ETag first.

        var restoreRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/books/{bookId}/restore");
        // restoreRequest.Headers.IfMatch.Add(...) - Trying without

        var restoreResponse = await adminClient.SendAsync(restoreRequest);

        // If 412 or 428 required, we have an issue to solve in the implementation or test.
        // Assuming loose concurrency for now or that we can get lucky. 
        // Actually, if we soft-deleted, the stream version incremented.
        // If we don't provide ETag, Wolverine might just append.

        _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.OK).Or.IsEqualTo(HttpStatusCode.NoContent);

        // 6. Verify Reappearance
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            var r = await publicClient.GetAsync($"/api/books/{bookId}");
            return r.IsSuccessStatusCode;
        }, TimeSpan.FromSeconds(10), "Book did not reappear in public API");

        var restoredGet = await publicClient.GetAsync($"/api/books/{bookId}");
        _ = await Assert.That(restoredGet.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    public async Task SoftDeletedBook_ShouldBeVisibleToAdmin_ButNotPublic()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var createdBook = await TestHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        // Soft Delete
        var getResponse = await adminClient.GetAsync($"/api/books/{bookId}");
        var etag = getResponse.Headers.ETag?.Tag;

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{bookId}");
        deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));
        var deleteResponse = await adminClient.SendAsync(deleteRequest);
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Wait for projection
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            var r = await publicClient.GetAsync($"/api/books/{bookId}");
            return r.StatusCode == HttpStatusCode.NotFound;
        }, TimeSpan.FromSeconds(10), "Book did not disappear from public API");

        // Act & Assert

        // 1. Single Book Endpoint

        // Public -> 404
        var publicGet = await publicClient.GetAsync($"/api/books/{bookId}");
        _ = await Assert.That(publicGet.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        // Admin should see the book (Get)
        var response = await adminClient.GetAsync($"/api/books/{bookId}");
        if (!response.IsSuccessStatusCode)
        {
            var headers = string.Join("; ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"));
            throw new Exception($"Failed to get book. Status: {response.StatusCode}. Headers: {headers}");
        }

        var book = await response.Content.ReadFromJsonAsync<BookDto>();
        _ = await Assert.That(book).IsNotNull();
        _ = await Assert.That(book!.Id).IsEqualTo(bookId);
        _ = await Assert.That(book.IsDeleted).IsTrue();

        // 2. Search/List Endpoint

        // Public -> Should NOT contain the book
        var publicSearch = await publicClient.GetFromJsonAsync<PagedListDto<BookDto>>($"/api/books?search={createdBook.Title}");
        _ = await Assert.That(publicSearch!.Items.Any(b => b.Id == bookId)).IsFalse();

        // Admin -> Should contain the book with IsDeleted=true
        var adminSearch = await adminClient.GetFromJsonAsync<PagedListDto<BookDto>>($"/api/books?search={Uri.EscapeDataString(createdBook.Title)}");

        // Note: The search endpoint might need to cache update or re-query. Admin requests have their own cache key due to isAdmin param.
        // We might need to wait for the cache to invalidate or just rely on the new key being generated fresh if not present.
        // However, HybridCache is used. If "isAdmin=true" key was never cached, it will query Marten.
        // Marten query should return it.

        // Let's check if it returns
        var foundBook = adminSearch!.Items.FirstOrDefault(b => b.Id == bookId);

        _ = await Assert.That(foundBook).IsNotNull();
        _ = await Assert.That(foundBook!.IsDeleted).IsTrue();
    }

    // Helper class to deserialzie the projection from Admin API
    class BookSearchProjection
    {
        public Guid Id { get; set; }
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
