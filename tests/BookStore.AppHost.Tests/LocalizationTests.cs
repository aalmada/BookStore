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

        BookDto? bookDto = null;

        var publicHttpClient = TestHelpers.GetUnauthenticatedClient();
        publicHttpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        publicHttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(acceptLanguage));
        var publicClient = RestService.For<IBooksClient>(publicHttpClient);

        await TestHelpers.WaitForConditionAsync(async () =>
        {
            try
            {
                bookDto = await publicClient.GetBookAsync(createdBook.Id);
                return bookDto != null;
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }, TestConstants.DefaultEventTimeout, "Book was not available in public API after creation");

        // Assert
        _ = await Assert.That(bookDto).IsNotNull();
        _ = await Assert.That(bookDto!.Description).IsEqualTo(expectedDescription);
    }
}
