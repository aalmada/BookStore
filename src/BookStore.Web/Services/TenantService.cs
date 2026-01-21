using Blazored.LocalStorage;
using BookStore.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace BookStore.Web.Services;

/// <summary>
/// Default tenant ID matching Marten's DefaultTenantId
/// </summary>
public class TenantService : IDisposable
{
    const string DefaultTenantId = "*DEFAULT*";

    readonly ITenantClient _tenantClient;
    readonly NavigationManager _navigation;
    readonly ILocalStorageService _localStorage;
    readonly IJSRuntime _js;

    public event Action? OnChange;

    bool _isSubscribed;

    public TenantService(ITenantClient tenantClient, NavigationManager navigation, ILocalStorageService localStorage,
        IJSRuntime js)
    {
        _tenantClient = tenantClient;
        _navigation = navigation;
        _localStorage = localStorage;
        _js = js;

        // NavigationManager may not be initialized during prerendering
        try
        {
            _navigation.LocationChanged += HandleLocationChanged;
            _isSubscribed = true;
        }
        catch (InvalidOperationException)
        {
            // During prerendering, NavigationManager is not initialized yet
            // The subscription will be set up when the component actually renders
        }
    }

    // ... (rest of the class)

    public void Dispose()
    {
        if (_isSubscribed)
        {
            try
            {
                _navigation.LocationChanged -= HandleLocationChanged;
            }
            catch (InvalidOperationException)
            {
                // Ignore if specific environment issues
            }
        }
    }

    public string CurrentTenantId { get; private set; } = DefaultTenantId;

    public string CurrentTenantName
    {
        get => field ?? "BookStore";
        private set;
    }

    public string CurrentTenantTagline { get; private set; } =
        "Discover your next great read from our curated collection";

    public string CurrentTenantPrimaryColor { get; private set; } = "#594AE2";
    public bool IsLoading { get; private set; }

    public async Task InitializeAsync()
    {
        // Priority 1: Check URL parameter
        var uri = _navigation.ToAbsoluteUri(_navigation.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("tenant", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId))
        {
            await SetTenantAsync(tenantId.ToString());
        }
        else
        {
            // Priority 2: Load from localStorage
            var savedTenant = await _localStorage.GetItemAsStringAsync("selected-tenant");
            if (!string.IsNullOrEmpty(savedTenant))
            {
                await SetTenantAsync(savedTenant);
            }
            else
            {
                // Priority 3: Default tenant
                await SetTenantAsync(DefaultTenantId);
            }
        }
    }

    async void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        await CheckUrlAndSetTenantAsync();
        OnChange?.Invoke();
    }

    async Task CheckUrlAndSetTenantAsync()
    {
        var uri = _navigation.ToAbsoluteUri(_navigation.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("tenant", out var tenantId))
        {
            await SetTenantAsync(tenantId.ToString());
        }
        else
        {
            // Load from localStorage if no URL parameter
            var savedTenant = await _localStorage.GetItemAsStringAsync("selected-tenant");
            if (!string.IsNullOrEmpty(savedTenant))
            {
                await SetTenantAsync(savedTenant);
            }
            else
            {
                await SetTenantAsync(DefaultTenantId);
            }
        }
    }

    public async Task SetTenantAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        // If tenant hasn't changed, do nothing
        if (CurrentTenantId == tenantId && CurrentTenantName != "BookStore" && CurrentTenantName != "Unknown Tenant")
        {
            return;
        }

        IsLoading = true;
        CurrentTenantId = tenantId;
        OnChange?.Invoke(); // Notify UI of loading state

        try
        {
            var info = await _tenantClient.GetTenantAsync(tenantId);
            CurrentTenantName = info?.Name ?? "BookStore";
            CurrentTenantTagline = info?.Tagline ?? "Discover your next great read from our curated collection";
            CurrentTenantPrimaryColor =
                !string.IsNullOrEmpty(info?.ThemePrimaryColor) ? info.ThemePrimaryColor : "#594AE2";

            // Save to localStorage
            await _localStorage.SetItemAsStringAsync("selected-tenant", tenantId);

            // Save to cookie for server-side middleware (SSR)
            try
            {
                // Load the module only when needed
                var module = await _js.InvokeAsync<IJSObjectReference>("import", "/js/cookie-storage.js");
                await module.InvokeVoidAsync("setCookie", "tenant", tenantId, 365);
                await module.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Ignore JS errors (e.g. during prerendering if not interactive yet)
                _ = ex;
            }
        }
        catch (Exception)
        {
            // Fallback for invalid/unknown tenant
            CurrentTenantName = "Unknown Tenant";
            CurrentTenantTagline = "Discover your next great read from our curated collection";
            CurrentTenantPrimaryColor = "#594AE2";
        }
        finally
        {
            IsLoading = false;
            OnChange?.Invoke(); // Notify UI of completion
        }
    }

    public async Task<List<TenantInfoDto>> GetAvailableTenantsAsync()
    {
        try
        {
            return await _tenantClient.GetTenantsAsync();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
