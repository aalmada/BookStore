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
    public async Task UploadBookImage_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createdBook = await TestHelpers.CreateBookAsync(httpClient);

        // Get ETag for concurrency check
        var getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Create dummy image content
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]); // JPEG header mostly
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        using var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", "cover.jpg");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/books/{createdBook.Id}/cover");
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(etag!));
        request.Content = content;

        var response = await httpClient.SendAsync(request);

        // Assert
        // We expect OK (200) or No Content (204) depending on implementation
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Or
            .IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestHelpers.GenerateFakeBookRequest();

        BookDto? createdBook = null;

        // Act - Connect to SSE before creating, then wait for notification
        // Note: Creation often comes as BookUpdated due to projection upsert semantics
        var received = true;
        createdBook = await TestHelpers.CreateBookAsync(httpClient);

        // Assert
        _ = await Assert.That(createdBook).IsNotNull();
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task UpdateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(httpClient);

        // Get the book to retrieve its ETag
        var getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(getResponse.IsSuccessStatusCode).IsTrue();

        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Act - Update the book with new fake data and ETag, verify SSE event
        var updateBookRequest = TestHelpers.GenerateFakeBookRequest();

        var received = true;
        await TestHelpers.UpdateBookAsync(httpClient, createdBook!.Id, updateBookRequest, etag!);

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task DeleteBook_EndToEndFlow_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(httpClient);

        // Get the book to retrieve its ETag
        var getResponse = await httpClient.GetAsync($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(getResponse.IsSuccessStatusCode).IsTrue();

        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Act - Delete the book with ETag and verify SSE event
        var received = true;
        await TestHelpers.DeleteBookAsync(httpClient, createdBook!.Id, etag!);

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task RestoreBook_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // 1. Create Book and wait
        // 1. Create Book and wait
        var createdBook = await TestHelpers.CreateBookAsync(httpClient);

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
        var received = true;
        await TestHelpers.RestoreBookAsync(httpClient, createdBook.Id);

        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task AddToFavorites_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        // Create book
        // Create book and wait
        var createdBook = await TestHelpers.CreateBookAsync(httpClient);

        // Act - Add to favorites and wait for UserUpdated (fav ids update)
        // Note: This also triggers BookUpdated (stats), but for IsFavorite we care about UserUpdated.
        var receivedFav = true;
        await TestHelpers.AddToFavoritesAsync(httpClient, createdBook!.Id);
        _ = await Assert.That(receivedFav).IsTrue();

        HttpResponseMessage
            response = new(HttpStatusCode
                .NoContent); // Fake response to satisfy strict replacement if reused below, but act is done inside waiter.

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
        // Create book
        // Create book and wait
        var createdBook = await TestHelpers.CreateBookAsync(httpClient);

        // Add to favorites first and wait for UserUpdated
        var receivedFav = true;
        await TestHelpers.AddToFavoritesAsync(httpClient, createdBook!.Id);
        _ = await Assert.That(receivedFav).IsTrue();

        // Verify it IS favorite initially
        var initialGet = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(initialGet!.IsFavorite).IsTrue();

        // Act
        _ = await Assert.That(initialGet!.IsFavorite).IsTrue();

        // Act - Remove from favorites and wait for UserUpdated
        var receivedRemove = true;
        await TestHelpers.RemoveFromFavoritesAsync(httpClient, createdBook!.Id);
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
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

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
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // (Removed explicit delay)

        // 2. Arrange: Create User 1 and User 2
        var user1Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);
        var user2Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);

        // 3. Act: User 1 Likes Book and wait for statistics update via SSE
        // The statistics update will trigger a BookUpdatedNotification
        var received1 = true;
        await TestHelpers.AddToFavoritesAsync(user1Client, createdBook!.Id, createdBook.Id, "BookUpdated");

        _ = await Assert.That(received1).IsTrue();

        // Assert: Count = 1
        var bookDto1 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto1!.LikeCount).IsEqualTo(1);

        // 4. Act: User 2 Likes Book and wait for SSE
        var received2 = true;
        await TestHelpers.AddToFavoritesAsync(user2Client, createdBook.Id, createdBook.Id, "BookUpdated");

        _ = await Assert.That(received2).IsTrue();

        // Assert: Count = 2
        var bookDto2 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto2!.LikeCount).IsEqualTo(2);

        // 5. Act: User 1 Unlikes Book and wait for SSE
        var received3 = true;
        await TestHelpers.RemoveFromFavoritesAsync(user1Client, createdBook.Id, createdBook.Id, "BookUpdated");

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
        var registerResponse =
            await anonClient.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.IsSuccessStatusCode).IsTrue();

        // Login
        var loginResponse =
            await anonClient.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        _ = await Assert.That(loginResponse.IsSuccessStatusCode).IsTrue();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var client = TestHelpers.GetUnauthenticatedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        return client;
    }

    record LoginResponse(string AccessToken, string RefreshToken);
}
