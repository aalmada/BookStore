// Place all {Resource} commands in Commands/{Resource}/{Resource}Commands.cs
namespace BookStore.ApiService.Commands;

// CREATE — ID auto-generated server-side
public record Create{Resource}(string Name /* other args */)
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

// UPDATE — ID from route, implement IHaveETag for optimistic concurrency
public record Update{Resource}(
    Guid Id,
    string Name /* other args */
) : IHaveETag
{
    public string? ETag { get; set; }
}

// DELETE (soft) — ID from route, implement IHaveETag for optimistic concurrency
public record SoftDelete{Resource}(Guid Id) : IHaveETag
{
    public string? ETag { get; set; }
}
