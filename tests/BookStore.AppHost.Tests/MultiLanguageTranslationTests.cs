using BookStore.Client;
using BookStore.Shared.Models;
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
            Id = Guid.CreateVersion7(),
            Name = authorName,
            Translations = new Dictionary<string, AuthorTranslationDto>
            {
                ["en"] = new("English Bio"), ["pt"] = new("Biografia em Português")
            }
        };

        // 1. Create Author
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);
        _ = await Assert.That(author).IsNotNull();

        // 2. Verify all translations are returned in Admin API
        var authorInList = await RetryUntilFoundAsync(async () =>
        {
            var pagedAuthors =
                await client.GetAllAuthorsAsync(new SharedModels.AuthorSearchRequest { Search = authorName });
            return pagedAuthors.Items.FirstOrDefault(a => a.Id == author.Id);
        });

        _ = await Assert.That(authorInList.Translations.Count).IsEqualTo(2);

        // 3. Update Author
        var translations = authorInList.Translations.ToDictionary(
            k => k.Key,
            v => new AuthorTranslationDto(v.Value.Biography)
        );

        var updateRequest = new UpdateAuthorRequest { Name = authorName + " Updated", Translations = translations };

        authorInList = await TestHelpers.UpdateAuthorAsync(client, authorInList, updateRequest);
        _ = await Assert.That(authorInList).IsNotNull();
        _ = await Assert.That(authorInList.Name).IsEqualTo(authorName + " Updated");

        // 4. Verify translations are still there
        _ = await Assert.That(authorInList.Translations.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Category_Update_ShouldPreserveAllNames()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var englishName = "English Cat " + Guid.NewGuid().ToString()[..8];

        var createRequest = new CreateCategoryRequest
        {
            Id = Guid.CreateVersion7(),
            Translations = new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new(englishName), ["pt"] = new("Categoria em Português")
            }
        };

        var category = await TestHelpers.CreateCategoryAsync(client, createRequest);
        _ = await Assert.That(category).IsNotNull();

        var categoryInList = await RetryUntilFoundAsync(async () =>
        {
            var pagedCategories =
                await client.GetAllCategoriesAsync(
                    new SharedModels.CategorySearchRequest { Search = englishName });
            return pagedCategories.Items.FirstOrDefault(c => c.Id == category.Id);
        });

        _ = await Assert.That(categoryInList.Translations.Count).IsEqualTo(2);

        // Update
        var translations = categoryInList.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == "en"
                ? new CategoryTranslationDto(englishName + " Updated")
                : new CategoryTranslationDto(kvp.Value.Name));

        var updateRequest = new UpdateCategoryRequest { Translations = translations };

        categoryInList = await TestHelpers.UpdateCategoryAsync(client, categoryInList, updateRequest);
        _ = await Assert.That(categoryInList).IsNotNull();

        // Verify
        _ = await Assert.That(categoryInList.Translations.Count).IsEqualTo(2);
        _ = await Assert.That(categoryInList.Translations["en"].Name).IsEqualTo(englishName + " Updated");
    }

    [Test]
    public async Task Book_Update_ShouldPreserveAllDescriptions()
    {
        // 1. Create Book with Translations
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();

        var title = "TransBook " + Guid.NewGuid().ToString()[..8];

        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Isbn = "978-1-23-456789-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto>
                {
                    ["en"] = new("English Desc"), ["es"] = new("Descripción Original")
                },
            PublicationDate = new SharedModels.PartialDate(2024),
            AuthorIds = [],
            CategoryIds = [],
            Prices = new Dictionary<string, decimal> { ["USD"] = 10m }
        };

        // Create
        var book = await TestHelpers.CreateBookAsync(client, createRequest);
        _ = await Assert.That(book).IsNotNull();

        // Fetch using Refit client to get ETag
        var response = await client.GetBookWithResponseAsync(book.Id);
        var etag = response.Headers.ETag?.Tag;
        var fetchedBook = response.Content;

        // Update
        var updateRequest = new UpdateBookRequest
        {
            Title = fetchedBook!.Title + " Updated",
            Isbn = fetchedBook.Isbn,
            Language = fetchedBook.Language,
            PublicationDate = fetchedBook.PublicationDate!.Value,
            AuthorIds = [],
            CategoryIds = [],
            Prices = createRequest.Prices,
            Translations = new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new("English Updated"), ["es"] = new("Descripción Original")
            }
        };

        var updatedBook = await TestHelpers.UpdateBookAsync(client, book.Id, updateRequest, book.ETag);
        _ = await Assert.That(updatedBook).IsNotNull();

        // 4. Verify using Accept-Language
        // English
        var publicClientEn = TestHelpers.GetUnauthenticatedClientWithLanguage<IBooksClient>("en");
        var bookEn = await publicClientEn.GetBookAsync(book.Id);
        _ = await Assert.That(bookEn.Description).IsEqualTo("English Updated");

        // Spanish
        var publicClientEs = TestHelpers.GetUnauthenticatedClientWithLanguage<IBooksClient>("es");
        var bookEs = await publicClientEs.GetBookAsync(book.Id);
        _ = await Assert.That(bookEs.Description).IsEqualTo("Descripción Original");
    }

    async Task<T> RetryUntilFoundAsync<T>(Func<Task<T?>> func)
    {
        T? result = default;
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            result = await func();
            return result != null;
        }, TestConstants.DefaultEventTimeout, "Item not found after retries");

        return result!;
    }
}
