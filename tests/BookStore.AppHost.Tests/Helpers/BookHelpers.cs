using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using TUnit.Assertions.Extensions;
using CreateBookRequest = BookStore.Client.CreateBookRequest;
using UpdateBookRequest = BookStore.Client.UpdateBookRequest;

namespace BookStore.AppHost.Tests.Helpers;

public static class BookHelpers
{
    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, object createBookRequest)
    {
        // Try to get Id from the request object if it's one of ours
        var entityId = Guid.Empty;
        if (createBookRequest is CreateBookRequest req)
        {
            entityId = req.Id;
        }

        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            entityId,
            [
                "BookCreated", "BookUpdated"
            ], // Async projections may report as Update regardless of Insert/Update
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                }

                _ = createResponse.EnsureSuccessStatusCode();
                if (entityId == Guid.Empty)
                {
                    var createdBook = await createResponse.Content.ReadFromJsonAsync<BookDto>();
                    entityId = createdBook?.Id ?? Guid.Empty;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received || entityId == Guid.Empty)
        {
            throw new Exception("Failed to create book or receive BookUpdated event.");
        }

        return (await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{entityId}"))!;
    }

    public static async Task<BookDto> CreateBookAsync(IBooksClient client, CreateBookRequest createBookRequest)
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            createBookRequest.Id,
            ["BookCreated", "BookUpdated"],
            async () =>
            {
                var response = await client.CreateBookWithResponseAsync(createBookRequest);
                if (response.Error != null)
                {
                    throw response.Error;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive BookCreated event.");
        }

        return await client.GetBookAsync(createBookRequest.Id);
    }

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, Guid? publisherId = null,
        IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null)
    {
        // Ensure dependencies exist
        if (publisherId == null)
        {
            var pClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
            var pub = await PublisherHelpers.CreatePublisherAsync(pClient, FakeDataGenerators.GenerateFakePublisherRequest());
            publisherId = pub.Id;
        }

        if (authorIds == null || !authorIds.Any())
        {
            var aClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
            var auth = await AuthorHelpers.CreateAuthorAsync(aClient, FakeDataGenerators.GenerateFakeAuthorRequest());
            authorIds = [auth.Id];
        }

        if (categoryIds == null || !categoryIds.Any())
        {
            var cClient = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
            var cat = await CategoryHelpers.CreateCategoryAsync(cClient, FakeDataGenerators.GenerateFakeCategoryRequest());
            categoryIds = [cat.Id];
        }

        var createBookRequest = FakeDataGenerators.GenerateFakeBookRequest(publisherId, authorIds, categoryIds);
        return await CreateBookAsync(httpClient, createBookRequest);
    }

    public static async Task<BookDto> CreateBookAsync(IBooksClient client, Guid? publisherId = null,
        IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null)
    {
        // Ensure dependencies exist
        if (publisherId == null)
        {
            var pClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
            var pub = await PublisherHelpers.CreatePublisherAsync(pClient, FakeDataGenerators.GenerateFakePublisherRequest());
            publisherId = pub.Id;
        }

        if (authorIds == null || !authorIds.Any())
        {
            var aClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
            var auth = await AuthorHelpers.CreateAuthorAsync(aClient, FakeDataGenerators.GenerateFakeAuthorRequest());
            authorIds = [auth.Id];
        }

        if (categoryIds == null || !categoryIds.Any())
        {
            var cClient = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
            var cat = await CategoryHelpers.CreateCategoryAsync(cClient, FakeDataGenerators.GenerateFakeCategoryRequest());
            categoryIds = [cat.Id];
        }

        var createBookRequest = FakeDataGenerators.GenerateFakeBookRequest(publisherId, authorIds, categoryIds);
        return await CreateBookAsync(client, createBookRequest);
    }

    public static async Task UpdateBookAsync(HttpClient client, Guid bookId, object updatePayload, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            "BookUpdated",
            async () =>
            {
                var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/books/{bookId}")
                {
                    Content = JsonContent.Create(updatePayload)
                };
                updateRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

                var updateResponse = await client.SendAsync(updateRequest);
                if (!updateResponse.IsSuccessStatusCode)
                {
                }

                _ = await Assert.That(updateResponse.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after UpdateBook.");
        }
    }

    public static async Task<BookDto> UpdateBookAsync(IBooksClient client, Guid bookId, UpdateBookRequest updatePayload,
        string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            "BookUpdated",
            async () => await client.UpdateBookAsync(bookId, updatePayload, etag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after UpdateBook.");
        }

        return await client.GetBookAsync(bookId);
    }

    public static async Task<AdminBookDto> UpdateBookAsync(IBooksClient client, AdminBookDto book,
        UpdateBookRequest request)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(book.ETag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            book.Id,
            "BookUpdated",
            async () => await client.UpdateBookAsync(book.Id, request, book.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive BookUpdated event.");
        }

        return await client.GetBookAdminAsync(book.Id);
    }

    // Helper to accept generic object and cast if possible or use fake request
    public static async Task<BookDto> UpdateBookAsync(IBooksClient client, Guid bookId, object updatePayload,
        string etag)
    {
        var json = JsonSerializer.Serialize(updatePayload);
        var request = JsonSerializer.Deserialize<UpdateBookRequest>(json);
        return await UpdateBookAsync(client, bookId, request!, etag);
    }

    public static async Task DeleteBookAsync(HttpClient client, Guid bookId, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            "BookDeleted",
            async () =>
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{bookId}");
                deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

                var deleteResponse = await client.SendAsync(deleteRequest);
                if (!deleteResponse.IsSuccessStatusCode)
                {
                }

                _ = await Assert.That(deleteResponse.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookDeleted event after DeleteBook.");
        }
    }

    public static async Task<AdminBookDto> DeleteBookAsync(IBooksClient client, Guid bookId, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            ["BookDeleted", "BookSoftDeleted"],
            async () => await client.SoftDeleteBookAsync(bookId, etag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookSoftDeleted event after DeleteBook.");
        }

        return await client.GetBookAdminAsync(bookId);
    }

    public static async Task<AdminBookDto> DeleteBookAsync(IBooksClient client, BookDto book)
    {
        var etag = book.ETag;
        if (string.IsNullOrEmpty(etag))
        {
            var latest = await client.GetBookAsync(book.Id);
            etag = latest.ETag;
        }

        return await DeleteBookAsync(client, book.Id, etag!);
    }

    public static async Task RestoreBookAsync(HttpClient client, Guid bookId, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            ["BookUpdated", "BookRestored"],
            async () =>
            {
                var restoreResponse = await client.PostAsync($"/api/admin/books/{bookId}/restore", null);
                if (!restoreResponse.IsSuccessStatusCode)
                {
                }

                _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after RestoreBook.");
        }
    }

    public static async Task<BookDto> RestoreBookAsync(IBooksClient client, Guid bookId, string? etag = null)
    {
        var currentETag = etag;
        if (string.IsNullOrEmpty(currentETag))
        {
            // Use Admin endpoint to get the book, including soft-deleted ones, to get the ETag
            var book = await client.GetBookAdminAsync(bookId);
            currentETag = book?.ETag;
            Console.WriteLine($"[TestHelpers] Fetched ETag for restore: {currentETag}");
        }

        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(currentETag) ?? 0;
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            ["BookUpdated", "BookRestored"],
            async () => await client.RestoreBookAsync(bookId, apiVersion: "1.0", etag: currentETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after RestoreBook.");
        }

        return await client.GetBookAsync(bookId);
    }

    public static async Task RateBookAsync(HttpClient client, Guid bookId, int rating, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.PostAsJsonAsync($"/api/books/{bookId}/rating", new { Rating = rating });
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RateBook.");
        }
    }

    public static async Task RateBookAsync(IBooksClient client, Guid bookId, int rating, Guid expectedEntityId,
        string expectedEvent) => await SseEventHelpers.ExecuteAndWaitForEventAsync(
        expectedEntityId,
        expectedEvent,
        () => client.RateBookAsync(bookId, new RateBookRequest(rating)),
        TimeSpan.FromSeconds(10), // Increased timeout
        minTimestamp: DateTimeOffset.UtcNow); // Use current time to avoid stale events

    public static async Task RemoveRatingAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.DeleteAsync($"/api/books/{bookId}/rating");
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveRating.");
        }
    }

    public static async Task RemoveRatingAsync(IBooksClient client, Guid bookId, Guid expectedEntityId,
        string expectedEvent) => await SseEventHelpers.ExecuteAndWaitForEventAsync(
        expectedEntityId,
        expectedEvent,
        () => client.RemoveBookRatingAsync(bookId),
        TimeSpan.FromSeconds(10),
        minTimestamp: DateTimeOffset.UtcNow);

    public static async Task AddToFavoritesAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.PostAsync($"/api/books/{bookId}/favorites", null);
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after AddToFavorites.");
        }
    }

    public static async Task AddToFavoritesAsync(IBooksClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () => await client.AddBookToFavoritesAsync(bookId,
                null),
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after AddToFavorites.");
        }
    }

    public static async Task RemoveFromFavoritesAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.DeleteAsync($"/api/books/{bookId}/favorites");
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveFromFavorites.");
        }
    }

    public static async Task RemoveFromFavoritesAsync(IBooksClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var book = await client.GetBookAsync(bookId);
                await client.RemoveBookFromFavoritesAsync(bookId, book?.ETag);
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveFromFavorites.");
        }
    }
}
