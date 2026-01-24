using BookStore.Shared.Models;
using BookStore.Web.Services;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BookStore.Web.Tests.Services;

public class ErrorLocalizationServiceTests
{
    [Test]
    public async Task GetLocalizedMessage_ReturnsConnectionError_WhenFetchOrNetwork()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<ErrorLocalizationService>>();
        var connectionError = new LocalizedString("ConnectionError", "Friendly Connection Error");
        _ = localizer["ConnectionError"].Returns(connectionError);
        var service = new ErrorLocalizationService(localizer);

        // Act & Assert
        _ = await Assert.That(service.GetLocalizedMessage("TypeError: Failed to fetch"))
            .IsEqualTo("Friendly Connection Error");
        _ = await Assert.That(service.GetLocalizedMessage("Network connection lost"))
            .IsEqualTo("Friendly Connection Error");
    }

    [Test]
    public async Task GetLocalizedMessage_ReturnsLocalizedCode_WhenCodeExists()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<ErrorLocalizationService>>();
        var code = "ERR_AUTH_INVALID_CREDENTIALS";
        var localizedString = new LocalizedString(code, "Invalid credentials friendly");
        _ = localizer[code].Returns(localizedString);
        var service = new ErrorLocalizationService(localizer);
        var error = Error.Unauthorized(code, "Technical message");

        // Act & Assert
        _ = await Assert.That(service.GetLocalizedMessage(error))
            .IsEqualTo("Invalid credentials friendly");
    }

    [Test]
    public async Task GetLocalizedMessage_ReturnsTypeDefault_WhenCodeNotFoundButTypeExists()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<ErrorLocalizationService>>();
        var code = "UNKNOWN_CODE";
        _ = localizer[code].Returns(new LocalizedString(code, code, true)); // ResourceNotFound = true

        var typeKey = "ErrorType_Validation";
        var localizedType = new LocalizedString(typeKey, "Validation default friendly");
        _ = localizer[typeKey].Returns(localizedType);

        var service = new ErrorLocalizationService(localizer);
        var error = Error.Validation(code, "Technical message");

        // Act & Assert
        _ = await Assert.That(service.GetLocalizedMessage(error))
            .IsEqualTo("Validation default friendly");
    }

    [Test]
    public async Task GetLocalizedMessage_ReturnsMessage_WhenCodeAndTypeNotFoundButMessageIsSafe()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<ErrorLocalizationService>>();
        _ = localizer[Arg.Any<string>()].Returns(x => new LocalizedString((string)x[0], (string)x[0], true));

        var service = new ErrorLocalizationService(localizer);
        var safeMessage = "This is a safe business error message";
        var error = Error.Failure("UNKNOWN", safeMessage);

        // Act & Assert
        _ = await Assert.That(service.GetLocalizedMessage(error))
            .IsEqualTo(safeMessage);
    }

    [Test]
    public async Task GetLocalizedMessage_ReturnsDefaultError_WhenTechnicalMessageDetected()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<ErrorLocalizationService>>();
        _ = localizer[Arg.Any<string>()].Returns(x => new LocalizedString((string)x[0], (string)x[0], true));

        var defaultError = new LocalizedString("DefaultError", "Something went wrong friendly");
        _ = localizer["DefaultError"].Returns(defaultError);

        var service = new ErrorLocalizationService(localizer);
        var techMessage = "System.Exception: Something crashed";
        var error = Error.InternalServerError("INTERNAL", techMessage);

        // Act & Assert
        _ = await Assert.That(service.GetLocalizedMessage(error))
            .IsEqualTo("Something went wrong friendly");
    }

    [Test]
    public async Task GetLocalizedMessage_StringOverload_ReturnsDefault_WhenNullOrEmpty()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<ErrorLocalizationService>>();
        var defaultError = new LocalizedString("DefaultError", "Friendly Default Error");
        _ = localizer["DefaultError"].Returns(defaultError);
        var service = new ErrorLocalizationService(localizer);

        // Act & Assert
        _ = await Assert.That(service.GetLocalizedMessage((string?)null)).IsEqualTo("Friendly Default Error");
        _ = await Assert.That(service.GetLocalizedMessage("")).IsEqualTo("Friendly Default Error");
        _ = await Assert.That(service.GetLocalizedMessage("   ")).IsEqualTo("Friendly Default Error");
    }
}
