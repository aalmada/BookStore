using System.Security.Cryptography;
using BookStore.Shared.Validation;
using Marten;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring application services
/// </summary>
public static class ApplicationServicesExtensions
{
    const int MinimumHs256SecretKeyBytes = 32;
    const int MinimumHs256DistinctCharacters = 4;
    const string DefaultHs256SecretKey = "your-secret-key-must-be-at-least-32-characters-long-for-hs256";

    static readonly HashSet<string> KnownPlaceholderHs256SecretNormalizations = new(StringComparer.Ordinal)
    {
        NormalizeJwtSecretForComparison(DefaultHs256SecretKey),
        NormalizeJwtSecretForComparison("change-me"),
        NormalizeJwtSecretForComparison("changeme"),
        NormalizeJwtSecretForComparison("default-secret"),
        NormalizeJwtSecretForComparison("jwt-secret"),
        NormalizeJwtSecretForComparison("secret-key"),
        NormalizeJwtSecretForComparison("replace-with-secure-key")
    };

    /// <summary>
    /// Configures all application services including pagination, OpenAPI, versioning, localization, etc.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
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

        _ = services.AddOptions<Identity.AccountCleanupOptions>()
            .BindConfiguration(Identity.AccountCleanupOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register Unverified Account Cleanup background service
        _ = services.AddHostedService<Infrastructure.Services.UnverifiedAccountCleanupService>();

        // Add HybridCache for L1 (in-memory) + L2 (Redis) caching
        _ = services.AddHybridCache();

        // Register Marten Projection Commit Listener in DI
        _ = services.AddSingleton<Infrastructure.ProjectionCommitListener>();

        // Configure Identity with JWT authentication
        AddIdentityServices(services, configuration, environment);

        // Log a startup warning when production uses the default HS256 algorithm.
        _ = services.AddHostedService(sp => new Infrastructure.Services.JwtAlgorithmWarningService(
            configuration.GetSection("Jwt"),
            environment.EnvironmentName,
            environment.IsDevelopment(),
            sp.GetRequiredService<ILogger<Infrastructure.Services.JwtAlgorithmWarningService>>()));

        // Configure Forwarded Headers
        AddForwardedHeaders(services);

        return services;
    }

