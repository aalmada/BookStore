using System.Net;
using System.Text.Json;
using Blazored.LocalStorage;
using BookStore.Client;
using BookStore.Client.Infrastructure;
using BookStore.Client.Services;
using BookStore.Shared;
using BookStore.Web.Components;
using BookStore.Web.Infrastructure;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubFilter, LoggingHubFilter>();

builder.Services.AddHttpContextAccessor();

// Add Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Add application services
builder.Services.AddScoped<TenantService>();

// Configure Forwarded Headers to correctly capture client IP behind proxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear known networks/proxies to trust standard proxies in the environment (Aspire/Docker)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null;
    options.RequireHeaderSymmetry = false;
});

// Add MudBlazor services
builder.Services.AddMudServices();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("keycloak-token");

// Get API base URL from service discovery (Aspire)
var apiServiceUrl = builder.Configuration[$"services:{BookStore.ServiceDefaults.ResourceNames.ApiService}:https:0"]
                    ?? builder.Configuration[$"services:{BookStore.ServiceDefaults.ResourceNames.ApiService}:http:0"]
                    ?? "http://localhost:5000";

// Create resilience pipeline for API calls
// This provides retry, circuit breaker, and timeout protection for all API requests
var resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(500),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => (int)r.StatusCode >= 500)
            .Handle<HttpRequestException>()
    })
    .AddTimeout(TimeSpan.FromSeconds(30))
    .Build();

// Register BookStore API clients with authorization handler and resilience.
RegisterScopedRefitClients(builder.Services, new Uri(apiServiceUrl), resiliencePipeline);

static void RegisterScopedRefitClients(
    IServiceCollection services,
    Uri baseAddress,
    ResiliencePipeline<HttpResponseMessage> resiliencePipeline)
{
    // Register TenantService & Handler
    _ = services.AddScoped<TenantService>();
    _ = services.AddTransient<TenantHeaderHandler>();

    // Register ITenantsClient separately — no TenantHeaderHandler to avoid circular dep:
    // TenantService -> ITenantsClient -> TenantHeaderHandler -> TenantService.
    _ = services.AddScoped<ITenantsClient>(sp =>
    {
        var tokenAccessor = sp.GetRequiredService<KeycloakTokenAccessor>();
        var authenticationStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
        var networkHandler = new HttpClientHandler();
        var authHandler = new DefaultTenantAuthHandler(tokenAccessor, authenticationStateProvider)
        { InnerHandler = networkHandler };
        var httpClient = new HttpClient(authHandler) { BaseAddress = baseAddress };
        return RestService.For<ITenantsClient>(httpClient);
    });

    // Register IConfigurationClient separately (No auth required for public config endpoints)
    _ = services.AddScoped<IConfigurationClient>(_ =>
    {
        var httpClient = new HttpClient { BaseAddress = baseAddress };
        return RestService.For<IConfigurationClient>(httpClient);
    });

    // Register aggregated clients with Polly resilience
    // Handler chain: Resilience (Polly) -> Auth -> Tenant -> Network
    void AddScopedClient<T>() where T : class => _ = services.AddScoped<T>(sp =>
    {
        var tokenAccessor = sp.GetRequiredService<KeycloakTokenAccessor>();
        var authenticationStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        var correlationService = sp.GetRequiredService<ClientContextService>();
        var tenantService = sp.GetRequiredService<TenantService>();

        // Build handler chain: Auth -> Tenant -> Headers -> Network
        var networkHandler = new HttpClientHandler();
        var headerHandler =
            new BookStore.Client.Infrastructure.BookStoreHeaderHandler() { InnerHandler = networkHandler };
        var tenantHandler = new TenantHeaderHandler(tenantService) { InnerHandler = headerHandler };
        var authHandler = new AuthorizationMessageHandler(
            tokenAccessor,
            authenticationStateProvider,
            httpContextAccessor,
            correlationService)
        { InnerHandler = tenantHandler };

        // Wrap with resilience handler: Resilience -> Auth -> Tenant -> Network
        var resilienceHandler = new ResilienceHandler(resiliencePipeline) { InnerHandler = authHandler };

        var httpClient = new HttpClient(resilienceHandler) { BaseAddress = baseAddress };
        return RestService.For<T>(httpClient);
    });

    AddScopedClient<IBooksClient>();
    AddScopedClient<IAuthorsClient>();
    AddScopedClient<ICategoriesClient>();
    AddScopedClient<IPublishersClient>();
    AddScopedClient<IShoppingCartClient>();
    AddScopedClient<ISystemClient>();
    AddScopedClient<IUsersClient>();
    AddScopedClient<ISalesClient>();
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddKeycloakOpenIdConnect(
        BookStore.ServiceDefaults.ResourceNames.Keycloak,
        realm: "bookstore",
        options =>
        {
            options.ClientId = "bookstore-web";
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
        });

builder.Services.AddAuthorization();
builder.Services.AddScoped<ClientContextService>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<KeycloakTokenAccessor>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options => options.AddPolicy("SystemAdmin",
    policy => policy.RequireRole("Admin")
        .RequireClaim("tenant_id", MultiTenancyConstants.DefaultTenantId)));

