using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Infrastructure;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int WindowInMinutes { get; set; } = 1;

    public int QueueLimit { get; set; } = 0;
}
