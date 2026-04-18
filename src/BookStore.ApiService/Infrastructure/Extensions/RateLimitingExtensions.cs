using System.Text.Json;
using System.Threading.RateLimiting;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookStore.ApiService.Infrastructure.Extensions;

public static class RateLimitingExtensions
{
    internal const string AuthRateLimitEmailItemKey = "AuthRateLimitEmail";

    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        var rateLimitOptions = new RateLimitOptions();
        configuration.GetSection(RateLimitOptions.SectionName).Bind(rateLimitOptions);

        // Check if rate limiting should be disabled (for tests)
        var disableRateLimiting = configuration.GetValue<bool>("RateLimit:Disabled");

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
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            double? retryAfterSeconds = null;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                retryAfterSeconds = retryAfter.TotalSeconds;
            }

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                retryAfter = retryAfterSeconds
            }, cancellationToken);
        };
    });

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
