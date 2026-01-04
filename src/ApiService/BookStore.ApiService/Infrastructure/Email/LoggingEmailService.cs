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

        logger.LogInformation("Sending verification email to {Email}", email);
        logger.LogInformation("Subject: {Subject}", subject);
        logger.LogInformation("Body: {Body}", body);

        return Task.CompletedTask;
    }
}
