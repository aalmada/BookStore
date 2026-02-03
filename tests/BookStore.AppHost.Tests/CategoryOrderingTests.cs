using System.Net;
using System.Net.Http.Headers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using TUnit.Core.Interfaces;
using CategoryTranslationDto = BookStore.Client.CategoryTranslationDto;

namespace BookStore.AppHost.Tests;

[NotInParallel]
public class CategoryOrderingTests
{
    string _prefix = "";

    [Before(Test)]
    public async Task Before() => _prefix = Guid.NewGuid().ToString("N")[..8];

    [Test]
    public async Task GetCategories_OrderedByName_ShouldReturnInCorrectOrder()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

        // Create categories with specific names to test ordering
        var names = (string[])["Z-Category", "A-Category", "M-Category"];
        var prefixedNames = names.Select(n => $"{_prefix}-{n}").ToArray();

        foreach (var name in prefixedNames)
        {
            var createRequest = new CreateCategoryRequest
            {
                Translations = new Dictionary<string, CategoryTranslationDto>
                {
                    ["en"] = new CategoryTranslationDto { Name = name, Description = $"Description for {name}" }
                }
            };
            await adminClient.CreateCategoryAsync(createRequest);
        }

        // Wait for projections to catch up
        await Task.Delay(TestConstants.DefaultProjectionDelay);

        // Act - Request public categories ordered by name asc
        var publicHttpClient = TestHelpers.GetUnauthenticatedClient();
        publicHttpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        publicHttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
        var publicClient = RestService.For<ICategoriesClient>(publicHttpClient);

        var result = await publicClient.GetCategoriesAsync(null, 100, "name", "asc");

        // Assert
        _ = await Assert.That(result).IsNotNull();
        var categoryNames = result!.Items.Select(c => c.Name).Where(prefixedNames.Contains).ToList();
        var expectedAsc = prefixedNames.OrderBy(n => n).ToList();

        _ = await Assert.That(categoryNames).IsEquivalentTo(expectedAsc);

        // Act - Request public categories ordered by name desc
        result = await publicClient.GetCategoriesAsync(null, 100, "name", "desc");

        // Assert
        categoryNames = [.. result!.Items.Select(c => c.Name).Where(prefixedNames.Contains)];
        var expectedDesc = prefixedNames.OrderByDescending(n => n).ToList();
        _ = await Assert.That(categoryNames).IsEquivalentTo(expectedDesc);
    }

    [Test]
    public async Task AdminGetAllCategories_OrderedByNameWithLanguage_ShouldReturnInCorrectOrder()
    {
        // Arrange
        var adminClient = await TestHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

        // Create categories with Portuguese and English names
        // Cat 1: EN: "C", PT: "A"
        // Cat 2: EN: "A", PT: "C"
        var categories = new[]
        {
            new { EN = $"{_prefix}-C-Category", PT = $"{_prefix}-A-Portuguese" },
            new { EN = $"{_prefix}-A-Category", PT = $"{_prefix}-C-Portuguese" }
        };

        foreach (var cat in categories)
        {
            var createRequest = new CreateCategoryRequest
            {
                Translations = new Dictionary<string, CategoryTranslationDto>
                {
                    ["en"] = new CategoryTranslationDto { Name = cat.EN, Description = "Desc" },
                    ["pt-PT"] = new CategoryTranslationDto { Name = cat.PT, Description = "Desc" }
                }
            };
            await adminClient.CreateCategoryAsync(createRequest);
        }

        // Wait for projections
        await Task.Delay(2000);

        // Act - Request admin categories ordered by name in English
        var result = await adminClient.GetAllCategoriesAsync(new CategorySearchRequest
        {
            SortBy = "name", SortOrder = "asc", Language = "en", PageSize = 100
        });

        // Assert - Should be A-Category followed by C-Category
        var enNames = result!.Items.Select(c => c.Name)
            .Where(n => n.StartsWith($"{_prefix}-A-Category") || n.StartsWith($"{_prefix}-C-Category")).ToList();
        var expectedEn = (List<string>)[$"{_prefix}-A-Category", $"{_prefix}-C-Category"];
        _ = await Assert.That(enNames).IsEquivalentTo(expectedEn);

        // Act - Request admin categories ordered by name in Portuguese
        result = await adminClient.GetAllCategoriesAsync(new CategorySearchRequest
        {
            SortBy = "name", SortOrder = "asc", Language = "pt-PT", PageSize = 100
        });

        // Assert - Should be A-Portuguese (which is Cat 1) followed by C-Portuguese (which is Cat 2)
        var ptItems = result!.Items
            .Where(c => c.Translations.ContainsKey("pt-PT") &&
                        (c.Translations["pt-PT"].Name == $"{_prefix}-A-Portuguese" ||
                         c.Translations["pt-PT"].Name == $"{_prefix}-C-Portuguese"))
            .ToList();

        var namesOrderedByPt = ptItems.Select(c => c.Translations["pt-PT"].Name).ToList();
        var expectedPt = (List<string>)[$"{_prefix}-A-Portuguese", $"{_prefix}-C-Portuguese"];

        _ = await Assert.That(namesOrderedByPt).IsEquivalentTo(expectedPt);
    }
}
