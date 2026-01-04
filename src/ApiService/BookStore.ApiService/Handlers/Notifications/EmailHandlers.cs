using BookStore.ApiService.Infrastructure.Email;
using BookStore.ApiService.Messages.Commands;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Handlers.Notifications;

public class EmailHandlers(
    IEmailService emailService,
    IOptions<EmailOptions> options,
    EmailTemplateService templateService,
    ILogger<EmailHandlers> logger)
{
    public async Task Handle(SendUserVerificationEmail command)
    {
        var settings = options.Value;

        if (settings.DeliveryMethod == "None")
        {
            return;
        }

        logger.LogInformation("Processing verification email for {Email}", command.Email);

        var verificationLink = $"{settings.BaseUrl}/verify-email?userId={Uri.EscapeDataString(command.UserId.ToString())}&code={Uri.EscapeDataString(command.VerificationCode)}";
        var (subject, body) = templateService.GetVerificationEmail(command.UserName, verificationLink);

        await emailService.SendAccountVerificationEmailAsync(command.Email, subject, body);
    }
}
