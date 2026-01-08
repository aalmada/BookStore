using System.Net;
using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class BookCrudTests
{
    [Test]
    public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        BookResponse? createdBook = null;

        // Act - Connect to SSE before creating, then wait for notification
        // Note: Creation often comes as BookUpdated due to projection upsert semantics
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty, // Match any ID since we don't know it yet
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TimeSpan.FromSeconds(10));

        // Assert
        _ = await Assert.That(createdBook).IsNotNull();
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task UpdateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();

        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
        _ = await Assert.That(createdBook).IsNotNull();

        // Wait for async projection to complete (eventual consistency)
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Get the book to retrieve its ETag (with retry for projection delay)
        HttpResponseMessage? getResponse = null;
        for (var i = 0; i < 5; i++)
        {
            getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
            if (getResponse.IsSuccessStatusCode)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        _ = await Assert.That(getResponse!.IsSuccessStatusCode).IsTrue();

        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Act - Update the book with new fake data and ETag, verify SSE event
        var updateBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookUpdated",
            async () =>
            {
                var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/books/{createdBook.Id}")
                {
                    Content = JsonContent.Create(updateBookRequest)
                };
                updateRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

                var updateResponse = await httpClient.SendAsync(updateRequest);

                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Update failed with status {updateResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(updateResponse.IsSuccessStatusCode).IsTrue();
            },
            TimeSpan.FromSeconds(10));

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task DeleteBook_EndToEndFlow_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();

        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
        _ = await Assert.That(createdBook).IsNotNull();

        // Wait for async projection to complete (eventual consistency)
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Get the book to retrieve its ETag (with retry for projection delay)
        HttpResponseMessage? getResponse = null;
        for (var i = 0; i < 5; i++)
        {
            getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
            if (getResponse.IsSuccessStatusCode)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        _ = await Assert.That(getResponse!.IsSuccessStatusCode).IsTrue();

        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Act - Delete the book with ETag and verify SSE event
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookDeleted",
            async () =>
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{createdBook.Id}");
                deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

                var deleteResponse = await httpClient.SendAsync(deleteRequest);

                if (deleteResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    var error = await deleteResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Delete failed with status {deleteResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(10));

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task RestoreBook_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // 1. Create Book
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();

        // 2. Wait for projection
        await Task.Delay(TimeSpan.FromSeconds(2));

        // 3. Get ETag for delete
        var getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
        var deleteEtag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(deleteEtag).IsNotNull();

        // 4. Soft Delete Book
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{createdBook.Id}");
        deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(deleteEtag!));
        var deleteResponse = await httpClient.SendAsync(deleteRequest);
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Act - Connect to SSE before restoring, then wait for notification
        // Note: Projecting a restore is seen as an Update (IsDeleted goes from true -> false)
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var restoreResponse = await httpClient.PostAsync($"/api/admin/books/{createdBook.Id}/restore", null);
                if (!restoreResponse.IsSuccessStatusCode)
                {
                    var error = await restoreResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SSE-TEST] Restore failed: {restoreResponse.StatusCode} - {error}");
                }

                _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(10));

        _ = await Assert.That(received).IsTrue();
    }

    record BookResponse(Guid Id, string Title, string Isbn);
}
