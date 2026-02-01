using System.Net;
using Blazored.LocalStorage;
using BookStore.Client;
using BookStore.Client.Infrastructure;
using BookStore.Client.Services;
using BookStore.Web.Components;
using BookStore.Web.Infrastructure;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
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

builder.Services.AddCascadingAuthenticationState();

// Add MudBlazor services
builder.Services.AddMudServices();

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

// Register BookStore API clients with authorization handler and resilience
// We must register clients as Scoped to ensure handlers share the same
// TokenService/TenantService instances as the Blazor Circuit.
RegisterScopedRefitClients(builder.Services, new Uri(apiServiceUrl), resiliencePipeline);

static void RegisterScopedRefitClients(
    IServiceCollection services,
    Uri baseAddress,
    ResiliencePipeline<HttpResponseMessage> resiliencePipeline)
{
    // Register TenantService & Handler
    _ = services.AddScoped<TenantService>();
    _ = services.AddTransient<TenantHeaderHandler>();

    // Register ITenantClient separately (No TenantHeaderHandler to prevent circular dep)
    _ = services.AddScoped<ITenantClient>(_ =>
    {
        var httpClient = new HttpClient { BaseAddress = baseAddress };
        return RestService.For<ITenantClient>(httpClient);
    });

    // Register aggregated clients with Polly resilience
    // Handler chain: Resilience (Polly) -> Auth -> Tenant -> Network
    void AddScopedClient<T>() where T : class => _ = services.AddScoped<T>(sp =>
    {
        var tokenService = sp.GetRequiredService<TokenService>();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        var correlationService = sp.GetRequiredService<ClientContextService>();
        var tenantService = sp.GetRequiredService<TenantService>();

        // Build handler chain: Auth -> Tenant -> Headers -> Network
        var networkHandler = new HttpClientHandler();
        var headerHandler =
            new BookStore.Client.Infrastructure.BookStoreHeaderHandler() { InnerHandler = networkHandler };
        var tenantHandler = new TenantHeaderHandler(tenantService) { InnerHandler = headerHandler };
        var authHandler = new AuthorizationMessageHandler(
            tokenService, tenantService, httpContextAccessor, correlationService)
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
    AddScopedClient<IIdentityClient>();
    AddScopedClient<IPasskeyClient>();
    AddScopedClient<IAdminTenantClient>();
    AddScopedClient<IAdminUserClient>();
    AddScopedClient<ISalesClient>();
}

// Add authentication services (JWT token-based)
builder.Services.AddAuthentication(options => options.DefaultScheme = "Cookies")
    .AddCookie("Cookies");
builder.Services.AddAuthorization();
builder.Services.AddScoped<ClientContextService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<PasskeyService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options => options.AddPolicy("SystemAdmin",
    policy => policy.RequireRole("Admin")
        .RequireClaim("tenant_id", "*DEFAULT*")));

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

// Register SSE events service
builder.Services.AddBookStoreEvents(new Uri(apiServiceUrl));

// Register Error Localization Service
builder.Services.AddScoped<ErrorLocalizationService>();

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

var supportedCultures = new[] { "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Add Log Enrichment Middleware
app.UseMiddleware<LogEnrichmentMiddleware>();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
