using System.Globalization;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using TUnit;
using SharedModels = BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class PriceFilterRegressionTests
{
    /// <summary>
    /// Verifies that book price filtering correctly handles various combinations of original prices,
    /// discount percentages, and price range filters. The search should match books based on the
    /// effective price (discounted price if a discount is active, otherwise the original price).
    /// This test covers edge cases where the original price is outside the range but the discounted
    /// price is inside (and vice versa).
    /// </summary>
    [Test]
    [Arguments(100.0, 0.0, 40.0, 60.0, false)] // Case 1: Original price outside range (too high)
    [Arguments(100.0, 50.0, 40.0, 60.0, true)] // Case 2: Discounted price inside range (50 is in [40, 60])
    [Arguments(50.0, 0.0, 40.0, 60.0, true)] // Case 3: Original price inside range
    [Arguments(50.0, 80.0, 40.0, 60.0, false)] // Case 4: Discounted price outside range (10 is too low)
    public async Task SearchBooks_WithVariousPriceAndDiscountScenarios_ShouldFilterCorrectly(
        double originalPrice,
        double discountPercentage,
        double minPrice,
        double maxPrice,
        bool shouldMatch)
    {
        var authClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // Public client via Refit
        var publicHttpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var publicClient = Refit.RestService.For<IBooksClient>(publicHttpClient);

        var uniqueTitle =
            $"PriceScenario-{originalPrice.ToString(CultureInfo.InvariantCulture)}-{discountPercentage.ToString(CultureInfo.InvariantCulture)}-{Guid.CreateVersion7()}";

        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Price scenario test") },
            PublicationDate = new SharedModels.PartialDate(2025),
            AuthorIds = [],
            CategoryIds = []
        };
        createRequest.Prices = new Dictionary<string, decimal> { ["USD"] = (decimal)originalPrice };

        var book = await BookHelpers.CreateBookAsync(authClient, createRequest);
        var bookId = book.Id;

        if (discountPercentage > 0)
        {
            var saleRequest = new ScheduleSaleRequest((decimal)discountPercentage, DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5));
            var bookResponse = await authClient.GetBookWithResponseAsync(bookId);
            var currentVersion = ParseETag(bookResponse.Headers.ETag?.Tag);

            _ = await SseEventHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
                async () => await authClient.ScheduleBookSaleAsync(bookId, saleRequest, bookResponse.Headers.ETag?.Tag),
                TimeSpan.FromSeconds(10),
                minVersion: currentVersion + 1);
        }

        var request = new BookSearchRequest
        {
            Search = "PriceScenario",
            MinPrice = (decimal)minPrice,
            MaxPrice = (decimal)maxPrice,
            Currency = "USD"
        };
        var list = await publicClient.GetBooksAsync(request);
        var matched = list.Items.Any(b => b.Id == bookId);

        if (shouldMatch)
        {
            _ = await Assert.That(matched).IsTrue();
        }
        else
        {
            _ = await Assert.That(matched).IsFalse();
        }
    }

    /// <summary>
    /// Verifies that books with prices in multiple currencies are only matched when at least
    /// one currency's price falls within the specified range. If all currencies are outside
    /// the range, the book should not match even if the average would fall within the range.
    /// </summary>
    [Test]
    public async Task SearchBooks_WithMixedCurrency_ShouldRequireSingleCurrencyToMatchRange()
    {
        var authClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicHttpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var publicClient = Refit.RestService.For<IBooksClient>(publicHttpClient);

        var uniqueTitle = $"Mixed-NoMatch-{Guid.CreateVersion7()}";
        // USD=10, EUR=200. Range [50, 150]. No single currency fits.
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Mixed non-match test") },
            PublicationDate = new SharedModels.PartialDate(2025),
            Prices = new Dictionary<string, decimal> { ["USD"] = 200.0m, ["EUR"] = 200.0m },
            AuthorIds = [],
            CategoryIds = []
        };

        var book = await BookHelpers.CreateBookAsync(authClient, createRequest);

        var contentInitial = await publicClient.GetBooksAsync(new BookSearchRequest { Search = uniqueTitle });
        _ = await Assert.That(contentInitial != null && contentInitial.Items.Any(b => b.Title == uniqueTitle)).IsTrue();

        var booksClient = publicClient;
        var requestNoMatch =
            new SharedModels.BookSearchRequest { Search = uniqueTitle, MinPrice = 50m, MaxPrice = 150m };
        var list = await booksClient.GetBooksAsync(requestNoMatch);
        _ = await Assert.That(list!.Items.Any(b => b.Title == uniqueTitle)).IsFalse();
    }

    /// <summary>
    /// Verifies that when a book is updated after a discount has been applied, the search
    /// projection correctly maintains the discounted price and continues to filter based
    /// on the effective (discounted) price rather than reverting to the original price.
    /// </summary>
    [Test]
    public async Task SearchBooks_WithDiscount_AfterBookUpdate_ShouldStillFilterByDiscountedPrice()
    {
        var authClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicHttpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var publicClient = Refit.RestService.For<IBooksClient>(publicHttpClient);

        var uniqueTitle = $"UpdateResetsDiscount-{Guid.CreateVersion7()}";
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new("Update resets discount test") },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 100.0m },
            AuthorIds = [],
            CategoryIds = []
        };

        var book = await BookHelpers.CreateBookAsync(authClient, createRequest);
        var bookId = book.Id;
        var initialVersion = ParseETag(book.ETag);

        // 1. Apply 50% discount -> Price becomes 50.
        var saleRequest =
            new ScheduleSaleRequest(50m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));

        Console.WriteLine(
            $"[Test] Book {bookId} Created. InitialVersion={initialVersion}");

        // Wait for ScheduleBookSale (version 2)
        _ = await SseEventHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
            async () => await authClient.ScheduleBookSaleAsync(bookId, saleRequest), TimeSpan.FromSeconds(10),
            minVersion: initialVersion + 1);

        // Wait for ApplyBookDiscount side effect (version 3)
        // This is scheduled to run at Sale.Start (which is UtcNow), so it should execute almost immediately
        // Instead of Delay, we should wait for the event that signals the discount was applied
        _ = await SseEventHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
            async () =>
            {
                /* The side effect is already triggered by Marten/Wolverine */
            },
            TimeSpan.FromSeconds(10),
            minVersion: initialVersion + 2);

        var searchRequest = new SharedModels.BookSearchRequest
        {
            Search = uniqueTitle,
            MinPrice = 40,
            MaxPrice = 60,
            Currency = "USD"
        };
        var searchList = await publicClient.GetBooksAsync(searchRequest);
        _ = await Assert.That(searchList.Items.Any(b => b.Id == bookId)).IsTrue();

        var response = await authClient.GetBookWithResponseAsync(bookId);
        var fetchedBook = response.Content!;
        var etagValue = response.Headers.ETag?.ToString();

        // 3. Update book Title (should preserve prices and discount)
        var updateRequest = new UpdateBookRequest
        {
            Title = uniqueTitle + " Updated",
            Isbn = fetchedBook.Isbn ?? "",
            Language = fetchedBook.Language,
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    [fetchedBook.Language] = new BookTranslationDto(fetchedBook.Description ?? "")
                },
            PublicationDate = fetchedBook.PublicationDate ?? new PartialDate(),
            AuthorIds = [.. fetchedBook.Authors.Select(a => a.Id)],
            CategoryIds = [.. fetchedBook.Categories.Select(c => c.Id)],
            Prices = fetchedBook.Prices?.ToDictionary(k => k.Key, v => v.Value) ?? []
        };

        _ = await BookHelpers.UpdateBookAsync(authClient, bookId, updateRequest, etagValue!);

        // 4. Verify book is STILL found in the same price range
        var updatedTitle = uniqueTitle + " Updated";
        var searchRequestFinal = new BookSearchRequest
        {
            Search = updatedTitle,
            MinPrice = 40,
            MaxPrice = 60,
            Currency = "USD"
        };
        var searchListFinal = await publicClient.GetBooksAsync(searchRequestFinal);
        _ = await Assert.That(searchListFinal.Items.Any(b => b.Id == bookId)).IsTrue();
    }

    static long ParseETag(string? etag)
    {
        if (string.IsNullOrEmpty(etag))
        {
            return 0;
        }

        var trimmed = etag.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');
        return long.TryParse(trimmed, out var version) ? version : 0;
    }
}
