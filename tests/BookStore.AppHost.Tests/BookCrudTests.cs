using System.Net;
using System.Net.Http.Headers;
using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using BookStore.AppHost.Tests.Helpers;

// Resolve ambiguities by preferring Client types
using CreateBookRequest = BookStore.Client.CreateBookRequest;
using UpdateBookRequest = BookStore.Client.UpdateBookRequest;

namespace BookStore.AppHost.Tests;

public class BookCrudTests
{
    [Test]
    public async Task UploadBookImage_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Get ETag for concurrency check
        var getResponse = await client.GetBookWithResponseAsync(createdBook.Id);
        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Create dummy image content
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]); // JPEG header mostly
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        // Act
        // Refit StreamPart uses stream.
        using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);
        var streamPart = new StreamPart(stream, "cover.jpg", "image/jpeg");

        await client.UploadBookCoverAsync(createdBook.Id, streamPart, etag);

        // Assert
        // Refit throws if not success, so if we reached here it's OK.
        // But we can assert on verified response if we wanted, or catching assertions.
        // Verify via Get? The test checked StatusCode OK/NoContent. Refit void task implies success.
        // We can double check strict status if we change return type to Task<IApiResponse>, but Task is fine for "ShouldReturnOk".
    }

    // I will modify ONLY the parts that DON'T need ETag for now?
    // No, most tests use ETag.
    // I absolutely need to solve the ETag retrieval with Refit.
    // Standard Refit pattern: use ApiResponse<T>.

    // Let's assume I CAN update IGetBookEndpoint.

    [Test]
    public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();

        // Act
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Assert
        _ = await Assert.That(createdBook).IsNotNull();
    }

    [Test]
    public async Task UpdateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Get the book to retrieve its ETag
        var getResponse = await client.GetBookWithResponseAsync(createdBook.Id);
        _ = await Assert.That(getResponse.IsSuccessStatusCode).IsTrue();

        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Act
        var updateBookRequest = FakeDataGenerators.GenerateFakeBookRequest();
        createdBook = await BookHelpers.UpdateBookAsync(client, createdBook.Id, updateBookRequest, etag!);

        // Assert - Success is validated inside UpdateBookAsync
    }

    [Test]
    public async Task DeleteBook_EndToEndFlow_ShouldReturnNoContent()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Get the book to retrieve its ETag
        var getResponse = await client.GetBookWithResponseAsync(createdBook.Id);
        _ = await Assert.That(getResponse.IsSuccessStatusCode).IsTrue();

        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // Act
        var deletedBook = await BookHelpers.DeleteBookAsync(client, createdBook.Id, etag!);

        // Assert - Success is validated inside DeleteBookAsync
    }

    [Test]
    public async Task RestoreBook_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Get ETag for delete
        var getResponse = await client.GetBookWithResponseAsync(createdBook.Id);
        var deleteEtag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(deleteEtag).IsNotNull();

        // Soft delete book
        _ = await BookHelpers.DeleteBookAsync(client, createdBook.Id, deleteEtag!);

        // Act
        createdBook = await BookHelpers.RestoreBookAsync(client, createdBook.Id);

        // Assert - Success is validated inside RestoreBookAsync
    }

    [Test]
    public async Task AddToFavorites_ShouldReturnNoContent()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Act
        await BookHelpers.AddToFavoritesAsync(client, createdBook.Id);

        // Assert
        var getResponse = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(getResponse!.IsFavorite).IsTrue();
    }

    [Test]
    public async Task RemoveFromFavorites_ShouldReturnNoContent()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(client);

        // Add to favorites first
        await BookHelpers.AddToFavoritesAsync(client, createdBook.Id);

        // Verify it IS favorite initially
        var initialGet = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(initialGet!.IsFavorite).IsTrue();

        // Act
        await BookHelpers.RemoveFromFavoritesAsync(client, createdBook.Id);

        // Assert
        var getResponse = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(getResponse!.IsFavorite).IsFalse();
    }

    [Test]
    public async Task GetBook_WhenNotAuthenticated_ShouldHaveIsFavoriteFalse()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);

        // Act
        var publicClient = HttpClientHelpers.GetUnauthenticatedClient<IBooksClient>();
        var getResponse = await publicClient.GetBookAsync(createdBook.Id);

        // Assert
        _ = await Assert.That(getResponse!.IsFavorite).IsFalse();
    }

    [Test]
    public async Task BookLikeCount_ShouldAggregateCorrectly_WhenMultipleUsersLikeBook()
    {
        var anonClient = HttpClientHelpers.GetUnauthenticatedClient<IBooksClient>();

        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);

        var user1Client = await CreateAuthenticatedUserAsync();
        var user2Client = await CreateAuthenticatedUserAsync();

        // Act & Assert: User 1 likes book
        await BookHelpers.AddToFavoritesAsync(user1Client, createdBook.Id, createdBook.Id, "BookStatisticsUpdate");
        var bookDto1 = await anonClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto1!.LikeCount).IsEqualTo(1);

        // Act & Assert: User 2 likes book
        await BookHelpers.AddToFavoritesAsync(user2Client, createdBook.Id, createdBook.Id, "BookStatisticsUpdate");
        var bookDto2 = await anonClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto2!.LikeCount).IsEqualTo(2);

        // Act & Assert: User 1 unlikes book
        await BookHelpers.RemoveFromFavoritesAsync(user1Client, createdBook.Id, createdBook.Id, "BookStatisticsUpdate");
        var bookDto3 = await anonClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto3!.LikeCount).IsEqualTo(1);
    }

    async Task<IBooksClient> CreateAuthenticatedUserAsync()
        // Wrapper for AuthenticationHelpers.CreateUserAndGetClientAsync
        => await AuthenticationHelpers.CreateUserAndGetClientAsync<IBooksClient>();
}
