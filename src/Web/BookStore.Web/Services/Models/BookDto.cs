namespace BookStore.Web.Services.Models;

public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Isbn { get; set; }
    public string Language { get; set; } = "en";
    public string LanguageName { get; set; } = "English";
    public string? Description { get; set; }
    public PartialDate? PublicationDate { get; set; }
    public bool IsPreRelease { get; set; }
    public PublisherDto? Publisher { get; set; }
    public List<AuthorDto> Authors { get; set; } = [];
    public List<CategoryDto> Categories { get; set; } = [];
}
