using System.ComponentModel.DataAnnotations;
using BookStore.ApiService.Models;

namespace BookStore.ApiService.Tests;

/// <summary>
/// Tests for configuration validation using data annotations
/// </summary>
public class ConfigurationValidationTests
{
    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_ValidConfiguration_PassesValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = 20,
            MaxPageSize = 100
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        _ = await Assert.That(results).IsEmpty();
    }

    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_DefaultPageSizeNegative_FailsValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = -1,
            MaxPageSize = 100
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsEqualTo(1);
        _ = await Assert.That(results[0].ErrorMessage).Contains("DefaultPageSize must be between 1 and 1000");
    }

    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_DefaultPageSizeZero_FailsValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = 0,
            MaxPageSize = 100
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsEqualTo(1);
        _ = await Assert.That(results[0].ErrorMessage).Contains("DefaultPageSize must be between 1 and 1000");
    }

    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_DefaultPageSizeTooLarge_FailsValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = 1001,
            MaxPageSize = 1001
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        _ = await Assert.That(results.Any(r => r.ErrorMessage!.Contains("DefaultPageSize must be between 1 and 1000"))).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_MaxPageSizeNegative_FailsValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = 20,
            MaxPageSize = -1
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsEqualTo(1);
        _ = await Assert.That(results[0].ErrorMessage).Contains("MaxPageSize must be between 1 and 1000");
    }

    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_MaxPageSizeZero_FailsValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = 20,
            MaxPageSize = 0
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsEqualTo(1);
        _ = await Assert.That(results[0].ErrorMessage).Contains("MaxPageSize must be between 1 and 1000");
    }

    [Test]
    [Category("Unit")]
    public async Task PaginationOptions_DefaultPageSizeGreaterThanMaxPageSize_FailsValidation()
    {
        // Arrange
        var options = new PaginationOptions
        {
            DefaultPageSize = 100,
            MaxPageSize = 50
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsEqualTo(1);
        _ = await Assert.That(results[0].ErrorMessage).Contains("DefaultPageSize (100) cannot be greater than MaxPageSize (50)");
    }

    [Test]
    [Category("Unit")]
    public async Task LocalizationOptions_ValidConfiguration_PassesValidation()
    {
        // Arrange
        var options = new LocalizationOptions
        {
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US", "pt-PT", "fr-FR"]
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        _ = await Assert.That(results).IsEmpty();
    }

    [Test]
    [Category("Unit")]
    public async Task LocalizationOptions_DefaultCultureEmpty_FailsValidation()
    {
        // Arrange
        var options = new LocalizationOptions
        {
            DefaultCulture = "",
            SupportedCultures = ["en-US"]
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).IsNotEmpty();
        _ = await Assert.That(results.Any(r => r.ErrorMessage!.Contains("DefaultCulture"))).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task LocalizationOptions_SupportedCulturesEmpty_FailsValidation()
    {
        // Arrange
        var options = new LocalizationOptions
        {
            DefaultCulture = "en-US",
            SupportedCultures = []
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).IsNotEmpty();
        _ = await Assert.That(results.Any(r => r.ErrorMessage!.Contains("At least one supported culture"))).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task LocalizationOptions_DefaultCultureNotInSupportedCultures_FailsValidation()
    {
        // Arrange
        var options = new LocalizationOptions
        {
            DefaultCulture = "en-US",
            SupportedCultures = ["pt-PT", "fr-FR"]
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        using var scope = Assert.Multiple();
        _ = await Assert.That(results).Count().IsEqualTo(1);
        _ = await Assert.That(results[0].ErrorMessage).Contains("DefaultCulture 'en-US' must be included in SupportedCultures");
    }

    [Test]
    [Category("Unit")]
    public async Task LocalizationOptions_DefaultCultureInSupportedCulturesCaseInsensitive_PassesValidation()
    {
        // Arrange
        var options = new LocalizationOptions
        {
            DefaultCulture = "en-us",
            SupportedCultures = ["EN-US", "pt-PT"]
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        _ = await Assert.That(results).IsEmpty();
    }

    /// <summary>
    /// Helper method to validate a model using data annotations
    /// </summary>
    static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        _ = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }
}
