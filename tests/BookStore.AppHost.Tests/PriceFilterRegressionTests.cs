using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using BookStore.AppHost.Tests;
using BookStore.Client;
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
    [Arguments(100.0, 0, 40.0, 60.0, false)] // Case 1: Original price outside range (too high)
    [Arguments(100.0, 50, 40.0, 60.0, true)] // Case 2: Discounted price inside range (50 is in [40, 60])
    [Arguments(50.0, 0, 40.0, 60.0, true)] // Case 3: Original price inside range
    [Arguments(50.0, 80, 40.0, 60.0, false)] // Case 4: Discounted price outside range (10 is too low)
    public async Task SearchBooks_WithVariousPriceAndDiscountScenarios_ShouldFilterCorrectly(
        decimal originalPrice,
        decimal discountPercentage,
        decimal minPrice,
        decimal maxPrice,
        bool shouldMatch)
    {
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // Public client via Refit
        var publicHttpClient = TestHelpers.GetUnauthenticatedClient();
        var publicClient = Refit.RestService.For<IBooksClient>(publicHttpClient);

        var uniqueTitle =
            $"PriceScenario-{originalPrice.ToString(CultureInfo.InvariantCulture)}-{discountPercentage.ToString(CultureInfo.InvariantCulture)}-{Guid.NewGuid()}";

        var createRequest = new CreateBookRequest
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new() { Description = "Price scenario test" } },
            PublicationDate = new SharedModels.PartialDate(2025),
            AuthorIds = [],
            CategoryIds = []
        };
        createRequest.Prices = new Dictionary<string, decimal> { ["USD"] = originalPrice };

        var book = await TestHelpers.CreateBookAsync(authClient, createRequest);
        var bookId = book.Id;

        if (discountPercentage > 0)
        {
            var saleRequest = new ScheduleSaleRequest(discountPercentage, DateTimeOffset.UtcNow.AddSeconds(-5),
                DateTimeOffset.UtcNow.AddDays(1));

            _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
                async () => await authClient.ScheduleBookSaleAsync(bookId, saleRequest), TimeSpan.FromSeconds(10));
        }

        // Wait for consistency and check
        var matched = false;
        for (var i = 0; i < TestConstants.LongRetryCount; i++)
        {
            try
            {
                var request = new SharedModels.BookSearchRequest
                {
                    Search = "PriceScenario",
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Currency = "USD"
                };

                var list = await publicClient.GetBooksAsync(request);
                matched = list.Items.Any(b => b.Id == bookId);

                if (matched == shouldMatch)
                {
                    break;
                }
            }
            catch
            {
                // Ignore errors during poll - we will retry or eventually fail the assert
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(matched).IsEqualTo(shouldMatch);
    }

    /// <summary>
    /// Verifies that books with prices in multiple currencies are only matched when at least
    /// one currency's price falls within the specified range. If all currencies are outside
    /// the range, the book should not match even if the average would fall within the range.
    /// </summary>
    [Test]
    public async Task SearchBooks_WithMixedCurrency_ShouldRequireSingleCurrencyToMatchRange()
    {
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicHttpClient = TestHelpers.GetUnauthenticatedClient();
        var publicClient = Refit.RestService.For<IBooksClient>(publicHttpClient);

        var uniqueTitle = $"Mixed-NoMatch-{Guid.NewGuid()}";
        // USD=10, EUR=200. Range [50, 150]. No single currency fits.
        var createRequest = new CreateBookRequest
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new() { Description = "Mixed non-match test" } },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m, ["EUR"] = 200.0m },
            AuthorIds = [],
            CategoryIds = []
        };

        var book = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Poll for search projection to update
        var ready = false;
        for (var i = 0; i < TestConstants.DefaultMaxRetries; i++)
        {
            try
            {
                var request = new SharedModels.BookSearchRequest { Search = uniqueTitle };
                var c = await publicClient.GetBooksAsync(request);
                if (c != null && c.Items.Any(b => b.Title == uniqueTitle))
                {
                    ready = true;
                    break;
                }
            }
            catch
            {
                // Expected if projection hasn't finished yet
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(ready).IsTrue();

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
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // Need raw client to fetch ETag
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync(); // Raw HttpClient

        var publicHttpClient = TestHelpers.GetUnauthenticatedClient();
        var publicClient = Refit.RestService.For<IBooksClient>(publicHttpClient);

        var uniqueTitle = $"UpdateResetsDiscount-{Guid.NewGuid()}";
        // Create book with Price=100 USD
        var createRequest = new CreateBookRequest
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    ["en"] = new() { Description = "Update resets discount test" }
                },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 100.0m },
            AuthorIds = [],
            CategoryIds = []
        };

        var book = await TestHelpers.CreateBookAsync(authClient, createRequest);
        var bookId = book.Id;

        // 1. Apply 50% discount -> Price becomes 50.
        var saleRequest =
            new ScheduleSaleRequest(50m, DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddDays(1));

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
            async () => await authClient.ScheduleBookSaleAsync(bookId, saleRequest), TimeSpan.FromSeconds(10));

        // 2. Verify it's found in range [40, 60]
        var foundBeforeUpdate = false;
        for (var i = 0; i < TestConstants.DefaultMaxRetries; i++)
        {
            try
            {
                var request = new SharedModels.BookSearchRequest
                {
                    Search = uniqueTitle,
                    MinPrice = 40m,
                    MaxPrice = 60m,
                    Currency = "USD"
                };

                var list = await publicClient.GetBooksAsync(request);
                if (list.Items.Any(b => b.Id == bookId))
                {
                    foundBeforeUpdate = true;
                    break;
                }
            }
            catch
            {
                // Wait for projection
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundBeforeUpdate).IsTrue();

        // 3. Update book - MUST include Prices to avoid Validation Failure
        var response = await authClient.GetBookWithHeadersAsync(bookId);
        var fetchedBook = response.Content;
        var etagValue = response.Headers.ETag?.Tag ?? string.Empty;

        var updateRequest = new UpdateBookRequest
        {
            Title = uniqueTitle + " Updated",
            Isbn = fetchedBook!.Isbn,
            Language = fetchedBook.Language,
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new() { Description = "Updated description" } },
            PublicationDate = new SharedModels.PartialDate(2024),
            AuthorIds = [],
            CategoryIds = []
        };
        // Explicitly set Prices from fetched book or default
        updateRequest.Prices = fetchedBook.Prices?.ToDictionary(k => k.Key, v => v.Value)
                               ?? new Dictionary<string, decimal> { ["USD"] = 100.0m };

        // Debug output if failure
        try
        {
            _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
                async () => await authClient.UpdateBookAsync(bookId, updateRequest, etagValue),
                TimeSpan.FromSeconds(5));
        }
        catch (Refit.ApiException ex)
        {
            // Log error content for debug
            Console.WriteLine($"UpdateBookAsync failed in test: {ex.Content}");
            throw;
        }

        var foundAfterUpdate = false;
        for (var i = 0; i < TestConstants.LongRetryCount; i++)
        {
            try
            {
                var request = new SharedModels.BookSearchRequest
                {
                    Search = uniqueTitle,
                    MinPrice = 40,
                    MaxPrice = 60,
                    Currency = "USD"
                };
                var list = await publicClient.GetBooksAsync(request);
                if (list.Items.Any(b => b.Id == bookId))
                {
                    foundAfterUpdate = true;
                    break;
                }
            }
            catch
            {
                // Wait for projection to reflect update
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundAfterUpdate).IsTrue();
    }
}
