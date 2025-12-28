using BookStore.Web;
using BookStore.Web.Components;
using BookStore.Web.Services;
using Polly;
using Polly.Extensions.Http;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Polly policies for resilience
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Register Refit API client with Polly policies
builder.Services
    .AddRefitClient<IBookStoreApi>()
    .ConfigureHttpClient(c =>
    {
        // Get API base URL from service discovery (Aspire)
        var apiServiceUrl = builder.Configuration["services:apiservice:https:0"] 
            ?? builder.Configuration["services:apiservice:http:0"]
            ?? "http://localhost:5000";
        c.BaseAddress = new Uri(apiServiceUrl);
    })
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);

// Register SignalR hub service for real-time notifications
builder.Services.AddSingleton<BookStoreHubService>();

// Register optimistic update service for eventual consistency
builder.Services.AddSingleton<OptimisticUpdateService>();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
