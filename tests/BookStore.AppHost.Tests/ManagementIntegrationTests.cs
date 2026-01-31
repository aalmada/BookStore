using System.Net;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class ManagementIntegrationTests
{
    [Test]
    public async Task AdminAuthors_Search_ShouldReturnMatches()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var uniqueName = $"SearchAuthor-{Guid.NewGuid()}";

        // 1. Create Author
        var createRequest = new
        {
            Name = uniqueName,
            Translations = new Dictionary<string, object> { ["en"] = new { Biography = "Test Biography" } }
        };
        _ = await CreateAuthorAsync(httpClient, createRequest);

        // Act - Search via Admin endpoint
        var searchResult = await SearchAdminAuthorsAsync(httpClient, uniqueName);

        // Assert
        _ = await Assert.That(searchResult).IsNotNull();
        _ = await Assert.That(searchResult!.Items).IsNotEmpty();
        _ = await Assert.That(searchResult.Items.Any(a => a.Name == uniqueName)).IsTrue();
    }

    [Test]
    public async Task AdminCategories_Search_ShouldReturnMatches()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var uniqueName = $"SearchCategory-{Guid.NewGuid()}";

        // 1. Create Category
        var createRequest = new
        {
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Name = uniqueName, Description = "Test" }
            }
        };
        _ = await CreateCategoryAsync(httpClient, createRequest);

        // Act - Search via Admin endpoint
        var searchResult = await SearchAdminCategoriesAsync(httpClient, uniqueName);

        // Assert
        _ = await Assert.That(searchResult).IsNotNull();
        _ = await Assert.That(searchResult!.Items).IsNotEmpty();
        _ = await Assert.That(searchResult.Items.Any(c => c.Name == uniqueName)).IsTrue();
    }

    [Test]
    public async Task AdminPublishers_Search_ShouldReturnMatches()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var uniqueName = $"SearchPublisher-{Guid.NewGuid()}";

        // 1. Create Publisher
        var createRequest = new { Name = uniqueName };
        _ = await CreatePublisherAsync(httpClient, createRequest);

        // Act - Search via Admin endpoint
        var searchResult = await SearchAdminPublishersAsync(httpClient, uniqueName);

        // Assert
        _ = await Assert.That(searchResult).IsNotNull();
        _ = await Assert.That(searchResult!.Items).IsNotEmpty();
        _ = await Assert.That(searchResult.Items.Any(p => p.Name == uniqueName)).IsTrue();
    }

    [Test]
    public async Task AdminAuthors_SoftDelete_ShouldHideFromPublicAndShowInAdmin()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();
        var uniqueName = $"DeleteAuthor-{Guid.NewGuid()}";

        // 1. Create Author
        var createRequest = new
        {
            Name = uniqueName,
            Translations = new Dictionary<string, object> { ["en"] = new { Biography = "Test Biography" } }
        };
        var createdAuthor = await CreateAuthorAsync(httpClient, createRequest);

        // 2. Verify it's in public list (eventually)
        _ = await VerifyInPublicAuthorsAsync(publicClient, uniqueName, true);

        // 3. Soft Delete
        var deleteResponse = await httpClient.DeleteAsync($"/api/admin/authors/{createdAuthor.Id}");
        _ = await Assert.That((int)deleteResponse.StatusCode).IsEqualTo((int)HttpStatusCode.NoContent);

        // Wait for projection
        await Task.Delay(1000);

        // Act & Assert
        // 4. Verify it's NOT in public list
        _ = await VerifyInPublicAuthorsAsync(publicClient, uniqueName, false);

        // 5. Verify it IS in admin list (admin sees soft-deleted items too)
        var adminResult = await SearchAdminAuthorsAsync(httpClient, uniqueName);
        _ = await Assert.That(adminResult!.Items.Any(a => a.Id == createdAuthor.Id)).IsTrue();
    }

    async Task<AuthorDto> CreateAuthorAsync(HttpClient client, object request)
    {
        AuthorDto? res = null;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "AuthorUpdated",
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/admin/authors", request);
                _ = await Assert.That((int)response.StatusCode).IsEqualTo((int)HttpStatusCode.Created);
                res = await response.Content.ReadFromJsonAsync<AuthorDto>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();
        return res!;
    }

    async Task<CategoryDto> CreateCategoryAsync(HttpClient client, object request)
    {
        CategoryDto? res = null;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "CategoryUpdated",
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/admin/categories", request);
                _ = await Assert.That((int)response.StatusCode).IsEqualTo((int)HttpStatusCode.Created);
                res = await response.Content.ReadFromJsonAsync<CategoryDto>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();
        return res!;
    }

    async Task<PublisherDto> CreatePublisherAsync(HttpClient client, object request)
    {
        PublisherDto? res = null;
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "PublisherUpdated",
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/admin/publishers", request);
                _ = await Assert.That((int)response.StatusCode).IsEqualTo((int)HttpStatusCode.Created);
                res = await response.Content.ReadFromJsonAsync<PublisherDto>();
            },
            TestConstants.DefaultEventTimeout);
        _ = await Assert.That(received).IsTrue();
        return res!;
    }

    Task<PagedListDto<AuthorDto>?> SearchAdminAuthorsAsync(HttpClient client, string search)
        => client.GetFromJsonAsync<PagedListDto<AuthorDto>>($"/api/admin/authors?search={search}");

    Task<PagedListDto<CategoryDto>?> SearchAdminCategoriesAsync(HttpClient client, string search)
        => client.GetFromJsonAsync<PagedListDto<CategoryDto>>($"/api/admin/categories?search={search}");

    Task<PagedListDto<PublisherDto>?> SearchAdminPublishersAsync(HttpClient client, string search)
        => client.GetFromJsonAsync<PagedListDto<PublisherDto>>($"/api/admin/publishers?search={search}");

    async Task<bool> VerifyInPublicAuthorsAsync(HttpClient client, string search, bool expected)
    {
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetFromJsonAsync<PagedListDto<AuthorDto>>($"/api/authors?search={search}");
            var found = response?.Items.Any(a => a.Name == search) ?? false;
            if (found == expected)
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    public record AuthorDto(Guid Id, string Name, string? Biography);

    public record CategoryDto(Guid Id, string Name);

    public record PublisherDto(Guid Id, string Name);
}
