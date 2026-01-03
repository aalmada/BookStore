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
        AddClient<IGetBooksEndpoint>();
        AddClient<IGetBookEndpoint>();
        AddClient<IGetAuthorsEndpoint>();
        AddClient<IGetAuthorEndpoint>();
        AddClient<IGetCategoriesEndpoint>();
        AddClient<IGetCategoryEndpoint>();
        AddClient<IGetPublishersEndpoint>();
        AddClient<IGetPublisherEndpoint>();

        // Admin endpoints
        AddClient<ICreateBookEndpoint>();
        AddClient<IUpdateBookEndpoint>();
        AddClient<ISoftDeleteBookEndpoint>();
        AddClient<IRestoreBookEndpoint>();
        AddClient<IUploadBookCoverEndpoint>();
        
        AddClient<ICreateAuthorEndpoint>();
        AddClient<IUpdateAuthorEndpoint>();
        AddClient<ISoftDeleteAuthorEndpoint>();
        AddClient<IRestoreAuthorEndpoint>();
        
        AddClient<ICreateCategoryEndpoint>();
        AddClient<IUpdateCategoryEndpoint>();
        AddClient<ISoftDeleteCategoryEndpoint>();
        AddClient<IRestoreCategoryEndpoint>();
        
        AddClient<ICreatePublisherEndpoint>();
        AddClient<IUpdatePublisherEndpoint>();
        AddClient<ISoftDeletePublisherEndpoint>();
        AddClient<IRestorePublisherEndpoint>();

        // System endpoints
        AddClient<IGetAllBooksAdminEndpoint>();
        AddClient<IRebuildProjectionsEndpoint>();
        AddClient<IGetProjectionStatusEndpoint>();

        // Identity endpoints
        // Note: Login/Register don't strictly need auth header, but it doesn't hurt.
        // Refresh token endpoint DOES need auth header if we implement "rotate me" logic dependent on old token,
        // but typically refresh endpoint uses credentials or refresh token in body.
        AddClient<IIdentityLoginEndpoint>();
        AddClient<IIdentityRegisterEndpoint>();
        AddClient<IIdentityRefreshEndpoint>();
        AddClient<IIdentityManageInfoEndpoint>();

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
        Action<IHttpClientBuilder> configureResilience)
    {
        return services.AddBookStoreClient(baseAddress, configureResilience);
    }
}
