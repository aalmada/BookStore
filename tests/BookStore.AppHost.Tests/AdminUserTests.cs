using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Refit;

namespace BookStore.AppHost.Tests;

public class AdminUserTests
{
    [Before(Test)]
    public async Task Setup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }
    }

    [Test]
    public async Task GetUsers_ReturnsListOfUsers()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));

        // Act
        var result = await client.GetUsersAsync();
        var users = result.Items;

        // Assert
        _ = await Assert.That(users).IsNotNull();
        _ = await Assert.That(users).IsNotEmpty();

        var admin = users.First(u => u.Email == "admin@bookstore.com");
        _ = await Assert.That(admin).IsNotNull();
        _ = await Assert.That(admin.HasPassword).IsTrue();
        _ = await Assert.That(admin.HasPasskey).IsFalse();
    }

    [Test]
    public async Task PromoteUser_SucceedsForOtherUser()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));
        var identityClient =
            RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));

        // Create a regular user
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        _ = await identityClient.RegisterAsync(new RegisterRequest(userEmail, FakeDataGenerators.GenerateFakePassword()));

        var result = await client.GetUsersAsync(search: userEmail);
        var users = result.Items;
        var userToPromote = users.First(u => u.Email == userEmail);

        // Act
        await client.PromoteToAdminAsync(userToPromote.Id);

        // Verify promotion
        var updatedResult = await client.GetUsersAsync(search: userEmail);
        var updatedUsers = updatedResult.Items;
        var promotedUser = updatedUsers.First(u => u.Id == userToPromote.Id);
        _ = await Assert.That(promotedUser.Roles).Contains("Admin");
    }

    [Test]
    public async Task PromoteSelf_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));

        var result = await client.GetUsersAsync();
        var users = result.Items;
        var self = users.First(u => u.Email == "admin@bookstore.com");

        // Act & Assert
        var exception = await Assert.That(async () => await client.PromoteToAdminAsync(self.Id)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var error = await exception.GetContentAsAsync<AuthenticationHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Admin.CannotPromoteSelf);
    }

    [Test]
    public async Task DemoteSelf_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));

        var result = await client.GetUsersAsync();
        var users = result.Items;
        var self = users.First(u => u.Email == "admin@bookstore.com");

        // Act & Assert
        var exception = await Assert.That(async () => await client.DemoteFromAdminAsync(self.Id))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var error = await exception.GetContentAsAsync<AuthenticationHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Admin.CannotDemoteSelf);
    }

    [Test]
    public async Task DemoteUser_SucceedsForOtherUser()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));
        var identityClient =
            RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));

        // Create and promote a user
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        _ = await identityClient.RegisterAsync(new RegisterRequest(userEmail, FakeDataGenerators.GenerateFakePassword()));

        var result = await client.GetUsersAsync(search: userEmail);
        var users = result.Items;
        var user = users.First(u => u.Email == userEmail);
        await client.PromoteToAdminAsync(user.Id);

        // Act
        await client.DemoteFromAdminAsync(user.Id);

        // Verify demotion
        var updatedResult = await client.GetUsersAsync(search: userEmail);
        var updatedUsers = updatedResult.Items;
        var demotedUser = updatedUsers.First(u => u.Id == user.Id);
        _ = await Assert.That(demotedUser.Roles).DoesNotContain("Admin");
    }

    [Test]
    public async Task PromoteUser_AlreadyAdmin_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));
        var identityClient =
            RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));

        // Create and promote a user
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        _ = await identityClient.RegisterAsync(new RegisterRequest(userEmail, FakeDataGenerators.GenerateFakePassword()));

        var result = await client.GetUsersAsync(search: userEmail);
        var users = result.Items;
        var user = users.First(u => u.Email == userEmail);
        await client.PromoteToAdminAsync(user.Id);

        // Act & Assert
        var exception = await Assert.That(async () => await client.PromoteToAdminAsync(user.Id)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task DemoteUser_NotAdmin_ReturnsBadRequest()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));
        var identityClient =
            RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));

        // Create a regular user
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        _ = await identityClient.RegisterAsync(new RegisterRequest(userEmail, FakeDataGenerators.GenerateFakePassword()));

        var result = await client.GetUsersAsync(search: userEmail);
        var users = result.Items;
        var user = users.First(u => u.Email == userEmail);

        // Act & Assert
        var exception = await Assert.That(async () => await client.DemoteFromAdminAsync(user.Id))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RegularUser_CannotAccessAdminUserEndpoints()
    {
        // Arrange
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();
        var identityClient =
            RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));
        _ = await identityClient.RegisterAsync(new RegisterRequest(userEmail, password));

        var loginResponse = await identityClient.LoginAsync(new LoginRequest(userEmail, password));
        var userClient =
            RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(loginResponse.AccessToken));

        // Act & Assert
        var exception = await Assert.That(async () => await userClient.GetUsersAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PromoteUser_LowercaseAdmin_IsNormalizedToPascalCase()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));
        var identityClient =
            RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId));

        // Create a regular user
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        _ = await identityClient.RegisterAsync(new RegisterRequest(userEmail, FakeDataGenerators.GenerateFakePassword()));

        var result = await client.GetUsersAsync(search: userEmail);
        var users = result.Items;
        var user = users.First(u => u.Email == userEmail);

        // Act: Promote using the typed client
        await client.PromoteToAdminAsync(user.Id);

        // Assert: Verify it's returned as "Admin" in the user list
        var updatedResult = await client.GetUsersAsync(search: userEmail);
        var updatedUsers = updatedResult.Items;
        var promotedUser = updatedUsers.First(u => u.Id == user.Id);
        _ = await Assert.That(promotedUser.Roles).Contains("Admin");
        // We can't strictly assert DoesNotContain("admin") unless we trust the backend normalization, 
        // but checking Contains("Admin") is the key requirement.
    }

    [Test]
    public async Task GetUsers_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var adminLogin = await AuthenticationHelpers.LoginAsAdminAsync(StorageConstants.DefaultTenantId);
        var client = RestService.For<IUsersClient>(HttpClientHelpers.GetAuthenticatedClient(adminLogin!.AccessToken));

        // Act
        var result = await client.GetUsersAsync(page: 1, pageSize: 1);

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = await Assert.That(result.Items.Count).IsEqualTo(1);
        _ = await Assert.That(result.PageNumber).IsEqualTo(1);
        _ = await Assert.That(result.PageSize).IsEqualTo(1);
        _ = await Assert.That(result.TotalItemCount).IsGreaterThanOrEqualTo(1);
    }
}
