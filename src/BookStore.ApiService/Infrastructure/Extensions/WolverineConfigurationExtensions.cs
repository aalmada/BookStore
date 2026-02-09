using BookStore.Shared.Notifications;
using JasperFx.Core;
using Wolverine;
using Wolverine.ErrorHandling;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring Wolverine messaging
/// </summary>
public static class WolverineConfigurationExtensions
{
    /// <summary>
    /// Configures Wolverine with command/handler pattern
    /// </summary>
    public static IServiceCollection AddWolverineMessaging(this IServiceCollection services)
    {
        _ = services.AddWolverine(opts =>
        {
            // Auto-discover handlers in this assembly
            _ = opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

            // Explicitly include static handler classes for discovery
            RegisterHandlers(opts);

            // Policies for automatic behavior
            opts.Policies.AutoApplyTransactions();
            opts.Policies.AddMiddleware(typeof(WolverineCorrelationMiddleware));
            opts.Policies.AddMiddleware(typeof(WolverineETagMiddleware));

            // This *should* have some performance improvements, but would
            // require downtime to enable in existing systems
            opts.Durability.EnableInboxPartitioning = true;

            // Extra resiliency for unexpected problems, but can't be
            // defaults because this causes database changes
            opts.Durability.InboxStaleTime = TimeSpan.FromMinutes(10);
            opts.Durability.OutboxStaleTime = TimeSpan.FromMinutes(10);

            // Just annoying
            opts.EnableAutomaticFailureAcks = false;

            // Relatively new behavior that will store "unknown" messages
            // in the dead letter queue for possible recovery later
            opts.UnknownMessageBehavior = UnknownMessageBehavior.DeadLetterQueue;
        });

        return services;
    }

    static void RegisterHandlers(WolverineOptions opts)
    {
        _ = opts.Discovery.IncludeType(typeof(Handlers.Authors.AuthorHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Books.BookHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Books.BookCoverHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Books.BookPriceHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Categories.CategoryHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Publishers.PublisherHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Notifications.EmailHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.UserCommandHandler));
        _ = opts.Discovery.IncludeType(typeof(Handlers.TenantAdminHandler));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Maintenance.AccountCleanupHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Sales.SaleHandlers));
    }
}
