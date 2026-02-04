using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Services;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Services;

public class LanguageServiceTests
{
    IConfigurationClient _configurationClient = null!;
    LanguageService _sut = null!;

    [Before(Test)]
    public void Setup()
    {
        _configurationClient = Substitute.For<IConfigurationClient>();
        _sut = new LanguageService(_configurationClient);
    }

    [Test]
    [Arguments("en-US", "English (United States)")]
    [Arguments("pt-PT", "Portuguese (Portugal)")]
    [Arguments("unknown", "unknown")]
    public async Task GetDisplayName_ShouldReturnCorrectLocalizedName(string code, string expected)
    {
        // Act
        var result = LanguageService.GetDisplayName(code);

        // Assert
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task GetAllLanguages_ShouldReturnManyLanguagesIncludingEnglish()
    {
        // Act
        var result = _sut.GetAllLanguages().ToList();

        // Assert
        _ = await Assert.That(result).IsNotEmpty();
        _ = await Assert.That(result).Any(l => l.Code == "en-US");
        _ = await Assert.That(result).Any(l => l.Code == "pt-PT");
    }

    [Test]
    public async Task GetLanguagesWithDisplayNamesAsync_ShouldReturnLocalizedNames()
    {
        // Arrange
        var config = new LocalizationConfigDto("en-US", ["en-US", "pt-PT"]);
        _ = _configurationClient.GetLocalizationConfigAsync().Returns(config);

        // Act
        var result = await _sut.GetLanguagesWithDisplayNamesAsync();

        // Assert
        _ = await Assert.That(result).ContainsKey("en-US");
        _ = await Assert.That(result["en-US"]).IsEqualTo("English (United States)");
        _ = await Assert.That(result).ContainsKey("pt-PT");
        _ = await Assert.That(result["pt-PT"]).IsEqualTo("Portuguese (Portugal)");
    }
}
