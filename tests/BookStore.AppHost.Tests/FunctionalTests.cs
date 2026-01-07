using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.AppHost.Tests;
namespace BookStore.AppHost.Tests;

public class FunctionalTests
{
    [Test]
    public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // 1. Login as Admin
        // Retry login mechanism to handle potential Seeding race condition
        HttpResponseMessage loginResponse = null!;
        for (int i = 0; i < 15; i++)
        {
            loginResponse = await httpClient.PostAsJsonAsync("/account/login", new
            {
                Email = "admin@bookstore.com",
                Password = "Admin123!"
            });

            if (loginResponse.IsSuccessStatusCode)
            {
                break;
            }

            // Wait 1 second before retrying
            await Task.Delay(1000);
        }

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(loginResult).IsNotNull();
        await Assert.That(loginResult!.AccessToken).IsNotNullOrEmpty();

        // 2. Create Book (Authorized)
        var createBookRequest = new
        {
            Title = "Integration Test Book",
            Isbn = "978-0-123-45678-9",
            Language = "en",
            Translations = new Dictionary<string, object>
            {
                ["en"] = new { Description = "A test book description" }
            },
            PublicationDate = new { Year = 2026, Month = 1, Day = 1 },
            // Using IDs that should likely exist or leave empty if optional/handled
            // For simplicity, we create a book without linking to complex existing entities if allowed,
            // or we must fetch them first.
            // Looking at AdminBookEndpoints, AuthorIds and CategoryIds are optional (default to empty).
            // PublisherId is nullable.
            PublisherId = (Guid?)null,
            AuthorIds = new Guid[] {},
            CategoryIds = new Guid[] {}
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/admin/books");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
        requestMessage.Content = JsonContent.Create(createBookRequest);

        // Act
        var createResponse = await httpClient.SendAsync(requestMessage);

        // Debugging failure
        if (!createResponse.IsSuccessStatusCode)
        {
            var errorContent = await createResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"CreateBook Failed! Status: {createResponse.StatusCode}, Content: {errorContent}");
        }

        // Assert
        // AdminBookEndpoints.CreateBook returns bus.InvokeAsync<IResult>(command).
        // Wolverine handlers typically return the created entity or OK.
        // We assert success code (200-299).
        await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
    }

    private record LoginResponse(string AccessToken, string RefreshToken);
}
