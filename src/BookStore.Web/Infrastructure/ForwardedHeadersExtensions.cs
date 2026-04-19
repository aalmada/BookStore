using Microsoft.AspNetCore.HttpOverrides;

namespace BookStore.Web.Infrastructure;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection ConfigureSecureForwardedHeaders(this IServiceCollection services)
        => services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            // Keep framework defaults for KnownIPNetworks/KnownProxies to avoid
            // trusting arbitrary X-Forwarded-* headers.
        });
}
