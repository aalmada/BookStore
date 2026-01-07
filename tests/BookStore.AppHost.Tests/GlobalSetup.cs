using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging;
using Projects;

[assembly: Retry(3)]
[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace BookStore.AppHost.Tests;

public static class GlobalHooks
{
    public static DistributedApplication? App { get; private set; }
    public static ResourceNotificationService? NotificationService { get; private set; }

    [Before(TestSession)]
    public static async Task SetUp()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>();
        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            logging.AddSimpleConsole(options => 
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
        });

        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        App = await builder.BuildAsync();
        
        NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();
        await App.StartAsync();
    }

    [After(TestSession)]
    public static async Task CleanUp()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }
}
