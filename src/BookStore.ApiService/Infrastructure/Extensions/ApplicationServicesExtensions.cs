using Microsoft.AspNetCore.HttpOverrides;
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
        AddIdentityServices(services, configuration);

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

                // Require email confirmation for login
                options.SignIn.RequireConfirmedEmail = true;

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
        var secretKey = jwtSettings["SecretKey"];
        var environment = services.BuildServiceProvider().GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        // SECURITY: Validate JWT secret key in production
        if (!environment.IsDevelopment())
        {
            if (string.IsNullOrEmpty(secretKey) ||
                secretKey == "your-secret-key-must-be-at-least-32-characters-long-for-hs256")
            {
                throw new InvalidOperationException(
                    "Production JWT secret key must be configured via environment variables or secure key vault. " +
                    "Set the 'Jwt:SecretKey' configuration value. Never use the default key in production.");
            }
        }

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
                            System.Text.Encoding.UTF8.GetBytes(secretKey!)),
                    ClockSkew = TimeSpan.Zero, // Remove default 5 minute clock skew
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };

                // Validate security stamp on each request to detect token revocation
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] OnMessageReceived - Path: {context.Request.Path}\n");
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] OnAuthenticationFailed - Exception: {context.Exception?.Message}\n");
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] OnChallenge - Error: {context.Error}, ErrorDescription: {context.ErrorDescription}, HasFailure: {context.AuthenticateFailure != null}\n");

                        // If authentication failed (either by exception or by calling context.Fail()), ensure 401 is returned
                        if (context.AuthenticateFailure != null || !string.IsNullOrEmpty(context.Error))
                        {
                            System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] *** SETTING 401 RESPONSE ***\n");
                            context.HandleResponse(); // Prevent default challenge behavior
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = context.Error ?? "unauthorized",
                                error_description = context.ErrorDescription ?? context.AuthenticateFailure?.Message ?? "Authentication failed"
                            });
                            System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] *** 401 RESPONSE WRITTEN ***\n");
                        }
                    },
                    OnTokenValidated = async context =>
                    {
                        System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] ===== OnTokenValidated FIRED =====\n");

                        // Get user directly from Marten session to bypass identity map caching
                        var session = context.HttpContext.RequestServices.GetRequiredService<Marten.IDocumentSession>();
                        var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                        System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] OnTokenValidated - UserId: {userId}\n");

                        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                        {
                            // Use Query instead of Load to bypass Marten's identity map caching
                            var user = session.Query<Models.ApplicationUser>()
                                .Where(u => u.Id == userGuid)
                                .FirstOrDefault();

                            if (user != null)
                            {
                                // Get security stamp from token (null if claim doesn't exist)
                                var tokenSecurityStamp = context.Principal?.FindFirst("security_stamp")?.Value;

                                System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] TokenStamp={tokenSecurityStamp}, UserStamp={user.SecurityStamp}\n");

                                // Only validate if token HAS a security_stamp claim and user HAS a security stamp
                                // This allows old tokens without the claim to work (backward compatibility)
                                if (!string.IsNullOrEmpty(tokenSecurityStamp) &&
                                    !string.IsNullOrEmpty(user.SecurityStamp) &&
                                    tokenSecurityStamp != user.SecurityStamp)
                                {
                                    System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] *** REJECTING TOKEN - STAMP MISMATCH ***\n");
                                    // CRITICAL: Clear the principal to prevent downstream middleware from seeing an authenticated user
                                    context.HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
                                    context.Fail("Token has been revoked due to security stamp change.");
                                }
                            }
                            else
                            {
                                System.IO.File.AppendAllText("/tmp/jwt_events.txt", $"[{DateTimeOffset.UtcNow:HH:mm:ss}] *** REJECTING TOKEN - USER NOT FOUND ***\n");
                                // CRITICAL: Clear the principal
                                context.HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
                                context.Fail("User not found.");
                            }
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
