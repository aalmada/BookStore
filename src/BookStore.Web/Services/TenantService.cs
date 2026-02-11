using Blazored.LocalStorage;
using BookStore.Client;
using BookStore.Shared;
using BookStore.Shared.Models;
using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace BookStore.Web.Services;

/// <summary>
/// Default tenant ID matching Marten's DefaultTenantId
/// </summary>
public class TenantService : IDisposable
{
    readonly ITenantsClient _tenantClient;
    readonly NavigationManager _navigation;
    readonly ILocalStorageService _localStorage;
    readonly IJSRuntime _js;

    public event Action? OnChange;

    bool _isSubscribed;

    public TenantService(ITenantsClient tenantClient, NavigationManager navigation, ILocalStorageService localStorage,
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

    public string CurrentTenantId { get; private set; } = MultiTenancyConstants.DefaultTenantId;

    public string CurrentTenantName
    {
        get => field ?? "BookStore";
        private set;
    }

    public string CurrentTenantTagline { get; private set; } =
        "Discover your next great read from our curated collection";

    public string CurrentTenantPrimaryColor { get; private set; } = "#594AE2";
    public bool IsLoading { get; private set; }
    public List<TenantInfoDto> AvailableTenants { get; private set; } = [];

    public async Task InitializeAsync()
    {
        // Priority 1: Check URL parameter
        var uri = _navigation.ToAbsoluteUri(_navigation.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        _ = await RefreshAvailableTenantsAsync();

        if (query.TryGetValue("tenant", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId))
        {
            _ = await SetTenantAsync(tenantId.ToString());
        }
        else
        {
            // Priority 2: Load from localStorage
            var savedTenant = await _localStorage.GetItemAsStringAsync("selected-tenant");
            if (!string.IsNullOrEmpty(savedTenant))
            {
                _ = await SetTenantAsync(savedTenant);
            }
            else
            {
                // Priority 3: Default tenant
                _ = await SetTenantAsync(MultiTenancyConstants.DefaultTenantId);
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
            _ = await SetTenantAsync(tenantId.ToString());
        }
        else
        {
            // Load from localStorage if no URL parameter
            var savedTenant = await _localStorage.GetItemAsStringAsync("selected-tenant");
            if (!string.IsNullOrEmpty(savedTenant))
            {
                _ = await SetTenantAsync(savedTenant);
            }
            else
            {
                _ = await SetTenantAsync(MultiTenancyConstants.DefaultTenantId);
            }
        }
    }

    public async Task<Result> SetTenantAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure(Error.Validation("ERR_TENANT_ID_REQUIRED", "Tenant ID is required"));
        }

        // If tenant hasn't changed, do nothing
        if (CurrentTenantId == tenantId && CurrentTenantName != "BookStore" && CurrentTenantName != "Unknown Tenant")
        {
            return Result.Success();
        }

        IsLoading = true;
        CurrentTenantId = tenantId;
        OnChange?.Invoke(); // Notify UI of loading state

        try
        {
            var info = await _tenantClient.GetTenantAsync(tenantId);
            CurrentTenantName = info.Name;
            CurrentTenantTagline = info.Tagline ?? "Discover your next great read from our curated collection";
            CurrentTenantPrimaryColor =
                !string.IsNullOrEmpty(info.ThemePrimaryColor) ? info.ThemePrimaryColor : "#594AE2";

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

            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            // Fallback for invalid/unknown/disabled tenant
            if (tenantId != MultiTenancyConstants.DefaultTenantId)
            {
                // If not already on default, redirect to default
                _ = await SetTenantAsync(MultiTenancyConstants.DefaultTenantId);
            }
            else
            {
                // Absolute fallback (should not happen if system is healthy)
                CurrentTenantName = "BookStore";
                CurrentTenantTagline = "Discover your next great read from our curated collection";
                CurrentTenantPrimaryColor = "#594AE2";
            }

            return ex.ToResult();
        }
        catch (Exception ex)
        {
            if (tenantId != MultiTenancyConstants.DefaultTenantId)
            {
                _ = await SetTenantAsync(MultiTenancyConstants.DefaultTenantId);
            }

            return Result.Failure(Error.Failure("ERR_TENANT_SWITCH_FAILED", ex.Message));
        }
        finally
        {
            IsLoading = false;
            OnChange?.Invoke(); // Notify UI of completion
        }
    }

    public async Task<Result> RefreshAvailableTenantsAsync()
    {
        try
        {
            AvailableTenants = await _tenantClient.GetTenantsAsync();
            OnChange?.Invoke();
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            AvailableTenants = [];
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            AvailableTenants = [];
            return Result.Failure(Error.Failure("ERR_TENANT_REFRESH_FAILED", ex.Message));
        }
    }

    public async Task<List<TenantInfoDto>> GetAvailableTenantsAsync()
    {
        if (AvailableTenants.Count == 0)
        {
            _ = await RefreshAvailableTenantsAsync();
        }

        return AvailableTenants;
    }
}
