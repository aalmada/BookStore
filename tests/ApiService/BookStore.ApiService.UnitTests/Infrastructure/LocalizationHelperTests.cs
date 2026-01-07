using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class LocalizationHelperTests
{
    static readonly Dictionary<string, string> Translations = new()
    {
        ["pt-PT"] = "Portuguese (Portugal)",
        ["pt"] = "Portuguese",
        ["en-US"] = "English (US)",
        ["en"] = "English"
    };

    [Test]
    [Category("Unit")]
    [MethodDataSource(nameof(FallbackTestCases))]
    public async Task GetLocalizedValue_ReturnsExpectedValue(
        Dictionary<string, string> translations,
        string requestedCulture,
        string defaultCulture,
        string fallback,
        string expectedResult)
    {
        // Act
        var result = LocalizationHelper.GetLocalizedValue(translations, requestedCulture, defaultCulture, fallback);

        // Assert
        _ = await Assert.That(result).IsEqualTo(expectedResult);
    }

    public static IEnumerable<Func<(Dictionary<string, string> Translations, string Requested, string Default, string Fallback, string Expected)>> FallbackTestCases()
    {
        // 1. Exact user culture match
        yield return () => (Translations, "pt-PT", "en-US", "Fallback", "Portuguese (Portugal)");

        // 2. Two-letter user culture
        yield return () => (Translations, "pt-BR", "en-US", "Fallback", "Portuguese");

        // 3. Default culture
        yield return () => (Translations, "fr-FR", "en-US", "Fallback", "English (US)");

        // 4. Two-letter default culture
        yield return () => (Translations, "fr-FR", "en-GB", "Fallback", "English");

        // 5. Fallback value (no match)
        yield return () => (Translations, "fr-FR", "es-ES", "Fallback", "Fallback");

        // 6. Null translations
        yield return () => (null!, "en-US", "en-US", "Fallback", "Fallback");

        // 7. Empty translations
        yield return () => ([], "en-US", "en-US", "Fallback", "Fallback");
    }
}
