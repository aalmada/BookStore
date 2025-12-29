using System;

namespace BookStore.ApiService.Tests;

// Invalid: Using Guid.NewGuid() in property initializer
public class InvalidGuidInProperty
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