    static void AddForwardedHeaders(IServiceCollection services) => _ = services.Configure<ForwardedHeadersOptions>(options =>
                                                                         {
                                                                             options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                                                                             options.ForwardLimit = 1;
                                                                             options.RequireHeaderSymmetry = true;
                                                                             // Keep framework defaults for KnownNetworks/KnownProxies
                                                                             // to avoid trusting arbitrary X-Forwarded-* headers.
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

    static void AddIdentityServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Add core Identity services without API endpoints (we'll use custom JWT endpoints)
        _ = services.AddIdentityCore<Models.ApplicationUser>(options =>
            {
                // Password requirements
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = PasswordValidator.MinLength;

                // Require email confirmation for login
                options.SignIn.RequireConfirmedEmail = true;

                // Explicit lockout configuration (do not rely on defaults)
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

            })
            .AddUserStore<Identity.MartenUserStore>()
            .AddPasswordValidator<Infrastructure.Identity.MaximumLengthPasswordValidator<Models.ApplicationUser>>()
            .AddSignInManager() // This registers SignInManager and IPasskeyHandler
            .AddDefaultTokenProviders();

        // Configure Passkey options
        _ = services.Configure<Microsoft.AspNetCore.Identity.IdentityPasskeyOptions>(options =>
        {
            var passkeyDomain = configuration["Authentication:Passkey:ServerDomain"];
            options.ServerDomain = !string.IsNullOrEmpty(passkeyDomain) ? passkeyDomain : "localhost";
            options.AuthenticatorTimeout = TimeSpan.FromMinutes(2);
            options.ChallengeSize = 32;
            options.UserVerificationRequirement = "required";
            options.ResidentKeyRequirement = "required";
            options.AttestationConveyancePreference = "none";
            options.IsAllowedAlgorithm = algorithm => algorithm is -7 or -8;

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
                if (!Uri.TryCreate(context.Origin, UriKind.Absolute, out var originUri))
                {
                    return ValueTask.FromResult(false);
                }

                // Only allow HTTP for localhost in Development environment
                var env = context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var isSecureOrigin = originUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                                     || (env.IsDevelopment() && originUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

                if (isSecureOrigin && allowedOrigins.Contains(context.Origin, StringComparer.OrdinalIgnoreCase))
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
        var algorithm = JwtAlgorithmResolver.Resolve(jwtSettings);
        ValidateJwtConfiguration(jwtSettings, algorithm, environment);

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
                    IssuerSigningKey = CreateJwtValidationKey(jwtSettings, algorithm),
                    ClockSkew = TimeSpan.FromSeconds(30), // Allow minor client/server clock drift without relaxing security too much
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };

                // Validate security stamp on each request to detect token revocation
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        // If authentication failed (either by exception or by calling context.Fail()), ensure 401 is returned
                        if (context.AuthenticateFailure != null || !string.IsNullOrEmpty(context.Error))
                        {
                            context.HandleResponse(); // Prevent default challenge behavior
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/problem+json";
                            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Status = 401,
                                Title = "Unauthorized",
                                Detail = "Authentication is required to access this resource.",
                                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
                            };
                            problemDetails.Extensions["error"] = context.Error ?? "unauthorized";
                            await context.Response.WriteAsJsonAsync(problemDetails);
                        }
                    },
                    OnTokenValidated = async context =>
                    {
                        try
                        {
                            var session = context.HttpContext.RequestServices.GetRequiredService<Marten.IDocumentSession>();
                            var cache = context.HttpContext.RequestServices.GetRequiredService<HybridCache>();
                            var tenantContext = context.HttpContext.RequestServices.GetRequiredService<Infrastructure.Tenant.ITenantContext>();
                            var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                            if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                            {
                                var cacheKey = SecurityStampCache.GetCacheKey(tenantContext.TenantId, userGuid);
                                var cacheTag = SecurityStampCache.GetCacheTag(tenantContext.TenantId, userGuid);

                                const string missingUserSentinel = "__missing__";

                                var currentSecurityStamp = await cache.GetOrCreateAsync(
                                    key: cacheKey,
                                    factory: async cancellationToken =>
                                    {
                                        // Use Query instead of Load to bypass Marten's identity map caching
                                        var user = await session.Query<Models.ApplicationUser>()
                                            .FirstOrDefaultAsync(u => u.Id == userGuid, cancellationToken);

                                        return user?.SecurityStamp ?? missingUserSentinel;
                                    },
                                    options: SecurityStampCache.CreateEntryOptions(),
                                    tags: [cacheTag],
                                    cancellationToken: context.HttpContext.RequestAborted);

                                if (currentSecurityStamp == missingUserSentinel)
                                {
                                    // CRITICAL: Clear the principal
                                    context.HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
                                    context.Fail("User not found.");
                                    return;
                                }

                                // Get security stamp from token (claim is required)
                                var tokenSecurityStamp = context.Principal?.FindFirst("security_stamp")?.Value;

                                if (string.IsNullOrEmpty(tokenSecurityStamp))
                                {
                                    context.HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
                                    context.Fail("Token missing required security stamp claim.");
                                    return;
                                }

                                if (string.IsNullOrEmpty(currentSecurityStamp) || tokenSecurityStamp != currentSecurityStamp)
                                {
                                    // CRITICAL: Clear the principal to prevent downstream middleware from seeing an authenticated user
                                    context.HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
                                    context.Fail("Token has been revoked due to security stamp change.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("BookStore.ApiService.TokenValidation");
                            Logging.Log.Infrastructure.TokenValidationUnexpectedError(logger, ex);
                            context.HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
                            context.Fail("Authentication failed.");
                        }
                    }
                };
            })
            // Add cookies required by SignInManager
            .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme)
            .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme)
            .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme);

        // Add JWT token service
        _ = services.AddSingleton<BookStore.ApiService.Services.JwtTokenService>();

        // Configure Email Services
        _ = services.AddOptions<Infrastructure.Email.EmailOptions>()
            .BindConfiguration(Infrastructure.Email.EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => environment.IsDevelopment() || !string.Equals(options.DeliveryMethod, "None", StringComparison.OrdinalIgnoreCase),
                "Email:DeliveryMethod cannot be 'None' outside Development. Use 'Logging' or 'Smtp'.")
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

    static void ValidateJwtConfiguration(IConfigurationSection jwtSettings, string algorithm, IWebHostEnvironment environment)
    {
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("JWT Issuer and Audience must be configured.");
        }

