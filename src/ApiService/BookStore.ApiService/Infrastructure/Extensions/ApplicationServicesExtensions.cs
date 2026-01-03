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

                // Passkey options
                var passkeyDomain = configuration["Authentication:Passkey:ServerDomain"];
                if (!string.IsNullOrEmpty(passkeyDomain))
                {
                    options.Passkey.ServerDomain = passkeyDomain;
                }
            })
            .AddUserStore<Identity.MartenUserStore>();
        
        // Add roles support not needed via AddRoles (which requires IRoleStore), 
        // as we use simple string roles on the user object via MartenUserStore implementation of IUserRoleStore.
        
        // Add HttpContextAccessor required for SignInManager
        _ = services.AddHttpContextAccessor();
        
        // Add SignInManager separately
        _ = services.AddScoped<Microsoft.AspNetCore.Identity.SignInManager<Models.ApplicationUser>>();

        // Add JWT Bearer authentication
        var jwtSettings = configuration.GetSection("Jwt");
        _ = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
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
                };
            });

        // Add JWT token service
        _ = services.AddSingleton<Services.JwtTokenService>();

        // Add authorization services
        _ = services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    }
}
