# Auth Guards and Tenant-Aware Components

## Authorization — Page Level

Add `[Authorize]` as a page-level attribute:

```razor
@attribute [Authorize(Roles = "Admin")]
```

For system-admin-only pages:
```razor
@attribute [Authorize(Policy = "SystemAdmin")]
```

This redirects unauthenticated users to the login page and returns 403 for authenticated users without the required role/policy.

---

## Authorization — Block Level (AuthorizeView)

Use `<AuthorizeView>` to conditionally render markup without redirecting:

```razor
<AuthorizeView Roles="Admin">
    <Authorized>
        <MudNavLink Href="admin/books" Icon="@Icons.Material.Filled.Book">
            Books
        </MudNavLink>
    </Authorized>
</AuthorizeView>

<AuthorizeView Policy="SystemAdmin">
    <MudNavLink Href="admin/tenants" Icon="@Icons.Material.Filled.Business">
        Tenants
    </MudNavLink>
</AuthorizeView>
```

When using `<AuthorizeView>` without separate `<Authorized>`/`<NotAuthorized>` child elements, markup inside the component renders only when authorized:

```razor
<AuthorizeView Roles="Admin">
    <MudButton OnClick="DeleteAll">Delete All</MudButton>
</AuthorizeView>
```

---

## Accessing the Current User

```csharp
[CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

protected override async Task OnInitializedAsync()
{
    var authState = await AuthStateTask;
    var user = authState.User;
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var isAdmin = user.IsInRole("Admin");
}
```

---

## Tenant-Aware Components

### Reading Tenant Info

Inject `TenantService` to access the currently active tenant:

```csharp
@inject TenantService TenantService

protected override async Task OnInitializedAsync()
{
    // Always initialize tenant before loading any tenant-scoped data
    await TenantService.InitializeAsync();
    
    // TenantService reads from ?tenant= URL param → localStorage → default
    var currentTenant = TenantService.CurrentTenant;
    
    await LoadDataAsync();
}
```

The `TenantHeaderHandler` (registered in `Program.cs`) automatically injects `X-Tenant-ID` into every Refit client call — you do **not** need to pass the tenant ID manually to client methods.

---

### Reacting to Tenant Changes

Some pages must reload data when the user switches tenants at runtime. Subscribe to `TenantService.OnChange`:

```razor
@implements IDisposable
@inject TenantService TenantService
```

```csharp
protected override async Task OnInitializedAsync()
{
    await TenantService.InitializeAsync();
    TenantService.OnChange += HandleTenantChanged;
    await LoadDataAsync();
}

private async void HandleTenantChanged()
{
    await InvokeAsync(async () =>
    {
        await LoadDataAsync();
        StateHasChanged();
    });
}

public void Dispose()
{
    // ...
    TenantService.OnChange -= HandleTenantChanged;
}
```

---

### Important: ITenantsClient vs. Other Clients

`ITenantsClient` is **not** registered with `TenantHeaderHandler` (to avoid a circular dependency when loading the tenant list itself). All other Refit clients automatically receive the `X-Tenant-ID` header.

---

## Multi-Language Support

The `LanguageSelector` component drives `LocalizationService`. Components that display localized content should read via `LocalizationService.CurrentLanguage` and react to changes similarly to tenant changes:

```csharp
protected override async Task OnInitializedAsync()
{
    LocalizationService.OnLanguageChanged += HandleLanguageChanged;
    await LoadLocalizedDataAsync();
}

private async void HandleLanguageChanged()
{
    await InvokeAsync(async () =>
    {
        await LoadLocalizedDataAsync();
        StateHasChanged();
    });
}

public void Dispose()
{
    // ...
    LocalizationService.OnLanguageChanged -= HandleLanguageChanged;
}
```
