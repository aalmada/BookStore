using System.Net;
using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class PublisherCrudTests
{
    [Test]
    public async Task CreatePublisher_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createPublisherRequest = TestDataGenerators.GenerateFakePublisherRequest();

        // Act - Connect to SSE before creating
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "PublisherUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/publishers", createPublisherRequest);
                _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);
        
        // Assert
        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task UpdatePublisher_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        dynamic createRequest = TestDataGenerators.GenerateFakePublisherRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/publishers", (object)createRequest);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var createdPublisher = await createResponse.Content.ReadFromJsonAsync<PublisherDto>();

        dynamic updateRequest = TestDataGenerators.GenerateFakePublisherRequest(); // New data

        // Act - Connect to SSE before updating, then wait for notification
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdPublisher!.Id,
            "PublisherUpdated",
            async () =>
            {
                var updateResponse = await httpClient.PutAsJsonAsync($"/api/admin/publishers/{createdPublisher.Id}", (object)updateRequest);
                if (updateResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"UpdatePublisher Failed with {updateResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(30));

        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task DeletePublisher_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        dynamic createRequest = TestDataGenerators.GenerateFakePublisherRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/publishers", (object)createRequest);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var createdPublisher = await createResponse.Content.ReadFromJsonAsync<PublisherDto>();

        // Act - Connect to SSE before deleting, then wait for notification
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdPublisher!.Id,
            "PublisherDeleted",
            async () =>
            {
                var deleteResponse = await httpClient.DeleteAsync($"/api/admin/publishers/{createdPublisher.Id}");
                if (deleteResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    var error = await deleteResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"DeletePublisher Failed with {deleteResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TimeSpan.FromSeconds(30));

        _ = await Assert.That(received).IsTrue();
    }

    [Test]
    public async Task RestorePublisher_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

        // 1. Create Publisher
        var createRequest = TestDataGenerators.GenerateFakePublisherRequest();
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/publishers", createRequest);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var createdPublisher = await createResponse.Content.ReadFromJsonAsync<PublisherDto>();

        // 2. Soft Delete Publisher
        var deleteResponse = await httpClient.DeleteAsync($"/api/admin/publishers/{createdPublisher!.Id}");
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Act - Connect to SSE before restoring, then wait for notification
        // Note: Projecting a restore is seen as an Update (IsDeleted goes from true -> false)
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            createdPublisher.Id,
            "PublisherUpdated",
            async () =>
            {
                var restoreResponse = await httpClient.PostAsync($"/api/admin/publishers/{createdPublisher.Id}/restore", null);
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

    record PublisherDto(Guid Id, string Name);
}
