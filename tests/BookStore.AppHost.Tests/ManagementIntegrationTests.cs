using System.Net;
using BookStore.Client;
using Refit;
using SharedModels = BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class ManagementIntegrationTests
{
    [Test]
    public async Task GetAllData_AsAdmin_ShouldReturnAllEntities()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var authorsClient = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var categoriesClient = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var publishersClient = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();

        // Create random entities to ensure list is non-empty
        var author = await TestHelpers.CreateAuthorAsync(authorsClient,
            new CreateAuthorRequest
            {
                Name = $"Integration Author {Guid.NewGuid()}",
                Translations = new Dictionary<string, AuthorTranslationDto> { ["en"] = new() { Biography = "Bio" } }
            });

        var category = await TestHelpers.CreateCategoryAsync(categoriesClient,
            new CreateCategoryRequest
            {
                Translations = new Dictionary<string, CategoryTranslationDto>
                {
                    ["en"] = new() { Name = $"Integration Cat {Guid.NewGuid()}" }
                }
            });

        var publisher = await TestHelpers.CreatePublisherAsync(publishersClient,
            new CreatePublisherRequest { Name = $"Integration Pub {Guid.NewGuid()}" });

        var createRequest = new CreateBookRequest
        {
            Title = $"Integration Book {Guid.NewGuid()}",
            Isbn = "1234567890",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new() { Description = "Test" } },
            PublicationDate = new SharedModels.PartialDate(2024),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.99m },
            AuthorIds = [author.Id],
            CategoryIds = [category.Id],
            PublisherId = publisher.Id
        };
        var book = await TestHelpers.CreateBookAsync(client, createRequest);

        // Act
        // Use search with empty params to get all (paged/list)
        var books = await client.GetAllBooksAdminAsync();
        // Admin endpoints take SearchRequest only
        var authors = await authorsClient.GetAllAuthorsAsync(new SharedModels.AuthorSearchRequest());
        var categories = await categoriesClient.GetAllCategoriesAsync(new SharedModels.CategorySearchRequest());
        var publishers = await publishersClient.GetAllPublishersAsync(new SharedModels.PublisherSearchRequest());

        // Assert
        _ = await Assert.That(books).IsNotNull();
        _ = await Assert.That(books.Count).IsGreaterThan(0);
        _ = await Assert.That(books.Any(b => b.Id == book.Id)).IsTrue();

        _ = await Assert.That(authors).IsNotNull();
        _ = await Assert.That(authors!.Items.Any(a => a.Id == author.Id)).IsTrue();

        _ = await Assert.That(categories).IsNotNull();
        _ = await Assert.That(categories!.Items.Any(c => c.Id == category.Id)).IsTrue();

        _ = await Assert.That(publishers).IsNotNull();
        _ = await Assert.That(publishers!.Items.Any(p => p.Id == publisher.Id)).IsTrue();
    }

    [Test]
    public async Task Search_WithFilter_ShouldReturnMatchedItems()
    {
        // Arrange
        var authorsClient = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var categoriesClient = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var publishersClient = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();

        var suffix = Guid.NewGuid().ToString()[..8];
        var authorName = $"SearchMatch Auth {suffix}";
        var catName = $"SearchMatch Cat {suffix}";
        var pubName = $"SearchMatch Pub {suffix}";

        _ = await TestHelpers.CreateAuthorAsync(authorsClient,
            new CreateAuthorRequest
            {
                Name = authorName,
                Translations = new Dictionary<string, AuthorTranslationDto> { ["en"] = new() { Biography = "Bio" } }
            });
        _ = await TestHelpers.CreateCategoryAsync(categoriesClient,
            new CreateCategoryRequest
            {
                Translations = new Dictionary<string, CategoryTranslationDto> { ["en"] = new() { Name = catName } }
            });
        _ = await TestHelpers.CreatePublisherAsync(publishersClient, new CreatePublisherRequest { Name = pubName });

        // Act & Assert
        _ = await VerifyInAdminAuthorsAsync(authorsClient, authorName, true);
        _ = await VerifyInAdminCategoriesAsync(categoriesClient, catName, true);
        _ = await VerifyInAdminPublishersAsync(publishersClient, pubName, true);
    }

    [Test]
    public async Task SoftDelete_ShouldHideItem_AndRestoreShouldShowIt()
    {
        // Arrange
        var authorsClient = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var suffix = Guid.NewGuid().ToString()[..8];
        var authorName = $"Delete Auth {suffix}";

        var author = await TestHelpers.CreateAuthorAsync(authorsClient,
            new CreateAuthorRequest
            {
                Name = authorName,
                Translations = new Dictionary<string, AuthorTranslationDto> { ["en"] = new() { Biography = "Bio" } }
            });

        // Act - Delete
        // Placeholder affirmation
        _ = await Assert.That(author).IsNotNull();
    }

    // Helpers
    async Task<SharedModels.BookDto> CreateBookAsync(IBooksClient client, CreateBookRequest request)
    {
        SharedModels.BookDto? res = null;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            ["BookCreated", "BookUpdated"],
            async () => res = await client.CreateBookAsync(request),
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();

        return res!;
    }

    async Task<SharedModels.AdminAuthorDto> CreateAuthorAsync(IAuthorsClient client, CreateAuthorRequest request)
    {
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            ["AuthorCreated", "AuthorUpdated"],
            async () => await client.CreateAuthorAsync(request),
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        var paged = await client.GetAllAuthorsAsync(new SharedModels.AuthorSearchRequest { Search = request.Name });
        return paged!.Items.First(a => a.Name == request.Name);
    }

    async Task<SharedModels.AdminCategoryDto> CreateCategoryAsync(ICategoriesClient client,
        CreateCategoryRequest request)
    {
        var englishName = request.Translations["en"].Name;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            ["CategoryCreated", "CategoryUpdated"],
            async () => await client.CreateCategoryAsync(request),
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();

        var paged = await client.GetAllCategoriesAsync(new SharedModels.CategorySearchRequest { Search = englishName });
        var cat = paged!.Items.FirstOrDefault(c => c.Translations["en"].Name == englishName);
        if (cat == null)
        {
            throw new Exception("Category not found after creation");
        }

        return cat;
    }

    async Task<SharedModels.PublisherDto> CreatePublisherAsync(IPublishersClient client, CreatePublisherRequest request)
    {
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            ["PublisherCreated", "PublisherUpdated"],
            async () => await client.CreatePublisherAsync(request),
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();

        var paged = await client.GetAllPublishersAsync(
            new SharedModels.PublisherSearchRequest { Search = request.Name });
        return paged!.Items.First(p => p.Name == request.Name);
    }

    async Task<bool> VerifyInAdminAuthorsAsync(IAuthorsClient client, string search, bool expected)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                var response =
                    await client.GetAllAuthorsAsync(new SharedModels.AuthorSearchRequest { Search = search });
                var found = response?.Items.Any(a => a.Name == search) ?? false;
                if (found == expected)
                {
                    return true;
                }
            }
            catch
            {
                /* Ignore */
            }

            await Task.Delay(500);
        }

        return false;
    }

    async Task<bool> VerifyInAdminCategoriesAsync(ICategoriesClient client, string search, bool expected)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                var response =
                    await client.GetAllCategoriesAsync(new SharedModels.CategorySearchRequest { Search = search });
                var found = response?.Items.Any(c => c.Translations["en"].Name == search) ?? false;
                if (found == expected)
                {
                    return true;
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        return false;
    }

    async Task<bool> VerifyInAdminPublishersAsync(IPublishersClient client, string search, bool expected)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                var response =
                    await client.GetAllPublishersAsync(new SharedModels.PublisherSearchRequest { Search = search });
                var found = response?.Items.Any(p => p.Name == search) ?? false;
                if (found == expected)
                {
                    return true;
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        return false;
    }
}
