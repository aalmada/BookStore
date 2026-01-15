using System;

namespace BookStore.ApiService.Tests;

// Valid: Not using Guid.NewGuid()
public class ValidGuidUsage
{
    public Guid Id { get; init; } = Guid.Empty;

    public void CreateEntity()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000000");
        var empty = Guid.Empty;
        var newGuid = new Guid();
    }
}

public record ValidRecord(Guid Id = default)
{
    public static ValidRecord Create() => new(Guid.Empty);
}
