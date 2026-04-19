using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.Identity;

public sealed class PasskeyAllowedOriginsOptions
{
    public const string SectionName = "Authentication:Passkey";

    public string[] AllowedOrigins { get; set; } = [];

    internal static string[] NormalizeOrigins(string[]? origins)
    {
        if (origins is null || origins.Length == 0)
        {
            return [];
        }

        var normalizedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in origins)
        {
            if (TryNormalizeOrigin(origin, out var normalizedOrigin))
            {
                _ = normalizedOrigins.Add(normalizedOrigin);
                continue;
            }

            var trimmedOrigin = origin?.Trim();
            if (!string.IsNullOrEmpty(trimmedOrigin))
            {
                _ = normalizedOrigins.Add(trimmedOrigin);
            }
        }

        return [.. normalizedOrigins];
    }

    internal static bool TryNormalizeOrigin(string? origin, out string normalizedOrigin)
    {
        normalizedOrigin = string.Empty;

        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var trimmedOrigin = origin.Trim();
        if (!Uri.TryCreate(trimmedOrigin, UriKind.Absolute, out var originUri))
        {
            return false;
        }

        if (!originUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !originUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasNonRootPath = !string.IsNullOrEmpty(originUri.AbsolutePath)
                             && !string.Equals(originUri.AbsolutePath, "/", StringComparison.Ordinal);

        if (hasNonRootPath
            || !string.IsNullOrEmpty(originUri.Query)
            || !string.IsNullOrEmpty(originUri.Fragment)
            || !string.IsNullOrEmpty(originUri.UserInfo))
        {
            return false;
        }

        normalizedOrigin = originUri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}

public sealed class PasskeyAllowedOriginsOptionsValidator(IWebHostEnvironment environment)
    : IValidateOptions<PasskeyAllowedOriginsOptions>
{
    public ValidateOptionsResult Validate(string? name, PasskeyAllowedOriginsOptions options)
    {
        var failures = new List<string>();
        var allowedOrigins = options.AllowedOrigins ?? [];

        if (!environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            failures.Add("Authentication:Passkey:AllowedOrigins must contain at least one allowed origin outside Development.");
        }

        for (var index = 0; index < allowedOrigins.Length; index++)
        {
            var origin = allowedOrigins[index];
            if (string.IsNullOrWhiteSpace(origin))
            {
                failures.Add($"Authentication:Passkey:AllowedOrigins[{index}] must be a non-empty absolute http/https origin.");
                continue;
            }

            if (!PasskeyAllowedOriginsOptions.TryNormalizeOrigin(origin, out _))
            {
                failures.Add($"Authentication:Passkey:AllowedOrigins[{index}] value '{origin}' is invalid. Use an absolute http/https origin without path, query, or fragment (for example, 'https://localhost:7260').");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
