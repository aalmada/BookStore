using System.Net;
using BookStore.AppHost.Tests.Helpers;
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
        var client = RestService.For<IBooksClient>(HttpClientHelpers.GetUnauthenticatedClient());
        var createBookRequest = FakeDataGenerators.GenerateFakeBookRequest();

        // Act & Assert
        var exception = await Assert.That(async () => await client.CreateBookAsync(createBookRequest))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateBook_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var client = RestService.For<IBooksClient>(await HttpClientHelpers.GetAuthenticatedClientAsync());

        var createBookRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = "", // Invalid: empty title
            Isbn = "invalid-isbn", // Invalid: bad ISBN format (handled by validation?)
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Test description") },
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
        var client = RestService.For<IBooksClient>(HttpClientHelpers.GetUnauthenticatedClient());
        var nonExistentId = Guid.CreateVersion7();

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetBookAsync(nonExistentId)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
