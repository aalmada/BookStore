using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BookStore.Shared.Models;
using Projects;

namespace BookStore.AppHost.Tests;

public class SearchTests
{
    [Test]
    public async Task SearchBooks_WithValidQuery_ShouldReturnMatches()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var uniqueTitle = $"UniqueSearchTerm-{Guid.NewGuid()}";

        // Create a book with a unique title
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-3-16-148410-0",
            Language = "en",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Description = "Test description" }
            },
            PublicationDate = new { Year = 2024, Month = 1, Day = 1 },
            PublisherId = (Guid?)null,
            AuthorIds = new Guid[] { },
            CategoryIds = new Guid[] { }
        };
        var createdBook = await TestHelpers.CreateBookAsync(httpClient, createRequest);

        // Act - Search for the unique term
        // We might need to wait for indexing (projection)
        // Since CreateBookAsync waits for the projection, it should be available immediately

        var publicClient = TestHelpers.GetUnauthenticatedClient();
        PagedListDto<BookDto>? searchResult = null;

        // Retry loop in case of slight indexing delay (though CreateBookAsync waits for projection)
        for (var i = 0; i < 5; i++)
        {
            var response = await publicClient.GetFromJsonAsync<PagedListDto<BookDto>>($"/api/books?search={uniqueTitle}");
            if (response != null && response.Items.Count > 0)
            {
                searchResult = response;
                break;
            }

            await Task.Delay(500);
        }

        // Assert
        _ = await Assert.That(searchResult).IsNotNull();
        _ = await Assert.That(searchResult!.Items).IsNotEmpty();
        _ = await Assert.That(searchResult.Items.Any(b => b.Title == uniqueTitle)).IsTrue();
    }

    [Test]
    public async Task SearchBooks_WithNoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var publicClient = TestHelpers.GetUnauthenticatedClient();
        var globalHooks = GlobalHooks.NotificationService; // ensure app is ready
        _ = await globalHooks!.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        var impossibleTerm = $"ImpossibleTerm-{Guid.NewGuid()}";

        // Act
        var response = await publicClient.GetFromJsonAsync<PagedListDto<BookDto>>($"/api/books?search={impossibleTerm}");

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.Items).IsEmpty();
    }
}
