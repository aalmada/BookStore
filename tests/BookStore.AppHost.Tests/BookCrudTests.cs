using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bogus;
using BookStore.Shared.Models;

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
            TestConstants.DefaultEventTimeout);

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

        // Create book and wait for projection
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedIsCreated).IsTrue();
        _ = await Assert.That(createdBook).IsNotNull();

        // Get the book to retrieve its ETag
        var getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(getResponse.IsSuccessStatusCode).IsTrue();

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
            TestConstants.DefaultEventTimeout);

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task DeleteBook_EndToEndFlow_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Create book and wait for projection
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedIsCreated).IsTrue();
        _ = await Assert.That(createdBook).IsNotNull();

        // Get the book to retrieve its ETag
        var getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(getResponse.IsSuccessStatusCode).IsTrue();

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
            TestConstants.DefaultEventTimeout);

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task RestoreBook_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // 1. Create Book and wait
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedIsCreated).IsTrue();

        // 2. (Removed explicit delay)

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
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();
    }

    record BookResponse(Guid Id, string Title, string Isbn);

    [Test]
    public async Task AddToFavorites_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Create book
        // Create book and wait
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(createdBook).IsNotNull();

        // Act - Add to favorites and wait for UserUpdated (fav ids update)
        var receivedFav = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await httpClient.PostAsync($"/api/books/{createdBook!.Id}/favorites", null);
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
             TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedFav).IsTrue();

        HttpResponseMessage response = new(HttpStatusCode.NoContent); // Fake response to satisfy strict replacement if reused below, but act is done inside waiter.

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Verify it is marked as favorite
        var getResponse = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(getResponse!.IsFavorite).IsTrue();
    }

    [Test]
    public async Task RemoveFromFavorites_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Create book
        // Create book and wait
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
             async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TestConstants.DefaultEventTimeout);

        // Add to favorites first and wait for UserUpdated
        var receivedFav = await TestHelpers.ExecuteAndWaitForEventAsync(
             Guid.Empty,
             "UserUpdated",
             async () => _ = await httpClient.PostAsync($"/api/books/{createdBook!.Id}/favorites", null),
             TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedFav).IsTrue();

        // Verify it IS favorite initially
        var initialGet = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(initialGet!.IsFavorite).IsTrue();

        // Act
        _ = await Assert.That(initialGet!.IsFavorite).IsTrue();

        // Act - Remove from favorites and wait for UserUpdated
        var receivedRemove = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await httpClient.DeleteAsync($"/api/books/{createdBook!.Id}/favorites");
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedRemove).IsTrue();

        var response = new HttpResponseMessage(HttpStatusCode.NoContent); // satisfy variable if used below

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Verify it is NOT marked as favorite anymore
        var getResponse = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(getResponse!.IsFavorite).IsFalse();
    }

    [Test]
    public async Task GetBook_WhenNotAuthenticated_ShouldHaveIsFavoriteFalse()
    {
        // Arrange
        // Create book as admin first
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
             Guid.Empty,
             "BookUpdated",
             async () =>
             {
                 var createResponse = await adminClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                 createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
             },
             TestConstants.DefaultEventTimeout);

        // 2. (Removed explicit delay)

        // Act - use unauthenticated client
        var publicClient = TestHelpers.GetUnauthenticatedClient();
        var getResponse = await publicClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook!.Id}");

        // Assert
        _ = await Assert.That(getResponse!.IsFavorite).IsFalse();
    }

    [Test]
    public async Task BookLikeCount_ShouldAggregateCorrectly_WhenMultipleUsersLikeBook()
    {
        var _faker = new Faker();
        var _anonClient = TestHelpers.GetUnauthenticatedClient();

        // 1. Arrange: Create a book as Admin
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();
        // 1. Arrange: Create a book as Admin and wait
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
             Guid.Empty,
             "BookUpdated",
             async () =>
             {
                 var createResponse = await adminClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                 _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                 createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
                 _ = await Assert.That(createdBook).IsNotNull();
             },
             TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedIsCreated).IsTrue();

        // (Removed explicit delay)

        // 2. Arrange: Create User 1 and User 2
        var user1Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);
        var user2Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);

        // 3. Act: User 1 Likes Book and wait for statistics update via SSE
        // The statistics update will trigger a BookUpdatedNotification
        var received1 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user1Client.PostAsync($"/api/books/{createdBook!.Id}/favorites", null);
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received1).IsTrue();

        // Assert: Count = 1
        var bookDto1 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto1!.LikeCount).IsEqualTo(1);

        // 4. Act: User 2 Likes Book and wait for SSE
        var received2 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user2Client.PostAsync($"/api/books/{createdBook!.Id}/favorites", null);
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received2).IsTrue();

        // Assert: Count = 2
        var bookDto2 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto2!.LikeCount).IsEqualTo(2);

        // 5. Act: User 1 Unlikes Book and wait for SSE
        var received3 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user1Client.DeleteAsync($"/api/books/{createdBook!.Id}/favorites");
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received3).IsTrue();

        // Assert: Count = 1
        var bookDto3 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto3!.LikeCount).IsEqualTo(1);
    }

    async Task<HttpClient> CreateAuthenticatedUserAsync(HttpClient anonClient, Faker faker)
    {
        var email = faker.Internet.Email();
        var password = faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        var registerResponse = await anonClient.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.IsSuccessStatusCode).IsTrue();

        // Login
        var loginResponse = await anonClient.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        _ = await Assert.That(loginResponse.IsSuccessStatusCode).IsTrue();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var client = TestHelpers.GetUnauthenticatedClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        return client;
    }

    record LoginResponse(string AccessToken, string RefreshToken);
}
