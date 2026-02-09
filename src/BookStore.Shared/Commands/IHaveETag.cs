namespace BookStore.Shared.Commands;

public interface IHaveETag
{
    string? ETag { get; set; }
}
