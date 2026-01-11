using System.Text.Json;
using BookStore.Client;
using BookStore.Shared.Infrastructure.Json;
using BookStore.Web;
using BookStore.Web.Components;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Polly;
using Polly.Extensions.Http;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

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
// builder.Services.AddProtectedBrowserStorage();

// Add MudBlazor services
builder.Services.AddMudServices();

// Get API base URL from service discovery (Aspire)
var apiServiceUrl = builder.Configuration[$"services:{BookStore.ServiceDefaults.ResourceNames.ApiService}:https:0"]
    ?? builder.Configuration[$"services:{BookStore.ServiceDefaults.ResourceNames.ApiService}:http:0"]
    ?? "http://localhost:5000";

// Configure Polly policies for resilience
// var retryPolicy = HttpPolicyExtensions
//     .HandleTransientHttpError()
//     .WaitAndRetryAsync(3, retryAttempt =>
//         TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// var circuitBreakerPolicy = HttpPolicyExtensions
//     .HandleTransientHttpError()
//     .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Register AuthorizationMessageHandler for JWT token injection
builder.Services.AddTransient<AuthorizationMessageHandler>();

// Register BookStore API client with authorization handler
// Register BookStore API client with authorization handler - MANUAL SCOPED REGISTRATION
// We must register clients as Scoped to ensure AuthorizationMessageHandler shares the same
// TokenService instance as the Blazor Circuit.
RegisterScopedRefitClients(builder.Services, new Uri(apiServiceUrl));

static void RegisterScopedRefitClients(IServiceCollection services, Uri baseAddress)
{
    // Register aggregated clients
    void AddScopedClient<T>() where T : class => _ = services.AddScoped<T>(sp =>
                                                      {
                                                          var tokenService = sp.GetRequiredService<TokenService>();
                                                          var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                                                          var authHandler = new AuthorizationMessageHandler(tokenService, httpContextAccessor);
                                                          // Ensure we have an InnerHandler
                                                          authHandler.InnerHandler = new HttpClientHandler();

                                                          var httpClient = new HttpClient(authHandler) { BaseAddress = baseAddress };
                                                          return RestService.For<T>(httpClient);
                                                      });

    AddScopedClient<IBooksClient>();
    AddScopedClient<IAuthorsClient>();
    AddScopedClient<ICategoriesClient>();
    AddScopedClient<IPublishersClient>();
    AddScopedClient<IShoppingCartClient>();
    AddScopedClient<ISystemClient>();
    AddScopedClient<IIdentityClient>();
}

// Add authentication services (JWT token-based)
builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<PasskeyService>(client => client.BaseAddress = new Uri(apiServiceUrl));

builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

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

// Register SSE events service
builder.Services.AddBookStoreEvents(new Uri(apiServiceUrl));

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

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
