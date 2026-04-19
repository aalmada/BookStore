using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;

namespace BookStore.ApiService.Infrastructure.Services;

public sealed class JwtAlgorithmWarningService(
    IConfigurationSection jwtSettings,
    string environmentName,
    bool isDevelopment,
    ILogger<JwtAlgorithmWarningService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var algorithm = JwtAlgorithmResolver.Resolve(jwtSettings);

        if (!isDevelopment && algorithm == JwtAlgorithmResolver.JwtAlgorithmHs256)
        {
            Log.Infrastructure.JwtHs256ConfiguredInProduction(logger, environmentName);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
