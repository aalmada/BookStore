namespace BookStore.ApiService.Projections;

using Marten.Events.Projections;

// This is typically a MultiStreamProjection that aggregates data from other streams
// OR it relies on data from other projections running in previous stages within the same composite.
public class {Summary}Projection : MultiStreamProjection<{Summary}Projection, Guid>
{
    public {Summary}Projection()
    {
        // Define identity mapping
        Identity<{Resource}Created>(e => GlobalId);
        
        // If dependent on another projection's output, you effectively
        // just handle the source events, but Marten groups the execution.
        // Currently, direct "Projection A Output -> Projection B Input" 
        // is handled by Marten ensuring Stage 1 commits before Stage 2 runs if needed,
        // or by grouping them in the same transaction for consistency.
    }
    
    public static readonly Guid GlobalId = Guid.Parse("...");

    public void Apply({Resource}Created @event, {Summary}Projection view)
    {
        view.Count++;
    }
}

public class {Summary}Projection 
{
    public Guid Id { get; set; }
    public int Count { get; set; }
}
