using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class AuthorCrudTests
{
    [Test]
    public async Task CreateAuthor_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createAuthorRequest = TestDataGenerators.GenerateFakeAuthorRequest();

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", createAuthorRequest);

        // Assert
        await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
    }
}
