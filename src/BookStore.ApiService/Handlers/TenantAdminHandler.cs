using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace BookStore.ApiService.Handlers;

public static class TenantAdminHandler
{
    public static async Task Handle(
        SeedTenantAdmin command,
        IDocumentSession session,
        UserManager<ApplicationUser> userManager,
        IMessageBus bus,
        ILogger logger,
        CancellationToken ct)
    {
        Log.Tenants.SeedingAdminUser(logger, command.TenantId, session.TenantId);

        // Seed the admin user using the tenant-scoped session
        // confirmEmail is false if verification is required
        var adminUser = await DatabaseSeeder.SeedAdminUserAsync(
            session,
            command.TenantId,
            command.Email,
            command.Password,
            confirmEmail: !command.VerificationRequired);

        if (adminUser != null)
        {
            // Initialize the UserProfile stream (enables favorites, cart, etc.)
            // and triggers UserUpdated SSE notification via ProjectionCommitListener
            _ = session.Events.StartStream<Projections.UserProfile>(
                adminUser.Id,
                new BookStore.Shared.Messages.Events.UserProfileCreated(adminUser.Id));

            if (command.VerificationRequired)
            {
                // Generate verification token and publish email command
                var code = await userManager.GenerateEmailConfirmationTokenAsync(adminUser);
                await bus.PublishAsync(new SendUserVerificationEmail(
                    adminUser.Id,
                    adminUser.Email!,
                    code,
                    adminUser.UserName!,
                    command.TenantId));
            }

            await session.SaveChangesAsync(ct);
        }
    }
}
