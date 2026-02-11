using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Extension methods for registering BookStore API client interfaces.
/// </summary>
public static class BookStoreClientExtensions
{
    /// <summary>
    /// Registers all BookStore API client interfaces with the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the API.</param>
    /// <param name="configureClient">Optional action to configure the HTTP client builder (e.g. adding handlers).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBookStoreClient(
        this IServiceCollection services,
        Uri baseAddress,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        // Register the handlers
        _ = services.AddTransient<BookStore.Client.Infrastructure.BookStoreHeaderHandler>();
        _ = services.AddTransient<BookStore.Client.Infrastructure.BookStoreErrorHandler>();

        // Aggregated clients
        _ = services.AddClient<IBooksClient>(baseAddress, configureClient);
        _ = services.AddClient<IAuthorsClient>(baseAddress, configureClient);
        _ = services.AddClient<ICategoriesClient>(baseAddress, configureClient);
        _ = services.AddClient<IPublishersClient>(baseAddress, configureClient);
        _ = services.AddClient<IShoppingCartClient>(baseAddress, configureClient);
        _ = services.AddClient<ISystemClient>(baseAddress, configureClient);
        _ = services.AddClient<IIdentityClient>(baseAddress, configureClient);
        _ = services.AddClient<ITenantsClient>(baseAddress, configureClient);
        _ = services.AddClient<IUsersClient>(baseAddress, configureClient);
        _ = services.AddClient<IPasskeyClient>(baseAddress, configureClient);
        _ = services.AddClient<ISalesClient>(baseAddress, configureClient);
        _ = services.AddClient<IConfigurationClient>(baseAddress, configureClient);

        return services;
    }

    // Helper to register client with standard configuration
    static IHttpClientBuilder AddClient<T>(this IServiceCollection services, Uri baseAddress, Action<IHttpClientBuilder>? configureClient = null) where T : class
    {
        var builder = services.AddRefitClient<T>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<BookStore.Client.Infrastructure.BookStoreHeaderHandler>()
            .AddHttpMessageHandler<BookStore.Client.Infrastructure.BookStoreErrorHandler>();

        _ = builder.AddStandardResilienceHandler();

        configureClient?.Invoke(builder);
        return builder;
    }

    /// <summary>
    /// Registers all BookStore API client interfaces with Polly resilience policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the API.</param>
    /// <param name="configureResilience">Action to configure resilience policies.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBookStoreClientWithResilience(
        this IServiceCollection services,
        Uri baseAddress,
        Action<IHttpClientBuilder> configureResilience) => services.AddBookStoreClient(baseAddress, configureResilience);

    /// <summary>
    /// Registers the BookStore SSE events service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the API.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBookStoreEvents(
        this IServiceCollection services,
        Uri baseAddress)
    {
        _ = services.AddHttpClient("BookStoreEvents", client => client.BaseAddress = baseAddress);
        // Explicitly register as Scoped to ensure one connection per Blazor Circuit
        _ = services.AddScoped<BookStoreEventsService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("BookStoreEvents");
            var logger = sp.GetRequiredService<ILogger<BookStoreEventsService>>();
            var context = sp.GetRequiredService<Services.ClientContextService>();
            return new BookStoreEventsService(client, logger, context);
        });
        return services;
    }
}
