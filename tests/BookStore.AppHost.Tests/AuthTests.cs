using System.Net;
using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class AuthTests
{
    readonly IIdentityClient _client;
    readonly Faker _faker;

    public AuthTests()
    {
        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        _client = RestService.For<IIdentityClient>(httpClient);
        _faker = new Faker();
    }

    [Test]
    public async Task Register_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var request = new RegisterRequest(
            FakeDataGenerators.GenerateFakeEmail(),
            FakeDataGenerators.GenerateFakePassword()
        );

        // Act
        var response = await _client.RegisterAsync(request);

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.AccessToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Register_WithExistingUser_ShouldReturnOk()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = FakeDataGenerators.GenerateFakePassword();
        var request = new RegisterRequest(email, password);

        // Register once
        _ = await _client.RegisterAsync(request);

        // Act - Register again with same email
        var response = await _client.RegisterAsync(request);

        // Assert - Should return OK to prevent enumeration (mapped to successful response in Refit)
        _ = await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task Login_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = FakeDataGenerators.GenerateFakePassword();

        // Register first
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        // Act
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        // Assert
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult.AccessToken).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(loginResult.RefreshToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    [Arguments("nonexistent@example.com", "WrongPassword!")]
    [Arguments("invalid-email", "Password123!")]
    [Arguments("user@example.com", "")]
    [Arguments("", "Password123!")]
    public async Task Login_WithInvalidData_ShouldReturnUnauthorized(string email, string password)
    {
        // Act & Assert
        try
        {
            _ = await _client.LoginAsync(new LoginRequest(email, password));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task RawLoginError_ShouldContainStandardizedCode()
    {
        // Act & Assert
        try
        {
            _ = await _client.LoginAsync(new LoginRequest("nonexistent@example.com", "password"));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            var problem = await ex.GetContentAsAsync<AuthenticationHelpers.ValidationProblemDetails>();
            _ = await Assert.That(problem?.Error).IsEqualTo("ERR_AUTH_INVALID_CREDENTIALS");
        }
    }

    [Test]
    public async Task Login_AsTenantAdmin_ShouldSucceed()
    {
        // Arrange
        var tenantId = "contoso";
        var email = $"admin@{tenantId}.com";
        var password = "Admin123!";

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));

        // Act
        var loginResult = await client.LoginAsync(new LoginRequest(email, password));

        // Assert
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult.AccessToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Refresh_WithValidToken_ShouldReturnNewToken()
    {
        // Arrange
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        // Register and Login
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        // Act
        var refreshResult = await _client.RefreshTokenAsync(new RefreshRequest(loginResult.RefreshToken));

        // Assert
        _ = await Assert.That(refreshResult).IsNotNull();
        _ = await Assert.That(refreshResult.AccessToken).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(refreshResult.AccessToken).IsNotEqualTo(loginResult.AccessToken);
    }

    [Test]
    public async Task Logout_WithValidRefreshToken_ShouldInvalidateToken()
    {
        // Arrange - Register and login
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        _ = await _client.RegisterAsync(new RegisterRequest(email, password));
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        // Create an authenticated client with the user's token
        var authClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetAuthenticatedClient(loginResult.AccessToken));

        // Act - Logout
        await authClient.LogoutAsync(new LogoutRequest(loginResult.RefreshToken));

        // Assert - Refresh token is now invalid
        try
        {
            _ = await _client.RefreshTokenAsync(new RefreshRequest(loginResult.RefreshToken));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task Logout_WithoutRefreshToken_ShouldInvalidateAllTokens()
    {
        // Arrange - Register, login twice to get two refresh tokens
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        var loginResult1 = await _client.LoginAsync(new LoginRequest(email, password));
        var loginResult2 = await _client.LoginAsync(new LoginRequest(email, password));

        // Create an authenticated client with the user's token
        var authClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetAuthenticatedClient(loginResult2.AccessToken));

        // Act - Logout without specifying refresh token (should clear all)
        await authClient.LogoutAsync(new LogoutRequest(null));

        // Assert - Both refresh tokens should be invalid
        try
        {
            _ = await _client.RefreshTokenAsync(new RefreshRequest(loginResult1.RefreshToken));
            Assert.Fail("First refresh token should be invalidated");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.Unauthorized);
        }

        try
        {
            _ = await _client.RefreshTokenAsync(new RefreshRequest(loginResult2.RefreshToken));
            Assert.Fail("Second refresh token should be invalidated");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.Unauthorized);
        }
    }
}
