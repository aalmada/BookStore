using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.AppHost.Tests.Helpers;
using BookStore.ServiceDefaults;
using Npgsql;

namespace BookStore.AppHost.Tests;

public class CorrelationTests
{
    [Test]
    public async Task ShouldPropagateCorrelationIdToMartenEvents()
    {
        // Arrange
        var app = GlobalHooks.App;
        if (app == null)
        {
            Assert.Fail("App not initialized");
        }

        var httpClient = await HttpClientHelpers.GetAuthenticatedClientAsync();

        var correlationId = Guid.NewGuid().ToString();
        var fakeBookId =
            Guid.NewGuid(); // Random ID, it will fail conceptually but event should be stored or rejected, 
        // actually better to use a real action that succeeds to guarantee persistence.
        // Let's use AddToCart which doesn't check for book existence in the aggregate *before* stream load?
        // Actually UserCommandHandler loads user profile.
        // Let's use rate book on a random book. The command handler might validate, 
        // checking Handlers/UserCommandHandler.cs: RateBook validates rating range, 
        // then appends event. It doesn't check if book exists in the projection 
        // inside the command handler (it might be done in UI or client).
        // Wait, UserCommandHandler does:
        // public static async Task Handle(RateBook command, IDocumentSession session)
        // { ... _ = session.Events.Append(..., new BookRated(...)); }
        // It *always* appends. Perfect.

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/books/{fakeBookId}/rating");
        request.Content = JsonContent.Create(new { Rating = 5 });
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"0\""));
        request.Headers.UserAgent.ParseAdd("TUnit-Test-Agent");

        // Act & Assert
        // We use ExecuteAndWaitForEventAsync to ensure the command is processed and events are persisted 
        // before we check the database. Rating a book triggers a UserUpdated notification in this system.
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await httpClient.SendAsync(request);
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();

                // USER REQUIREMENT: Response must have the same correlation ID as the request
                using (Assert.Multiple())
                {
                    _ = await Assert.That(response.Headers.Contains("X-Correlation-ID")).IsTrue();
                    var responseId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
                    _ = await Assert.That(responseId).IsEqualTo(correlationId);
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        // Verify in DB
        var connectionString = await app!.GetConnectionStringAsync(ResourceNames.BookStoreDb);
        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Fail("Connection string not found");
        }

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT correlation_id, causation_id, headers FROM mt_events WHERE correlation_id = @cid", conn);
        _ = cmd.Parameters.AddWithValue("cid", correlationId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var dbCorrelationId = reader["correlation_id"] as string;
            var dbCausationId = reader["causation_id"] as string;
            var dbHeadersJson = reader["headers"] as string;

            using (Assert.Multiple())
            {
                _ = await Assert.That(dbCorrelationId).IsEqualTo(correlationId);
                _ = await Assert.That(dbCausationId).IsNotNull();
                _ = await Assert.That(dbHeadersJson).IsNotNull();

                // Verify technical headers in JSON
                _ = await Assert.That(dbHeadersJson).Contains("\"user-id\"");
                _ = await Assert.That(dbHeadersJson).Contains("\"remote-ip\"");
                _ = await Assert.That(dbHeadersJson).Contains("\"user-agent\"");
                _ = await Assert.That(dbHeadersJson).Contains("TUnit-Test-Agent");
            }
        }
        else
        {
            // Diagnostics: print last 5 events
            reader.Close();
            using var diagCmd = new NpgsqlCommand(
                "SELECT stream_id, type, correlation_id, causation_id, headers FROM mt_events ORDER BY seq_id DESC LIMIT 5",
                conn);
            using var diagReader = await diagCmd.ExecuteReaderAsync();

            Assert.Fail(
                $"Event with correlation_id '{correlationId}' not found in mt_events table despite receiving SSE notification.");
        }
    }

    [Test]
    public async Task ShouldGenerateAndPropagateCorrelationIdWhenMissing()
    {
        // Arrange
        var app = GlobalHooks.App;
        if (app == null)
        {
            Assert.Fail("App not initialized");
        }

        var httpClient = await HttpClientHelpers.GetAuthenticatedClientAsync();

        var fakeBookId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/books/{fakeBookId}/rating");
        request.Content = JsonContent.Create(new { Rating = 4 });
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"0\""));
        request.Headers.UserAgent.ParseAdd("TUnit-Test-Agent-No-ID");
        // NOTE: No X-Correlation-ID header added

        // Act & Assert
        string? responseCorrelationId = null;

        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await httpClient.SendAsync(request);
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();

                // USER REQUIREMENT: Case missing id, a new one must be created and returned.
                using (Assert.Multiple())
                {
                    _ = await Assert.That(response.Headers.Contains("X-Correlation-ID")).IsTrue();
                    responseCorrelationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
                    _ = await Assert.That(responseCorrelationId).IsNotNull();
                    _ = await Assert.That(Guid.TryParse(responseCorrelationId, out _)).IsTrue();
                }
            },
            TestConstants.DefaultEventTimeout);

        _ = await Assert.That(received).IsTrue();

        // Verify in DB
        var connectionString = await app!.GetConnectionStringAsync(ResourceNames.BookStoreDb);
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT correlation_id, headers FROM mt_events WHERE correlation_id = @cid", conn);
        _ = cmd.Parameters.AddWithValue("cid", responseCorrelationId!);

        using var reader = await cmd.ExecuteReaderAsync();
        using (Assert.Multiple())
        {
            _ = await Assert.That(await reader.ReadAsync()).IsTrue();
            _ = await Assert.That(reader["correlation_id"] as string).IsEqualTo(responseCorrelationId);

            var dbHeadersJson = reader["headers"] as string;
            _ = await Assert.That(dbHeadersJson).IsNotNull();
            _ = await Assert.That(dbHeadersJson).Contains("\"user-id\"");
            _ = await Assert.That(dbHeadersJson).Contains("\"remote-ip\"");
            _ = await Assert.That(dbHeadersJson).Contains("\"user-agent\"");
        }
    }
}
