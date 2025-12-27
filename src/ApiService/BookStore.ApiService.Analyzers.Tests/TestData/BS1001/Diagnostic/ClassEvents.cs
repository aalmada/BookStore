using System;

namespace BS1001.Diagnostic.Events;

// This should trigger BS1001 - event declared as class instead of record
public class BookAdded
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Isbn { get; init; }
    public DateOnly? PublicationDate { get; init; }
}

// This should also trigger BS1001
public class AuthorUpdated
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
