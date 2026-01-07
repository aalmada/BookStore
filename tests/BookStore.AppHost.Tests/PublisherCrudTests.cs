using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class PublisherCrudTests
{
    [Test]
    public async Task CreatePublisher_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createPublisherRequest = TestDataGenerators.GenerateFakePublisherRequest();

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/publishers", createPublisherRequest);

        // Assert
        await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
    }
}
