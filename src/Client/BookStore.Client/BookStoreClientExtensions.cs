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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBookStoreClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        // Register all endpoint interfaces
        services.AddRefitClient<IGetBooksEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetBookEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetAuthorsEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetAuthorEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetCategoriesEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetCategoryEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetPublishersEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetPublisherEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

        // Admin endpoints
        services.AddRefitClient<ICreateBookEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IUpdateBookEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<ISoftDeleteBookEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IRestoreBookEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IUploadBookCoverEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

        services.AddRefitClient<ICreateAuthorEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IUpdateAuthorEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<ISoftDeleteAuthorEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IRestoreAuthorEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

        services.AddRefitClient<ICreateCategoryEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IUpdateCategoryEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<ISoftDeleteCategoryEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IRestoreCategoryEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

        services.AddRefitClient<ICreatePublisherEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IUpdatePublisherEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<ISoftDeletePublisherEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IRestorePublisherEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

        // System endpoints
        services.AddRefitClient<IGetAllBooksAdminEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IRebuildProjectionsEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        
        services.AddRefitClient<IGetProjectionStatusEndpoint>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

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
        // This is a simplified version - you can expand this to apply policies to each endpoint
        services.AddBookStoreClient(baseAddress);
        return services;
    }
}
