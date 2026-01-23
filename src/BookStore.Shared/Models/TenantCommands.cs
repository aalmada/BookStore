namespace BookStore.Shared.Models;

public record CreateTenantCommand(string Id, string Name, string? Tagline = null, string? ThemePrimaryColor = null, bool IsEnabled = true, string? AdminEmail = null, string? AdminPassword = null);
public record UpdateTenantCommand(string Name, string? Tagline, string? ThemePrimaryColor, bool IsEnabled);
