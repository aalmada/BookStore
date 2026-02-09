using System.Net;
using System.Net.Http.Headers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

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
        var adminClient = RestService.For<IBooksClient>(await TestHelpers.GetAuthenticatedClientAsync());

        // Create the book
        // Use dictionary for Translations as per contract
        var translations = new Dictionary<string, BookTranslationDto>
        {
            ["en"] = new BookTranslationDto("Default Description"),
            ["pt-PT"] = new BookTranslationDto("Descrição em Português"),
            ["es"] = new BookTranslationDto("Descripción en Español")
        };

        var request = TestHelpers.GenerateFakeBookRequest();
        request.Translations = translations;
        request.Title = "Localized Book";

        var createdBook = await TestHelpers.CreateBookAsync(adminClient, request);

        // Simple retry policy for the GET check
        var retries = 5;
        BookDto? bookDto = null;

        var publicHttpClient = TestHelpers.GetUnauthenticatedClient();
        publicHttpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        publicHttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(acceptLanguage));
        var publicClient = RestService.For<IBooksClient>(publicHttpClient);

        while (retries-- > 0)
        {
            try
            {
                bookDto = await publicClient.GetBookAsync(createdBook.Id);
                break;
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore 404 while projecting
            }

            await Task.Delay(500);
        }

        // Assert
        _ = await Assert.That(bookDto).IsNotNull();
        _ = await Assert.That(bookDto!.Description).IsEqualTo(expectedDescription);
    }
}
