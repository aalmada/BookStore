using BookStore.ApiService.Infrastructure.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BookStore.ApiService.Infrastructure.Email;

public class SmtpEmailService(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAccountVerificationEmailAsync(string email, string subject, string body)
    {
        var settings = options.Value;

        // Safety check - although the handler should prevent this, double check here
        if (string.IsNullOrEmpty(settings.SmtpHost))
        {
            Log.Email.SmtpHostNotConfigured(logger, email);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName ?? "BookStore", settings.FromEmail ?? "noreply@bookstore.com"));
            message.To.Add(new MailboxAddress(email, email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            // Create a lightweight client per request for thread safety and low memory footprint
            // SmtpClient is disposable and should be disposed after use
            using var client = new SmtpClient();

            // Connect to the server
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls);

            // Authenticate if needed
            if (!string.IsNullOrEmpty(settings.SmtpUsername))
            {
                await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword);
            }

            // Send the message
            _ = await client.SendAsync(message);

            // Disconnect cleanly
            await client.DisconnectAsync(true);

            Log.Email.EmailSentSmtp(logger, email);
        }
        catch (Exception ex)
        {
            Log.Email.EmailFailedSmtp(logger, ex, email);
            throw; // Re-throw to let Wolverine handle retries
        }
    }
}
