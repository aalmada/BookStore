using System.Text.Json;
using BookStore.Shared.Infrastructure.Json;
using BookStore.Web;
using BookStore.Web.Components;
using BookStore.Web.Services;
using BookStore.Client;
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

// Register BookStore API client
builder.Services.AddBookStoreClient(new Uri(apiServiceUrl));

// Add Polly resilience policies to all HTTP clients
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddPolicyHandler(retryPolicy);
    http.AddPolicyHandler(circuitBreakerPolicy);
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
