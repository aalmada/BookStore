namespace BookStore.Shared.Models;

public record CreateTenantCommand(
    string Id,
    string Name,
    string? Tagline = null,
    string? ThemePrimaryColor = null,
    bool IsEnabled = true,
    string? AdminEmail = null,
    string? AdminPassword = null,
    string? ThemeSecondaryColor = null,
    string? LogoUrl = null,
    string? FontFamily = null,
    string? BorderRadiusStyle = null,
    string? HeroBannerUrl = null,
    string? SuccessColor = null,
    string? ErrorColor = null);

public record UpdateTenantCommand(
    string Name,
    string? Tagline,
    string? ThemePrimaryColor,
    bool IsEnabled,
    string? ThemeSecondaryColor = null,
    string? LogoUrl = null,
    string? FontFamily = null,
    string? BorderRadiusStyle = null,
    string? HeroBannerUrl = null,
    string? SuccessColor = null,
    string? ErrorColor = null);
