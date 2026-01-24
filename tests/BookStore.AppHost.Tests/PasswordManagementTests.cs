using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using JasperFx;
using JasperFx.Core;
using Marten;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class PasswordManagementTests
{
    readonly HttpClient _client;
    readonly Faker _faker;

    public PasswordManagementTests()
    {
        _client = TestHelpers.GetUnauthenticatedClient();
        _faker = new Faker();
    }

    [Test]
    public async Task GetPasswordStatus_WhenUserHasPassword_ShouldReturnTrue()
    {
        // Arrange
        var authenticatedClient = await TestHelpers.CreateUserAndGetClientAsync();

        // Act
        var response = await authenticatedClient.GetAsync("/account/password-status");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<PasswordStatusResponse>();
        _ = await Assert.That(status).IsNotNull();
        _ = await Assert.That(status!.HasPassword).IsTrue();
    }

    [Test]
    public async Task ChangePassword_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword123!";

        // Register
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = oldPassword });

        // Login to get token
        var loginResponse =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = oldPassword });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();

        var authClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        authClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Act
        var changeResponse = await authClient.PostAsJsonAsync("/account/change-password",
            new ChangePasswordRequest(oldPassword, newPassword));

        // Assert
        _ = await Assert.That(changeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act - Try login with new password
        var nextLoginResponse =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = newPassword });

        // Assert
        _ = await Assert.That(nextLoginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AddPassword_WhenManualClearance_ShouldSucceed()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var tempPassword = "TempPassword123!";
        var newPassword = "AddedPassword123!";

        // Register normally
        var regResponse =
            await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = tempPassword });
        _ = await Assert.That(regResponse.IsSuccessStatusCode).IsTrue();

        var loginResponse =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = tempPassword });
        _ = await Assert.That(loginResponse.IsSuccessStatusCode).IsTrue();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();

        var authClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        authClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Manually clear password hash in DB
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await using (var session = store.LightweightSession(StorageConstants.DefaultTenantId))
        {
            var user = await session.Query<ApplicationUser>()
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            _ = await Assert.That(user).IsNotNull();
            user!.PasswordHash = null;
            session.Update(user);
            await session.SaveChangesAsync();
        }

        // Verify it reports no password
        var statusResponse = await authClient.GetAsync("/account/password-status");
        var status = await statusResponse.Content.ReadFromJsonAsync<PasswordStatusResponse>();
        _ = await Assert.That(status!.HasPassword).IsFalse();

        // Act
        var addResponse =
            await authClient.PostAsJsonAsync("/account/add-password", new AddPasswordRequest(newPassword));

        // Assert
        _ = await Assert.That(addResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act - Try login with added password
        var finalLoginResponse =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = newPassword });

        // Assert
        _ = await Assert.That(finalLoginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ChangePassword_WithSamePassword_ShouldReturnBadRequest()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = "SamePassword123!";

        // Register
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        // Login to get token
        var loginResponse =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();

        var authClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        authClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        // Act
        var changeResponse =
            await authClient.PostAsJsonAsync("/account/change-password",
                new ChangePasswordRequest(password, password));

        // Assert
        _ = await Assert.That(changeResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error = await changeResponse.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Auth.PasswordReuse);
    }
}
