using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Projects;

namespace BookStore.AppHost.Tests;

public class InfrastructureTests
{
    [Test]
    [Arguments("postgres")]
    [Arguments("cache")]
    [Arguments("blobs")]
    public async Task ResourceIsHealthy(string resourceName)
    {
        // Arrange
        var notificationService = GlobalHooks.NotificationService;

        // Act & Assert
        _ = await notificationService!.WaitForResourceHealthyAsync(resourceName, CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);
    }
}
