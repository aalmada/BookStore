namespace BookStore.ApiService.Infrastructure.Email;

public class EmailTemplateService
{
    private const string DefaultSubject = "Welcome to BookStore! Please verify your email.";
    
    // Simple HTML template avoiding Razor overhead
    private const string DefaultBodyTemplate = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .button { display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; }
        .footer { margin-top: 30px; font-size: 0.8em; color: #666; }
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Welcome to BookStore, {{UserName}}!</h2>
        <p>Thanks for creating an account. Please verify your email address to get started.</p>
        <p>
            <a href=""{{VerificationLink}}"" class=""button"">Verify Email</a>
        </p>
        <p>If the button doesn't work, copy and paste this link into your browser:</p>
        <p>{{VerificationLink}}</p>
        <div class=""footer"">
            <p>If you didn't create this account, you can safely ignore this email.</p>
        </div>
    </div>
</body>
</html>";

    public (string Subject, string Body) GetVerificationEmail(string userName, string verificationLink)
    {
        var body = DefaultBodyTemplate
            .Replace("{{UserName}}", userName)
            .Replace("{{VerificationLink}}", verificationLink);

        return (DefaultSubject, body);
    }
}
