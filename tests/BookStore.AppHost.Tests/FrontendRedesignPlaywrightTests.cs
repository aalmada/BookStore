using BookStore.AppHost.Tests.Helpers;
using BookStore.ServiceDefaults;
using Microsoft.Playwright;

namespace BookStore.AppHost.Tests;

public class FrontendRedesignPlaywrightTests
{
    [Test]
    [Category("Integration")]
    public async Task CatalogPage_LoadsBookGridAndSearch()
    {
        await using var session = await FrontendBrowserSession.CreateAsync();

        await session.GotoAsync("/");

        await SseEventHelpers.WaitForConditionAsync(
            async () => await session.Page.Locator(".book-grid .book-card").CountAsync() > 0,
            TestConstants.DefaultTimeout,
            "Catalog book cards were not rendered.");

        _ = await Assert.That(await session.Page.Locator(".book-grid").CountAsync()).IsGreaterThan(0);
        _ = await Assert.That(await session.Page.Locator("input[aria-label='Search catalog']").IsVisibleAsync()).IsTrue();
        _ = await Assert.That(await session.Page.Locator(".book-grid .book-card").CountAsync()).IsGreaterThan(0);
    }

    [Test]
    [Category("Integration")]
    public async Task CatalogFilters_ApplyingAuthorFilterWithNoMatches_ShouldChangeResults()
    {
        await using var session = await FrontendBrowserSession.CreateAsync();

        await session.GotoAsync("/");

        await SseEventHelpers.WaitForConditionAsync(
            async () => await session.Page.Locator(".book-grid .book-card").CountAsync() > 0,
            TestConstants.DefaultTimeout,
            "Initial catalog results were not rendered.");

        var initialCardCount = await session.Page.Locator(".book-grid .book-card").CountAsync();
        var authorFilter = session.Page.Locator("aside[aria-label='Filters'] label:has-text('Author') + input");

        await authorFilter.FillAsync($"NoMatch-{Guid.CreateVersion7()}");

        await SseEventHelpers.WaitForConditionAsync(
            async () =>
                await session.Page.Locator(".catalog-empty").CountAsync() > 0 ||
                await session.Page.Locator(".book-grid .book-card").CountAsync() != initialCardCount,
            TestConstants.DefaultTimeout,
            "Catalog results did not react to the filter change.");

        _ = await Assert.That(await session.Page.Locator("aside[aria-label='Filters']").IsVisibleAsync()).IsTrue();
        _ = await Assert.That(await session.Page.Locator(".catalog-empty").IsVisibleAsync()).IsTrue();
    }

    [Test]
    [Category("Integration")]
    public async Task CatalogPage_ClickingBookCard_ShouldNavigateToBookDetails()
    {
        await using var session = await FrontendBrowserSession.CreateAsync();

        await session.GotoAsync("/");

        var firstCard = session.Page.Locator(".book-grid .book-card").First;

        await SseEventHelpers.WaitForConditionAsync(
            async () => await session.Page.Locator(".book-grid .book-card").CountAsync() > 0,
            TestConstants.DefaultTimeout,
            "Catalog book cards were not rendered.");

        var expectedTitle = (await firstCard.Locator("h3").InnerTextAsync()).Trim();

        await firstCard.GetByRole(AriaRole.Button, new() { Name = "View Details" }).ClickAsync();

        await SseEventHelpers.WaitForConditionAsync(
            () => Task.FromResult(session.Page.Url.Contains("/book/", StringComparison.OrdinalIgnoreCase)),
            TestConstants.DefaultTimeout,
            "Navigation to book details did not complete.");

        var heading = (await session.Page.Locator("h1").InnerTextAsync()).Trim();

        _ = await Assert.That(session.Page.Url).Contains("/book/");
        _ = await Assert.That(heading).IsEqualTo(expectedTitle);
    }

    [Test]
    [Category("Integration")]
    public async Task LoginPage_ShouldRenderEmailPasswordAndSubmit()
    {
        await using var session = await FrontendBrowserSession.CreateAsync();

        await session.GotoAsync("/login");

        _ = await Assert.That(await session.Page.Locator("#email").IsVisibleAsync()).IsTrue();
        _ = await Assert.That(await session.Page.Locator("#password").IsVisibleAsync()).IsTrue();
        _ = await Assert.That(await session.Page.Locator("button[type='submit']").InnerTextAsync()).Contains("Sign In");
    }

    [Test]
    [Category("Integration")]
    public async Task AdminBooks_UnauthenticatedRequest_ShouldRedirectToLoginOrShowAdminTable()
    {
        await using var session = await FrontendBrowserSession.CreateAsync();

        await session.GotoAsync("/admin/books");

        await SseEventHelpers.WaitForConditionAsync(
            async () =>
                session.Page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
                await session.Page.Locator("table.admin-table").CountAsync() > 0 ||
                await session.Page.Locator("h1").CountAsync() > 0,
            TestConstants.DefaultTimeout,
            "Admin books page did not reach an authenticated or redirected state.");

        if (session.Page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            await session.Page.Locator("#email").CountAsync() > 0)
        {
            _ = await Assert.That(await session.Page.Locator("button[type='submit']").InnerTextAsync()).Contains("Sign In");
            return;
        }

        _ = await Assert.That(await session.Page.Locator("table.admin-table").IsVisibleAsync()).IsTrue();
        _ = await Assert.That(await session.Page.Locator("h1").InnerTextAsync()).Contains("Book Management");
    }

    sealed class FrontendBrowserSession : IAsyncDisposable
    {
        readonly IPlaywright _playwright;
        readonly IBrowser _browser;
        readonly IBrowserContext _context;

        FrontendBrowserSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page)
        {
            _playwright = playwright;
            _browser = browser;
            _context = context;
            Page = page;
        }

        public IPage Page { get; }

        public static async Task<FrontendBrowserSession> CreateAsync()
        {
            var app = GlobalHooks.App ?? throw new InvalidOperationException("GlobalHooks.App is not initialized.");
            var notificationService = GlobalHooks.NotificationService ?? throw new InvalidOperationException("GlobalHooks.NotificationService is not initialized.");

            _ = await notificationService.WaitForResourceHealthyAsync(ResourceNames.WebFrontend, CancellationToken.None)
                .WaitAsync(TestConstants.DefaultTimeout);

            var baseUrl = app.CreateHttpClient(ResourceNames.WebFrontend).BaseAddress?.ToString().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Unable to resolve the frontend base URL.");
            }

            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = baseUrl,
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });

            var page = await context.NewPageAsync();
            return new FrontendBrowserSession(playwright, browser, context, page);
        }

        public async Task GotoAsync(string path)
        {
            _ = await Page.GotoAsync(path, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = (float)TestConstants.DefaultTimeout.TotalMilliseconds
            });
        }

        public async ValueTask DisposeAsync()
        {
            await Page.CloseAsync();
            await _context.CloseAsync();
            await _browser.CloseAsync();
            _playwright.Dispose();
        }
    }
}