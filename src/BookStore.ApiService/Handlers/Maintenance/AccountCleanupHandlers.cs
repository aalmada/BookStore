using BookStore.ApiService.Infrastructure.Identity;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Models;
using Marten;
using Marten.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace BookStore.ApiService.Handlers.Maintenance;

/// <summary>
/// Wolverine command to trigger unverified account cleanup.
/// </summary>
public record CleanupUnverifiedAccounts;

/// <summary>
/// Handler for unverified account cleanup.
/// </summary>
public static class AccountCleanupHandlers
{
    /// <summary>
    /// Deletes user accounts that have not verified their email address after a period of time.
    /// </summary>
    public static async Task Handle(
        CleanupUnverifiedAccounts _,
        IDocumentSession session,
        IOptions<AccountCleanupOptions> options,
        IMessageContext bus,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            return;
        }

        var expirationThreshold = DateTimeOffset.UtcNow.AddHours(-config.UnverifiedAccountExpirationHours);

        Log.Maintenance.AccountCleanupStarted(logger, config.UnverifiedAccountExpirationHours);

        try
        {
            // Find unverified accounts older than the threshold
            var staleUsers = await session.Query<ApplicationUser>()
                .Where(u => !u.EmailConfirmed && u.CreatedAt < expirationThreshold)
                .ToListAsync(cancellationToken);

            if (staleUsers.Count > 0)
            {
                foreach (var user in staleUsers)
                {
                    session.Delete(user);
                }

                await session.SaveChangesAsync(cancellationToken);
            }

            Log.Maintenance.AccountCleanupCompleted(logger, staleUsers.Count);

            // Reschedule for the next interval if a bus is provided.
            // This creates the "recurring" behavior.
            if (bus is not null)
            {
                await bus.ScheduleAsync(new CleanupUnverifiedAccounts(), TimeSpan.FromHours(config.CleanupIntervalHours));
            }
        }
        catch (Exception ex)
        {
            Log.Maintenance.AccountCleanupFailed(logger, ex);
            throw;
        }
    }
}
