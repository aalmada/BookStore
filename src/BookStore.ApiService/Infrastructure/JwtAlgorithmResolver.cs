using Microsoft.Extensions.Configuration;

namespace BookStore.ApiService.Infrastructure;

static class JwtAlgorithmResolver
{
    public const string JwtAlgorithmHs256 = "HS256";
    public const string JwtAlgorithmRs256 = "RS256";

    public static string Resolve(IConfigurationSection jwtSettings)
    {
        var configuredAlgorithm = jwtSettings["Algorithm"];
        if (!string.IsNullOrWhiteSpace(configuredAlgorithm))
        {
            return configuredAlgorithm.ToUpperInvariant();
        }

        return HasRs256KeyPair(jwtSettings)
            ? JwtAlgorithmRs256
            : JwtAlgorithmHs256;
    }

    static bool HasRs256KeyPair(IConfigurationSection jwtSettings)
    {
        var privateKeyPem = jwtSettings["RS256:PrivateKeyPem"];
        var publicKeyPem = jwtSettings["RS256:PublicKeyPem"];

        return !string.IsNullOrWhiteSpace(privateKeyPem) && !string.IsNullOrWhiteSpace(publicKeyPem);
    }
}
