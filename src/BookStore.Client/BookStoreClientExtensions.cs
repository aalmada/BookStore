using Microsoft.Extensions.DependencyInjection;
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
        // Helper to register client with optional configuration
        IHttpClientBuilder AddClient<T>() where T : class
        {
            var builder = services.AddRefitClient<T>()
                .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

            configureClient?.Invoke(builder);
            return builder;
        }

        // Aggregated clients
        _ = AddClient<IBooksClient>();
        _ = AddClient<IAuthorsClient>();
        _ = AddClient<ICategoriesClient>();
        _ = AddClient<IPublishersClient>();
        _ = AddClient<IShoppingCartClient>();
        _ = AddClient<ISystemClient>();
        _ = AddClient<IIdentityClient>();
        _ = AddClient<ITenantClient>();

        return services;
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
        _ = services.AddHttpClient<BookStoreEventsService>(client => client.BaseAddress = baseAddress);
        return services;
    }
}
