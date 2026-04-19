using BookStore.ApiService.Infrastructure.Logging;

namespace BookStore.ApiService.Infrastructure.Services;

public sealed class JwtAlgorithmWarningService(
    string? configuredAlgorithm,
    string environmentName,
    bool isDevelopment,
    ILogger<JwtAlgorithmWarningService> logger) : IHostedService
{
    const string JwtAlgorithmHs256 = "HS256";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var algorithm = (configuredAlgorithm ?? JwtAlgorithmHs256).ToUpperInvariant();

        if (!isDevelopment && algorithm == JwtAlgorithmHs256)
        {
            Log.Infrastructure.JwtHs256ConfiguredInProduction(logger, environmentName);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
