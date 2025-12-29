namespace BookStore.ApiService.Tests;

/// <summary>
/// Tests for CultureCache validation
/// </summary>
public class CultureCacheTests
{
    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_ValidFullCultureCode_ReturnsTrue()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureCode("en-US");

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_ValidTwoLetterISOCode_ReturnsTrue()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureCode("pt");

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_ValidThreeLetterISOCode_ReturnsTrue()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureCode("fil"); // Filipino

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_InvalidCode_ReturnsFalse()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureCode("xx-XX");

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_EmptyString_ReturnsFalse()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureCode("");

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_Null_ReturnsFalse()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureCode(null!);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureCode_CaseInsensitive_ReturnsTrue()
    {
        // Arrange & Act
        var result1 = Infrastructure.CultureCache.IsValidCultureCode("EN-us");
        var result2 = Infrastructure.CultureCache.IsValidCultureCode("PT");

        // Assert
        _ = await Assert.That(result1).IsTrue();
        _ = await Assert.That(result2).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureName_ValidCultureName_ReturnsTrue()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureName("pt-PT");

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureName_InvalidCultureName_ReturnsFalse()
    {
        // Arrange & Act
        var result = Infrastructure.CultureCache.IsValidCultureName("xx-XX");

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsValidCultureName_ISOCodeOnly_MayBeValidNeutralCulture()
    {
        // Arrange & Act - Neutral cultures like "pt", "en" are valid culture names
        var result = Infrastructure.CultureCache.IsValidCultureName("pt");

        // Assert - "pt" is a valid neutral culture
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task GetInvalidCodes_AllValid_ReturnsEmpty()
    {
        // Arrange
        var codes = new[] { "en-US", "pt-PT", "fr", "fil" };

        // Act
        var invalidCodes = Infrastructure.CultureCache.GetInvalidCodes(codes);

        // Assert
        _ = await Assert.That(invalidCodes).IsEmpty();
    }

    [Test]
    [Category("Unit")]
    public async Task GetInvalidCodes_SomeInvalid_ReturnsInvalidOnes()
    {
        // Arrange
        var codes = new[] { "en-US", "xx-XX", "pt-PT", "invalid" };

        // Act
        var invalidCodes = Infrastructure.CultureCache.GetInvalidCodes(codes);

        // Assert
        _ = await Assert.That(invalidCodes).Count().IsEqualTo(2);
        _ = await Assert.That(invalidCodes).Contains("xx-XX");
        _ = await Assert.That(invalidCodes).Contains("invalid");
    }

    [Test]
    [Category("Unit")]
    public async Task GetInvalidCodes_EmptyString_ReturnsEmptyMarker()
    {
        // Arrange
        var codes = new[] { "en-US", "", "pt-PT" };

        // Act
        var invalidCodes = Infrastructure.CultureCache.GetInvalidCodes(codes);

        // Assert
        _ = await Assert.That(invalidCodes).Count().IsEqualTo(1);
        _ = await Assert.That(invalidCodes).Contains("(empty)");
    }

    [Test]
    [Category("Unit")]
    public async Task GetInvalidCodes_WhitespaceString_ReturnsEmptyMarker()
    {
        // Arrange
        var codes = new[] { "en-US", "   ", "pt-PT" };

        // Act
        var invalidCodes = Infrastructure.CultureCache.GetInvalidCodes(codes);

        // Assert
        _ = await Assert.That(invalidCodes).Count().IsEqualTo(1);
        _ = await Assert.That(invalidCodes).Contains("(empty)");
    }
}
