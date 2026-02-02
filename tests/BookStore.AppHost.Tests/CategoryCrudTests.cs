using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Refit;
using TUnit.Core.Interfaces;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class CategoryCrudTests
{
    [Test]
    public async Task CreateCategory_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createCategoryRequest = TestHelpers.GenerateFakeCategoryRequest();

        // Act
        var category = await TestHelpers.CreateCategoryAsync(client, createCategoryRequest);

        // Assert
        _ = await Assert.That(category).IsNotNull();
        _ = await Assert.That(category!.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task UpdateCategory_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var createdCategory = await TestHelpers.CreateCategoryAsync(client, createRequest);

        var updateRequest = TestHelpers.GenerateFakeUpdateCategoryRequest(); // New data

        // Act
        await TestHelpers.UpdateCategoryAsync(client, createdCategory!, updateRequest);

        // Verify update in public API (data should be consistent now)
        // We use public unauthenticated client to verify
        // But need to set Accept-Language headers to verify specific translations

        var publicClient =
            RestService.For<IGetCategoryEndpoint>(
                TestHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
        var expectedName = updateRequest.Translations["en"].Name;

        var updatedCategory = await publicClient.GetCategoryAsync(createdCategory!.Id, acceptLanguage: "en");
        _ = await Assert.That(updatedCategory!.Name).IsEqualTo(expectedName);
    }

    [Test]
    public async Task DeleteCategory_ShouldReturnNoContent()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var createdCategory = await TestHelpers.CreateCategoryAsync(client, createRequest);

        // Act
        await TestHelpers.DeleteCategoryAsync(client, createdCategory!);

        // Verify it's gone from public API
        // Verify it's gone from public API
        var publicClient =
            RestService.For<IGetCategoryEndpoint>(
                TestHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
        try
        {
            _ = await publicClient.GetCategoryAsync(createdCategory!.Id);
            Assert.Fail("Category should have been deleted");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
    public async Task CreateCategory_WithInvalidName_ShouldReturnBadRequest(string? invalidName)
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var request = new CreateCategoryRequest
        {
            Translations = new Dictionary<string, BookStore.Client.CategoryTranslationDto>
            {
                ["en"] = new()
                {
                    Name = invalidName, // Invalid
                    Description = "Description"
                }
            }
        };

        // Act & Assert
        try
        {
            await client.CreateCategoryAsync(request);
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            return;
        }

        // Fail if no exception
        Assert.Fail("Expected ApiException was not thrown");
    }

    [Test]
    [Arguments("pt-PT", "Nome da Categoria")]
    [Arguments("es", "Nombre de la Categoría")]
    [Arguments("es-MX", "Nombre de la Categoría")]
    [Arguments("fr-FR", "Default Name")]
    [Arguments("en", "Default Name")]
    public async Task GetCategory_WithLocalizedHeader_ShouldReturnExpectedContent(string acceptLanguage,
        string expectedName)
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

        var createRequest = new CreateCategoryRequest
        {
            Translations = new Dictionary<string, BookStore.Client.CategoryTranslationDto>
            {
                ["en"] = new() { Name = "Default Name", Description = "Default Description" },
                ["pt-PT"] = new() { Name = "Nome da Categoria", Description = "Descrição em Português" },
                ["es"] = new() { Name = "Nombre de la Categoría", Description = "Descripción en Español" }
            }
        };

        var createdCategory = await TestHelpers.CreateCategoryAsync(client, createRequest);
        _ = await Assert.That(createdCategory).IsNotNull();

        // Retry policy for the GET check (eventual consistency)
        var publicClient =
            RestService.For<IGetCategoryEndpoint>(
                TestHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
        var retries = 5;
        CategoryDto? categoryDto = null;

        while (retries-- > 0)
        {
            try
            {
                categoryDto = await publicClient.GetCategoryAsync(createdCategory!.Id, acceptLanguage: acceptLanguage);
                break;
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Wait for projection
            }

            await Task.Delay(500);
        }

        // Assert
        _ = await Assert.That(categoryDto).IsNotNull();
        _ = await Assert.That(categoryDto!.Name).IsEqualTo(expectedName);
    }

    [Test]
    public async Task RestoreCategory_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

        // 1. Create Category
        var createRequest = TestHelpers.GenerateFakeCategoryRequest();
        var createdCategory = await TestHelpers.CreateCategoryAsync(client, createRequest);

        // 2. Soft Delete Category
        await TestHelpers.DeleteCategoryAsync(client, createdCategory!);

        // Act - Restore
        await TestHelpers.RestoreCategoryAsync(client, createdCategory!);

        // Verify
        // Use client to get it (should succeed now if visible to admin, which it is)
        var restored = await client.GetCategoryAsync(createdCategory!.Id);
        _ = await Assert.That(restored).IsNotNull();
    }
}
