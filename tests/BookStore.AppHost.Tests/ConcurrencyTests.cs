using System.Net;
using BookStore.Client;
using TUnit.Assertions.Extensions;

namespace BookStore.AppHost.Tests;

public class ConcurrencyTests
{
    [Test]
    public async Task UpdateAuthor_TwiceWithSameETag_ShouldFailOnSecondUpdate()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetAuthorWithResponseAsync(author.Id);
        var etag = response.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var updateRequest1 = TestHelpers.GenerateFakeUpdateAuthorRequest();
        var updateRequest2 = TestHelpers.GenerateFakeUpdateAuthorRequest();

        // Act - First update succeeds
        await client.UpdateAuthorAsync(author.Id, updateRequest1, etag);

        // Act - Second update with SAME OLD ETag should fail
        var failResponse = await client.UpdateAuthorWithResponseAsync(author.Id, updateRequest2, etag);

        // Assert
        _ = await Assert.That((int)failResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task UpdateThenDeleteAuthor_WithSameETag_ShouldFailOnDelete()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetAuthorWithResponseAsync(author.Id);
        var etag = response.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var updateRequest = TestHelpers.GenerateFakeUpdateAuthorRequest();

        // Act - Update succeeds
        await client.UpdateAuthorAsync(author.Id, updateRequest, etag);

        // Act - Delete with SAME OLD ETag should fail
        var deleteResponse = await client.SoftDeleteAuthorWithResponseAsync(author.Id, etag);

        // Assert
        _ = await Assert.That((int)deleteResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task DeleteThenUpdateAuthor_WithSameETag_ShouldFailOnUpdate()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetAuthorWithResponseAsync(author.Id);
        var etag = response.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        var updateRequest = TestHelpers.GenerateFakeUpdateAuthorRequest();

        // Act - Delete succeeds
        await client.SoftDeleteAuthorAsync(author.Id, etag);

        // Act - Update with SAME OLD ETag should fail
        var updateResponse = await client.UpdateAuthorWithResponseAsync(author.Id, updateRequest, etag);

        // Assert
        _ = await Assert.That((int)updateResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task UpdateAuthor_MissingETag_ShouldFail()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        var updateRequest = TestHelpers.GenerateFakeUpdateAuthorRequest();

        // Act - Update without ETag should fail (once we make it mandatory)
        var updateResponse = await client.UpdateAuthorWithResponseAsync(author.Id, updateRequest, null);

        // Assert
        // Currently it might succeed because it's optional. TDD: we want it to fail.
        _ = await Assert.That((int)updateResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionRequired);
    }
}
