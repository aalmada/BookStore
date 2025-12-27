namespace BookStore.Web.Services.Models;

public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Isbn { get; set; }
    public DateOnly? PublishedDate { get; set; }
    public string AuthorNames { get; set; } = string.Empty;
    public string? PublisherName { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}
