using System.Net;
using System.Net.Http.Headers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class LocalizationTests
{
    [Test]
    [Arguments("pt-PT", "Descrição em Português")]
    [Arguments("es", "Descripción en Español")]
    [Arguments("es-MX", "Descripción en Español")]
    [Arguments("fr-FR", "Default Description")]
    [Arguments("en", "Default Description")]
    public async Task GetBook_WithLocalizedHeader_ShouldReturnExpectedContent(string acceptLanguage,
        string expectedDescription)
    {
        // Arrange
        var adminClient = RestService.For<IBooksClient>(await HttpClientHelpers.GetAuthenticatedClientAsync());

        // Create the book
        // Use dictionary for Translations as per contract
        var translations = new Dictionary<string, BookTranslationDto>
        {
            ["en"] = new BookTranslationDto("Default Description"),
            ["pt-PT"] = new BookTranslationDto("Descrição em Português"),
            ["es"] = new BookTranslationDto("Descripción en Español")
        };

        var request = FakeDataGenerators.GenerateFakeBookRequest();
        request.Translations = translations;
        request.Title = "Localized Book";

        var createdBook = await BookHelpers.CreateBookAsync(adminClient, request);

        var publicClient = HttpClientHelpers.GetUnauthenticatedClientWithLanguage<IBooksClient>(acceptLanguage);
        var bookDto = await publicClient.GetBookAsync(createdBook.Id);

        // Assert
        _ = await Assert.That(bookDto).IsNotNull();
        _ = await Assert.That(bookDto!.Description).IsEqualTo(expectedDescription);
    }
}
