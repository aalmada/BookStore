using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared;
using Refit;
using TUnit;

namespace BookStore.AppHost.Tests;

public class SalesManagementTests
{
    const string SecondaryTenantId = "tenant-a";

    [Before(Class)]
    public static Task ClassSetup() => DatabaseHelpers.CreateTenantViaApiAsync(SecondaryTenantId);

    [Test]
    [Category("Integration")]
    [Arguments(MultiTenancyConstants.DefaultTenantId)]
    [Arguments(SecondaryTenantId)]
    public async Task GetSales_AfterSchedulingActiveSale_ReturnsSaleWithActiveStatus(string tenantId)
    {
        var otherTenantId = GetOtherTenantId(tenantId);
        var adminLogin = await GetAdminLoginAsync(tenantId);
        var otherTenantLogin = await GetAdminLoginAsync(otherTenantId);
        var adminClient = CreateAuthenticatedClient<IBooksClient>(adminLogin.AccessToken, tenantId);
        var salesClient = CreateAuthenticatedClient<ISalesClient>(adminLogin.AccessToken, tenantId);
        var otherTenantSalesClient = CreateAuthenticatedClient<ISalesClient>(otherTenantLogin.AccessToken, otherTenantId);

        var book = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var bookResponse = await adminClient.GetBookWithResponseAsync(book.Id);
        var currentETag = bookResponse.Headers.ETag?.Tag;
        var currentVersion = ParseETag(currentETag);
        var saleRequest = new ScheduleSaleRequest(25m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            book.Id,
            "BookUpdated",
            async () => await adminClient.ScheduleBookSaleAsync(book.Id, saleRequest, currentETag),
            TimeSpan.FromSeconds(10),
            minVersion: currentVersion + 1,
            minTimestamp: DateTimeOffset.UtcNow,
            tenantId: tenantId,
            accessToken: adminLogin.AccessToken);

        _ = await Assert.That(received).IsTrue();

        var result = await salesClient.GetSalesAsync(1, 100);
        var sale = result.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleRequest.Start);

