using BookStore.Web.Helpers;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BookStore.Web.Tests.Helpers;

public class AuthErrorHelperTests
{
    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsConnectionError_WhenFetchOrNetwork()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["ConnectionError"].Returns(new LocalizedString("ConnectionError", "Friendly Connection Error"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert.That(helper.GetFriendlyErrorMessage("TypeError: Failed to fetch"))
            .IsEqualTo("Friendly Connection Error");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Network connection lost"))
            .IsEqualTo("Friendly Connection Error");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsInvalidCredentials_When401OrUnauthorized()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["InvalidCredentials"]
            .Returns(new LocalizedString("InvalidCredentials", "Friendly Invalid Credentials"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert
            .That(helper.GetFriendlyErrorMessage("Response status code does not indicate success: 401 (Unauthorized)."))
            .IsEqualTo("Friendly Invalid Credentials");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Invalid email or password"))
            .IsEqualTo("Friendly Invalid Credentials");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsAccountLocked_WhenLockedOut()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["AccountLocked"].Returns(new LocalizedString("AccountLocked", "Friendly Account Locked"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert.That(helper.GetFriendlyErrorMessage("User is locked out."))
            .IsEqualTo("Friendly Account Locked");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsVerificationRequired_WhenRequiresVerification()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["VerificationRequired"]
            .Returns(new LocalizedString("VerificationRequired", "Friendly Verification Required"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Sign in requires verification."))
            .IsEqualTo("Friendly Verification Required");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Email not confirmed"))
            .IsEqualTo("Friendly Verification Required");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsPasskeyLoginFailed_WhenPasskeyFailed()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["PasskeyLoginFailed"]
            .Returns(new LocalizedString("PasskeyLoginFailed", "Friendly Passkey Login Failed"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Passkey login failed"))
            .IsEqualTo("Friendly Passkey Login Failed");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Invalid passkey assertion"))
            .IsEqualTo("Friendly Passkey Login Failed");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsDefaultError_WhenNullOrEmpty()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["DefaultError"].Returns(new LocalizedString("DefaultError", "Friendly Default Error"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert.That(helper.GetFriendlyErrorMessage(null)).IsEqualTo("Friendly Default Error");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("")).IsEqualTo("Friendly Default Error");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("   ")).IsEqualTo("Friendly Default Error");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsInvalidRequest_When400OrBadRequest()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        _ = localizer["InvalidRequest"].Returns(new LocalizedString("InvalidRequest", "Friendly Invalid Request"));
        var helper = new AuthErrorHelper(localizer);

        // Act & Assert
        _ = await Assert
            .That(helper.GetFriendlyErrorMessage("Response status code does not indicate success: 400 (Bad Request)."))
            .IsEqualTo("Friendly Invalid Request");
        _ = await Assert.That(helper.GetFriendlyErrorMessage("Bad Request"))
            .IsEqualTo("Friendly Invalid Request");
    }

    [Test]
    public async Task GetFriendlyErrorMessage_ReturnsOriginalError_WhenUnknown()
    {
        // Arrange
        var localizer = Substitute.For<IStringLocalizer<AuthErrorHelper>>();
        var helper = new AuthErrorHelper(localizer);
        var unknownError = "Some random business logic error";

        // Act & Assert
        _ = await Assert.That(helper.GetFriendlyErrorMessage(unknownError)).IsEqualTo(unknownError);
    }
}
