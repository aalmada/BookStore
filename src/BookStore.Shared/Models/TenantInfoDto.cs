namespace BookStore.Shared.Models;

public record TenantInfoDto(
    string Id,
    string Name,
    string? Tagline,
    string? ThemePrimaryColor,
    bool IsEnabled = true,
    string? ETag = null,
    string? ThemeSecondaryColor = null,
    string? LogoUrl = null,
    string? FontFamily = null,
    string? BorderRadiusStyle = null,
    string? HeroBannerUrl = null,
    string? SuccessColor = null,
    string? ErrorColor = null);
