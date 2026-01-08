using Microsoft.AspNetCore.Identity;
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
        _ = services.AddSingleton<Services.BlobStorageService>();

        // Add HybridCache for L1 (in-memory) + L2 (Redis) caching
        _ = services.AddHybridCache();

        // Configure Identity with JWT authentication
        AddIdentityServices(services, configuration);

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

    static void AddIdentityServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add core Identity services without API endpoints (we'll use custom JWT endpoints)
        _ = services.AddIdentityCore<Models.ApplicationUser>(options =>
            {
                // Password requirements
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

            })
            .AddUserStore<Identity.MartenUserStore>()
            .AddSignInManager() // This registers SignInManager and IPasskeyHandler
            .AddDefaultTokenProviders();

        // Configure Passkey options
        _ = services.Configure<Microsoft.AspNetCore.Identity.IdentityPasskeyOptions>(options =>
        {
            var passkeyDomain = configuration["Authentication:Passkey:ServerDomain"];
            options.ServerDomain = !string.IsNullOrEmpty(passkeyDomain) ? passkeyDomain : "localhost";
            options.AuthenticatorTimeout = TimeSpan.FromMinutes(2);
            options.ChallengeSize = 32;

            // Configure origin validation to allow Web app origin
            // By default, ASP.NET Core Identity rejects cross-origin passkey requests
            // We need to explicitly allow the Web app origin (https://localhost:7260)
            options.ValidateOrigin = context =>
            {
                // Allow same-origin requests (when API is called directly)
                if (!context.CrossOrigin)
                {
                    return ValueTask.FromResult(true);
                }

                // Allow requests from the Web app (even if marked as same-origin by browser)
                var allowedOrigins = configuration.GetSection("Authentication:Passkey:AllowedOrigins").Get<string[]>() ?? [];
                if (allowedOrigins.Contains(context.Origin, StringComparer.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult(true);
                }

                return ValueTask.FromResult(false);
            };
        });

        // Add roles support not needed via AddRoles (which requires IRoleStore), 
        // as we use simple string roles on the user object via MartenUserStore implementation of IUserRoleStore.

        // Add HttpContextAccessor required for SignInManager
        _ = services.AddHttpContextAccessor();

        // Add JWT Bearer authentication
        var jwtSettings = configuration.GetSection("Jwt");
        _ = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
                ClockSkew = TimeSpan.Zero // Remove default 5 minute clock skew
            })
            // Add cookies required by SignInManager
            .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme)
            .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme)
            .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme);

        // Add JWT token service
        _ = services.AddSingleton<Services.JwtTokenService>();

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

        // Add authorization services
        _ = services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin", "ADMIN"));
    }
}
