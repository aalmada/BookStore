using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using UpdateAuthorRequest = BookStore.Client.UpdateAuthorRequest;
using UpdateBookRequest = BookStore.Client.UpdateBookRequest;
using UpdateCategoryRequest = BookStore.Client.UpdateCategoryRequest;
using UpdatePublisherRequest = BookStore.Client.UpdatePublisherRequest;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class UpdateTests
{
    [Test]
    public async Task UpdateAuthor_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = FakeDataGenerators.GenerateFakeAuthorRequest();
        var author = await AuthorHelpers.CreateAuthorAsync(client, createRequest);

        var updateRequest = new UpdateAuthorRequest
        {
            Name = $"Updated Author {Guid.NewGuid()}",
            Translations = new Dictionary<string, AuthorTranslationDto>
            {
                ["en"] = new("Updated Biography EN"),
                ["pt-PT"] = new("Biografia Atualizada PT")
            }
        };

        // Act
        author = await AuthorHelpers.UpdateAuthorAsync(client, author, updateRequest);

        // Assert
        var updatedAuthorAdmin = await client.GetAuthorAsync(author.Id);
        _ = await Assert.That(updatedAuthorAdmin.Name).IsEqualTo(updateRequest.Name);

        // Verify translations via public API
        var publicClient = RestService.For<IAuthorsClient>(HttpClientHelpers.GetUnauthenticatedClient());

        var enAuthor = await publicClient.GetAuthorAsync(author.Id, "en");
        var ptAuthor = await publicClient.GetAuthorAsync(author.Id, "pt-PT");

        _ = await Assert.That(enAuthor.Biography).IsEqualTo("Updated Biography EN");
        _ = await Assert.That(ptAuthor.Biography).IsEqualTo("Biografia Atualizada PT");
    }

    [Test]
    public async Task UpdateCategory_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = FakeDataGenerators.GenerateFakeCategoryRequest();
        var category = await CategoryHelpers.CreateCategoryAsync(client, createRequest);

        var updateRequest = new UpdateCategoryRequest
        {
            Translations = new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new("Updated Category EN"),
                ["es"] = new("Categoría Actualizada ES")
            }
        };

        // Act
        category = await CategoryHelpers.UpdateCategoryAsync(client, category, updateRequest);

        // Assert
        var publicClient = RestService.For<ICategoriesClient>(HttpClientHelpers.GetUnauthenticatedClient());

        var enCat = await publicClient.GetCategoryAsync(category.Id, null, "en");
        var esCat = await publicClient.GetCategoryAsync(category.Id, null, "es");

        _ = await Assert.That(enCat.Name).IsEqualTo("Updated Category EN");
        _ = await Assert.That(esCat.Name).IsEqualTo("Categoría Actualizada ES");
    }

    [Test]
    public async Task UpdatePublisher_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
        var createRequest = FakeDataGenerators.GenerateFakePublisherRequest();
        var publisher = await PublisherHelpers.CreatePublisherAsync(client, createRequest);

        var updateRequest = new UpdatePublisherRequest { Name = "Updated Publisher Name" };

        // Act
        publisher = await PublisherHelpers.UpdatePublisherAsync(client, publisher, updateRequest);

        // Assert
        var publicClient = RestService.For<IPublishersClient>(HttpClientHelpers.GetUnauthenticatedClient());

        var updatedPub = await publicClient.GetPublisherAsync(publisher.Id);
        _ = await Assert.That(updatedPub.Name).IsEqualTo(updateRequest.Name);
    }

    [Test]
    public async Task UpdateBook_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var authorsClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var categoriesClient = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var publishersClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IPublishersClient>();

        var originalAuthor =
            await AuthorHelpers.CreateAuthorAsync(authorsClient, FakeDataGenerators.GenerateFakeAuthorRequest());
        var originalCategory =
            await CategoryHelpers.CreateCategoryAsync(categoriesClient, FakeDataGenerators.GenerateFakeCategoryRequest());
        var originalPublisher =
            await PublisherHelpers.CreatePublisherAsync(publishersClient, FakeDataGenerators.GenerateFakePublisherRequest());

        var createRequest =
            FakeDataGenerators.GenerateFakeBookRequest(originalPublisher.Id, [originalAuthor.Id], [originalCategory.Id]);
        var book = await BookHelpers.CreateBookAsync(client, createRequest);

        // Retrieve ETag
        var getResponse = await client.GetBookWithResponseAsync(book.Id);
        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // New dependencies
        var newAuthor = await AuthorHelpers.CreateAuthorAsync(authorsClient, FakeDataGenerators.GenerateFakeAuthorRequest());
        var newCategory =
            await CategoryHelpers.CreateCategoryAsync(categoriesClient, FakeDataGenerators.GenerateFakeCategoryRequest());
        var newPublisher =
            await PublisherHelpers.CreatePublisherAsync(publishersClient, FakeDataGenerators.GenerateFakePublisherRequest());

        var updateRequest = new UpdateBookRequest
        {
            Title = "Fully Updated Book Title",
            Isbn = "9999999999999",
            Language = "pt-PT",
            PublicationDate = new PartialDate(2025, 12, 25),
            PublisherId = newPublisher.Id,
            AuthorIds = [newAuthor.Id],
            CategoryIds = [newCategory.Id],
            Prices = new Dictionary<string, decimal> { ["USD"] = 19.99m },
            Translations = new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new("New English Description"),
                ["pt-PT"] = new("Nova Descrição em Português")
            }
        };

        book = await BookHelpers.UpdateBookAsync(client, book.Id, updateRequest, etag!);

        // Assert
        var enClient = HttpClientHelpers.GetUnauthenticatedClientWithLanguage<IBooksClient>("en");
        var ptClient = HttpClientHelpers.GetUnauthenticatedClientWithLanguage<IBooksClient>("pt-PT");

        var enBook = await enClient.GetBookAsync(book.Id);
        var ptBook = await ptClient.GetBookAsync(book.Id);

        _ = await Assert.That(enBook.Title).IsEqualTo(updateRequest.Title);
        _ = await Assert.That(enBook.Isbn).IsEqualTo(updateRequest.Isbn);
        _ = await Assert.That(enBook.PublicationDate?.Year).IsEqualTo(2025);
        _ = await Assert.That(enBook.Publisher?.Id).IsEqualTo(newPublisher.Id);
        _ = await Assert.That(enBook.Authors.Select(a => a.Id)).Contains(newAuthor.Id);
        _ = await Assert.That(enBook.Categories.Select(c => c.Id)).Contains(newCategory.Id);
        _ = await Assert.That(enBook.Description).IsEqualTo("New English Description");
        _ = await Assert.That(enBook.Prices).IsNotNull();
        _ = await Assert.That(enBook.Prices!["USD"]).IsEqualTo(19.99m);
        _ = await Assert.That(ptBook.Description).IsEqualTo("Nova Descrição em Português");
    }
}
