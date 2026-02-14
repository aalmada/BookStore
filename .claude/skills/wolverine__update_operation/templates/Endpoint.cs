using Marten.Schema;
using Wolverine.Http;
using Wolverine.Marten;

public static class {Resource}Endpoints
{
    [WolverinePut("/{resource_plural}/{id}")]
    public static (IResult, IEvent) Update(
        Guid id, 
        Update{Resource} cmd,
        [Aggregate] {Resource} aggregate)
    {
        // Aggregate is automatically loaded by Wolverine.
        // If not found, Wolverine returns 404 automatically (if configured).
        
        // Pure logic: call the aggregate to get the event
        var evt = aggregate.Update(cmd.NewName);

        // Return 204 No Content and the event to be appended
        return (Results.NoContent(), evt);
    }
}
