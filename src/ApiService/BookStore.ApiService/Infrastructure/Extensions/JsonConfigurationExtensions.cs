namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring JSON serialization
/// </summary>
public static class JsonConfigurationExtensions
{
    /// <summary>
    /// Configures JSON serialization options for HTTP responses
    /// </summary>
    public static IServiceCollection AddJsonConfiguration(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            // Use web defaults (camelCase properties)
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

            // Serialize enums as strings (not integers) for better readability and API evolution
            options.SerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));

            // Pretty print in development for easier debugging
            options.SerializerOptions.WriteIndented = environment.IsDevelopment();

            // ISO 8601 date/time format is default in System.Text.Json
            // DateTimeOffset automatically serializes as: "2025-12-26T17:16:09.123Z"
        });

        return services;
    }
}
