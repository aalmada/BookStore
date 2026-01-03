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

        // Register all endpoint interfaces
        _ = AddClient<IGetBooksEndpoint>();
        _ = AddClient<IGetBookEndpoint>();
        _ = AddClient<IGetAuthorsEndpoint>();
        _ = AddClient<IGetAuthorEndpoint>();
        _ = AddClient<IGetCategoriesEndpoint>();
        _ = AddClient<IGetCategoryEndpoint>();
        _ = AddClient<IGetPublishersEndpoint>();
        _ = AddClient<IGetPublisherEndpoint>();

        // Admin endpoints
        _ = AddClient<ICreateBookEndpoint>();
        _ = AddClient<IUpdateBookEndpoint>();
        _ = AddClient<ISoftDeleteBookEndpoint>();
        _ = AddClient<IRestoreBookEndpoint>();
        _ = AddClient<IUploadBookCoverEndpoint>();

        _ = AddClient<ICreateAuthorEndpoint>();
        _ = AddClient<IUpdateAuthorEndpoint>();
        _ = AddClient<ISoftDeleteAuthorEndpoint>();
        _ = AddClient<IRestoreAuthorEndpoint>();

        _ = AddClient<ICreateCategoryEndpoint>();
        _ = AddClient<IUpdateCategoryEndpoint>();
        _ = AddClient<ISoftDeleteCategoryEndpoint>();
        _ = AddClient<IRestoreCategoryEndpoint>();

        _ = AddClient<ICreatePublisherEndpoint>();
        _ = AddClient<IUpdatePublisherEndpoint>();
        _ = AddClient<ISoftDeletePublisherEndpoint>();
        _ = AddClient<IRestorePublisherEndpoint>();

        // System endpoints
        _ = AddClient<IGetAllBooksAdminEndpoint>();
        _ = AddClient<IRebuildProjectionsEndpoint>();
        _ = AddClient<IGetProjectionStatusEndpoint>();

        // Identity endpoints
        // Note: Login/Register don't strictly need auth header, but it doesn't hurt.
        // Refresh token endpoint DOES need auth header if we implement "rotate me" logic dependent on old token,
        // but typically refresh endpoint uses credentials or refresh token in body.
        _ = AddClient<IIdentityLoginEndpoint>();
        _ = AddClient<IIdentityRegisterEndpoint>();
        _ = AddClient<IIdentityRefreshEndpoint>();
        _ = AddClient<IIdentityManageInfoEndpoint>();

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
}
