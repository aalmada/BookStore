using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using UpdateAuthorRequest = BookStore.Client.UpdateAuthorRequest;
using UpdateBookRequest = BookStore.Client.UpdateBookRequest;
using UpdateCategoryRequest = BookStore.Client.UpdateCategoryRequest;
using UpdatePublisherRequest = BookStore.Client.UpdatePublisherRequest;

namespace BookStore.AppHost.Tests;

public class UpdateTests
{
    [Test]
    public async Task UpdateAuthor_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        var updateRequest = new UpdateAuthorRequest
        {
            Name = $"Updated Author {Guid.NewGuid()}",
            Translations = new Dictionary<string, AuthorTranslationDto>
            {
                ["en"] = new("Updated Biography EN"), ["pt-PT"] = new("Biografia Atualizada PT")
            }
        };

        // Act
        author = await TestHelpers.UpdateAuthorAsync(client, author, updateRequest);

        // Assert
        var updatedAuthorAdmin = await client.GetAuthorAsync(author.Id);
        _ = await Assert.That(updatedAuthorAdmin.Name).IsEqualTo(updateRequest.Name);

        // Verify translations via public API
        var publicClient = RestService.For<IGetAuthorEndpoint>(TestHelpers.GetUnauthenticatedClient());

        var enAuthor = await publicClient.GetAuthorAsync(author.Id, "en");
        var ptAuthor = await publicClient.GetAuthorAsync(author.Id, "pt-PT");

        _ = await Assert.That(enAuthor.Biography).IsEqualTo("Updated Biography EN");
        _ = await Assert.That(ptAuthor.Biography).IsEqualTo("Biografia Atualizada PT");
    }

    [Test]
    public async Task UpdateCategory_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var category = await TestHelpers.CreateCategoryAsync(client, createRequest);

        var updateRequest = new UpdateCategoryRequest
        {
            Translations = new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new("Updated Category EN"), ["es"] = new("Categoría Actualizada ES")
            }
        };

        // Act
        category = await TestHelpers.UpdateCategoryAsync(client, category, updateRequest);

        // Assert
        var publicClient = RestService.For<IGetCategoryEndpoint>(TestHelpers.GetUnauthenticatedClient());

        var enCat = await publicClient.GetCategoryAsync(category.Id, null, "en");
        var esCat = await publicClient.GetCategoryAsync(category.Id, null, "es");

        _ = await Assert.That(enCat.Name).IsEqualTo("Updated Category EN");
        _ = await Assert.That(esCat.Name).IsEqualTo("Categoría Actualizada ES");
    }

    [Test]
    public async Task UpdatePublisher_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
        var createRequest = TestHelpers.GenerateFakePublisherRequest();
        var publisher = await TestHelpers.CreatePublisherAsync(client, createRequest);

        var updateRequest = new UpdatePublisherRequest { Name = "Updated Publisher Name" };

        // Act
        publisher = await TestHelpers.UpdatePublisherAsync(client, publisher, updateRequest);

        // Assert
        var publicClient = RestService.For<IGetPublisherEndpoint>(TestHelpers.GetUnauthenticatedClient());

        var updatedPub = await publicClient.GetPublisherAsync(publisher.Id);
        _ = await Assert.That(updatedPub.Name).IsEqualTo(updateRequest.Name);
    }

    [Test]
    public async Task UpdateBook_FullUpdate_ShouldReflectChanges()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var authorsClient = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var categoriesClient = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var publishersClient = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();

        var originalAuthor =
            await TestHelpers.CreateAuthorAsync(authorsClient, TestHelpers.GenerateFakeAuthorRequest());
        var originalCategory =
            await TestHelpers.CreateCategoryAsync(categoriesClient, TestHelpers.GenerateFakeCategoryRequest());
        var originalPublisher =
            await TestHelpers.CreatePublisherAsync(publishersClient, TestHelpers.GenerateFakePublisherRequest());

        var createRequest =
            TestHelpers.GenerateFakeBookRequest(originalPublisher.Id, [originalAuthor.Id], [originalCategory.Id]);
        var book = await TestHelpers.CreateBookAsync(client, createRequest);

        // Retrieve ETag
        var getResponse = await client.GetBookWithResponseAsync(book.Id);
        var etag = getResponse.Headers.ETag?.Tag;
        _ = await Assert.That(etag).IsNotNull();

        // New dependencies
        var newAuthor = await TestHelpers.CreateAuthorAsync(authorsClient, TestHelpers.GenerateFakeAuthorRequest());
        var newCategory =
            await TestHelpers.CreateCategoryAsync(categoriesClient, TestHelpers.GenerateFakeCategoryRequest());
        var newPublisher =
            await TestHelpers.CreatePublisherAsync(publishersClient, TestHelpers.GenerateFakePublisherRequest());

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
                ["en"] = new("New English Description"), ["pt-PT"] = new("Nova Descrição em Português")
            }
        };

        book = await TestHelpers.UpdateBookAsync(client, book.Id, updateRequest, etag!);

        // Assert
        var enClient = TestHelpers.GetUnauthenticatedClientWithLanguage<IGetBookEndpoint>("en");
        var ptClient = TestHelpers.GetUnauthenticatedClientWithLanguage<IGetBookEndpoint>("pt-PT");

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
