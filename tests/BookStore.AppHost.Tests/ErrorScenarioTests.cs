using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class ErrorScenarioTests
{
    [Test]
    public async Task CreateBook_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());
        var createBookRequest = TestHelpers.GenerateFakeBookRequest();

        // Act & Assert
        var exception = await Assert.That(async () => await client.CreateBookAsync(createBookRequest))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateBook_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var client = RestService.For<IBooksClient>(await TestHelpers.GetAuthenticatedClientAsync());

        var createBookRequest = new CreateBookRequest
        {
            Title = "", // Invalid: empty title
            Isbn = "invalid-isbn", // Invalid: bad ISBN format (handled by validation?)
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    ["en"] = new BookTranslationDto("Test description")
                },
            PublicationDate = new PartialDate(2026, 1, 1),
            PublisherId = null,
            AuthorIds = [],
            CategoryIds = []
        };

        // Act & Assert
        var exception = await Assert.That(async () => await client.CreateBookAsync(createBookRequest))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetBook_NotFound_ShouldReturn404()
    {
        // Arrange
        var client = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetBookAsync(nonExistentId)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
