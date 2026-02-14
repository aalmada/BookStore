using Marten.Schema;
using Wolverine.Http;
using Wolverine.Marten;

public static class {Resource}Endpoints
{
    [WolverineDelete("/{resource_plural}/{id}")]
    public static (IResult, IEvent) Delete(
        Guid id, 
        Delete{Resource} cmd,
        [Aggregate] {Resource} aggregate)
    {
        // Aggregate is automatically loaded.
        
        // Pure logic
        var evt = aggregate.Delete();

        // Return 204 No Content and the event to be appended
        return (Results.NoContent(), evt);
    }
}
