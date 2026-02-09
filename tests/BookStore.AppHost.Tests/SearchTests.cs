using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class SearchTests
{
    [Test]
    public async Task SearchBooks_WithValidQuery_ShouldReturnMatches()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var uniqueTitle = $"UniqueSearchTerm-{Guid.NewGuid()}";

        // Create a book with a unique title using proper request model
        var createRequest = new CreateBookRequest
        {
            Title = uniqueTitle,
            Isbn = "978-3-16-148410-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Test description") },
            PublicationDate = new PartialDate(2024, 1, 1),
            PublisherId = null,
            AuthorIds = [],
            CategoryIds = [],
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
        };
        var createdBook = await TestHelpers.CreateBookAsync(adminClient, createRequest);

        // Act
        var publicClient = TestHelpers.GetUnauthenticatedClient<IBooksClient>();
        PagedListDto<BookDto>? searchResult = null;

        // Retry loop in case of slight indexing delay
        for (var i = 0; i < TestConstants.ShortRetryCount; i++)
        {
            var response = await publicClient.GetBooksAsync(new BookSearchRequest { Search = uniqueTitle });
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
        var publicClient = TestHelpers.GetUnauthenticatedClient<IBooksClient>();
        var globalHooks = GlobalHooks.NotificationService; // ensure app is ready
        _ = await globalHooks!.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var impossibleTerm = $"ImpossibleTerm-{Guid.NewGuid()}";

        // Act
        var response = await publicClient.GetBooksAsync(new BookSearchRequest { Search = impossibleTerm });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.Items).IsEmpty();
    }
}
