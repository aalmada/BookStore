using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Infrastructure.Identity;

/// <summary>
/// Configuration options for automatic unverified account cleanup.
/// </summary>
public sealed class AccountCleanupOptions
{
    public const string SectionName = "Account:Cleanup";

    /// <summary>
    /// The number of hours after which an unverified account is considered expired.
    /// Default is 24 hours.
    /// </summary>
    [Range(1, 8760)] // 1 hour to 1 year
    public int UnverifiedAccountExpirationHours { get; set; } = 24;

    /// <summary>
    /// How often the cleanup job should run.
    /// Default is every 1 hour.
    /// </summary>
    [Range(1, 720)] // 1 hour to 30 days
    public int CleanupIntervalHours { get; set; } = 1;

    /// <summary>
    /// Whether the cleanup job is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
