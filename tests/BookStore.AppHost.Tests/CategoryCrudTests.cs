using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class CategoryCrudTests
{
    [Test]
    public async Task CreateCategory_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createCategoryRequest = TestDataGenerators.GenerateFakeCategoryRequest();

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/categories", createCategoryRequest);

        // Assert
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
    }
}
