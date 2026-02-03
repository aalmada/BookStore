namespace BookStore.Shared.Models;

/// <summary>
/// DTO for localization configuration
/// </summary>
public record LocalizationConfigDto(
    string DefaultCulture,
    string[] SupportedCultures
);
