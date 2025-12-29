using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    const string HealthEndpointPath = "/health";
    const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        _ = builder.ConfigureOpenTelemetry();

        _ = builder.AddDefaultHealthChecks();

        _ = builder.Services.AddServiceDiscovery();

        _ = builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            _ = http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            _ = http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Configure structured logging
        _ = builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Configure console logging for better structured output
        _ = builder.Logging.AddConsole(options => options.FormatterName = builder.Environment.IsDevelopment()
            ? "simple"  // Human-readable for development
            : "json");  // JSON for production/structured logging

        // Configure simple console formatter for development
        if (builder.Environment.IsDevelopment())
        {
            _ = builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = false;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                options.UseUtcTimestamp = true;
            });
        }
        else
        {
            // Configure JSON console formatter for production
            _ = builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                options.UseUtcTimestamp = true;
            });
        }

        _ = builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => _ = metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => _ = tracing.AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(tracing =>
                    // Exclude health check requests from tracing
                    tracing.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                        && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                )
                // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                //.AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation());

        _ = builder.AddOpenTelemetryExporters();

        return builder;
    }

    static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            _ = builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        _ = builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health check endpoints are essential for production (load balancers, orchestrators, monitoring).
        // These endpoints return minimal information (200 OK or 503 Service Unavailable).
        // For production deployments, use network-level protection (firewall rules) to restrict access
        // to trusted sources: load balancers, Kubernetes, monitoring systems, internal networks.
        // See https://aka.ms/dotnet/aspire/healthchecks for more details.

        // Readiness probe: All health checks must pass for app to be ready to accept traffic
        _ = app.MapHealthChecks(HealthEndpointPath);

        // Liveness probe: Only "live" tagged checks must pass for app to be considered alive
        _ = app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
