# MudBlazor Setup and Layout Shell

## Required Providers

Add to `Routes.razor` (the root component, **not** `MainLayout.razor`):

```razor
<MudThemeProvider @ref="_mudThemeProvider" @bind-IsDarkMode="_isDarkMode" Theme="@_currentTheme"/>
<MudPopoverProvider/>
<MudDialogProvider/>
<MudSnackbarProvider/>
```

Register services in `Program.cs`:

```csharp
builder.Services.AddMudServices();
```

---

## Theme and Dark Mode (BookStore pattern)

Theme is driven by `ThemeService` and is tenant-aware — the primary colour is set per-tenant. The Routes.razor wires this up:

```razor
@inject ThemeService ThemeInfo
@inject TenantService TenantService
@implements IDisposable

@code {
    private MudTheme _currentTheme = new();
    private bool _isDarkMode;
    private MudThemeProvider _mudThemeProvider = null!;

    protected override async Task OnInitializedAsync()
    {
        TenantService.OnChange += UpdateTheme;
        ThemeInfo.OnChange += OnThemeChanged;
        UpdateTheme();
        await ThemeInfo.InitializeAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await ApplyThemeAsync();
    }

    private async void OnThemeChanged()
    {
        await ApplyThemeAsync();
        StateHasChanged();
    }

    private async Task ApplyThemeAsync()
    {
        if (ThemeInfo.CurrentTheme == ThemeMode.System)
        {
            _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
            await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
        }
        else
        {
            _isDarkMode = ThemeInfo.CurrentTheme == ThemeMode.Dark;
        }
        StateHasChanged();
    }

    private async Task OnSystemPreferenceChanged(bool isDark)
    {
        if (ThemeInfo.CurrentTheme == ThemeMode.System)
        {
            _isDarkMode = isDark;
            StateHasChanged();
        }
    }

    private void UpdateTheme()
    {
        _currentTheme = new MudTheme
        {
            PaletteLight = new PaletteLight { Primary = TenantService.CurrentTenantPrimaryColor },
            PaletteDark  = new PaletteDark  { Primary = TenantService.CurrentTenantPrimaryColor }
        };
        StateHasChanged();
    }

    public void Dispose()
    {
        TenantService.OnChange -= UpdateTheme;
        ThemeInfo.OnChange -= OnThemeChanged;
    }
}
```

### Manual theme (no ThemeService)

```csharp
private MudTheme _theme = new()
{
    PaletteLight = new PaletteLight { Primary = "#1976D2", Secondary = "#FF4081" },
    PaletteDark  = new PaletteDark  { Primary = "#90CAF9" }
};
```

---

## Application Shell (MainLayout.razor)

```razor
@inherits LayoutComponentBase

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu"
                       Color="Color.Inherit" Edge="Edge.Start"
                       OnClick="@ToggleDrawer"/>
        <MudText Typo="Typo.h5" Class="ml-3">BookStore</MudText>
        <MudSpacer/>
        @* toolbar actions, ThemeSwitcher, LoginDisplay *@
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always" Elevation="2">
        <NavMenu/>
    </MudDrawer>

    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    bool _drawerOpen = true;
    void ToggleDrawer() => _drawerOpen = !_drawerOpen;
}
```

### Key layout properties

| Component | Important props |
|---|---|
| `MudAppBar` | `Elevation`, `Dense`, `Color` |
| `MudDrawer` | `@bind-Open`, `ClipMode`, `Elevation`, `Variant` (Responsive/Persistent/Temporary) |
| `MudMainContent` | wraps `@Body`; no extra props needed |
| `MudContainer` | `MaxWidth` (Small/Medium/Large/ExtraLarge/ExtraExtraLarge/False) |

---

## ThemeSwitcher component pattern

A small component that lets users switch Light / Dark / System:

```razor
@inject ThemeService ThemeInfo
@implements IDisposable

<MudMenu Icon="@GetIcon()" Color="Color.Inherit" Dense="true">
    <MudMenuItem OnClick="@(() => SetTheme(ThemeMode.Light))">Light</MudMenuItem>
    <MudMenuItem OnClick="@(() => SetTheme(ThemeMode.Dark))">Dark</MudMenuItem>
    <MudMenuItem OnClick="@(() => SetTheme(ThemeMode.System))">System</MudMenuItem>
</MudMenu>

@code {
    private string GetIcon() => ThemeInfo.CurrentTheme switch
    {
        ThemeMode.Light  => Icons.Material.Filled.LightMode,
        ThemeMode.Dark   => Icons.Material.Filled.DarkMode,
        _                => Icons.Material.Filled.SettingsBrightness
    };

    private async Task SetTheme(ThemeMode mode) =>
        await ThemeInfo.SetThemeModeAsync(mode);

    protected override void OnInitialized()  => ThemeInfo.OnChange += StateHasChanged;
    public void Dispose()                    => ThemeInfo.OnChange -= StateHasChanged;
}
```
