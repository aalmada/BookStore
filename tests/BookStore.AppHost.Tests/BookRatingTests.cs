using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bogus;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class BookRatingTests
{
    [Test]
    public async Task RateBook_ShouldUpdateUserRatingAndStatistics()
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

        // Act - Rate the book and wait for BookUpdated (statistics update triggers this)
        var rating = 4;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookUpdated",
            async () =>
            {
                var response = await httpClient.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = rating });
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();

        // Assert - Verify statistics and user rating
        var bookDto = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto!.AverageRating).IsEqualTo(4.0f);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(1);
        _ = await Assert.That(bookDto.UserRating).IsEqualTo(rating);
    }

    [Test]
    public async Task UpdateRating_ShouldChangeExistingRating()
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

        // Rate the book initially and wait for update
        var initialRating = 3;
        var receivedInitial = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookUpdated",
            async () => _ = await httpClient.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = initialRating }),
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedInitial).IsTrue();

        // Verify initial rating
        var initialGet = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(initialGet!.AverageRating).IsEqualTo(3.0f);
        _ = await Assert.That(initialGet.RatingCount).IsEqualTo(1);

        // Act - Update the rating and wait for BookUpdated
        var updatedRating = 5;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await httpClient.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = updatedRating });
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();

        // Assert - Verify updated statistics
        var bookDto = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto!.AverageRating).IsEqualTo(5.0f);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(1); // Still 1 user, not 2
        _ = await Assert.That(bookDto.UserRating).IsEqualTo(updatedRating);
    }

    [Test]
    public async Task RemoveRating_ShouldClearUserRatingAndUpdateStatistics()
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

        // Rate the book first and wait for update
        var receivedRating = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookUpdated",
            async () => _ = await httpClient.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = 4 }),
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedRating).IsTrue();

        // Verify rating is set
        var initialGet = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(initialGet!.UserRating).IsEqualTo(4);
        _ = await Assert.That(initialGet.RatingCount).IsEqualTo(1);

        // Act - Remove the rating and wait for BookUpdated
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await httpClient.DeleteAsync($"/api/books/{createdBook!.Id}/rating");
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();

        // Assert - Verify rating is removed
        var bookDto = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto!.AverageRating).IsEqualTo(0.0f);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(0);
        _ = await Assert.That(bookDto.UserRating).IsEqualTo(0);
    }

    [Test]
    public async Task BookRatingStatistics_ShouldAggregateCorrectly_WhenMultipleUsersRateBook()
    {
        var _faker = new Faker();
        var _anonClient = TestHelpers.GetUnauthenticatedClient();

        // 1. Arrange: Create a book as Admin and wait
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();
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

        // 2. Arrange: Create User 1, User 2, and User 3
        var user1Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);
        var user2Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);
        var user3Client = await CreateAuthenticatedUserAsync(_anonClient, _faker);

        // 3. Act: User 1 Rates Book with 3 stars and wait for statistics update via SSE
        var received1 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook!.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user1Client.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = 3 });
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received1).IsTrue();

        // Assert: Average = 3.0, Count = 1
        var bookDto1 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto1!.AverageRating).IsEqualTo(3.0f);
        _ = await Assert.That(bookDto1.RatingCount).IsEqualTo(1);

        // 4. Act: User 2 Rates Book with 4 stars and wait for SSE
        var received2 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user2Client.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = 4 });
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received2).IsTrue();

        // Assert: Average = 3.5, Count = 2
        var bookDto2 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto2!.AverageRating).IsEqualTo(3.5f);
        _ = await Assert.That(bookDto2.RatingCount).IsEqualTo(2);

        // 5. Act: User 3 Rates Book with 5 stars and wait for SSE
        var received3 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user3Client.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = 5 });
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received3).IsTrue();

        // Assert: Average = 4.0, Count = 3
        var bookDto3 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto3!.AverageRating).IsEqualTo(4.0f);
        _ = await Assert.That(bookDto3.RatingCount).IsEqualTo(3);

        // 6. Act: User 1 Updates their rating to 5 stars and wait for SSE
        var received4 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user1Client.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = 5 });
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received4).IsTrue();

        // Assert: Average = 4.67 (rounded from 14/3), Count = 3
        var bookDto4 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(Math.Abs(bookDto4!.AverageRating - 4.67f) < 0.01f).IsTrue();
        _ = await Assert.That(bookDto4.RatingCount).IsEqualTo(3);

        // 7. Act: User 2 Removes their rating and wait for SSE
        var received5 = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdBook.Id,
            "BookUpdated",
            async () =>
            {
                var response = await user2Client.DeleteAsync($"/api/books/{createdBook!.Id}/rating");
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received5).IsTrue();

        // Assert: Average = 5.0, Count = 2
        var bookDto5 = await _anonClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook.Id}");
        _ = await Assert.That(bookDto5!.AverageRating).IsEqualTo(5.0f);
        _ = await Assert.That(bookDto5.RatingCount).IsEqualTo(2);
    }

    [Test]
    public async Task RateBook_WithInvalidRating_ShouldReturnBadRequest()
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

        // Act & Assert - Try invalid ratings
        var invalidRatings = new[] { 0, 6, -1, 10 };
        foreach (var invalidRating in invalidRatings)
        {
            var response = await httpClient.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = invalidRating });
            _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

            var errorMessage = await response.Content.ReadAsStringAsync();
            _ = await Assert.That(errorMessage).Contains("Rating must be between 1 and 5");
        }

        // Verify no statistics were created
        var bookDto = await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{createdBook!.Id}");
        _ = await Assert.That(bookDto!.RatingCount).IsEqualTo(0);
        _ = await Assert.That(bookDto.AverageRating).IsEqualTo(0.0f);
    }

    [Test]
    public async Task RateBook_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Create book and wait for projection
        BookResponse? createdBook = null;
        var receivedIsCreated = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await adminClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(receivedIsCreated).IsTrue();

        // Act - Try to rate without authentication
        var unauthenticatedClient = TestHelpers.GetUnauthenticatedClient();
        var response = await unauthenticatedClient.PostAsJsonAsync($"/api/books/{createdBook!.Id}/rating", new { Rating = 4 });

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
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

    record BookResponse(Guid Id, string Title, string Isbn);
    record LoginResponse(string AccessToken, string RefreshToken);
}
