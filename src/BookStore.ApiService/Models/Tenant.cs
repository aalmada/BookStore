using Marten.Schema;

namespace BookStore.ApiService.Models;

[Marten.Schema.DoNotPartition]
public class Tenant
{
    [Identity]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? ThemePrimaryColor { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
