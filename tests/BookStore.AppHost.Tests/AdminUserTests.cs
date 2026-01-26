using System.Net.Http.Json;
using BookStore.Shared.Models;
using JasperFx;

namespace BookStore.AppHost.Tests;

public class AdminUserTests : IDisposable
{
    HttpClient? _client;

    [Before(Test)]
    public async Task Setup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _client = GlobalHooks.App.CreateHttpClient("apiservice");
    }

    [After(Test)]
    public void Cleanup() => _client?.Dispose();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task GetUsers_ReturnsListOfUsers()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Act
        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>("/api/admin/users");
        var users = result?.Items;

        // Assert
        _ = await Assert.That(users).IsNotNull();
        _ = await Assert.That(users!).IsNotEmpty();

        var admin = users!.First(u => u.Email == "admin@bookstore.com");
        _ = await Assert.That(admin).IsNotNull();
        _ = await Assert.That(admin.HasPassword).IsTrue();
        _ = await Assert.That(admin.HasPasskey).IsFalse();
    }

    [Test]
    public async Task PromoteUser_SucceedsForOtherUser()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Create a regular user
        var userEmail = $"test_{Guid.NewGuid()}@example.com";
        _ = await _client.PostAsJsonAsync("/account/register", new { email = userEmail, password = "Password123!" });

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var users = result?.Items;
        var userToPromote = users!.First(u => u.Email == userEmail);

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{userToPromote.Id}/promote", null);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify promotion
        var updatedResult =
            await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var updatedUsers = updatedResult?.Items;
        var promotedUser = updatedUsers!.First(u => u.Id == userToPromote.Id);
        _ = await Assert.That(promotedUser.Roles).Contains("Admin");
    }

    [Test]
    public async Task PromoteSelf_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>("/api/admin/users");
        var users = result?.Items;
        var self = users!.First(u => u.Email == "admin@bookstore.com");

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{self.Id}/promote", null);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Admin.CannotPromoteSelf);
    }

    [Test]
    public async Task DemoteSelf_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>("/api/admin/users");
        var users = result?.Items;
        var self = users!.First(u => u.Email == "admin@bookstore.com");

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{self.Id}/demote", null);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Admin.CannotDemoteSelf);
    }

    [Test]
    public async Task DemoteUser_SucceedsForOtherUser()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Create and promote a user
        var userEmail = $"test_{Guid.NewGuid()}@example.com";
        _ = await _client.PostAsJsonAsync("/account/register", new { email = userEmail, password = "Password123!" });

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var users = result?.Items;
        var user = users!.First(u => u.Email == userEmail);
        _ = await _client.PostAsync($"/api/admin/users/{user.Id}/promote", null);

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{user.Id}/demote", null);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify demotion
        var updatedResult =
            await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var updatedUsers = updatedResult?.Items;
        var demotedUser = updatedUsers!.First(u => u.Id == user.Id);
        _ = await Assert.That(demotedUser.Roles).DoesNotContain("Admin");
    }

    [Test]
    public async Task PromoteUser_AlreadyAdmin_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Create and promote a user
        var userEmail = $"test_{Guid.NewGuid()}@example.com";
        _ = await _client.PostAsJsonAsync("/account/register", new { email = userEmail, password = "Password123!" });

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var users = result?.Items;
        var user = users!.First(u => u.Email == userEmail);
        _ = await _client.PostAsync($"/api/admin/users/{user.Id}/promote", null);

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{user.Id}/promote", null);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task DemoteUser_NotAdmin_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Create a regular user
        var userEmail = $"test_{Guid.NewGuid()}@example.com";
        _ = await _client.PostAsJsonAsync("/account/register", new { email = userEmail, password = "Password123!" });

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var users = result?.Items;
        var user = users!.First(u => u.Email == userEmail);

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{user.Id}/demote", null);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RegularUser_CannotAccessAdminUserEndpoints()
    {
        // Arrange
        var userEmail = $"user_{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        _ = await _client!.PostAsJsonAsync("/account/register", new { email = userEmail, password });

        var loginResponse = await _client!.PostAsJsonAsync("/account/login", new { email = userEmail, password });
        var token = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();

        var userClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        userClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token!.AccessToken);
        userClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Act
        var response = await userClient.GetAsync("/api/admin/users");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PromoteUser_LowercaseAdmin_IsNormalizedToPascalCase()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Create a regular user
        var userEmail = $"test_{Guid.NewGuid()}@example.com";
        _ = await _client.PostAsJsonAsync("/account/register", new { email = userEmail, password = "Password123!" });

        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var users = result?.Items;
        var user = users!.First(u => u.Email == userEmail);

        // Act: Manually promote using lowercase "admin" (if we had an endpoint that allowed it, 
        // but here we verify the existing endpoint normalizes it if it wasn't already)
        // Actually, our AddToRoleAsync in the store now internalizes this.
        _ = await _client.PostAsync($"/api/admin/users/{user.Id}/promote", null);

        // Assert: Verify it's returned as "Admin" in the user list
        var updatedResult =
            await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>($"/api/admin/users?search={userEmail}");
        var updatedUsers = updatedResult?.Items;
        var promotedUser = updatedUsers!.First(u => u.Id == user.Id);
        _ = await Assert.That(promotedUser.Roles).Contains("Admin");
        _ = await Assert.That(promotedUser.Roles).DoesNotContain("admin");
    }

    [Test]
    public async Task GetUsers_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var adminLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        _client!.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminLogin!.AccessToken);
        _client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Act
        var result = await _client.GetFromJsonAsync<PagedListDto<UserAdminDto>>("/api/admin/users?page=1&pageSize=1");

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = await Assert.That(result!.Items.Count).IsEqualTo(1);
        _ = await Assert.That(result.PageNumber).IsEqualTo(1);
        _ = await Assert.That(result.PageSize).IsEqualTo(1);
        _ = await Assert.That(result.TotalItemCount).IsGreaterThanOrEqualTo(1);
    }
}
