using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Extension methods for registering the BookStore API client
/// </summary>
public static class BookStoreClientExtensions
{
    /// <summary>
    /// Adds the BookStore API client to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="baseAddress">The base address of the API</param>
    /// <returns>An IHttpClientBuilder for further configuration</returns>
    public static IHttpClientBuilder AddBookStoreClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        return services
            .AddRefitClient<IBookStoreApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
    }

    /// <summary>
    /// Adds the BookStore API client to the service collection with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureClient">Action to configure the HttpClient</param>
    /// <returns>An IHttpClientBuilder for further configuration</returns>
    public static IHttpClientBuilder AddBookStoreClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        return services
            .AddRefitClient<IBookStoreApi>()
            .ConfigureHttpClient(configureClient);
    }
}
