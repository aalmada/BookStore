using BookStore.Shared.Validation;

namespace BookStore.ApiService.UnitTests;

public class PasswordValidatorTests
{
    [Test]
    [Category("Unit")]
    public async Task Validate_WithPasswordAtMaxLength_ShouldBeValid()
    {
        // Arrange
        var password = BuildValidPassword(PasswordValidator.MaxLength);

        // Act
        var (isValid, errors) = PasswordValidator.Validate(password);

        // Assert
        _ = await Assert.That(isValid).IsTrue();
        _ = await Assert.That(errors.Count).IsEqualTo(0);
    }

    [Test]
    [Category("Unit")]
    public async Task Validate_WithPasswordAboveMaxLength_ShouldReturnClearError()
    {
        // Arrange
        var password = BuildValidPassword(PasswordValidator.MaxLength + 1);

        // Act
        var (isValid, errors) = PasswordValidator.Validate(password);

        // Assert
        _ = await Assert.That(isValid).IsFalse();
        _ = await Assert.That(errors).Contains($"At most {PasswordValidator.MaxLength} characters");
    }

    static string BuildValidPassword(int length)
    {
        const string requiredChars = "Aa1!";
        return requiredChars + new string('b', length - requiredChars.Length);
    }
}
