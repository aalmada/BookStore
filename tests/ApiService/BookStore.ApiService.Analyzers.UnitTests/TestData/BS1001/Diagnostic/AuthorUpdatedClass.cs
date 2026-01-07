using System;

namespace BS1001.Diagnostic.Events;

// This should trigger BS1001 - event declared as class instead of record
public class AuthorUpdated
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
