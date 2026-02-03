using BookStore.Client;
using Refit;
using SharedModels = BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class MultiLanguageTranslationTests
{
    [Test]
    public async Task Author_Update_ShouldPreserveAllBiographies()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var authorName = "Translation Author " + Guid.NewGuid().ToString()[..8];

        var createRequest = new CreateAuthorRequest
        {
            Name = authorName,
            Translations = new Dictionary<string, AuthorTranslationDto>
            {
                ["en"] = new() { Biography = "English Bio" },
                ["pt"] = new() { Biography = "Biografia em Português" }
            }
        };

        // 1. Create Author
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            ["AuthorCreated", "AuthorUpdated"],
            async () => await client.CreateAuthorAsync(createRequest),
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        // 2. Verify all translations are returned in Admin API
        var authorInList = await RetryUntilFoundAsync(async () =>
        {
            var pagedAuthors =
                await client.GetAllAuthorsAsync(new SharedModels.AuthorSearchRequest { Search = authorName });
            return pagedAuthors.Items.FirstOrDefault(a => a.Name == authorName);
        });

        _ = await Assert.That(authorInList.Translations.Count).IsEqualTo(2);

        // 3. Update Author
        var translations = authorInList.Translations.ToDictionary(
            k => k.Key,
            v => new AuthorTranslationDto { Biography = v.Value.Biography }
        );

        var updateRequest = new UpdateAuthorRequest { Name = authorName + " Updated", Translations = translations };

        var putReceived = await TestHelpers.ExecuteAndWaitForEventAsync(
            authorInList.Id,
            "AuthorUpdated",
            async () => await client.UpdateAuthorAsync(authorInList.Id, updateRequest),
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(putReceived).IsTrue();

        // 4. Verify translations are still there
        var finalPagedAuthors =
            await client.GetAllAuthorsAsync(new SharedModels.AuthorSearchRequest { Search = authorName });
        var finalAuthor = finalPagedAuthors.Items.First(a => a.Id == authorInList.Id);

        _ = await Assert.That(finalAuthor.Name).IsEqualTo(authorName + " Updated");
        _ = await Assert.That(finalAuthor.Translations.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Category_Update_ShouldPreserveAllNames()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var englishName = "English Cat " + Guid.NewGuid().ToString()[..8];

        var createRequest = new CreateCategoryRequest
        {
            Translations = new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new() { Name = englishName },
                ["pt"] = new() { Name = "Categoria em Português" }
            }
        };

        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "CategoryUpdated",
            async () => await client.CreateCategoryAsync(createRequest),
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        var categoryInList = await RetryUntilFoundAsync(async () =>
        {
            var pagedCategories =
                await client.GetAllCategoriesAsync(
                    new SharedModels.CategorySearchRequest { Search = englishName });
            return pagedCategories.Items.FirstOrDefault(c =>
                c.Translations.ContainsKey("en") && c.Translations["en"].Name == englishName);
        });

        _ = await Assert.That(categoryInList.Translations.Count).IsEqualTo(2);

        // Update
        var translations = categoryInList.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == "en"
                ? new CategoryTranslationDto { Name = englishName + " Updated" }
                : new CategoryTranslationDto { Name = kvp.Value.Name });

        var updateRequest = new UpdateCategoryRequest { Translations = translations };

        var putReceived = await TestHelpers.ExecuteAndWaitForEventAsync(
            categoryInList.Id,
            "CategoryUpdated",
            async () => await client.UpdateCategoryAsync(categoryInList.Id, updateRequest),
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(putReceived).IsTrue();

        var finalPagedCategories =
            await client.GetAllCategoriesAsync(new SharedModels.CategorySearchRequest { Search = englishName });
        var finalCategory = finalPagedCategories.Items.First(c => c.Id == categoryInList.Id);

        _ = await Assert.That(finalCategory.Translations.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Book_Update_ShouldPreserveAllDescriptions()
    {
        // 1. Create Book with Translations
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();

        var uniqueTitle = "TransBook " + Guid.NewGuid().ToString()[..8];

        var createRequest = new CreateBookRequest
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    ["en"] = new() { Description = "English Desc" },
                    ["es"] = new() { Description = "Descripción Original" }
                },
            PublicationDate = new SharedModels.PartialDate(2024),
            AuthorIds = [],
            CategoryIds = [],
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m }
        };

        var book = await TestHelpers.CreateBookAsync(client, createRequest);

        // 2. Fetch using Refit client to get ETag
        var response = await client.GetBookWithHeadersAsync(book.Id);
        _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
        _ = await Assert.That(response.Content).IsNotNull();
        var etag = response.Headers.ETag?.Tag;
        var fetchedBook = response.Content;

        // 3. Update Book
        var translations = new Dictionary<string, BookTranslationDto>
        {
            ["en"] = new() { Description = "English Updated" },
            ["es"] = new() { Description = "Descripción Original" }
        };

        var updateRequest = new UpdateBookRequest
        {
            Title = fetchedBook!.Title,
            Isbn = fetchedBook.Isbn,
            Language = fetchedBook.Language,
            Translations = translations,
            PublicationDate = new SharedModels.PartialDate(2025),
            AuthorIds = [],
            CategoryIds = []
        };
        // Explicitly set Prices
        updateRequest.Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m };

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(book.Id, "BookUpdated",
            async () => await client.UpdateBookAsync(book.Id, updateRequest, etag),
            TestConstants.DefaultEventTimeout);

        // 4. Verify using Accept-Language
        // English
        var clientEn = TestHelpers.GetUnauthenticatedClient();
        clientEn.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        var publicClientEn = RestService.For<IBooksClient>(clientEn);
        var bookEn = await publicClientEn.GetBookAsync(book.Id);
        _ = await Assert.That(bookEn.Description).IsEqualTo("English Updated");

        // Spanish
        var clientEs = TestHelpers.GetUnauthenticatedClient();
        clientEs.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es");
        var publicClientEs = RestService.For<IBooksClient>(clientEs);
        var bookEs = await publicClientEs.GetBookAsync(book.Id);
        _ = await Assert.That(bookEs.Description).IsEqualTo("Descripción Original");
    }

    async Task<T> RetryUntilFoundAsync<T>(Func<Task<T?>> func, int maxRetries = 10)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            var result = await func();
            if (result != null)
            {
                return result;
            }

            await Task.Delay(500);
        }

        throw new Exception("Item not found after retries");
    }
}
