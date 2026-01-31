using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Shared.Models;
using TUnit.Core.Interfaces;

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
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = GlobalHooks.App!.CreateHttpClient("apiservice");

        // Create categories with specific names to test ordering
        var names = (string[])["Z-Category", "A-Category", "M-Category"];
        var prefixedNames = names.Select(n => $"{_prefix}-{n}").ToArray();

        foreach (var name in prefixedNames)
        {
            var createRequest = new
            {
                Translations = new Dictionary<string, object>
                {
                    ["en"] = new { Name = name, Description = $"Description for {name}" }
                }
            };
            var createResponse = await httpClient.PostAsJsonAsync("/api/admin/categories", createRequest);
            _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }

        // Wait for projections to catch up
        await Task.Delay(2000);

        // Act - Request public categories ordered by name asc
        publicClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        publicClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        var response = await publicClient.GetAsync("/api/categories?sortBy=name&sortOrder=asc&pageSize=100");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedListDto<CategoryDto>>();

        // Assert
        _ = await Assert.That(result).IsNotNull();
        var categoryNames = result!.Items.Select(c => c.Name).Where(prefixedNames.Contains).ToList();
        var expectedAsc = prefixedNames.OrderBy(n => n).ToList();

        _ = await Assert.That(categoryNames.Count).IsEqualTo(expectedAsc.Count);
        for (var i = 0; i < expectedAsc.Count; i++)
        {
            _ = await Assert.That(categoryNames[i]).IsEqualTo(expectedAsc[i]);
        }

        // Act - Request public categories ordered by name desc
        response = await publicClient.GetAsync("/api/categories?sortBy=name&sortOrder=desc&pageSize=100");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        result = await response.Content.ReadFromJsonAsync<PagedListDto<CategoryDto>>();

        // Assert
        categoryNames = result!.Items.Select(c => c.Name).Where(prefixedNames.Contains).ToList();
        var expectedDesc = prefixedNames.OrderByDescending(n => n).ToList();
        _ = await Assert.That(categoryNames.Count).IsEqualTo(expectedDesc.Count);
        for (var i = 0; i < expectedDesc.Count; i++)
        {
            _ = await Assert.That(categoryNames[i]).IsEqualTo(expectedDesc[i]);
        }
    }

    [Test]
    public async Task AdminGetAllCategories_OrderedByNameWithLanguage_ShouldReturnInCorrectOrder()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

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
            var createRequest = new
            {
                Translations = new Dictionary<string, object>
                {
                    ["en"] = new { Name = cat.EN, Description = "Desc" },
                    ["pt-PT"] = new { Name = cat.PT, Description = "Desc" }
                }
            };
            var createResponse = await httpClient.PostAsJsonAsync("/api/admin/categories", createRequest);
            _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }

        // Wait for projections
        await Task.Delay(2000);

        // Act - Request admin categories ordered by name in English
        var response =
            await httpClient.GetAsync("/api/admin/categories?sortBy=name&sortOrder=asc&language=en&pageSize=100");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedListDto<AdminCategoryDto>>();

        // Assert - Should be A-Category followed by C-Category
        var enNames = result!.Items.Select(c => c.Name)
            .Where(n => n.StartsWith($"{_prefix}-A-Category") || n.StartsWith($"{_prefix}-C-Category")).ToList();
        var expectedEn = (List<string>)[$"{_prefix}-A-Category", $"{_prefix}-C-Category"];
        _ = await Assert.That(enNames.Count).IsEqualTo(expectedEn.Count);
        for (var i = 0; i < expectedEn.Count; i++)
        {
            _ = await Assert.That(enNames[i]).IsEqualTo(expectedEn[i]);
        }

        // Act - Request admin categories ordered by name in Portuguese
        response = await httpClient.GetAsync(
            $"/api/admin/categories?sortBy=name&sortOrder=asc&language=pt-PT&pageSize=100");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        result = await response.Content.ReadFromJsonAsync<PagedListDto<AdminCategoryDto>>();

        // Assert - Should be A-Portuguese (which is Cat 1) followed by C-Portuguese (which is Cat 2)
        var ptItems = result!.Items
            .Where(c => c.Translations.ContainsKey("pt-PT") &&
                        (c.Translations["pt-PT"].Name == $"{_prefix}-A-Portuguese" ||
                         c.Translations["pt-PT"].Name == $"{_prefix}-C-Portuguese"))
            .ToList();

        var namesOrderedByPt = ptItems.Select(c => c.Translations["pt-PT"].Name).ToList();
        var expectedPt = (List<string>)[$"{_prefix}-A-Portuguese", $"{_prefix}-C-Portuguese"];

        _ = await Assert.That(namesOrderedByPt.Count).IsEqualTo(expectedPt.Count);
        for (var i = 0; i < expectedPt.Count; i++)
        {
            _ = await Assert.That(namesOrderedByPt[i]).IsEqualTo(expectedPt[i]);
        }
    }
}
