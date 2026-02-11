using System.Reflection;
using BookStore.Client;
using BookStore.Client.Services;
using BookStore.Shared.Notifications;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BookStore.Web.Tests.Services;

public class BookStoreEventsServiceTests
{
    HttpClient _httpClient = null!;
    ILogger<BookStoreEventsService> _logger = null!;
    ClientContextService _clientContext = null!;
    BookStoreEventsService _sut = null!;

    [Before(Test)]
    public void Setup()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost") };
        _logger = Substitute.For<ILogger<BookStoreEventsService>>();
        _clientContext = Substitute.For<ClientContextService>();
        _sut = new BookStoreEventsService(_httpClient, _logger, _clientContext);
    }

    [Test]
    [Arguments("Ping",
        "{\"NotificationType\": \"Ping\", \"eventId\": \"00000000-0000-0000-0000-000000000000\", \"entityId\": \"00000000-0000-0000-0000-000000000000\", \"eventType\": \"Ping\", \"timestamp\": \"2026-02-11T16:41:51.630742+00:00\", \"version\": 0}",
        typeof(PingNotification))]
    [Arguments("Connected",
        "{\"NotificationType\": \"Ping\", \"eventId\": \"00000000-0000-0000-0000-000000000000\", \"entityId\": \"00000000-0000-0000-0000-000000000000\", \"eventType\": \"Ping\", \"timestamp\": \"2026-02-11T16:41:51.630742+00:00\", \"version\": 0}",
        typeof(PingNotification))]
    [Arguments("BookStatisticsUpdate",
        "{\"NotificationType\": \"BookStatisticsUpdate\", \"eventId\": \"5701a88b-21d7-464a-8fbb-7d90a2f578ee\", \"entityId\": \"5701a88b-21d7-464a-8fbb-7d90a2f578ee\", \"eventType\": \"BookStatisticsUpdate\", \"timestamp\": \"2026-02-11T16:41:51.630742+00:00\", \"version\": 0}",
        typeof(BookStatisticsUpdateNotification))]
    [Arguments("BookCreated",
        "{\"NotificationType\": \"BookCreated\", \"eventId\": \"5701a88b-21d7-464a-8fbb-7d90a2f578ee\", \"entityId\": \"5701a88b-21d7-464a-8fbb-7d90a2f578ee\", \"title\": \"Test Book\", \"eventType\": \"BookCreated\", \"timestamp\": \"2026-02-11T16:41:51.630742+00:00\", \"version\": 0}",
        typeof(BookCreatedNotification))]
    public async Task DeserializeNotification_ShouldReturnCorrectType(string eventType, string data, Type expectedType)
    {
        // Arrange
        var method = typeof(BookStoreEventsService).GetMethod("DeserializeNotification",
            BindingFlags.NonPublic | BindingFlags.Instance);
        _ = await Assert.That(method).IsNotNull();

        // Act
        var result = method!.Invoke(_sut, [eventType, data]);

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = await Assert.That(result!.GetType()).IsEqualTo(expectedType);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _httpClient.Dispose();
        await _sut.DisposeAsync();
    }
}
