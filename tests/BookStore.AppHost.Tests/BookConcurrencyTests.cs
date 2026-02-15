using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using TUnit.Assertions.Extensions;

namespace BookStore.AppHost.Tests;

public class BookConcurrencyTests
{
    [Test]
    public async Task UpdateBook_TwiceWithSameETag_ShouldFailOnSecondUpdate()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createRequest = FakeDataGenerators.GenerateFakeBookRequest();
        var book = await BookHelpers.CreateBookAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetBookWithResponseAsync(book.Id);
        var etag = response.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var updateRequest1 = FakeDataGenerators.GenerateFakeUpdateBookRequest(book.Publisher?.Id,
            book.Authors.Select(a => a.Id), book.Categories.Select(c => c.Id));
        var updateRequest2 = FakeDataGenerators.GenerateFakeUpdateBookRequest(book.Publisher?.Id,
            book.Authors.Select(a => a.Id), book.Categories.Select(c => c.Id));

        // Act - First update succeeds
        await client.UpdateBookAsync(book.Id, updateRequest1, etag);

        // Act - Second update with SAME OLD ETag should fail
        var failResponse = await client.UpdateBookWithResponseAsync(book.Id, updateRequest2, etag);

        // Assert
        _ = await Assert.That((int)failResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task UpdateThenDeleteBook_WithSameETag_ShouldFailOnDelete()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createRequest = FakeDataGenerators.GenerateFakeBookRequest();
        var book = await BookHelpers.CreateBookAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetBookWithResponseAsync(book.Id);
        var etag = response.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var updateRequest = FakeDataGenerators.GenerateFakeUpdateBookRequest(book.Publisher?.Id,
            book.Authors.Select(a => a.Id), book.Categories.Select(c => c.Id));

        // Act - Update succeeds
        await client.UpdateBookAsync(book.Id, updateRequest, etag);

        // Act - Delete with SAME OLD ETag should fail
        var deleteResponse = await client.SoftDeleteBookWithResponseAsync(book.Id, etag);

        // Assert
        _ = await Assert.That((int)deleteResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task DeleteThenUpdateBook_WithSameETag_ShouldFailOnUpdate()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var createRequest = FakeDataGenerators.GenerateFakeBookRequest();
        var book = await BookHelpers.CreateBookAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetBookWithResponseAsync(book.Id);
        var etag = response.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var updateRequest = FakeDataGenerators.GenerateFakeUpdateBookRequest(book.Publisher?.Id,
            book.Authors.Select(a => a.Id), book.Categories.Select(c => c.Id));

        // Act - Delete succeeds
        await client.SoftDeleteBookAsync(book.Id, etag);

        // Act - Update with SAME OLD ETag should fail
        var updateResponse = await client.UpdateBookWithResponseAsync(book.Id, updateRequest, etag);

        // Assert
        _ = await Assert.That((int)updateResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }
}
