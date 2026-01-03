using System.Text.Json;
using BookStore.Client;
using BookStore.Shared.Infrastructure.Json;
using BookStore.Web;
using BookStore.Web.Components;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
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

// Add MudBlazor services
builder.Services.AddMudServices();

// Get API base URL from service discovery (Aspire)
var apiServiceUrl = builder.Configuration["services:apiservice:https:0"]
    ?? builder.Configuration["services:apiservice:http:0"]
    ?? "http://localhost:5000";

// Configure Polly policies for resilience
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Register AuthorizationMessageHandler for JWT token injection
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Register BookStore API client with authorization handler
builder.Services.AddBookStoreClient(
    new Uri(apiServiceUrl),
    clientBuilder => clientBuilder.AddHttpMessageHandler<AuthorizationMessageHandler>());

// Add authentication services (JWT token-based)
builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<PasskeyService>(client => client.BaseAddress = new Uri("https+http://bookstore-api")).AddHttpMessageHandler<AuthorizationMessageHandler>();

builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

// Add Polly resilience policies to all HTTP clients
builder.Services.ConfigureHttpClientDefaults(http =>
{
    // Cookies are sent automatically by the browser - no need for manual token injection
    _ = http.AddPolicyHandler(retryPolicy);
    _ = http.AddPolicyHandler(circuitBreakerPolicy);
});

// Register SignalR hub service for real-time notifications
builder.Services.AddSingleton<BookStoreHubService>();

// Register optimistic update service for eventual consistency
builder.Services.AddSingleton<OptimisticUpdateService>();

builder.Services.AddOutputCache();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
