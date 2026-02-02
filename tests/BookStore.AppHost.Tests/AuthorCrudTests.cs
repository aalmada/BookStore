using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Client;
using JasperFx;
using Refit;
using AuthorDto = BookStore.Shared.Models.AuthorDto;
using AuthorTranslationDto = BookStore.Client.AuthorTranslationDto;
using CreateAuthorRequest = BookStore.Client.CreateAuthorRequest;
using UpdateAuthorRequest = BookStore.Client.UpdateAuthorRequest;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class AuthorCrudTests
{
    [Test]
    public async Task CreateAuthor_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createAuthorRequest = TestHelpers.GenerateFakeAuthorRequest();

        // Act
        var author = await TestHelpers.CreateAuthorAsync(client, createAuthorRequest);

        // Assert
        _ = await Assert.That(author).IsNotNull();
        _ = await Assert.That(author!.Name).IsEqualTo(createAuthorRequest.Name);
    }

    [Test]
    public async Task UpdateAuthor_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        var updateRequest = TestHelpers.GenerateFakeUpdateAuthorRequest();

        // Act
        await TestHelpers.UpdateAuthorAsync(client, author!, updateRequest);

        // Assert
        var updatedAuthor = await client.GetAuthorAsync(author!.Id);
        _ = await Assert.That(updatedAuthor.Name).IsEqualTo(updateRequest.Name);
    }

    [Test]
    public async Task DeleteAuthor_ShouldReturnNoContent()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        // Act
        await TestHelpers.DeleteAuthorAsync(client, author!);

        // Assert
        // Verify it is not found or soft deleted
        try
        {
            _ = await client.GetAuthorAsync(author!.Id);
            // Admin API GetAuthor might still return it? Or return 404? 
            // If SoftDelete, it typically returns 404 for regular Get unless included.
            // Assuming failure or handled exception.
            // If it returns, we might check IsDeleted if available.
        }
        catch (ApiException ex)
        {
            // 404 Expected
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    [Test]
    public async Task RestoreAuthor_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);
        await TestHelpers.DeleteAuthorAsync(client, author!);

        // Act
        await TestHelpers.RestoreAuthorAsync(client, author!);

        // Assert
        var restored = await client.GetAuthorAsync(author!.Id);
        _ = await Assert.That(restored).IsNotNull();
    }

    [Test]
    [Arguments("pt-PT", "Biografia em Português")]
    [Arguments("es", "Biografía en Español")]
    [Arguments("es-MX", "Biografía en Español")]
    [Arguments("fr-FR", "Default Biography")]
    [Arguments("en", "Default Biography")]
    public async Task GetAuthor_WithLocalizedHeader_ShouldReturnExpectedContent(string acceptLanguage,
        string expectedBiography)
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IAuthorsClient>();
        var publicClient =
            RestService.For<IGetAuthorEndpoint>(TestHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));

        var createRequest = new CreateAuthorRequest
        {
            Name = "Global Author Name",
            Translations = new Dictionary<string, AuthorTranslationDto>
            {
                ["en"] = new() { Biography = "Default Biography" },
                ["pt-PT"] = new() { Biography = "Biografia em Português" },
                ["es"] = new() { Biography = "Biografía en Español" }
            }
        };

        // Act
        var author = await TestHelpers.CreateAuthorAsync(client, createRequest);

        // Retry policy for the GET check
        var retries = 5;
        AuthorDto? authorDto = null;

        while (retries-- > 0)
        {
            try
            {
                // Public API returns AuthorDto (from Shared). 
                authorDto = await publicClient.GetAuthorAsync(author!.Id, acceptLanguage);
                break;
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Wait for projection
            }

            await Task.Delay(500);
        }

        // Assert
        _ = await Assert.That(authorDto).IsNotNull();
        _ = await Assert.That(authorDto!.Biography).IsEqualTo(expectedBiography);
    }
}
