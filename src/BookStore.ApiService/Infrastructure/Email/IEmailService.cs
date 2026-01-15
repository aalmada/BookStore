namespace BookStore.ApiService.Infrastructure.Email;

public interface IEmailService
{
    Task SendAccountVerificationEmailAsync(string email, string subject, string body);
}
