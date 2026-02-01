using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class MultiLanguageTranslationTests
{
    [Test]
    public async Task Author_Update_ShouldPreserveAllBiographies()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var authorName = "Translation Author " + Guid.NewGuid().ToString()[..8];

        var createRequest = new
        {
            Name = authorName,
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Biography = "English Bio" },
                ["pt"] = new { Biography = "Biografia em Português" }
            }
        };

        // 1. Create Author & Wait for Projection
        // Async projections often report as 'Updated' even on first insert due to Upsert logic
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "AuthorUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", createRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                    var error = await createResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Create failed: {createResponse.StatusCode} - {error}");
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        // 2. Verify all translations are returned in Admin API
        var authorInList = await RetryUntilFoundAsync(async () =>
        {
            var getResponse = await httpClient.GetAsync("/api/admin/authors");
            if (!getResponse.IsSuccessStatusCode) return null;
            var pagedAuthors = await getResponse.Content.ReadFromJsonAsync<PagedListDto<AdminAuthorDto>>();
            return pagedAuthors!.Items.FirstOrDefault(a => a.Name == authorName);
        });

        _ = await Assert.That(authorInList.Translations).IsNotNull();
        _ = await Assert.That(authorInList.Translations!.Count).IsEqualTo(2);

        // 3. Update Author
        var updateRequest = new { Name = authorName + " Updated", authorInList.Translations };

        var putReceived = await TestHelpers.ExecuteAndWaitForEventAsync(
            authorInList.Id,
            "AuthorUpdated",
            async () =>
            {
                var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/authors/{authorInList.Id}")
                {
                    Content = JsonContent.Create(updateRequest)
                };
                // Omitting If-Match workaround (Admin list doesn't serve individual ETags)

                var updateResponse = await httpClient.SendAsync(putRequest);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Update failed: {updateResponse.StatusCode} - {error}");
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(putReceived).IsTrue();

        // 4. Verify translations are still there
        var finalGetResponse = await httpClient.GetAsync("/api/admin/authors");
        var finalPagedAuthors = await finalGetResponse.Content.ReadFromJsonAsync<PagedListDto<AdminAuthorDto>>();
        var finalAuthor = finalPagedAuthors!.Items.First(a => a.Id == authorInList.Id);

        _ = await Assert.That(finalAuthor.Name).IsEqualTo(authorName + " Updated");
        _ = await Assert.That(finalAuthor.Translations).IsNotNull();
        _ = await Assert.That(finalAuthor.Translations!.Count).IsEqualTo(2);
        _ = await Assert.That(finalAuthor.Translations["en"].Biography).IsEqualTo("English Bio");
        _ = await Assert.That(finalAuthor.Translations["pt"].Biography).IsEqualTo("Biografia em Português");
    }

    [Test]
    public async Task Category_Update_ShouldPreserveAllNames()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var englishName = "English Cat " + Guid.NewGuid().ToString()[..8];

        var createRequest = new
        {
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Name = englishName }, ["pt"] = new { Name = "Categoria em Português" }
            }
        };

        // 1. Create Category & Wait for Projection
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "CategoryUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/categories", createRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                    var error = await createResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Create failed: {createResponse.StatusCode} - {error}");
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        // 2. Verify all translations are returned in Admin API
        var categoryInList = await RetryUntilFoundAsync(async () =>
        {
            var getResponse = await httpClient.GetAsync("/api/admin/categories");
            if (!getResponse.IsSuccessStatusCode) return null;
            var pagedCategories = await getResponse.Content.ReadFromJsonAsync<PagedListDto<AdminCategoryDto>>();
            return pagedCategories!.Items.FirstOrDefault(c =>
                c.Translations != null && c.Translations.ContainsKey("en") && c.Translations["en"].Name == englishName);
        });

        _ = await Assert.That(categoryInList.Translations).IsNotNull();
        _ = await Assert.That(categoryInList.Translations!.Count).IsEqualTo(2);

        // 3. Update Category
        var Translations = categoryInList.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == "en" ? new { Name = englishName + " Updated" } : (object)new { kvp.Value.Name });
        var updateRequest = new { Translations };

        var putReceived = await TestHelpers.ExecuteAndWaitForEventAsync(
            categoryInList.Id,
            "CategoryUpdated",
            async () =>
            {
                var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/categories/{categoryInList.Id}")
                {
                    Content = JsonContent.Create(updateRequest)
                };
                // Omitting If-Match workaround

                var updateResponse = await httpClient.SendAsync(putRequest);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Update failed: {updateResponse.StatusCode} - {error}");
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(putReceived).IsTrue();

        // 4. Verify all translations are still there
        var finalGetResponse = await httpClient.GetAsync("/api/admin/categories");
        var finalPagedCategories = await finalGetResponse.Content.ReadFromJsonAsync<PagedListDto<AdminCategoryDto>>();
        var finalCategory = finalPagedCategories!.Items.First(c => c.Id == categoryInList.Id);

        _ = await Assert.That(finalCategory.Translations).IsNotNull();
        _ = await Assert.That(finalCategory.Translations!.Count).IsEqualTo(2);
        _ = await Assert.That(finalCategory.Translations["en"].Name).IsEqualTo(englishName + " Updated");
        _ = await Assert.That(finalCategory.Translations["pt"].Name).IsEqualTo("Categoria em Português");
    }

    [Test]
    public async Task Book_Update_ShouldPreserveAllDescriptions()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var bookTitle = "Translation Book " + Guid.NewGuid().ToString()[..8];

        var createRequest = new
        {
            Title = bookTitle,
            Isbn = "1234567890",
            Language = "en",
            Translations =
                new Dictionary<string, object>
                {
                    ["en"] = new { Description = "English Description" },
                    ["pt"] = new { Description = "Descrição em Português" }
                },
            PublicationDate = new { Year = 2024 },
            AuthorIds = new List<Guid>(),
            CategoryIds = new List<Guid>(),
            Prices = new Dictionary<string, decimal> { ["USD"] = 19.99m }
        };

        // 1. Create Book & Wait for Projection
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                    var error = await createResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Create failed: {createResponse.StatusCode} - {error}");
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        // 2. Verify all translations are returned in Admin API
        var bookInList = await RetryUntilFoundAsync(async () =>
        {
            var getResponse = await httpClient.GetAsync("/api/admin/books");
            if (!getResponse.IsSuccessStatusCode) return null;
            var books = await getResponse.Content.ReadFromJsonAsync<List<AdminBookDto>>();
            return books!.FirstOrDefault(b => b.Title == bookTitle);
        });

        _ = await Assert.That(bookInList.Translations).IsNotNull();
        _ = await Assert.That(bookInList.Translations!.Count).IsEqualTo(2);

        // 3. Update Book
        var updateRequest = new
        {
            Title = bookTitle + " Updated",
            Isbn = "1234567890",
            Language = "en",
            bookInList.Translations,
            PublicationDate = new { Year = 2024 },
            AuthorIds = new List<Guid>(),
            CategoryIds = new List<Guid>(),
            Prices = new Dictionary<string, decimal> { ["USD"] = 19.99m }
        };

        var putReceived = await TestHelpers.ExecuteAndWaitForEventAsync(
            bookInList.Id,
            "BookUpdated",
            async () =>
            {
                var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/books/{bookInList.Id}")
                {
                    Content = JsonContent.Create(updateRequest)
                };
                // Omitting If-Match workaround

                var updateResponse = await httpClient.SendAsync(putRequest);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Update failed: {updateResponse.StatusCode} - {error}");
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(putReceived).IsTrue();

        // 4. Verify all translations are still there
        var finalGetResponse = await httpClient.GetAsync("/api/admin/books");
        var finalBooks = await finalGetResponse.Content.ReadFromJsonAsync<List<AdminBookDto>>();
        var finalBook = finalBooks!.First(b => b.Id == bookInList.Id);

        _ = await Assert.That(finalBook.Title).IsEqualTo(bookTitle + " Updated");
        _ = await Assert.That(finalBook.Translations).IsNotNull();
        _ = await Assert.That(finalBook.Translations!.Count).IsEqualTo(2);
        _ = await Assert.That(finalBook.Translations["en"].Description).IsEqualTo("English Description");
        _ = await Assert.That(finalBook.Translations["pt"].Description).IsEqualTo("Descrição em Português");
    }

    private async Task<T> RetryUntilFoundAsync<T>(Func<Task<T?>> activeSearch)
    {
        var cts = new CancellationTokenSource(TestConstants.DefaultEventTimeout);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var result = await activeSearch();
                    if (result != null) return result;
                }
                catch
                {
                    // Ignore and retry
                }

                await Task.Delay(500);
            }
        }
        catch (OperationCanceledException)
        {
        }

        throw new Exception("Timed out waiting for entity to appear in projection.");
    }
}
