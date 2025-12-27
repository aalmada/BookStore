using System;

namespace BS1001.NoDiagnostic;

// This is a valid event - it's a record type
public record BookAdded(
    Guid Id,
    string Title,
    string? Isbn,
    DateOnly? PublicationDate);

public record AuthorUpdated(
    Guid Id,
    string Name,
    string? Biography);