        if (algorithm == JwtAlgorithmResolver.JwtAlgorithmRs256)
        {
            var privateKeyPem = jwtSettings["RS256:PrivateKeyPem"];
            var publicKeyPem = jwtSettings["RS256:PublicKeyPem"];
            if (string.IsNullOrWhiteSpace(privateKeyPem) || string.IsNullOrWhiteSpace(publicKeyPem))
            {
                throw new InvalidOperationException("RS256 requires both Jwt:RS256:PrivateKeyPem and Jwt:RS256:PublicKeyPem.");
            }

            return;
        }

        if (algorithm != JwtAlgorithmResolver.JwtAlgorithmHs256)
        {
            throw new InvalidOperationException($"Unsupported JWT algorithm: {algorithm}");
        }

        var secretKey = jwtSettings["SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("HS256 requires Jwt:SecretKey to be configured.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(secretKey) < MinimumHs256SecretKeyBytes)
        {
            throw new InvalidOperationException("HS256 requires Jwt:SecretKey to be at least 32 bytes when UTF-8 encoded.");
        }

        if (!environment.IsDevelopment())
        {
            var weakSecretError = GetNonDevelopmentHs256SecretValidationError(secretKey);
            if (!string.IsNullOrWhiteSpace(weakSecretError))
            {
                throw new InvalidOperationException(
                    $"HS256 Jwt:SecretKey is too weak for non-development environments: {weakSecretError}. " +
                    "Provide a high-entropy secret from a secure secret store, or prefer RS256 for production deployments.");
            }
        }
    }

    internal static string? GetNonDevelopmentHs256SecretValidationError(string secretKey)
    {
        if (IsAllIdenticalCharacters(secretKey))
        {
            return "the key cannot be made of a single repeated character";
        }

        if (CountDistinctCharacters(secretKey) < MinimumHs256DistinctCharacters)
        {
            return $"the key must contain at least {MinimumHs256DistinctCharacters} distinct characters";
        }

        if (IsKnownPlaceholderHs256Secret(secretKey))
        {
            return "the key matches a known placeholder/default value";
        }

        return null;
    }

    internal static bool IsAllIdenticalCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var firstCharacter = value[0];
        for (var index = 1; index < value.Length; index++)
        {
            if (value[index] != firstCharacter)
            {
                return false;
            }
        }

        return true;
    }

    internal static int CountDistinctCharacters(string value)
    {
        var distinctCharacters = new HashSet<char>();
        foreach (var character in value)
        {
            _ = distinctCharacters.Add(character);
        }

        return distinctCharacters.Count;
    }

    internal static bool IsKnownPlaceholderHs256Secret(string secretKey)
        => KnownPlaceholderHs256SecretNormalizations.Contains(NormalizeJwtSecretForComparison(secretKey));

    internal static string NormalizeJwtSecretForComparison(string secretKey)
    {
        var normalized = new System.Text.StringBuilder(secretKey.Length);
        foreach (var character in secretKey)
        {
            if (char.IsLetterOrDigit(character))
            {
                _ = normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
    }

    static SecurityKey CreateJwtValidationKey(IConfigurationSection jwtSettings, string algorithm) => algorithm switch
    {
        JwtAlgorithmResolver.JwtAlgorithmHs256 => CreateHs256ValidationKey(jwtSettings),
        JwtAlgorithmResolver.JwtAlgorithmRs256 => CreateRs256ValidationKey(jwtSettings),
        _ => throw new InvalidOperationException($"Unsupported JWT algorithm: {algorithm}")
    };

    static SecurityKey CreateHs256ValidationKey(IConfigurationSection jwtSettings)
    {
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");

        if (System.Text.Encoding.UTF8.GetByteCount(secretKey) < MinimumHs256SecretKeyBytes)
        {
            throw new InvalidOperationException("JWT HS256 SecretKey must be at least 32 bytes when UTF-8 encoded");
        }

        return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey));
    }

    static SecurityKey CreateRs256ValidationKey(IConfigurationSection jwtSettings)
    {
        var publicKeyPem = jwtSettings["RS256:PublicKeyPem"];
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            throw new InvalidOperationException("JWT RS256:PublicKeyPem not configured");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem.ToCharArray());
        return new RsaSecurityKey(rsa);
    }
}
