using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class AuthorCrudTests
{
    [Test]
    public async Task CreateAuthor_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createAuthorRequest = TestHelpers.GenerateFakeAuthorRequest();

        // Act - Connect to SSE before creating
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "AuthorUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", createAuthorRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task UpdateAuthor_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        dynamic createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", (object)createRequest);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var createdAuthor = await createResponse.Content.ReadFromJsonAsync<AuthorDto>();

        dynamic updateRequest = TestHelpers.GenerateFakeAuthorRequest(); // New data

        // Act - Connect to SSE before updating, then wait for notification
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdAuthor!.Id,
            "AuthorUpdated",
            async () =>
            {
                var updateResponse = await httpClient.PutAsJsonAsync($"/api/admin/authors/{createdAuthor.Id}", (object)updateRequest);
                if (updateResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"UpdateAuthor Failed with {updateResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(30));

        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task DeleteAuthor_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        dynamic createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", (object)createRequest);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var createdAuthor = await createResponse.Content.ReadFromJsonAsync<AuthorDto>();

        // Act - Connect to SSE before deleting, then wait for notification
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdAuthor!.Id,
            "AuthorDeleted",
            async () =>
            {
                var deleteResponse = await httpClient.DeleteAsync($"/api/admin/authors/{createdAuthor.Id}");
                if (deleteResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    var error = await deleteResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"DeleteAuthor Failed with {deleteResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(30));

        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task RestoreAuthor_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // 1. Create Author
        var createRequest = TestHelpers.GenerateFakeAuthorRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", createRequest);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var createdAuthor = await createResponse.Content.ReadFromJsonAsync<AuthorDto>();

        // 2. Soft Delete Author
        var deleteResponse = await httpClient.DeleteAsync($"/api/admin/authors/{createdAuthor!.Id}");
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Act - Connect to SSE before restoring, then wait for notification
        // Note: Projecting a restore is seen as an Update (IsDeleted goes from true -> false)
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdAuthor.Id,
            "AuthorUpdated",
            async () =>
            {
                var restoreResponse = await httpClient.PostAsync($"/api/admin/authors/{createdAuthor.Id}/restore", null);
                if (!restoreResponse.IsSuccessStatusCode)
                {
                    var error = await restoreResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SSE-TEST] Restore failed: {restoreResponse.StatusCode} - {error}");
                }

                _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(30));

        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    [Arguments("pt-PT", "Biografia em Português")]
    [Arguments("es", "Biografía en Español")]
    [Arguments("es-MX", "Biografía en Español")]
    [Arguments("fr-FR", "Default Biography")]
    [Arguments("en", "Default Biography")]
    public async Task GetAuthor_WithLocalizedHeader_ShouldReturnExpectedContent(string acceptLanguage, string expectedBiography)
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = GlobalHooks.App!.CreateHttpClient("apiservice");

        var createRequest = new
        {
            Name = "Global Author Name",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Biography = "Default Biography" },
                ["pt-PT"] = new { Biography = "Biografia em Português" },
                ["es"] = new { Biography = "Biografía en Español" }
            }
        };

        AuthorDto? res = null;

        // Execute create and wait for SSE notification
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "AuthorUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", createRequest);
                _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
                res = await createResponse.Content.ReadFromJsonAsync<AuthorDto>();
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(res).IsNotNull();
        _ = await Assert.That(received).IsTrue();

        // Retry policy for the GET check
        var retries = 5;
        AuthorDto? authorDto = null;

        publicClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        publicClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(acceptLanguage));

        while (retries-- > 0)
        {
            var response = await publicClient.GetAsync($"/api/authors/{res!.Id}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                authorDto = await response.Content.ReadFromJsonAsync<AuthorDto>();
                break;
            }
            await Task.Delay(500);
        }

        // Assert
        _ = await Assert.That(authorDto).IsNotNull();
        _ = await Assert.That(authorDto!.Biography).IsEqualTo(expectedBiography);
    }

    record AuthorDto(Guid Id, string Name, string? Biography);
}
