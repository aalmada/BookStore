using Microsoft.Extensions.Logging;

namespace BookStore.Web.Tests;

public class WebTests
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2); // Aspire startup can take time in CI

    [Test]
    [Category("Integration")]
    public async Task GetWebResourceRootReturnsOkStatusCode(CancellationToken cancellationToken)
    {
        // Arrange

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>(cancellationToken);
        _ = appHost.Services.AddLogging(logging =>
        {
            _ = logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            _ = logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            _ = logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        _ = appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");
        _ = await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        var response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
