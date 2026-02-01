using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using TUnit.Core.Interfaces;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class PublisherCrudTests
{
    [Test]
    public async Task CreatePublisher_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
        var createPublisherRequest = TestHelpers.GenerateFakePublisherRequest();

        // Act
        var publisher = await TestHelpers.CreatePublisherAsync(client, createPublisherRequest);

        // Assert
        _ = await Assert.That(publisher).IsNotNull();
        _ = await Assert.That(publisher.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task UpdatePublisher_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
        var createRequest = TestHelpers.GenerateFakePublisherRequest();
        var createdPublisher = await TestHelpers.CreatePublisherAsync(client, createRequest);

        var updateRequest = new UpdatePublisherRequest { Name = "Updated Publisher Name" };

        // Act
        await TestHelpers.UpdatePublisherAsync(client, createdPublisher, updateRequest);

        // Verify update in public API (data should be consistent now)
        var publicClient = TestHelpers.GetUnauthenticatedClient();
        var updatedPublisher =
            await publicClient.GetFromJsonAsync<PublisherDto>($"/api/publishers/{createdPublisher.Id}");
        _ = await Assert.That(updatedPublisher!.Name).IsEqualTo(updateRequest.Name);
    }

    [Test]
    public async Task DeletePublisher_ShouldReturnNoContent()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();
        var createRequest = TestHelpers.GenerateFakePublisherRequest();
        var createdPublisher = await TestHelpers.CreatePublisherAsync(client, createRequest);

        // Act
        await TestHelpers.DeletePublisherAsync(client, createdPublisher);

        // Verify it's gone from public API
        var publicClient = TestHelpers.GetUnauthenticatedClient();
        var getResponse = await publicClient.GetAsync($"/api/publishers/{createdPublisher.Id}");
        _ = await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RestorePublisher_ShouldReturnOk()
    {
        // Arrange
        var client = await TestHelpers.GetAuthenticatedClientAsync<IPublishersClient>();

        // 1. Create Publisher
        var createRequest = TestHelpers.GenerateFakePublisherRequest();
        var createdPublisher = await TestHelpers.CreatePublisherAsync(client, createRequest);

        // 2. Soft Delete Publisher
        await TestHelpers.DeletePublisherAsync(client, createdPublisher);

        // Act - Restore
        await TestHelpers.RestorePublisherAsync(client, createdPublisher);

        // Verify
        // Use client to get it (should succeed now if visible to admin, which it is)
        var restored = await client.GetPublisherAsync(createdPublisher.Id);
        _ = await Assert.That(restored).IsNotNull();
    }
}
