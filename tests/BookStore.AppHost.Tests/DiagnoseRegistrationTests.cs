using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Temporary test to diagnose 400 Bad Request registration failures
/// </summary>
public class DiagnoseRegistrationTests
{
    readonly IIdentityClient _client;

    public DiagnoseRegistrationTests()
    {
        _client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
    }

    [Test]
    public async Task DiagnoseRegistrationFailure_CaptureActualErrorMessage()
    {
        // Arrange
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        Console.WriteLine($"=== DIAGNOSTIC INFO ===");
        Console.WriteLine($"Generated Email: {email}");
        Console.WriteLine($"Generated Password: {password}");
        Console.WriteLine($"Password Length: {password.Length}");
        Console.WriteLine($"Has Uppercase: {password.Any(char.IsUpper)}");
        Console.WriteLine($"Has Lowercase: {password.Any(char.IsLower)}");
        Console.WriteLine($"Has Digit: {password.Any(char.IsDigit)}");
        Console.WriteLine($"Has Special: {password.Any(c => !char.IsLetterOrDigit(c))}");
        Console.WriteLine($"========================");

        try
        {
            var response = await _client.RegisterAsync(new RegisterRequest(email, password));
            Console.WriteLine("✅ Registration SUCCEEDED");
            Console.WriteLine($"Access Token (first 20 chars): {response.AccessToken[..20]}...");
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"❌ Registration FAILED");
            Console.WriteLine($"Status Code: {ex.StatusCode}");
            Console.WriteLine($"Raw Response Content: {ex.Content}");

            // Parse ProblemDetails to get error code
            var problemDetails = await ex.GetContentAsAsync<AuthenticationHelpers.ValidationProblemDetails>();

            Console.WriteLine($"\n=== PROBLEM DETAILS ===");
            Console.WriteLine($"Title: {problemDetails?.Title}");
            Console.WriteLine($"Status: {problemDetails?.Status}");
            Console.WriteLine($"Detail: {problemDetails?.Detail}");
            Console.WriteLine($"Error Code: {problemDetails?.Error}");
            Console.WriteLine($"=======================");

            // Fail the test with detailed info
            throw new Exception(
                $"Registration failed with {ex.StatusCode}\n" +
                $"Error Code: {problemDetails?.Error}\n" +
                $"Detail: {problemDetails?.Detail}",
                ex);
        }
    }

    [Test]
    public async Task DiagnosePasswordGeneration_Multiple()
    {
        Console.WriteLine($"=== TESTING 10 PASSWORD GENERATIONS ===");

        for (int i = 1; i <= 10; i++)
        {
            var password = FakeDataGenerators.GenerateFakePassword();
            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));
            var meetsLength = password.Length >= 8;
            var meetsAll = hasUpper && hasLower && hasDigit && hasSpecial && meetsLength;

            Console.WriteLine($"#{i}: '{password}' | Len:{password.Length} U:{hasUpper} L:{hasLower} D:{hasDigit} S:{hasSpecial} | OK:{meetsAll}");

            if (!meetsAll)
            {
                throw new Exception($"Password #{i} does not meet requirements: {password}");
            }
        }

        Console.WriteLine($"✅ All 10 passwords meet requirements");
    }
}
