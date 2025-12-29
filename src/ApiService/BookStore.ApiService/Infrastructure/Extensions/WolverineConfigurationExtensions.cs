using Wolverine;
using Wolverine.SignalR;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring Wolverine messaging
/// </summary>
public static class WolverineConfigurationExtensions
{
    /// <summary>
    /// Configures Wolverine with command/handler pattern and SignalR integration
    /// </summary>
    public static IServiceCollection AddWolverineMessaging(this IServiceCollection services)
    {
        _ = services.AddWolverine(opts =>
        {
            // Auto-discover handlers in this assembly
            _ = opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

            // Explicitly include static handler classes for discovery
            RegisterHandlers(opts);

            // Enable SignalR transport for real-time notifications
            _ = opts.UseSignalR();

            // Route domain event notifications to SignalR
            ConfigureEventPublishing(opts);

            // Policies for automatic behavior
            opts.Policies.AutoApplyTransactions();
        });

        return services;
    }

    static void RegisterHandlers(WolverineOptions opts)
    {
        _ = opts.Discovery.IncludeType(typeof(Handlers.Authors.AuthorHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Books.BookHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Books.BookCoverHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Categories.CategoryHandlers));
        _ = opts.Discovery.IncludeType(typeof(Handlers.Publishers.PublisherHandlers));
    }

    static void ConfigureEventPublishing(WolverineOptions opts) => opts.Publish(x =>
                                                                        {
                                                                            x.MessagesImplementing<Events.Notifications.IDomainEventNotification>();
                                                                            _ = x.ToSignalR();
                                                                        });
}
