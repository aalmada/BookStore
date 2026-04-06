namespace BookStore.ApiService.Infrastructure.Auth;

public sealed record KeycloakAdminOptions
{
    public const string SectionName = "Keycloak:Admin";

    public string BaseUrl { get; init; } = string.Empty;
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string Realm { get; init; } = "bookstore";
}
