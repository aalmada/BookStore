using System.Net;
using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class BookCrudTests
{
    [Test]
    public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);

        // Assert
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
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

        // Act - Update the book with new fake data and ETag
        var updateBookRequest = TestDataGenerators.GenerateFakeBookRequest();
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

        // Assert
        _ = await Assert.That(updateResponse.IsSuccessStatusCode).IsTrue();
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

        // Act - Delete the book with ETag
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{createdBook.Id}");
        deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

        var deleteResponse = await httpClient.SendAsync(deleteRequest);

        if (deleteResponse.StatusCode != HttpStatusCode.NoContent)
        {
            var error = await deleteResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Delete failed with status {deleteResponse.StatusCode}: {error}");
        }

        // Assert
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    record BookResponse(Guid Id, string Title, string Isbn);
}
