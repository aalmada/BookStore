using System.Net;
using System.Net.Http.Json;

namespace BookStore.AppHost.Tests;

public class BookCrudTests
{
    [Test]
    public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        // Act
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);

        // Assert
        await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    public async Task UpdateBook_EndToEndFlow_ShouldReturnOk()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
        await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
        
        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
        await Assert.That(createdBook).IsNotNull();

        // Act - Update the book with new fake data
        var updateBookRequest = TestDataGenerators.GenerateFakeBookRequest();
        var updateResponse = await httpClient.PutAsJsonAsync($"/api/admin/books/{createdBook!.Id}", updateBookRequest);

        // Assert
        await Assert.That(updateResponse.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    public async Task DeleteBook_EndToEndFlow_ShouldReturnNoContent()
    {
        // Arrange
        var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
        var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
        await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
        
        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
        await Assert.That(createdBook).IsNotNull();

        // Act - Delete the book
        var deleteResponse = await httpClient.DeleteAsync($"/api/admin/books/{createdBook!.Id}");

        // Assert
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    private record BookResponse(Guid Id, string Title, string Isbn);
}
