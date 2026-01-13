using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BookStore.Shared.Models;
using Projects;

namespace BookStore.AppHost.Tests;

public class LocalizationTests
{
    [Test]
    [Arguments("pt-PT", "Descrição em Português")]
    [Arguments("es", "Descripción en Español")]
    [Arguments("es-MX", "Descripción en Español")]
    [Arguments("fr-FR", "Default Description")]
    [Arguments("en", "Default Description")]
    public async Task GetBook_WithLocalizedHeader_ShouldReturnExpectedContent(string acceptLanguage, string expectedDescription)
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = GlobalHooks.App!.CreateHttpClient("apiservice");

        var createRequest = new
        {
            Title = "Localized Book",
            Isbn = "1234567890",
            Language = "en",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Description = "Default Description" },
                ["pt-PT"] = new { Description = "Descrição em Português" },
                ["es"] = new { Description = "Descripción en Español" }
            },
            PublicationDate = new { Year = 2023 },
            PublisherId = (Guid?)null,
            AuthorIds = new List<Guid>(),
            CategoryIds = new List<Guid>(),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
        };

        // Create the book
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createRequest);
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookDto>();

        // Wait for projection
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Simple retry policy for the GET check
        var retries = 5;
        BookDto? bookDto = null;

        publicClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        publicClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(acceptLanguage));

        while (retries-- > 0)
        {
            var response = await publicClient.GetAsync($"/api/books/{createdBook!.Id}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                bookDto = await response.Content.ReadFromJsonAsync<BookDto>();
                break;
            }

            await Task.Delay(500);
        }

        // Assert
        _ = await Assert.That(bookDto).IsNotNull();
        _ = await Assert.That(bookDto!.Description).IsEqualTo(expectedDescription);
    }
}
