using System.Net;
using System.Text.Json;
using BookStore.Shared.Models;
using BookStore.Web.Infrastructure;
using Refit;

namespace BookStore.Web.Tests.Infrastructure;

public class ProblemDetailsExtensionsTests
{
    [Test]
    public async Task ToError_WithCodeInExtensions_ShouldExtractCode()
    {
        // Arrange
        var content = JsonSerializer.Serialize(new
        {
            detail = "Error message",
            extensions = new { code = "ERR_TEST_CODE" }
        });
        var exception = await CreateApiException(HttpStatusCode.BadRequest, content);

        // Act
        var error = exception.ToError();

        // Assert
        _ = await Assert.That(error.Code).IsEqualTo("ERR_TEST_CODE");
        _ = await Assert.That(error.Message).IsEqualTo("Error message");
    }

    [Test]
    public async Task ToError_WithErrorInExtensions_ShouldExtractCode()
    {
        // Arrange
        var content = JsonSerializer.Serialize(new
        {
            detail = "Error message",
            extensions = new { error = "ERR_LEGACY_CODE" }
        });
        var exception = await CreateApiException(HttpStatusCode.BadRequest, content);

        // Act
        var error = exception.ToError();

        // Assert
        _ = await Assert.That(error.Code).IsEqualTo("ERR_LEGACY_CODE");
    }

    [Test]
    public async Task ToError_WithNoCode_ShouldUseFallbackCode()
    {
        // Arrange
        var content = JsonSerializer.Serialize(new
        {
            detail = "Error message"
        });
        var exception = await CreateApiException(HttpStatusCode.BadRequest, content);

        // Act
        var error = exception.ToError();

        // Assert
        _ = await Assert.That(error.Code).IsEqualTo("ERR_HTTP_BADREQUEST");
    }

    [Test]
    public async Task ToError_WithIdentityErrors_ShouldExtractDescription()
    {
        // Arrange
        var content = JsonSerializer.Serialize(new
        {
            errors = new[]
            {
                new { description = "Identity error message" }
            }
        });
        var exception = await CreateApiException(HttpStatusCode.BadRequest, content);

        // Act
        var error = exception.ToError();

        // Assert
        _ = await Assert.That(error.Message).IsEqualTo("Identity error message");
    }

    static async Task<ApiException> CreateApiException(HttpStatusCode statusCode, string content)
    {
        var message = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
        var request = new HttpRequestMessage();
        return await ApiException.Create(request, HttpMethod.Post, message, new RefitSettings());
    }
}
