using BookStore.Client;
using TUnit.Assertions.Extensions;
using System.Net;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class CategoryConcurrencyTests
{
    [Test]
    public async Task UpdateCategory_TwiceWithSameETag_ShouldFailOnSecondUpdate()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var category = await TestHelpers.CreateCategoryAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetCategoryWithResponseAsync(category.Id);
        var etag = response.Headers.ETag?.Tag;
        await Assert.That(etag).IsNotNull();

        var updateRequest1 = TestHelpers.GenerateFakeUpdateCategoryRequest();
        var updateRequest2 = TestHelpers.GenerateFakeUpdateCategoryRequest();

        // Act - First update succeeds
        await client.UpdateCategoryAsync(category.Id, updateRequest1, etag);

        // Act - Second update with SAME OLD ETag should fail
        var failResponse = await client.UpdateCategoryWithResponseAsync(category.Id, updateRequest2, etag);

        // Assert
        await Assert.That((int)failResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task UpdateThenDeleteCategory_WithSameETag_ShouldFailOnDelete()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var category = await TestHelpers.CreateCategoryAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetCategoryWithResponseAsync(category.Id);
        var etag = response.Headers.ETag?.Tag;
        await Assert.That(etag).IsNotNull();

        var updateRequest = TestHelpers.GenerateFakeUpdateCategoryRequest();

        // Act - Update succeeds
        await client.UpdateCategoryAsync(category.Id, updateRequest, etag);

        // Act - Delete with SAME OLD ETag should fail
        var deleteResponse = await client.SoftDeleteCategoryWithResponseAsync(category.Id, etag);

        // Assert
        await Assert.That((int)deleteResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task DeleteThenUpdateCategory_WithSameETag_ShouldFailOnUpdate()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var category = await TestHelpers.CreateCategoryAsync(client, createRequest);

        // Get initial state and ETag
        var response = await client.GetCategoryWithResponseAsync(category.Id);
        var etag = response.Headers.ETag?.Tag;
        await Assert.That(etag).IsNotNull();

        var updateRequest = TestHelpers.GenerateFakeUpdateCategoryRequest();

        // Act - Delete succeeds
        await client.SoftDeleteCategoryAsync(category.Id, etag);

        // Act - Update with SAME OLD ETag should fail
        var updateResponse = await client.UpdateCategoryWithResponseAsync(category.Id, updateRequest, etag);

        // Assert
        await Assert.That((int)updateResponse.StatusCode).IsEqualTo((int)HttpStatusCode.PreconditionFailed);
    }
}
