using Blazored.LocalStorage;

namespace BookStore.Web.Services;

public class ThemeService(ILocalStorageService localStorage)
{
    const string ThemeKey = "theme_preference";

    public ThemeMode CurrentTheme { get; private set; } = ThemeMode.System;

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (await localStorage.ContainKeyAsync(ThemeKey))
        {
            CurrentTheme = await localStorage.GetItemAsync<ThemeMode>(ThemeKey);
            OnChange?.Invoke();
        }
    }

    public async Task SetThemeModeAsync(ThemeMode mode)
    {
        CurrentTheme = mode;
        await localStorage.SetItemAsync(ThemeKey, mode);
        OnChange?.Invoke();
    }
}
