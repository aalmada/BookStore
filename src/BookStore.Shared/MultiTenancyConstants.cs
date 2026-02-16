namespace BookStore.Shared;

/// <summary>
/// Constants for multi-tenancy.
/// </summary>
public static class MultiTenancyConstants
{
    /// <summary>
    /// The default tenant ID.
    /// </summary>
    public const string DefaultTenantId = "*DEFAULT*";

    /// <summary>
    /// Human-readable alias for the default tenant that can be used in test scenarios.
    /// This maps to DefaultTenantId ("*DEFAULT*").
    /// </summary>
    public const string DefaultTenantAlias = "default";
}
