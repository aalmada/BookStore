using System.Text.Json;
using System.Threading.RateLimiting;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Extensions;

public static class RateLimitingExtensions
{
    internal const string AuthRateLimitEmailItemKey = "AuthRateLimitEmail";

    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Disabling rate limiting is only permitted in development and test environments.
        var disableRateLimiting = configuration.GetValue<bool>("RateLimit:Disabled");
        if (disableRateLimiting && !environment.IsDevelopment() && !IsTestEnvironment(environment))
        {
            // Use a startup logger to emit the warning before the DI container is fully built
            var startupLogger = LoggerFactory.Create(b => b.AddConsole())
                .CreateLogger("BookStore.ApiService.RateLimiting");
            Log.Infrastructure.RateLimitingDisabledInProduction(startupLogger, environment.EnvironmentName);
        }

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            var rateLimitOptions = new RateLimitOptions();
            configuration.GetSection(RateLimitOptions.SectionName).Bind(rateLimitOptions);

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Disable rate limiting if configured
                if (disableRateLimiting)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                // Exempt health checks and metrics
                if (context.Request.Path.StartsWithSegments("/health") ||
                    context.Request.Path.StartsWithSegments("/metrics"))
                {
                    return RateLimitPartition.GetNoLimiter("exempt");
                }

                // Per-tenant rate limiting
                var tenantId = context.Items["TenantId"]?.ToString()
                    ?? JasperFx.StorageConstants.DefaultTenantId;

                return RateLimitPartition.GetFixedWindowLimiter(tenantId, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitOptions.WindowInMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 100
                    });
            });

            // Stricter rate limiting for authentication endpoints
            _ = options.AddPolicy("AuthPolicy", httpContext =>
            {
                // Disable rate limiting if configured
                if (disableRateLimiting)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var partitionKey = BuildAuthPolicyPartitionKey(httpContext);

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.AuthWindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitOptions.AuthQueueLimit
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                double? retryAfterSeconds = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = retryAfter.TotalSeconds;
                }

                await WriteRateLimitProblemDetailsAsync(context.HttpContext, retryAfterSeconds, cancellationToken);
            };
        });
    }

    static bool IsTestEnvironment(IWebHostEnvironment environment)
        => environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase) ||
        environment.EnvironmentName.Equals("Test", StringComparison.OrdinalIgnoreCase);

    internal static async Task WriteRateLimitProblemDetailsAsync(
        HttpContext context,
        double? retryAfterSeconds,
        CancellationToken cancellationToken)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please retry after the specified duration.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        };
        problemDetails.Extensions["error"] = ErrorCodes.Auth.RateLimitExceeded;
        if (retryAfterSeconds.HasValue)
        {
            problemDetails.Extensions["retryAfter"] = retryAfterSeconds.Value;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
    }

    public static IApplicationBuilder UseAuthRateLimitIdentityExtraction(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            if (ShouldExtractAuthEmail(context.Request))
            {
                context.Request.EnableBuffering();

                try
                {
                    using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                    if (TryGetAuthEmail(document.RootElement, out var email))
                    {
                        context.Items[AuthRateLimitEmailItemKey] = email;
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed JSON and continue without an email-specific partition.
                }
                finally
                {
                    context.Request.Body.Position = 0;
                }
            }

            await next();
        });

    internal static string BuildAuthPolicyPartitionKey(HttpContext context)
    {
        var tenantId = context.Items["TenantId"]?.ToString()
            ?? JasperFx.StorageConstants.DefaultTenantId;

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var email = context.Items[AuthRateLimitEmailItemKey]?.ToString();
        var normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? "anonymous"
            : email.Trim().ToUpperInvariant();

        return $"{tenantId}:{ipAddress}:{normalizedEmail}";
    }

    internal static bool TryGetAuthEmail(JsonElement body, out string email)
    {
        email = string.Empty;

        if (!body.TryGetProperty("email", out var emailProperty))
        {
            return false;
        }

        var value = emailProperty.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        email = value.Trim();
        return true;
    }

    static bool ShouldExtractAuthEmail(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments("/account"))
        {
            return false;
        }

        if (request.ContentLength is null or <= 0)
        {
            return false;
        }

        return request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
