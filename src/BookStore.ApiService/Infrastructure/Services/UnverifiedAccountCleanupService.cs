using BookStore.ApiService.Handlers.Maintenance;
using BookStore.ApiService.Infrastructure.Identity;
using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace BookStore.ApiService.Infrastructure.Services;

/// <summary>
/// Background service that triggers the initial unverified account cleanup job.
/// The cleanup cycle continues via self-rescheduling in the handler.
/// </summary>
public class UnverifiedAccountCleanupService(
    IServiceProvider serviceProvider,
    IOptions<AccountCleanupOptions> options,
    ILogger<UnverifiedAccountCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            return;
        }

        try
        {
            // Give the app a moment to start and Wolverine to be ready
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            Log.Maintenance.InitialCleanupTriggered(logger);
            await bus.PublishAsync(new CleanupUnverifiedAccounts());
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Log.Maintenance.InitialCleanupTriggerFailed(logger, ex);
        }
    }
}