// Add Polly resilience policies to all HTTP clients
// builder.Services.ConfigureHttpClientDefaults(http =>
// {
//     // Cookies are sent automatically by the browser - no need for manual token injection
//     _ = http.AddPolicyHandler(retryPolicy);
//     _ = http.AddPolicyHandler(circuitBreakerPolicy);
// });

// Register optimistic update service for eventual consistency
builder.Services.AddSingleton<OptimisticUpdateService>();

// Register query invalidation service
builder.Services.AddSingleton<QueryInvalidationService>();

// Register currency service
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<AnonymousCartService>();

// Register SSE events service
builder.Services.AddBookStoreEvents(new Uri(apiServiceUrl));

// Register Error Localization Service
builder.Services.AddScoped<ErrorLocalizationService>();

// Register Catalog Service
builder.Services.AddScoped<CatalogService>();

builder.Services.AddOutputCache();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

// Add Forwarded Headers middleware early in the pipeline

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        var accessToken = await context.GetTokenAsync("access_token");
        var refreshToken = await context.GetTokenAsync("refresh_token");
        if (!string.IsNullOrWhiteSpace(sub))
        {
            var tokenStore = context.RequestServices.GetRequiredService<KeycloakTokenAccessor>();

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var accessExpiry = ParseJwtExpiry(accessToken, DateTimeOffset.UtcNow.AddMinutes(5));
                tokenStore.SetToken(sub, accessToken, accessExpiry);
            }

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var refreshExpiry = ParseJwtExpiry(refreshToken, DateTimeOffset.UtcNow.AddMinutes(30));
                tokenStore.SetRefreshToken(sub, refreshToken, refreshExpiry);
            }
        }
    }

    await next(context);
});

// Fetch localization configuration from backend
string[] supportedCultures;
string defaultCulture;
try
{
    using var scope = app.Services.CreateScope();
    var configClient = scope.ServiceProvider.GetRequiredService<IConfigurationClient>();
    var localizationConfig = await configClient.GetLocalizationConfigAsync();
    supportedCultures = [.. localizationConfig.SupportedCultures];
    defaultCulture = localizationConfig.DefaultCulture;
}
catch (Exception ex)
{
    // Fallback to default if backend is not available
#pragma warning disable CA1848 // For improved performance, use the LoggerMessage delegates
    app.Logger.LogWarning(ex, "Failed to fetch localization configuration from backend. Using default configuration.");
#pragma warning restore CA1848
    supportedCultures = ["en"];
    defaultCulture = "en";
}

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(defaultCulture)
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Add Log Enrichment Middleware
app.UseMiddleware<LogEnrichmentMiddleware>();

app.UseHttpsRedirection();

app.MapGet("/login/oidc", (string? returnUrl) =>
{
    var redirectUri = returnUrl;

    if (string.IsNullOrWhiteSpace(redirectUri) || !Uri.IsWellFormedUriString(redirectUri, UriKind.Relative))
    {
        redirectUri = "/";
    }

    var properties = new AuthenticationProperties { RedirectUri = redirectUri };

    return Results.Challenge(
        properties,
        [OpenIdConnectDefaults.AuthenticationScheme]);
});

app.MapGet("/logout", async (HttpContext httpContext, string? returnUrl) =>
{
    var redirectUri = returnUrl;

    if (string.IsNullOrWhiteSpace(redirectUri) || !Uri.IsWellFormedUriString(redirectUri, UriKind.Relative))
    {
        redirectUri = "/";
    }

    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await httpContext.SignOutAsync(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = redirectUri });

    return Results.Empty;
});

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

static DateTimeOffset ParseJwtExpiry(string token, DateTimeOffset fallback)
{
    try
    {
        var segments = token.Split('.');
        if (segments.Length < 2)
        {
            return fallback;
        }

        var payloadBytes = DecodeBase64Url(segments[1]);
        using var document = JsonDocument.Parse(payloadBytes);

        if (!document.RootElement.TryGetProperty("exp", out var expProperty))
        {
            return fallback;
        }

        var unixSeconds = expProperty.ValueKind switch
        {
            JsonValueKind.Number when expProperty.TryGetInt64(out var numberValue) => numberValue,
            JsonValueKind.String when long.TryParse(expProperty.GetString(), out var stringValue) => stringValue,
            _ => long.MinValue
        };

        if (unixSeconds == long.MinValue)
        {
            return fallback;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.UtcNow.AddMinutes(5);
        }
    }
    catch (FormatException)
    {
        return fallback;
    }
    catch (JsonException)
    {
        return fallback;
    }
}

static byte[] DecodeBase64Url(string input)
{
    var normalized = input.Replace('-', '+').Replace('_', '/');

    switch (normalized.Length % 4)
    {
        case 2:
            normalized += "==";
            break;
        case 3:
            normalized += "=";
            break;
    }

    return Convert.FromBase64String(normalized);
}

