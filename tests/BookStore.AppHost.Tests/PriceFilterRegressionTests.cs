using System.Globalization;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class PriceFilterRegressionTests
{
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
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var uniqueTitle =
            $"PriceScenario-{originalPrice.ToString(CultureInfo.InvariantCulture)}-{discountPercentage.ToString(CultureInfo.InvariantCulture)}-{Guid.NewGuid()}";
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Price scenario test" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = originalPrice }
        };

        var bookId = Guid.Empty;
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "BookCreated", async () =>
        {
            var res = await authClient.PostAsJsonAsync("/api/admin/books", createRequest);
            var content = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            bookId = content.GetProperty("id").GetGuid();
        }, TimeSpan.FromSeconds(5));

        if (discountPercentage > 0)
        {
            var saleRequest = new
            {
                Percentage = discountPercentage,
                Start = DateTimeOffset.UtcNow.AddSeconds(-5),
                End = DateTimeOffset.UtcNow.AddDays(1)
            };

            _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookDiscountUpdated", async () =>
            {
                var res = await authClient.PostAsJsonAsync($"/api/books/{bookId}/sales", saleRequest);
                _ = await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            }, TimeSpan.FromSeconds(10));
        }

        // Wait for consistency and check
        var matched = false;
        for (var i = 0; i < 20; i++)
        {
            var res = await publicClient.GetAsync(
                $"/api/books?search={uniqueTitle}&minPrice={minPrice.ToString(CultureInfo.InvariantCulture)}&maxPrice={maxPrice.ToString(CultureInfo.InvariantCulture)}&currency=USD");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
                if (list != null && list.Items.Any(b => b.Id == bookId))
                {
                    matched = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(matched).IsEqualTo(shouldMatch);
    }

    [Test]
    public async Task SearchBooks_WithMixedCurrency_ShouldRequireSingleCurrencyToMatchRange()
    {
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var uniqueTitle = $"Mixed-NoMatch-{Guid.NewGuid()}";
        // USD=10, EUR=200. Range [50, 150]. No single currency fits.
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Mixed non-match test" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m, ["EUR"] = 200.0m }
        };

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "BookCreated",
            async () => { _ = await authClient.PostAsJsonAsync("/api/admin/books", createRequest); },
            TimeSpan.FromSeconds(5));

        // Poll for search projection to update
        var ready = false;
        for (var i = 0; i < 10; i++)
        {
            var r = await publicClient.GetAsync($"/api/books?search={uniqueTitle}");
            if (r.IsSuccessStatusCode)
            {
                var c = await r.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
                if (c != null && c.Items.Any(b => b.Title == uniqueTitle))
                {
                    ready = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(ready).IsTrue();

        var res = await publicClient.GetAsync($"/api/books?search={uniqueTitle}&minPrice=50&maxPrice=150");
        var list = await res.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();

        _ = await Assert.That(list!.Items.Any(b => b.Title == uniqueTitle)).IsFalse();
    }

    [Test]
    public async Task SearchBooks_WithDiscount_AfterBookUpdate_ShouldStillFilterByDiscountedPrice()
    {
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var uniqueTitle = $"UpdateResetsDiscount-{Guid.NewGuid()}";
        // Create book with Price=100 USD
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, object> { ["en"] = new { Description = "Update resets discount test" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 100.0m }
        };

        var bookId = Guid.Empty;
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "BookCreated", async () =>
        {
            var res = await authClient.PostAsJsonAsync("/api/admin/books", createRequest);
            var content = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            bookId = content.GetProperty("id").GetGuid();
        }, TimeSpan.FromSeconds(5));

        // 1. Apply 50% discount -> Price becomes 50.
        var saleRequest = new
        {
            Percentage = 50m, Start = DateTimeOffset.UtcNow.AddSeconds(-5), End = DateTimeOffset.UtcNow.AddDays(1)
        };

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookDiscountUpdated", async () =>
        {
            var res = await authClient.PostAsJsonAsync($"/api/books/{bookId}/sales", saleRequest);
            _ = await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }, TimeSpan.FromSeconds(10));

        // 2. Verify it's found in range [40, 60]
        var foundBeforeUpdate = false;
        for (var i = 0; i < 10; i++)
        {
            var res = await publicClient.GetAsync(
                $"/api/books?search={uniqueTitle}&minPrice=40&maxPrice=60&currency=USD");
            var list = await res.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
            if (list != null && list.Items.Any(b => b.Id == bookId))
            {
                foundBeforeUpdate = true;
                break;
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundBeforeUpdate).IsTrue();

        // 3. Update book
        var fetchedRes = await publicClient.GetAsync($"/api/books/{bookId}");
        var fetchedBook = await fetchedRes.Content.ReadFromJsonAsync<BookDto>();
        var etagValue = fetchedRes.Headers.ETag?.Tag ?? "";

        var updateRequest = new
        {
            Title = uniqueTitle + " Updated",
            fetchedBook!.Isbn,
            fetchedBook.Language,
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Updated description" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 200.0m } // Double the price
        };

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated", async () =>
        {
            var req = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/books/{bookId}")
            {
                Content = JsonContent.Create(updateRequest)
            };
            req.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etagValue));
            var res = await authClient.SendAsync(req);
            _ = await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }, TimeSpan.FromSeconds(5));

        // 4. Verify original price is 200, discounted (preserved 50%) is 100.
        // Range [90, 110] should find it.
        var foundAfterUpdate = false;
        for (var i = 0; i < 20; i++)
        {
            var res = await publicClient.GetAsync(
                $"/api/books?search={uniqueTitle}&minPrice=90&maxPrice=110&currency=USD");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
                if (list != null && list.Items.Any(b => b.Id == bookId))
                {
                    foundAfterUpdate = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundAfterUpdate).IsTrue();
    }
}
