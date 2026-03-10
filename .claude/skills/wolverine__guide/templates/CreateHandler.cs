using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.{Resources};

// Handler — place in Handlers/{Resources}/{Resource}Handlers.cs
public static class {Resource}Handlers
{
    public static async Task<IResult> Handle(
        Create{Resource} command,
        IDocumentSession session,
        HybridCache cache,
        ILogger logger)
    {
        Log.{Resources}.{Resource}Creating(logger, command.Id);

        // Validate inputs and call aggregate factory method
        var eventResult = {Resource}Aggregate.CreateEvent(command.Id, command.Name /*, other args */);
        if (eventResult.IsFailure)
            return eventResult.ToProblemDetails();

        _ = session.Events.StartStream<{Resource}Aggregate>(command.Id, eventResult.Value);

        // Invalidate list cache
        await cache.RemoveByTagAsync([CacheTags.{Resource}List], default);

        Log.{Resources}.{Resource}Created(logger, command.Id);

        return Results.Created(
            $"/api/admin/{resource_plural}/{command.Id}",
            new { id = command.Id });
    }
}
