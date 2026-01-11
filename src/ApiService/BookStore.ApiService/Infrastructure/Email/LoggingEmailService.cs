using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.Email;

public class LoggingEmailService(
    ILogger<LoggingEmailService> logger,
    IOptions<EmailOptions> options) : IEmailService
{
    public Task SendAccountVerificationEmailAsync(string email, string subject, string body)
    {
        if (options.Value.DeliveryMethod != "Logging")
        {
            return Task.CompletedTask;
        }

        Log.Email.VerificationEmailLogged(logger, email, subject, body);

        return Task.CompletedTask;
    }
}
