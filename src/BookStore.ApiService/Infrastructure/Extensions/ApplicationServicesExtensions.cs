using BookStore.ApiService.Infrastructure.Auth;
using BookStore.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
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
        _ = services.AddExceptionHandler<BookStore.ApiService.Infrastructure.ExceptionHandlers.GlobalExceptionHandler>();

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
        AddLocalization(services);

        // Configure currency
        AddCurrency(services);

        // Add SSE for real-time notifications
        // Uses Redis pub/sub when available (Aspire), falls back to in-memory gracefully
        _ = services.AddSingleton<Infrastructure.Notifications.INotificationService>(sp =>
        {
            // Try to get Redis connection - it may not be available in test environments
            var redis = sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<Infrastructure.Notifications.RedisNotificationService>>();

            return new Infrastructure.Notifications.RedisNotificationService(redis!, logger);
        });

        // Add Blob Storage service
        _ = services.AddSingleton<BookStore.ApiService.Services.BlobStorageService>();

        // Add HybridCache for L1 (in-memory) + L2 (Redis) caching
        _ = services.AddHybridCache();

        // Register Marten Projection Commit Listener in DI
        _ = services.AddSingleton<Infrastructure.ProjectionCommitListener>();

        // Configure email services
        AddEmailServices(services);

        // Configure Keycloak authentication
        AddKeycloakAuthentication(services, configuration);

        // Configure Forwarded Headers
        AddForwardedHeaders(services);

        return services;
    }

    static void AddForwardedHeaders(IServiceCollection services) => _ = services.Configure<ForwardedHeadersOptions>(options =>
                                                                         {
                                                                             options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                                                                             // Clear known networks/proxies to trust standard proxies in the environment (Aspire/Docker)
                                                                             options.KnownIPNetworks.Clear();
                                                                             options.KnownProxies.Clear();
                                                                             options.ForwardLimit = null;
                                                                             options.RequireHeaderSymmetry = false;
                                                                         });

    static void AddApiVersioning(IServiceCollection services) => services.AddApiVersioning(options =>
                                                                      {
                                                                          options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
                                                                          options.AssumeDefaultVersionWhenUnspecified = true;
                                                                          options.ReportApiVersions = true;
                                                                          options.ApiVersionReader = new Asp.Versioning.HeaderApiVersionReader("api-version");
                                                                      });

    static void AddLocalization(IServiceCollection services)
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

    static void AddCurrency(IServiceCollection services)
        // Configure currency from appsettings.json with validation
        => _ = services.AddOptions<Infrastructure.CurrencyOptions>()
            .BindConfiguration(Infrastructure.CurrencyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

    static void AddEmailServices(IServiceCollection services)
    {
        // Configure Email Services
        _ = services.AddOptions<Infrastructure.Email.EmailOptions>()
            .BindConfiguration(Infrastructure.Email.EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        _ = services.AddSingleton<Infrastructure.Email.EmailTemplateService>();

        // Register IEmailService conditionally based on DeliveryMethod
        _ = services.AddSingleton<Infrastructure.Email.IEmailService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<Infrastructure.Email.EmailOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<Infrastructure.Email.LoggingEmailService>>();
            var smtpLogger = sp.GetRequiredService<ILogger<Infrastructure.Email.SmtpEmailService>>();

            return options.DeliveryMethod?.ToLowerInvariant() switch
            {
                "smtp" => new Infrastructure.Email.SmtpEmailService(sp.GetRequiredService<IOptions<Infrastructure.Email.EmailOptions>>(), smtpLogger),
                // Logging enabled if method is Logging, OR if default behavior is needed.
                // If "None", we still register LoggingEmailService but it might no-op if checking inside,
                // OR we can implement a pure NoOp service.
                // Our LoggingEmailService checks options.DeliveryMethod == "Logging".
                _ => new Infrastructure.Email.LoggingEmailService(logger, sp.GetRequiredService<IOptions<Infrastructure.Email.EmailOptions>>())
            };
        });
    }

    static void AddKeycloakAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var isDevelopment = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"],
            "Development",
            StringComparison.OrdinalIgnoreCase);

        _ = services.AddAuthentication()
            .AddKeycloakJwtBearer(ResourceNames.Keycloak, realm: "bookstore", options =>
            {
                options.Audience = "bookstore-api";
                if (isDevelopment)
                {
                    options.RequireHttpsMetadata = false;
                }
            });

        _ = services.AddSingleton<IClaimsTransformation, KeycloakRoleClaimsTransformation>();

        _ = services.AddOptions<KeycloakAdminOptions>()
            .BindConfiguration(KeycloakAdminOptions.SectionName)
            .ValidateOnStart();

        _ = services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>();

        // Add authorization services
        _ = services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin", "ADMIN"));
    }
}
