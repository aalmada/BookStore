using BookStore.ApiService.Handlers.Notifications;
using BookStore.ApiService.Infrastructure.Email;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Messages.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers.Notifications;

public class EmailHandlersTests
{
    readonly IEmailService _emailService = Substitute.For<IEmailService>();
    readonly ILogger<EmailHandlers> _logger = Substitute.For<ILogger<EmailHandlers>>();
    readonly EmailTemplateService _templateService = new(); // Using real template service as it has no dependencies

    [Test]
    [Category("Unit")]
    public async Task Handle_WithDeliveryMethodNone_ShouldNotSendEmail()
    {
        // Arrange
        var settings = new EmailOptions { DeliveryMethod = "None" };
        var options = Options.Create(settings);
        var handler = new EmailHandlers(_emailService, options, _templateService, _logger);

        var command = new SendUserVerificationEmail(
            Guid.CreateVersion7(),
            "test@example.com",
            "VERIFY123",
            "TestUser");

        // Act
        await handler.Handle(command);

        // Assert
        await _emailService.DidNotReceive().SendAccountVerificationEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Test]
    [Category("Unit")]
    public async Task Handle_WithSmtpDelivery_ShouldSendEmail()
    {
        // Arrange
        var settings = new EmailOptions
        {
            DeliveryMethod = "Smtp",
            BaseUrl = "https://bookstore.local"
        };
        var options = Options.Create(settings);
        var handler = new EmailHandlers(_emailService, options, _templateService, _logger);

        var userId = Guid.CreateVersion7();
        var email = "user@example.com";
        var userName = "Test User";
        var code = "ABC-123";

        var command = new SendUserVerificationEmail(userId, email, code, userName);

        // Act
        await handler.Handle(command);

        // Assert
        await _emailService.Received(1).SendAccountVerificationEmailAsync(
            email,
            Arg.Any<string>(), // Subject
            Arg.Is<string>(body =>
                body.Contains(userName) &&
                body.Contains("verify-email") &&
                body.Contains(userId.ToString()) &&
                body.Contains(code))
        );
    }

    [Test]
    [Category("Unit")]
    public async Task Handle_ShouldUrlEscapeParameters()
    {
        // Arrange
        var settings = new EmailOptions
        {
            DeliveryMethod = "Smtp",
            BaseUrl = "https://bookstore.local"
        };
        var options = Options.Create(settings);
        var handler = new EmailHandlers(_emailService, options, _templateService, _logger);

        var userId = Guid.CreateVersion7();
        // Create special characters in code
        var code = "ABC+123/456==";

        var command = new SendUserVerificationEmail(userId, "test@example.com", code, "User");

        // Act
        await handler.Handle(command);

        // Assert
        var expectedEncodedCode = Uri.EscapeDataString(code);

        await _emailService.Received(1).SendAccountVerificationEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains($"code={expectedEncodedCode}"))
        );
    }
}