        _ = await Assert.That(sale).IsNotNull();
        _ = await Assert.That(sale!.Percentage).IsEqualTo(25m);
        _ = await Assert.That(sale!.Status).IsEqualTo("Active");

        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, book.Id, saleRequest.Start);
    }

    [Test]
    [Category("Integration")]
    [Arguments(MultiTenancyConstants.DefaultTenantId)]
    [Arguments(SecondaryTenantId)]
    public async Task GetSales_FutureSale_StatusIsScheduled(string tenantId)
    {
        var otherTenantId = GetOtherTenantId(tenantId);
        var adminLogin = await GetAdminLoginAsync(tenantId);
        var otherTenantLogin = await GetAdminLoginAsync(otherTenantId);
        var adminClient = CreateAuthenticatedClient<IBooksClient>(adminLogin.AccessToken, tenantId);
        var salesClient = CreateAuthenticatedClient<ISalesClient>(adminLogin.AccessToken, tenantId);
        var otherTenantSalesClient = CreateAuthenticatedClient<ISalesClient>(otherTenantLogin.AccessToken, otherTenantId);

        var book = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var bookResponse = await adminClient.GetBookWithResponseAsync(book.Id);
        var currentETag = bookResponse.Headers.ETag?.Tag;
        var currentVersion = ParseETag(currentETag);
        var saleRequest = new ScheduleSaleRequest(15m, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddDays(1));

        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            book.Id,
            "BookUpdated",
            async () => await adminClient.ScheduleBookSaleAsync(book.Id, saleRequest, currentETag),
            TimeSpan.FromSeconds(10),
            minVersion: currentVersion + 1,
            minTimestamp: DateTimeOffset.UtcNow,
            tenantId: tenantId,
            accessToken: adminLogin.AccessToken);

        _ = await Assert.That(received).IsTrue();

        var result = await salesClient.GetSalesAsync(1, 100);
        var sale = result.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleRequest.Start);

        _ = await Assert.That(sale).IsNotNull();
        _ = await Assert.That(sale!.Status).IsEqualTo("Scheduled");

        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, book.Id, saleRequest.Start);
    }

    [Test]
    [Category("Integration")]
    [Arguments(MultiTenancyConstants.DefaultTenantId)]
    [Arguments(SecondaryTenantId)]
    public async Task GetSales_ExpiredSale_StatusIsExpired(string tenantId)
    {
        var otherTenantId = GetOtherTenantId(tenantId);
        var adminLogin = await GetAdminLoginAsync(tenantId);
        var otherTenantLogin = await GetAdminLoginAsync(otherTenantId);
        var adminClient = CreateAuthenticatedClient<IBooksClient>(adminLogin.AccessToken, tenantId);
        var salesClient = CreateAuthenticatedClient<ISalesClient>(adminLogin.AccessToken, tenantId);
        var otherTenantSalesClient = CreateAuthenticatedClient<ISalesClient>(otherTenantLogin.AccessToken, otherTenantId);

        var book = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var bookResponse = await adminClient.GetBookWithResponseAsync(book.Id);
        var currentETag = bookResponse.Headers.ETag?.Tag;
        var currentVersion = ParseETag(currentETag);
        var saleRequest = new ScheduleSaleRequest(
            35m,
            DateTimeOffset.UtcNow.AddSeconds(-120),
            DateTimeOffset.UtcNow.AddSeconds(-60));

        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            book.Id,
            "BookUpdated",
            async () => await adminClient.ScheduleBookSaleAsync(book.Id, saleRequest, currentETag),
            TimeSpan.FromSeconds(10),
            minVersion: currentVersion + 1,
            minTimestamp: DateTimeOffset.UtcNow,
            tenantId: tenantId,
            accessToken: adminLogin.AccessToken);

        _ = await Assert.That(received).IsTrue();

        var result = await salesClient.GetSalesAsync(1, 100);
        var sale = result.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleRequest.Start);

        _ = await Assert.That(sale).IsNotNull();
        _ = await Assert.That(sale!.Status).IsEqualTo("Expired");

        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, book.Id, saleRequest.Start);
    }

    [Test]
    [Category("Integration")]
    [Arguments(MultiTenancyConstants.DefaultTenantId)]
    [Arguments(SecondaryTenantId)]
    public async Task GetSales_AfterCancelSale_SaleAbsentFromList(string tenantId)
    {
        var otherTenantId = GetOtherTenantId(tenantId);
        var adminLogin = await GetAdminLoginAsync(tenantId);
        var otherTenantLogin = await GetAdminLoginAsync(otherTenantId);
        var adminClient = CreateAuthenticatedClient<IBooksClient>(adminLogin.AccessToken, tenantId);
        var salesClient = CreateAuthenticatedClient<ISalesClient>(adminLogin.AccessToken, tenantId);
        var otherTenantSalesClient = CreateAuthenticatedClient<ISalesClient>(otherTenantLogin.AccessToken, otherTenantId);

        var book = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var bookResponse = await adminClient.GetBookWithResponseAsync(book.Id);
        var scheduleETag = bookResponse.Headers.ETag?.Tag;
        var scheduleVersion = ParseETag(scheduleETag);
        var saleRequest = new ScheduleSaleRequest(20m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2));

        var scheduleReceived = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            book.Id,
            "BookUpdated",
            async () => await adminClient.ScheduleBookSaleAsync(book.Id, saleRequest, scheduleETag),
            TimeSpan.FromSeconds(10),
            minVersion: scheduleVersion + 1,
            minTimestamp: DateTimeOffset.UtcNow,
            tenantId: tenantId,
            accessToken: adminLogin.AccessToken);

        _ = await Assert.That(scheduleReceived).IsTrue();

        var salesAfterSchedule = await salesClient.GetSalesAsync(1, 100);
        var scheduledSale = salesAfterSchedule.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleRequest.Start);

        _ = await Assert.That(scheduledSale).IsNotNull();
        _ = await Assert.That(scheduledSale!.BookETag).IsNotNull().And.IsNotEmpty();

        var cancelVersion = ParseETag(scheduledSale.BookETag);
        var cancelReceived = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            book.Id,
            "BookUpdated",
            async () => await adminClient.CancelBookSaleAsync(book.Id, scheduledSale.Start, scheduledSale.BookETag),
            TimeSpan.FromSeconds(10),
            minVersion: cancelVersion + 1,
            minTimestamp: DateTimeOffset.UtcNow,
            tenantId: tenantId,
            accessToken: adminLogin.AccessToken);

        _ = await Assert.That(cancelReceived).IsTrue();

        var salesAfterCancel = await salesClient.GetSalesAsync(1, 100);
        var cancelledSale = salesAfterCancel.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleRequest.Start);

        _ = await Assert.That(cancelledSale).IsNull();

        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, book.Id, saleRequest.Start);
    }

    [Test]
    [Category("Integration")]
    [Arguments(MultiTenancyConstants.DefaultTenantId)]
    [Arguments(SecondaryTenantId)]
    public async Task GetSales_WithoutAdminAuth_ReturnsUnauthorizedOrForbidden(string tenantId)
    {
        var otherTenantId = GetOtherTenantId(tenantId);
        var adminLogin = await GetAdminLoginAsync(tenantId);
        var otherTenantLogin = await GetAdminLoginAsync(otherTenantId);
        var adminClient = CreateAuthenticatedClient<IBooksClient>(adminLogin.AccessToken, tenantId);
        var otherTenantSalesClient = CreateAuthenticatedClient<ISalesClient>(otherTenantLogin.AccessToken, otherTenantId);
        var book = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var saleStart = DateTimeOffset.UtcNow.AddMinutes(2);

        await ScheduleSaleAsync(adminClient, adminLogin.AccessToken, tenantId, book.Id, 18m, saleStart, saleStart.AddDays(1));
        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, book.Id, saleStart);

        var client = RestService.For<ISalesClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));

        try
        {
            _ = await client.GetSalesAsync(1, 10);
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            var isUnauthorized = ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
            _ = await Assert.That(isUnauthorized).IsTrue();
        }
    }

    [Test]
    [Category("Integration")]
    [Arguments(MultiTenancyConstants.DefaultTenantId)]
    [Arguments(SecondaryTenantId)]
    public async Task GetSales_Pagination_ReturnsCorrectPage(string tenantId)
    {
        var otherTenantId = GetOtherTenantId(tenantId);
        var adminLogin = await GetAdminLoginAsync(tenantId);
        var otherTenantLogin = await GetAdminLoginAsync(otherTenantId);
        var adminClient = CreateAuthenticatedClient<IBooksClient>(adminLogin.AccessToken, tenantId);
        var salesClient = CreateAuthenticatedClient<ISalesClient>(adminLogin.AccessToken, tenantId);
        var otherTenantSalesClient = CreateAuthenticatedClient<ISalesClient>(otherTenantLogin.AccessToken, otherTenantId);

        var firstBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var secondBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var firstSaleStart = DateTimeOffset.UtcNow.AddMinutes(20);
        var secondSaleStart = DateTimeOffset.UtcNow.AddMinutes(10);

        await ScheduleSaleAsync(adminClient, adminLogin.AccessToken, tenantId, firstBook.Id, 10m, firstSaleStart, firstSaleStart.AddDays(1));
        await ScheduleSaleAsync(adminClient, adminLogin.AccessToken, tenantId, secondBook.Id, 30m, secondSaleStart, secondSaleStart.AddDays(1));

        var page = await salesClient.GetSalesAsync(1, 1);

        _ = await Assert.That(page.Items.Count).IsEqualTo(1);
        _ = await Assert.That(page.TotalItemCount).IsGreaterThanOrEqualTo(2);
        _ = await Assert.That(page.HasNextPage).IsTrue();

        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, firstBook.Id, firstSaleStart);
        await AssertSaleHiddenFromTenantAsync(otherTenantSalesClient, secondBook.Id, secondSaleStart);
    }

    [Test]
    [Category("Integration")]
    public async Task GetSales_SaleCreatedInTenantA_NotVisibleFromTenantB()
    {
        var tenantB = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenantB);

        var tenantABooksClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var tenantASalesClient = await HttpClientHelpers.GetAuthenticatedClientAsync<ISalesClient>();

        var tenantBLogin = await AuthenticationHelpers.LoginAsAdminAsync(tenantB);
        _ = await Assert.That(tenantBLogin).IsNotNull();

        var tenantBSalesClient = RestService.For<ISalesClient>(
            HttpClientHelpers.GetAuthenticatedClient(tenantBLogin!.AccessToken, tenantB));

        var book = await BookHelpers.CreateBookAsync(tenantABooksClient, FakeDataGenerators.GenerateFakeBookRequest());
        var saleStart = DateTimeOffset.UtcNow.AddMinutes(5);
        await ScheduleSaleAsync(tenantABooksClient, GlobalHooks.AdminAccessToken!, MultiTenancyConstants.DefaultTenantId,
            book.Id, 12m, saleStart, saleStart.AddDays(1));

        var tenantBSales = await tenantBSalesClient.GetSalesAsync(1, 100);
        var tenantBVisibleSale = tenantBSales.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleStart);

        _ = await Assert.That(tenantBVisibleSale).IsNull();

        var tenantASales = await tenantASalesClient.GetSalesAsync(1, 100);
        var tenantAVisibleSale = tenantASales.Items.FirstOrDefault(item => item.BookId == book.Id && item.Start == saleStart);

        _ = await Assert.That(tenantAVisibleSale).IsNotNull();
        _ = await Assert.That(tenantAVisibleSale!.BookId).IsEqualTo(book.Id);
    }

    static string GetOtherTenantId(string tenantId)
        => tenantId.Equals(MultiTenancyConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase)
            ? SecondaryTenantId
            : MultiTenancyConstants.DefaultTenantId;

    static async Task<AuthenticationHelpers.LoginResponse> GetAdminLoginAsync(string tenantId)
    {
        var login = await AuthenticationHelpers.LoginAsAdminAsync(tenantId);
        if (login is null)
        {
            throw new InvalidOperationException($"Unable to authenticate admin for tenant '{tenantId}'.");
        }

        return login;
    }

    static T CreateAuthenticatedClient<T>(string accessToken, string tenantId)
        => RestService.For<T>(HttpClientHelpers.GetAuthenticatedClient(accessToken, tenantId));

    static async Task AssertSaleHiddenFromTenantAsync(ISalesClient salesClient, Guid bookId, DateTimeOffset saleStart)
    {
        var sales = await salesClient.GetSalesAsync(1, 100);
        var hiddenSale = sales.Items.FirstOrDefault(item => item.BookId == bookId && item.Start == saleStart);

        _ = await Assert.That(hiddenSale).IsNull();
    }

    static async Task ScheduleSaleAsync(IBooksClient adminClient, string accessToken, string tenantId, Guid bookId,
        decimal percentage, DateTimeOffset start, DateTimeOffset end)
    {
        var bookResponse = await adminClient.GetBookWithResponseAsync(bookId);
        var etag = bookResponse.Headers.ETag?.Tag;
        var version = ParseETag(etag);
        var saleRequest = new ScheduleSaleRequest(percentage, start, end);

        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            bookId,
            "BookUpdated",
            async () => await adminClient.ScheduleBookSaleAsync(bookId, saleRequest, etag),
            TimeSpan.FromSeconds(10),
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow,
            tenantId: tenantId,
            accessToken: accessToken);

        _ = await Assert.That(received).IsTrue();
    }

    static long ParseETag(string? etag)
    {
        if (string.IsNullOrEmpty(etag))
        {
            return 0;
        }

        var trimmed = etag.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');
        return long.TryParse(trimmed, out var version) ? version : 0;
    }
}
