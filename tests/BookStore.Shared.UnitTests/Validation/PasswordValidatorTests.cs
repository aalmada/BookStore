using BookStore.Shared.Validation;

namespace BookStore.Shared.Tests.Validation;

public class PasswordValidatorTests
{
    [Test]
    [Category("Unit")]
    public async Task Validate_WithPasswordBelowMinimumLength_ShouldReturnClearError()
    {
        // Arrange
        var password = BuildValidPassword(PasswordValidator.MinLength - 1);

        // Act
        var (isValid, errors) = PasswordValidator.Validate(password);

        // Assert
        _ = await Assert.That(isValid).IsFalse();
        _ = await Assert.That(errors).Contains($"At least {PasswordValidator.MinLength} characters");
    }

    [Test]
    [Category("Unit")]
    public async Task Validate_WithPasswordAtMinimumLength_ShouldBeValid()
    {
        // Arrange
        var password = BuildValidPassword(PasswordValidator.MinLength);

        // Act
        var (isValid, errors) = PasswordValidator.Validate(password);

        // Assert
        _ = await Assert.That(isValid).IsTrue();
        _ = await Assert.That(errors.Count).IsEqualTo(0);
    }

    static string BuildValidPassword(int length)
    {
        const string requiredChars = "Aa1!";
        return requiredChars + new string('b', length - requiredChars.Length);
    }
}
