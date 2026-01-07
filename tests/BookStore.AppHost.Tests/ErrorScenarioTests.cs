using System.Net;
using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class ErrorScenarioTests
{
    [Test]
    public async Task CreateBook_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        var httpClient = TestHelpers.GetUnauthenticatedClient();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);

        // Assert
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateBook_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        
        var createBookRequest = new
        {
            Title = "", // Invalid: empty title
            Isbn = "invalid-isbn", // Invalid: bad ISBN format
            Language = "en",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Description = "Test description" }
            },
            PublicationDate = new { Year = 2026, Month = 1, Day = 1 },
            PublisherId = (Guid?)null,
            AuthorIds = new Guid[] {},
            CategoryIds = new Guid[] {}
        };

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);

        // Assert
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetBook_NotFound_ShouldReturn404()
    {
        // Arrange
        var httpClient = TestHelpers.GetUnauthenticatedClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var getResponse = await httpClient.GetAsync($"/api/books/{nonExistentId}");

        // Assert
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
