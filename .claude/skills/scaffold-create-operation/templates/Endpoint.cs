using Marten.Schema;
using Wolverine.Http;
using Wolverine.Marten;

public static class {Resource}Endpoints
{
    [WolverinePost("/{resource_plural}")]
    public static (IResult, StartStream<{Resource}>) Create(Create{Resource} cmd)
    {
        var id = Guid.CreateVersion7();
        var evt = new {Resource}Created(id, cmd.Name, DateTimeOffset.UtcNow);

        // Return the 201 response AND the Marten side-effect to start the stream
        return (
            Results.Created($"/{resource_plural}/{id}", new { Id = id }), 
            MartenOps.StartStream<{Resource}>(id, evt)
        );
    }
}
