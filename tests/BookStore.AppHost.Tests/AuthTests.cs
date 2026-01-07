using Bogus;
using System.Net;
using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class AuthTests
{
    private readonly HttpClient _client;
    private readonly Faker _faker;

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
        await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/account/login", new
        {
            Email = email,
            Password = password
        });

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
        var loginRequest = new
        {
            Email = email,
            Password = password
        };

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
        var registerResponse = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new
        {
            loginResult!.RefreshToken
        };

        // Act
        var refreshResponse = await _client.PostAsJsonAsync("/account/refresh-token", refreshRequest);

        // Assert
        _ = await Assert.That(refreshResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        
        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _ = await Assert.That(refreshResult).IsNotNull();
        _ = await Assert.That(refreshResult!.AccessToken).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(refreshResult.AccessToken).IsNotEqualTo(loginResult.AccessToken);
    }

    record LoginResponse(string AccessToken, string RefreshToken);
}
