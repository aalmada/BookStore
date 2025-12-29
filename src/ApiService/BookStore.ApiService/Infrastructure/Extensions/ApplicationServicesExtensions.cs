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
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Problem details for error handling
        services.AddProblemDetails();

        // Configure pagination options with validation
        services.AddOptions<Models.PaginationOptions>()
            .Bind(configuration.GetSection(Models.PaginationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure OpenAPI with metadata
        services.AddOpenApi(options => options.AddBookStoreApiDocumentation());

        // Configure API Versioning (header-based)
        AddApiVersioning(services);

        // Configure localization
        AddLocalization(services, configuration);

        // Add SignalR for real-time notifications
        services.AddSignalR();

        // Add Blob Storage service
        services.AddSingleton<Services.BlobStorageService>();

        // Add Marten health checks
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("bookstore")!);

        // Add response caching for performance
        services.AddResponseCaching();
        services.AddOutputCache();

        return services;
    }

    static void AddApiVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new Asp.Versioning.HeaderApiVersionReader("api-version");
        });
    }

    static void AddLocalization(IServiceCollection services, IConfiguration configuration)
    {
        // Configure localization from appsettings.json with validation
        services.AddOptions<Models.LocalizationOptions>()
            .Bind(configuration.GetSection(Models.LocalizationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddLocalization();
        services.AddOptions<RequestLocalizationOptions>()
            .Configure<IOptions<Models.LocalizationOptions>>((options, localizationOptions) =>
            {
                var localization = localizationOptions.Value;

                _ = options.SetDefaultCulture(localization.DefaultCulture)
                    .AddSupportedCultures(localization.SupportedCultures)
                    .AddSupportedUICultures(localization.SupportedCultures);
            });
    }
}
