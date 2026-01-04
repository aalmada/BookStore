namespace BookStore.ApiService.Infrastructure.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string BaseUrl { get; set; } = "https://localhost:7100";
    public string DeliveryMethod { get; set; } = "None";
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
}
