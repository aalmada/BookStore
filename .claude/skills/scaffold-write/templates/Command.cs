public record {CommandName}(Guid Id, ... DateTimeOffset Timestamp)
{
    public static {CommandName} Create(...) => new(Guid.CreateVersion7(), ..., DateTimeOffset.UtcNow);
}
