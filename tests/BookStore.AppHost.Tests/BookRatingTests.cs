using System.Net.Http.Headers;
using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class BookRatingTests
{
    [Test]
    public async Task RateBook_ShouldUpdateUserRatingAndStatistics()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act - Rate the book and wait for UserUpdated (since we assert UserRating)
        var rating = 4;
        await TestHelpers.RateBookAsync(client, createdBook.Id, rating, createdBook.Id, "BookUpdated");

        // Assert - Verify statistics and user rating
        var bookDto = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto.AverageRating).IsEqualTo(4.0f);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(1);
        _ = await Assert.That(bookDto.UserRating).IsEqualTo(rating);
    }

    [Test]
    public async Task UpdateRating_ShouldChangeExistingRating()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Rate the book initially and wait for update
        var initialRating = 3;
        await TestHelpers.RateBookAsync(client, createdBook.Id, initialRating, createdBook.Id, "BookUpdated");

        // Verify initial rating
        var initialGet = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(initialGet.AverageRating).IsEqualTo(3.0f);
        _ = await Assert.That(initialGet.RatingCount).IsEqualTo(1);

        // Act - Update the rating and wait for UserUpdated
        var updatedRating = 5;
        await TestHelpers.RateBookAsync(client, createdBook.Id, updatedRating, createdBook.Id, "BookUpdated");

        // Assert - Verify updated statistics
        var bookDto = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto.AverageRating).IsEqualTo(5.0f);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(1); // Still 1 user, not 2
        _ = await Assert.That(bookDto.UserRating).IsEqualTo(updatedRating);
    }

    [Test]
    public async Task RemoveRating_ShouldClearUserRatingAndUpdateStatistics()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Rate the book first and wait for update
        await TestHelpers.RateBookAsync(client, createdBook.Id, 4, createdBook.Id, "BookUpdated");

        // Verify rating is set
        var initialGet = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(initialGet.UserRating).IsEqualTo(4);
        _ = await Assert.That(initialGet.RatingCount).IsEqualTo(1);

        // Act - Remove the rating and wait for UserUpdated
        await TestHelpers.RemoveRatingAsync(client, createdBook.Id, createdBook.Id, "BookUpdated");

        // Assert - Verify rating is removed
        var bookDto = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto.AverageRating).IsEqualTo(0.0f);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(0);
        _ = await Assert.That(bookDto.UserRating).IsEqualTo(0);
    }

    [Test]
    public async Task BookRatingStatistics_ShouldAggregateCorrectly_WhenMultipleUsersRateBook()
    {
        var _faker = new Faker();

        // 1. Arrange: Create a book as Admin and wait
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // 2. Arrange: Create User 1, User 2, and User 3
        var user1Client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();
        var user2Client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();
        var user3Client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();

        // 3. Act: User 1 Rates Book with 3 stars and wait for statistics update via SSE
        await TestHelpers.RateBookAsync(user1Client, createdBook.Id, 3, createdBook.Id, "BookUpdated");

        // Assert: Average = 3.0, Count = 1
        var bookDto1 = await adminClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto1.AverageRating).IsEqualTo(3.0f);
        _ = await Assert.That(bookDto1.RatingCount).IsEqualTo(1);

        // 4. Act: User 2 Rates Book with 4 stars and wait for SSE
        await TestHelpers.RateBookAsync(user2Client, createdBook.Id, 4, createdBook.Id, "BookUpdated");

        // Assert: Average = 3.5, Count = 2
        var bookDto2 = await adminClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto2.AverageRating).IsEqualTo(3.5f);
        _ = await Assert.That(bookDto2.RatingCount).IsEqualTo(2);

        // 5. Act: User 3 Rates Book with 5 stars and wait for SSE
        await TestHelpers.RateBookAsync(user3Client, createdBook.Id, 5, createdBook.Id, "BookUpdated");

        // Assert: Average = 4.0, Count = 3
        var bookDto3 = await adminClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto3.AverageRating).IsEqualTo(4.0f);
        _ = await Assert.That(bookDto3.RatingCount).IsEqualTo(3);

        // 6. Act: User 1 Updates their rating to 5 stars and wait for SSE
        await TestHelpers.RateBookAsync(user1Client, createdBook.Id, 5, createdBook.Id, "BookUpdated");

        // Assert: Average = 4.67 (rounded from 14/3), Count = 3
        var bookDto4 = await adminClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(Math.Abs(bookDto4.AverageRating - 4.67f) < 0.01f).IsTrue();
        _ = await Assert.That(bookDto4.RatingCount).IsEqualTo(3);

        // 7. Act: User 2 Removes their rating and wait for SSE
        await TestHelpers.RemoveRatingAsync(user2Client, createdBook.Id, createdBook.Id, "BookUpdated");

        // Assert: Average = 5.0, Count = 2
        var bookDto5 = await adminClient.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto5.AverageRating).IsEqualTo(5.0f);
        _ = await Assert.That(bookDto5.RatingCount).IsEqualTo(2);
    }

    [Test]
    public async Task RateBook_WithInvalidRating_ShouldReturnBadRequest()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var client = await TestHelpers.CreateUserAndGetClientAsync<IBooksClient>();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act & Assert - Try invalid ratings
        var invalidRatings = new[] { 0, 6, -1, 10 };
        foreach (var invalidRating in invalidRatings)
        {
            try
            {
                await client.RateBookAsync(createdBook.Id, new RateBookRequest(invalidRating));
                Assert.Fail("Should have thrown ApiException");
            }
            catch (ApiException ex)
            {
                _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
                var error = await ex.GetContentAsAsync<TestHelpers.ValidationProblemDetails>();
                _ = await Assert.That(error?.Detail).Contains("Rating must be between 1 and 5");
            }
        }

        // Verify no statistics were created
        var bookDto = await client.GetBookAsync(createdBook.Id);
        _ = await Assert.That(bookDto.RatingCount).IsEqualTo(0);
        _ = await Assert.That(bookDto.AverageRating).IsEqualTo(0.0f);
    }

    [Test]
    public async Task RateBook_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // Create book and wait for projection
        var createdBook = await TestHelpers.CreateBookAsync(adminClient);

        // Act - Try to rate without authentication
        var unauthenticatedClient = TestHelpers.GetUnauthenticatedClient();
        var client = RestService.For<IBooksClient>(unauthenticatedClient);

        try
        {
            await client.RateBookAsync(createdBook.Id, new RateBookRequest(4));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }
}
