public static class {Resource}Handlers
{
    public static async Task<IResult> Handle(
        {CommandName} cmd,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        // 1. Fetch State
        // 2. Business Validation
        // 3. Update Aggregate
        // 4. Store Event
        return Results.Ok();
    }
}
