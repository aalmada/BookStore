using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

public static partial class Log
{
    public static partial class Email
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "SMTP Host is not configured. Cannot send email to {Email}")]
        public static partial void SmtpHostNotConfigured(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Sent email via SMTP to {Email}")]
        public static partial void EmailSentSmtp(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to send email via SMTP to {Email}")]
        public static partial void EmailFailedSmtp(ILogger logger, Exception ex, string email);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Verification email logged for {Email}. Subject: {Subject}; Body: {Body}")]
        public static partial void VerificationEmailLogged(ILogger logger, string email, string subject, string body);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Processing verification email for {Email}")]
        public static partial void ProcessingVerificationEmail(ILogger logger, string email);
    }
}
