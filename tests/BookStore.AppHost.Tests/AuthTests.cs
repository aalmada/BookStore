using System.Net;
using System.Net.Http.Json;
using Bogus;

namespace BookStore.AppHost.Tests;

public class AuthTests
{
    readonly HttpClient _client;
    readonly Faker _faker;

    public AuthTests()
    {
        _client = TestHelpers.GetUnauthenticatedClient();
        _faker = new Faker();
    }

    [Test]
    public async Task Register_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var setupRequest = new
        {
            Email = _faker.Internet.Email(),
            Password = _faker.Internet.Password(8, false, "\\w", "Aa1!") // Ensure complexity
        };

        // Act
        var response = await _client.PostAsJsonAsync("/account/register", setupRequest);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register first
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });

        // Assert
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult!.AccessToken).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(loginResult.RefreshToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    [Arguments("nonexistent@example.com", "WrongPassword!")]
    [Arguments("invalid-email", "Password123!")]
    [Arguments("user@example.com", "")]
    [Arguments("", "Password123!")]
    public async Task Login_WithInvalidData_ShouldReturnUnauthorized(string email, string password)
    {
        // Arrange
        var loginRequest = new { Email = email, Password = password };

        // Act
        var response = await _client.PostAsJsonAsync("/account/login", loginRequest);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Refresh_WithValidToken_ShouldReturnNewToken()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register and Login
        var registerResponse =
            await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new { loginResult!.RefreshToken };

        // Act
        var refreshResponse = await _client.PostAsJsonAsync("/account/refresh-token", refreshRequest);

        // Assert
        _ = await Assert.That(refreshResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _ = await Assert.That(refreshResult).IsNotNull();
        _ = await Assert.That(refreshResult!.AccessToken).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(refreshResult.AccessToken).IsNotEqualTo(loginResult.AccessToken);
    }

    [Test]
    public async Task Logout_WithValidRefreshToken_ShouldInvalidateToken()
    {
        // Arrange - Register and login
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create an authenticated client with the user's token
        var authClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        // Act - Logout
        var logoutResponse = await authClient.PostAsJsonAsync("/account/logout", new { loginResult.RefreshToken });

        // Assert - Logout succeeded
        _ = await Assert.That(logoutResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);

        // Assert - Refresh token is now invalid
        var refreshResponse = await _client.PostAsJsonAsync("/account/refresh-token", new { loginResult.RefreshToken });
        _ = await Assert.That(refreshResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Logout_WithoutRefreshToken_ShouldInvalidateAllTokens()
    {
        // Arrange - Register, login twice to get two refresh tokens
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        var loginResponse1 =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        var loginResult1 = await loginResponse1.Content.ReadFromJsonAsync<LoginResponse>();

        var loginResponse2 =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        var loginResult2 = await loginResponse2.Content.ReadFromJsonAsync<LoginResponse>();

        // Create an authenticated client with the user's token
        var authClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult2!.AccessToken);

        // Act - Logout without specifying refresh token (should clear all)
        var logoutResponse = await authClient.PostAsJsonAsync("/account/logout", new { RefreshToken = (string?)null });
        _ = await Assert.That(logoutResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);

        // Assert - Both refresh tokens should be invalid
        var refresh1 = await _client.PostAsJsonAsync("/account/refresh-token", new { loginResult1!.RefreshToken });
        var refresh2 = await _client.PostAsJsonAsync("/account/refresh-token", new { loginResult2.RefreshToken });

        _ = await Assert.That(refresh1.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Unauthorized);
        _ = await Assert.That(refresh2.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Unauthorized);
    }

    record LoginResponse(string AccessToken, string RefreshToken);
}
