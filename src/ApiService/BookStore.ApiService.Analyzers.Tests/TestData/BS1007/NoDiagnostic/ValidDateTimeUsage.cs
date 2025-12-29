using System;

namespace BookStore.ApiService.Tests;

// Valid: Using DateTimeOffset.UtcNow
public class ValidDateTimeUsage
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public void CreateEvent()
    {
        var now = DateTimeOffset.UtcNow;
        var parsed = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var min = DateTimeOffset.MinValue;
    }
}

public record ValidEvent(DateTimeOffset Timestamp)
{
    public static ValidEvent Create() => new(DateTimeOffset.UtcNow);
}
