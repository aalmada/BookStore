using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Refit;
using TUnit.Core.Interfaces;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class CategoryCrudTests
{
    [Test]
    public async Task CreateCategory_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createCategoryRequest = FakeDataGenerators.GenerateFakeCategoryRequest();

        // Act
        var category = await CategoryHelpers.CreateCategoryAsync(client, createCategoryRequest);

        // Assert
        _ = await Assert.That(category).IsNotNull();
        _ = await Assert.That(category!.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task UpdateCategory_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = FakeDataGenerators.GenerateFakeCategoryRequest();
        var createdCategory = await CategoryHelpers.CreateCategoryAsync(client, createRequest);

        var updateRequest = FakeDataGenerators.GenerateFakeUpdateCategoryRequest(); // New data

        // Act
        createdCategory = await CategoryHelpers.UpdateCategoryAsync(client, createdCategory!, updateRequest);

        // Verify update in public API (data should be consistent now)
        // We use public unauthenticated client to verify
        // But need to set Accept-Language headers to verify specific translations

        var publicClient =
            RestService.For<ICategoriesClient>(
                HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
        var expectedName = updateRequest.Translations["en"].Name;

        var updatedCategory = await publicClient.GetCategoryAsync(createdCategory!.Id, acceptLanguage: "en");
        _ = await Assert.That(updatedCategory!.Name).IsEqualTo(expectedName);
    }

    [Test]
    public async Task DeleteCategory_ShouldReturnNoContent()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var createRequest = FakeDataGenerators.GenerateFakeCategoryRequest();
        var createdCategory = await CategoryHelpers.CreateCategoryAsync(client, createRequest);

        // Act
        createdCategory = await CategoryHelpers.DeleteCategoryAsync(client, createdCategory!);

        // Verify it's gone from public API
        // Verify it's gone from public API
        var publicClient =
            RestService.For<ICategoriesClient>(
                HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
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
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
        var request = new CreateCategoryRequest
        {
            Id = Guid.CreateVersion7(),
            Translations = new Dictionary<string, CategoryTranslationDto> { ["en"] = new(invalidName!) }
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
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

        var createRequest = new CreateCategoryRequest
        {
            Id = Guid.CreateVersion7(),
            Translations = new Dictionary<string, CategoryTranslationDto>
            {
                ["en"] = new("Default Name"),
                ["pt-PT"] = new("Nome da Categoria"),
                ["es"] = new("Nombre de la Categoría")
            }
        };

        var createdCategory = await CategoryHelpers.CreateCategoryAsync(client, createRequest);
        _ = await Assert.That(createdCategory).IsNotNull();

        var publicClient =
            RestService.For<ICategoriesClient>(
                HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
        var categoryDto = await publicClient.GetCategoryAsync(createdCategory!.Id, acceptLanguage: acceptLanguage);

        // Assert
        _ = await Assert.That(categoryDto).IsNotNull();
        _ = await Assert.That(categoryDto!.Name).IsEqualTo(expectedName);
    }

    [Test]
    public async Task RestoreCategory_ShouldReturnOk()
    {
        // Arrange
        var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

        // 1. Create Category
        var createRequest = FakeDataGenerators.GenerateFakeCategoryRequest();
        var createdCategory = await CategoryHelpers.CreateCategoryAsync(client, createRequest);

        // 2. Soft Delete Category
        createdCategory = await CategoryHelpers.DeleteCategoryAsync(client, createdCategory!);

        // Act - Restore
        createdCategory = await CategoryHelpers.RestoreCategoryAsync(client, createdCategory!);

        // Verify
        // Use client to get it (should succeed now if visible to admin, which it is)
        var restored = await client.GetCategoryAsync(createdCategory!.Id);
        _ = await Assert.That(restored).IsNotNull();
    }
}
