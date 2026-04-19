namespace BookStore.ApiService.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public const string HstsValue = "max-age=31536000; includeSubDomains; preload";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            SetHeaderIfMissing(headers, "X-Content-Type-Options", "nosniff");
            SetHeaderIfMissing(headers, "X-Frame-Options", "DENY");
            SetHeaderIfMissing(headers, "Referrer-Policy", "no-referrer");
            SetHeaderIfMissing(headers, "Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            SetHeaderIfMissing(headers, "Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");

            // HSTS should only be emitted in non-development HTTPS environments.
            if (!environment.IsDevelopment() && context.Request.IsHttps)
            {
                SetHeaderIfMissing(headers, "Strict-Transport-Security", HstsValue);
            }

            return Task.CompletedTask;
        });

        await next(context);
    }

    static void SetHeaderIfMissing(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}
