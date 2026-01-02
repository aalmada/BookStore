using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring application services
/// </summary>
public static class ApplicationServicesExtensions
{
    /// <summary>
    /// Configures all application services including pagination, OpenAPI, versioning, localization, etc.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Problem details for error handling
        _ = services.AddProblemDetails();

        // Configure pagination options with validation
        _ = services.AddOptions<Infrastructure.PaginationOptions>()
            .BindConfiguration(Infrastructure.PaginationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure OpenAPI with metadata
        _ = services.AddOpenApi(options => options.AddBookStoreApiDocumentation());

        // Configure API Versioning (header-based)
        AddApiVersioning(services);

        // Configure localization
        AddLocalization(services, configuration);

        // Add SignalR for real-time notifications
        _ = services.AddSignalR();

        // Add Blob Storage service
        _ = services.AddSingleton<Services.BlobStorageService>();

        // Add Marten health checks
        _ = services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("bookstore")!);

        // Add response caching for performance
        _ = services.AddResponseCaching();
        _ = services.AddOutputCache();

#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        _ = services.AddHybridCache();
#pragma warning restore EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        // Configure Identity with JWT authentication
        AddIdentityServices(services);

        return services;
    }

    static void AddApiVersioning(IServiceCollection services) => services.AddApiVersioning(options =>
                                                                      {
                                                                          options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
                                                                          options.AssumeDefaultVersionWhenUnspecified = true;
                                                                          options.ReportApiVersions = true;
                                                                          options.ApiVersionReader = new Asp.Versioning.HeaderApiVersionReader("api-version");
                                                                      });

#pragma warning disable IDE0060 // Remove unused parameter
    static void AddLocalization(IServiceCollection services, IConfiguration configuration)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        // Configure localization from appsettings.json with validation
        _ = services.AddOptions<Infrastructure.LocalizationOptions>()
            .BindConfiguration(Infrastructure.LocalizationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        _ = services.AddLocalization();
        _ = services.AddOptions<RequestLocalizationOptions>()
            .Configure<IOptions<Infrastructure.LocalizationOptions>>((options, localizationOptions) =>
            {
                var localization = localizationOptions.Value;

                _ = options.SetDefaultCulture(localization.DefaultCulture)
                    .AddSupportedCultures(localization.SupportedCultures)
                    .AddSupportedUICultures(localization.SupportedCultures);
            });
    }

    static void AddIdentityServices(IServiceCollection services)
    {
        // Add Identity with custom Marten user store
        // AddIdentityApiEndpoints already configures Bearer token authentication
        _ = services.AddIdentityApiEndpoints<Models.ApplicationUser>()
            .AddUserStore<Identity.MartenUserStore>();

        // Add authorization services
        _ = services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    }
}
