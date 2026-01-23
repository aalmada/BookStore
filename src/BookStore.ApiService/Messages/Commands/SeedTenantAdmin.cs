namespace BookStore.ApiService.Messages.Commands;

/// <summary>
/// Command to seed an initial admin user for a tenant.
/// This command is intended to be run in the context of the target tenant.
/// </summary>
public record SeedTenantAdmin(
    string TenantId,
    string Email,
    string? Password,
    bool VerificationRequired);
